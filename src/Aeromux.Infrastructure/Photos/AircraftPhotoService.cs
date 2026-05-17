// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.

using System.Collections.Concurrent;
using System.Globalization;
using Aeromux.Core.Tracking;
using Serilog;

namespace Aeromux.Infrastructure.Photos;

/// <summary>
/// Outcome of a photo lookup, distinguishing terminal results from transient
/// upstream failures (the latter must not be cached — clients should retry).
/// </summary>
public enum PhotoOutcome
{
    /// <summary>A photo was found and is available.</summary>
    HasPhoto,
    /// <summary>The lookup completed and the airframe has no Planespotters photo.</summary>
    NoPhoto,
    /// <summary>The upstream call failed transiently; caller should retry on next selection.</summary>
    UpstreamFailure,
}

/// <summary>
/// Result of <see cref="IAircraftPhotoService.GetAsync"/>: an outcome flag plus
/// the metadata payload when one is available.
/// </summary>
/// <param name="Outcome">Discriminator indicating whether a photo was found, no photo exists, or the lookup failed transiently.</param>
/// <param name="Metadata">
/// The cached or freshly-fetched metadata. Non-null when <paramref name="Outcome"/> is
/// <see cref="PhotoOutcome.HasPhoto"/> or <see cref="PhotoOutcome.NoPhoto"/>; <c>null</c>
/// when <paramref name="Outcome"/> is <see cref="PhotoOutcome.UpstreamFailure"/>.
/// </param>
public sealed record PhotoResult(PhotoOutcome Outcome, PhotoMetadata? Metadata);

/// <summary>
/// Orchestrates photo lookups: cache check → ICAO API call → registration
/// fallback → cache insert. Single-flight per-ICAO so concurrent requests
/// for the same aircraft trigger only one upstream call. Subscribes to
/// <see cref="AircraftStateTracker.OnAircraftExpired"/> and routes evictions
/// through the same per-ICAO semaphore so an in-flight insert and a tracker
/// eviction for the same ICAO are serialised — preventing a stale entry from
/// surviving the aircraft's expiry.
/// </summary>
public interface IAircraftPhotoService
{
    /// <summary>
    /// Looks up the photo for an ICAO. Returns from cache if available;
    /// otherwise calls Planespotters, falls back to a registration lookup
    /// when the primary call returns "no photo" and the local aircraft
    /// database has a registration for the ICAO.
    /// </summary>
    /// <param name="icao">24-bit ICAO address of the airframe.</param>
    /// <param name="ct">Cancellation token; observed during cache lookup, semaphore acquisition, and the upstream HTTP calls.</param>
    /// <returns>
    /// A <see cref="PhotoResult"/> describing the outcome. Cache hits return synchronously;
    /// misses pay one or two upstream Planespotters calls before completing.
    /// </returns>
    Task<PhotoResult> GetAsync(uint icao, CancellationToken ct);
}

/// <summary>
/// Default <see cref="IAircraftPhotoService"/> implementation backed by an
/// in-memory <see cref="AircraftPhotoCache"/> and a real Planespotters HTTP
/// client. Subscribes to <see cref="IAircraftStateTracker.OnAircraftExpired"/>
/// at construction so the cache evicts entries when their aircraft expire from
/// the tracker; <see cref="Dispose"/> unsubscribes and releases the per-ICAO
/// semaphores accumulated during the daemon's lifetime.
/// </summary>
public sealed class AircraftPhotoService : IAircraftPhotoService, IDisposable
{
    private readonly IPlanespottersApiClient _planespotters;
    private readonly AircraftPhotoCache _cache;
    private readonly IAircraftStateTracker _tracker;
    private readonly ConcurrentDictionary<uint, SemaphoreSlim> _semaphores = new();
    private bool _disposed;

