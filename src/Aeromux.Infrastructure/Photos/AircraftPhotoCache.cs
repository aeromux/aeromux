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
/// In-memory cache of aircraft photo metadata, keyed by 24-bit ICAO. Bounded
/// by a hard cap (default 1000 entries) with LRU eviction as a safety net —
/// the primary eviction trigger is <c>AircraftPhotoService</c> calling
/// <see cref="Evict"/> when the tracker reports an aircraft as expired.
/// </summary>
/// <remarks>
/// The cache is intentionally dumb: it doesn't subscribe to anything and
/// has no understanding of the per-ICAO single-flight semaphore. The service
/// is responsible for serialising <see cref="Insert"/> and <see cref="Evict"/>
/// calls per-ICAO so that the insert-vs-eviction race stays correct.
/// </remarks>
public sealed class AircraftPhotoCache
{
    /// <summary>Default hard cap. Bounds memory use to a few hundred KB even if every cached entry is a positive hit.</summary>
    private const int DefaultCapacity = 1000;

    private readonly int _capacity;
    private readonly ConcurrentDictionary<uint, CacheEntry> _entries = new();

    /// <summary>Creates a cache with the given hard cap (defaults to 1000 entries).</summary>
    /// <param name="capacity">Maximum number of entries the cache will hold before LRU sweeping. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is zero or negative.</exception>
    public AircraftPhotoCache(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }
        _capacity = capacity;
    }

    /// <summary>Current number of cached entries (positive + negative).</summary>
    public int Count => _entries.Count;

    /// <summary>Hard cap on entry count — LRU sweep triggers when this is reached.</summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Looks up the cached metadata for an ICAO. On a hit, atomically advances
    /// the entry's <c>LastAccessTicks</c> via <see cref="Interlocked.Exchange(ref long, long)"/>
    /// so that frequently-read entries stay clear of the LRU eviction list.
    /// </summary>
    /// <param name="icao">24-bit ICAO address to look up.</param>
    /// <param name="metadata">Receives the cached metadata when the call returns <c>true</c>; undefined otherwise.</param>
    /// <returns><c>true</c> if an entry was found for the ICAO; <c>false</c> otherwise.</returns>
    public bool TryGet(uint icao, out PhotoMetadata metadata)
    {
        if (_entries.TryGetValue(icao, out CacheEntry? entry))
        {
            Interlocked.Exchange(ref entry.LastAccessTicks, Environment.TickCount64);
            metadata = entry.Metadata;
            return true;
        }
        metadata = null!;
        return false;
    }

    /// <summary>
    /// Inserts or replaces the entry for the given ICAO. The cache wraps the
    /// metadata in a fresh <see cref="CacheEntry"/> with
    /// <c>LastAccessTicks = Environment.TickCount64</c> — load-bearing because
    /// a default-zero ticks value would make the new entry the immediate LRU
    /// candidate. If <see cref="Count"/> is already at <see cref="Capacity"/>,
    /// an LRU sweep evicts one entry first.
    /// </summary>
    /// <param name="icao">24-bit ICAO address to associate with the metadata.</param>
    /// <param name="metadata">Photo metadata to cache. Must not be null. May be a positive or negative <see cref="PhotoMetadata"/>; both are valid cache entries.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is null.</exception>
    public void Insert(uint icao, PhotoMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (_entries.Count >= _capacity && !_entries.ContainsKey(icao))
        {
            EvictLeastRecentlyUsed();
        }

        var entry = new CacheEntry(metadata, Environment.TickCount64);
        _entries[icao] = entry;
    }

    /// <summary>
    /// Removes the cached entry for the given ICAO if present. Idempotent —
    /// calling for an unknown ICAO is a no-op.
    /// </summary>
    /// <param name="icao">24-bit ICAO address whose entry should be removed.</param>
    public void Evict(uint icao) =>
        _entries.TryRemove(icao, out _);

    /// <summary>
    /// LRU sweep: walks the (weakly-consistent) snapshot of the dictionary,
    /// finds the entry with the smallest <c>LastAccessTicks</c>, and removes
    /// it. If <see cref="ConcurrentDictionary{TKey, TValue}.TryRemove"/> returns
    /// false (a concurrent eviction beat us to it), we accept the outcome —
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
    /// Cache slot — wraps an immutable <see cref="PhotoMetadata"/> with a
    /// mutable <c>LastAccessTicks</c> for LRU bookkeeping. Class (not record)
    /// because <c>LastAccessTicks</c> needs in-place atomic mutation; record
    /// init-only properties can't be modified after construction.
    /// </summary>
    /// <remarks>
    /// <c>LastAccessTicks</c> uses <see cref="Environment.TickCount64"/> as
    /// the source — monotonic, so an entry's tick value strictly increases
    /// over its lifetime even if NTP corrects the wall clock.
    /// </remarks>
    private sealed class CacheEntry(PhotoMetadata metadata, long lastAccessTicks)
    {
        public PhotoMetadata Metadata { get; } = metadata;

        // Public mutable field (not property) so callers can pass it by ref to
        // Interlocked.Exchange / Interlocked.Read for atomic LRU updates.
        // A property would require a backing field anyway and would block ref usage.
        public long LastAccessTicks = lastAccessTicks;
    }
}
