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

namespace Aeromux.Infrastructure.Photos;

/// <summary>
/// In-memory cache of aircraft photo metadata, keyed by 24-bit ICAO. The primary
/// eviction trigger is <c>AircraftPhotoService</c> calling <see cref="Evict"/> when
/// the tracker reports an aircraft as expired. Two limits back it up: a hard cap
/// (default 1000 entries) with LRU eviction, and a time to live (default 12 hours)
/// after which an entry is never served.
/// </summary>
/// <remarks>
/// The cache is intentionally dumb: it doesn't subscribe to anything and
/// has no understanding of the per-ICAO single-flight semaphore. The service
/// is responsible for serializing <see cref="Insert"/> and <see cref="Evict"/>
/// calls per-ICAO so that the insert-vs-eviction race stays correct.
/// <para>
/// The time to live exists because Planespotters allows its metadata to be cached
/// for at most 24 hours, and tracker expiry alone does not guarantee that: an
/// aircraft parked within range of the receiver can stay tracked for days. Twelve
/// hours keeps a margin that does not depend on when expired entries are swept.
/// </para>
/// </remarks>
public sealed class AircraftPhotoCache
{
    /// <summary>Default hard cap. Bounds memory use to a few hundred KB even if every cached entry is a positive hit.</summary>
    private const int DefaultCapacity = 1000;

    /// <summary>Default time to live, well inside the 24-hour limit Planespotters sets on cached metadata.</summary>
    private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromHours(12);

    private readonly int _capacity;
    private readonly TimeSpan _timeToLive;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<uint, CacheEntry> _entries = new();

