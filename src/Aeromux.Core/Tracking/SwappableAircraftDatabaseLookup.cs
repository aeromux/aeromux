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

namespace Aeromux.Core.Tracking;

/// <summary>
/// An <see cref="IAircraftDatabaseLookup"/> wrapper whose inner lookup service can be
/// atomically replaced at runtime (a database hot-swap). Lookups and swaps are serialized
/// by a single lock; the previous inner service is disposed under that lock, so an in-flight
/// lookup can never touch a disposed connection.
/// </summary>
/// <remarks>
/// The wrapper holds a stable reference for the lifetime of the tracker, while the underlying
/// database (and its version) can change beneath it. A <c>null</c> inner is valid — it represents
/// "enrichment enabled but no database loaded yet" (e.g. a first-run download in progress) and
/// returns <see cref="AircraftDatabaseRecord.Empty"/> for every lookup.
/// </remarks>
public sealed class SwappableAircraftDatabaseLookup : IAircraftDatabaseLookup, IDisposable
{
    private readonly Lock _gate = new();
    private IAircraftDatabaseLookup? _inner;
    private string? _currentVersion;
    private bool _disposed;

    /// <summary>
    /// Creates the wrapper around an initial inner lookup service (which may be <c>null</c>).
    /// </summary>
    /// <param name="inner">The initial inner lookup service, or <c>null</c> if none is loaded yet.</param>
    /// <param name="version">The loaded database version, or <c>null</c> when there is no inner service.</param>
    public SwappableAircraftDatabaseLookup(IAircraftDatabaseLookup? inner, string? version)
    {
        _inner = inner;
        _currentVersion = version;
    }

    /// <summary>
    /// Gets the version of the currently loaded database, or <c>null</c> when no inner service is present.
    /// Read live by the Web Map <c>Metadata</c> push so the header reflects a hot-swap.
    /// </summary>
    public string? CurrentVersion
    {
        get
        {
            lock (_gate)
            {
                return _currentVersion;
            }
        }
    }

    /// <inheritdoc />
    public AircraftDatabaseRecord LookupAircraft(string icaoAddress)
    {
        lock (_gate)
        {
            return _inner?.LookupAircraft(icaoAddress) ?? AircraftDatabaseRecord.Empty;
        }
    }

    /// <summary>
    /// Atomically replaces the inner lookup service and its reported version, disposing the
    /// previous inner service. Safe to call concurrently with lookups.
    /// </summary>
    /// <param name="newInner">The new inner lookup service (may be <c>null</c>).</param>
    /// <param name="newVersion">The new database version (or <c>null</c>).</param>
    public void Swap(IAircraftDatabaseLookup? newInner, string? newVersion)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                // Wrapper already torn down — do not adopt the new service.
                (newInner as IDisposable)?.Dispose();
                return;
            }

            IAircraftDatabaseLookup? old = _inner;
            _inner = newInner;
            _currentVersion = newVersion;

            // Dispose the old inner under the same lock that serializes lookups, so no in-flight
            // lookup can still be executing against it.
            (old as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Disposes the current inner lookup service. Idempotent.
    /// </summary>
    public void Dispose()
    {
        IAircraftDatabaseLookup? old;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            old = _inner;
            _inner = null;
            _currentVersion = null;
        }

        (old as IDisposable)?.Dispose();
    }
}