    /// <summary>
    /// Creates a service that uses the given upstream client, cache, and tracker.
    /// Subscribes to <see cref="IAircraftStateTracker.OnAircraftExpired"/> immediately.
    /// </summary>
    /// <param name="planespotters">Upstream Planespotters API client used for cache misses.</param>
    /// <param name="cache">Backing in-memory metadata cache. Owned by the caller (typically registered as a DI singleton).</param>
    /// <param name="tracker">Aircraft state tracker. Used both to look up registrations for the fallback path and to drive cache eviction via the expiry event.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="planespotters"/>, <paramref name="cache"/>, or <paramref name="tracker"/> is null.</exception>
    public AircraftPhotoService(
        IPlanespottersApiClient planespotters,
        AircraftPhotoCache cache,
        IAircraftStateTracker tracker)
    {
        _planespotters = planespotters ?? throw new ArgumentNullException(nameof(planespotters));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));

        _tracker.OnAircraftExpired += HandleAircraftExpired;
    }

    /// <inheritdoc />
    public async Task<PhotoResult> GetAsync(uint icao, CancellationToken ct)
    {
        SemaphoreSlim sem = GetSemaphore(icao);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check the cache under the semaphore — another caller may have
            // populated it while we were waiting (the whole point of single-flight).
            if (_cache.TryGet(icao, out PhotoMetadata cached))
            {
                return new PhotoResult(
                    cached.HasPhoto ? PhotoOutcome.HasPhoto : PhotoOutcome.NoPhoto,
                    cached);
            }

            // Primary lookup by ICAO hex.
            PhotoMetadata? hexResult = await _planespotters.GetByHexAsync(icao, ct).ConfigureAwait(false);
            if (hexResult is null)
            {
                // Transient failure — do not cache, caller retries on next selection.
                return new PhotoResult(PhotoOutcome.UpstreamFailure, null);
            }
            if (hexResult.HasPhoto)
            {
                _cache.Insert(icao, hexResult);
                return new PhotoResult(PhotoOutcome.HasPhoto, hexResult);
            }

            // Hex returned terminal "no photo" — try registration fallback if
            // the tracker has a registration for this ICAO.
            string hex = icao.ToString("X6", CultureInfo.InvariantCulture);
            Aircraft? aircraft = _tracker.GetAircraft(hex);
            string? registration = aircraft?.DatabaseRecord.Registration;

            if (!string.IsNullOrWhiteSpace(registration))
            {
                PhotoMetadata? regResult = await _planespotters.GetByRegAsync(registration, ct).ConfigureAwait(false);
                if (regResult is null)
                {
                    // Transient failure on the fallback — do not cache.
                    return new PhotoResult(PhotoOutcome.UpstreamFailure, null);
                }
                if (regResult.HasPhoto)
                {
                    _cache.Insert(icao, regResult);
                    return new PhotoResult(PhotoOutcome.HasPhoto, regResult);
                }
                // Reg also returned terminal "no photo" — fall through and cache
                // the negative entry. We don't chain a second fallback.
            }

            var negative = PhotoMetadata.Negative();
            _cache.Insert(icao, negative);
            return new PhotoResult(PhotoOutcome.NoPhoto, negative);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Tracker eviction handler. Routes through the same per-ICAO semaphore as
    /// inserts so the eviction either runs strictly before or strictly after
    /// any in-flight insert for the same ICAO — guaranteeing no stale entries.
    /// </summary>
    private void HandleAircraftExpired(object? sender, AircraftEventArgs args)
    {
        if (args?.Aircraft is null)
        {
            return;
        }

        if (!uint.TryParse(args.Aircraft.Identification.ICAO,
                           NumberStyles.HexNumber,
                           CultureInfo.InvariantCulture,
                           out uint icao))
        {
            // Defensive — tracker ICAOs are always 6-char hex, so this shouldn't fire.
            Log.Warning("Tracker emitted OnAircraftExpired with non-hex ICAO {Icao}; skipping cache evict",
                args.Aircraft.Identification.ICAO);
            return;
        }

        SemaphoreSlim sem = GetSemaphore(icao);
        sem.Wait();
        try
        {
            _cache.Evict(icao);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Returns the per-ICAO single-flight semaphore, lazily creating it on
    /// first access. Semaphores accumulate over the daemon's lifetime
    /// (~80 bytes per unique ICAO ever seen) — accepted overhead.
    /// </summary>
    private SemaphoreSlim GetSemaphore(uint icao) =>
        _semaphores.GetOrAdd(icao, static _ => new SemaphoreSlim(1, 1));

    /// <inheritdoc />
    /// <remarks>
    /// Unsubscribes from <see cref="IAircraftStateTracker.OnAircraftExpired"/> first
    /// (so no further eviction handlers fire after disposal), then disposes every
    /// per-ICAO semaphore accumulated during the daemon's lifetime. Idempotent —
    /// safe to call multiple times.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _tracker.OnAircraftExpired -= HandleAircraftExpired;

        foreach (SemaphoreSlim sem in _semaphores.Values)
        {
            sem.Dispose();
        }
        _semaphores.Clear();
    }
}