    /// <summary>Creates a cache with the given hard cap and time to live.</summary>
    /// <param name="capacity">Maximum number of entries the cache will hold before LRU sweeping. Must be positive.</param>
    /// <param name="timeToLive">How long an entry may be served after insertion. Defaults to 12 hours. Must be positive.</param>
    /// <param name="timeProvider">Clock for entry ages and LRU bookkeeping. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> or <paramref name="timeToLive"/> is zero or negative.</exception>
    public AircraftPhotoCache(
        int capacity = DefaultCapacity,
        TimeSpan? timeToLive = null,
        TimeProvider? timeProvider = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        TimeSpan ttl = timeToLive ?? DefaultTimeToLive;
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), ttl, "Time to live must be positive.");
        }

        _capacity = capacity;
        _timeToLive = ttl;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Current number of cached entries (positive + negative).</summary>
    public int Count => _entries.Count;

    /// <summary>Hard cap on entry count; an LRU sweep triggers when this is reached.</summary>
    public int Capacity => _capacity;

    /// <summary>How long an entry may be served after it was inserted.</summary>
    public TimeSpan TimeToLive => _timeToLive;

    /// <summary>
    /// Looks up the cached metadata for an ICAO. On a hit, atomically advances
    /// the entry's <c>LastAccessTicks</c> via <see cref="Interlocked.Exchange(ref long, long)"/>
    /// so that frequently-read entries stay clear of the LRU eviction list. An entry
    /// older than <see cref="TimeToLive"/> is removed and reported as a miss.
    /// </summary>
    /// <param name="icao">24-bit ICAO address to look up.</param>
    /// <param name="metadata">Receives the cached metadata when the call returns <c>true</c>; undefined otherwise.</param>
    /// <returns><c>true</c> if a live entry was found for the ICAO; <c>false</c> otherwise.</returns>
    public bool TryGet(uint icao, out PhotoMetadata metadata)
    {
        if (_entries.TryGetValue(icao, out CacheEntry? entry))
        {
            if (IsExpired(entry))
            {
                // Remove only this entry: a concurrent insert may already have replaced it.
                _entries.TryRemove(KeyValuePair.Create(icao, entry));
            }
            else
            {
                Interlocked.Exchange(ref entry.LastAccessTicks, _timeProvider.GetTimestamp());
                metadata = entry.Metadata;
                return true;
            }
        }
        metadata = null!;
        return false;
    }

    /// <summary>
    /// Inserts or replaces the entry for the given ICAO. The cache wraps the
    /// metadata in a fresh <see cref="CacheEntry"/> with
    /// <c>LastAccessTicks</c> set to the current timestamp. That is load-bearing, because
    /// a default-zero ticks value would make the new entry the immediate LRU
    /// candidate. Expired entries are swept first; if <see cref="Count"/> is then
    /// still at <see cref="Capacity"/>, an LRU sweep evicts one entry.
    /// </summary>
    /// <param name="icao">24-bit ICAO address to associate with the metadata.</param>
    /// <param name="metadata">Photo metadata to cache. Must not be null. May be a positive or negative <see cref="PhotoMetadata"/>; both are valid cache entries.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is null.</exception>
    public void Insert(uint icao, PhotoMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        RemoveExpired();

        if (_entries.Count >= _capacity && !_entries.ContainsKey(icao))
        {
            EvictLeastRecentlyUsed();
        }

        long now = _timeProvider.GetTimestamp();
        var entry = new CacheEntry(metadata, now, now);
        _entries[icao] = entry;
    }

    /// <summary>
    /// Removes the cached entry for the given ICAO if present. Idempotent:
    /// calling for an unknown ICAO is a no-op.
    /// </summary>
    /// <param name="icao">24-bit ICAO address whose entry should be removed.</param>
    public void Evict(uint icao) =>
        _entries.TryRemove(icao, out _);

    /// <summary>
    /// Whether an entry has outlived <see cref="TimeToLive"/>.
    /// </summary>
    private bool IsExpired(CacheEntry entry) =>
        _timeProvider.GetElapsedTime(entry.InsertedTimestamp) >= _timeToLive;

    /// <summary>
    /// Removes every expired entry. Runs on each insert, which keeps entries that
    /// are never read again from lingering past their time to live; at most
    /// <see cref="Capacity"/> entries are walked, and inserts happen only on an
    /// upstream lookup.
    /// </summary>
    private void RemoveExpired()
    {
        foreach (KeyValuePair<uint, CacheEntry> kvp in _entries)
        {
            if (IsExpired(kvp.Value))
            {
                _entries.TryRemove(kvp);
            }
        }
    }

    /// <summary>
    /// LRU sweep: walks the (weakly-consistent) snapshot of the dictionary,
    /// finds the entry with the smallest <c>LastAccessTicks</c>, and removes
    /// it. If <see cref="ConcurrentDictionary{TKey, TValue}.TryRemove"/> returns
    /// false (a concurrent eviction beat us to it), we accept the outcome:
    /// the cache may briefly hold one extra entry until the next sweep, which
    /// is harmless.
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        long oldestTicks = long.MaxValue;
        uint oldestKey = 0;
        bool found = false;

        foreach (KeyValuePair<uint, CacheEntry> kvp in _entries)
        {
            long ticks = Interlocked.Read(ref kvp.Value.LastAccessTicks);
            if (ticks < oldestTicks)
            {
                oldestTicks = ticks;
                oldestKey = kvp.Key;
                found = true;
            }
        }

        if (found)
        {
            _entries.TryRemove(oldestKey, out _);
        }
    }

    /// <summary>
    /// Cache slot: wraps an immutable <see cref="PhotoMetadata"/> with its
    /// insertion timestamp and a mutable <c>LastAccessTicks</c> for LRU
    /// bookkeeping. Class (not record) because <c>LastAccessTicks</c> needs
    /// in-place atomic mutation; record init-only properties can't be modified
    /// after construction.
    /// </summary>
    /// <remarks>
    /// Both timestamps come from <see cref="TimeProvider.GetTimestamp"/>, which is
    /// monotonic, so ages and LRU order are unaffected if NTP corrects the wall clock.
    /// </remarks>
    private sealed class CacheEntry(PhotoMetadata metadata, long insertedTimestamp, long lastAccessTicks)
    {
        public PhotoMetadata Metadata { get; } = metadata;

        public long InsertedTimestamp { get; } = insertedTimestamp;

        // Public mutable field (not property) so callers can pass it by ref to
        // Interlocked.Exchange / Interlocked.Read for atomic LRU updates.
        // A property would require a backing field anyway and would block ref usage.
        public long LastAccessTicks = lastAccessTicks;
    }
}
