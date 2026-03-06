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

namespace Aeromux.Core.ModeS;

/// <summary>
/// Filters duplicate Mode S frames within a configurable time window.
/// Prevents expensive parsing operations on frames caused by FRUIT (False Replies
/// Unsynchronized to Interrogator Transmissions), multipath propagation, and multiple
/// interrogator responses.
/// </summary>
/// <remarks>
/// <para><b>Why Deduplication is Needed:</b></para>
/// <list type="bullet">
/// <item>FRUIT: Transponder replies to interrogators not visible to receiver</item>
/// <item>Multipath: Signal reflections create duplicate receptions</item>
/// <item>Multiple Interrogators: Same aircraft responds to different ground stations</item>
/// <item>These duplicates are legitimate RF phenomena, not detection bugs</item>
/// <item>Parsing is expensive (~30% CPU savings by filtering duplicates early)</item>
/// </list>
///
/// <para><b>Deduplication Strategy:</b></para>
/// <list type="bullet">
/// <item>Track unique frames by content (byte array value comparison)</item>
/// <item>50ms default window: legitimate Mode S retransmissions are 400-600ms apart</item>
/// <item>LRU eviction when cache exceeds maxTrackedFrames to prevent unbounded growth</item>
/// <item>O(1) lookup using Dictionary with ByteArrayComparer</item>
/// </list>
///
/// <para><b>Integration Point:</b></para>
/// <para>
/// Deduplication occurs after CRC validation but before parsing:
/// Raw Frame → CRC Validation → Confidence Tracking → **Deduplication** → Parsing → Output
/// </para>
/// </remarks>
public sealed class FrameDeduplicator
{
    private readonly int _deduplicationWindowMs;
    private readonly int _maxTrackedFrames;

    // Node map: frame data → LinkedList node for O(1) lookup and removal
    private readonly Dictionary<byte[], LinkedListNode<(byte[] Data, long Timestamp)>> _nodeMap;

    // LRU tracking: ordered list of (frame data, timestamp) for eviction
    private readonly LinkedList<(byte[] Data, long Timestamp)> _lruList;

    // Statistics
    public long TotalFramesProcessed { get; private set; }
    public long DuplicatesFiltered { get; private set; }
    public long CacheEvictions { get; private set; }

    /// <summary>
    /// Initializes a new frame deduplicator.
    /// </summary>
    /// <param name="deduplicationWindow">Time window in milliseconds for considering frames as duplicates (default: 50ms).</param>
    /// <param name="maxTrackedFrames">Maximum number of unique frames to track before LRU eviction (default: 1000).</param>
    public FrameDeduplicator(int deduplicationWindow = 50, int maxTrackedFrames = 1000)
    {
        if (deduplicationWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deduplicationWindow), "Must be greater than 0");
        }

        if (maxTrackedFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTrackedFrames), "Must be greater than 0");
        }

        _deduplicationWindowMs = deduplicationWindow;
        _maxTrackedFrames = maxTrackedFrames;
        _nodeMap = new Dictionary<byte[], LinkedListNode<(byte[] Data, long Timestamp)>>(maxTrackedFrames, ByteArrayComparer.Instance);
        _lruList = new LinkedList<(byte[] Data, long Timestamp)>();
    }

    /// <summary>
    /// Checks if a frame is a duplicate within the deduplication window.
    /// </summary>
    /// <param name="frameData">Frame data bytes (7 or 14 bytes).</param>
    /// <param name="currentTimestamp">Current timestamp for the frame.</param>
    /// <returns>true if frame is a duplicate (seen within deduplication window); false if new or outside window.</returns>
    /// <remarks>
    /// <para><b>Algorithm:</b></para>
    /// <list type="number">
    /// <item>Convert timestamp to milliseconds since epoch for comparison</item>
    /// <item>Look up frame in cache using content-based comparison</item>
    /// <item>If found and within window: mark as duplicate, update timestamp</item>
    /// <item>If not found or outside window: add to cache, perform LRU eviction if needed</item>
    /// </list>
    /// </remarks>
    public bool IsDuplicate(byte[] frameData, DateTime currentTimestamp)
    {
        ArgumentNullException.ThrowIfNull(frameData);

        TotalFramesProcessed++;

        long currentMs = new DateTimeOffset(currentTimestamp).ToUnixTimeMilliseconds();

        // Check if frame exists in cache
        if (_nodeMap.TryGetValue(frameData, out LinkedListNode<(byte[] Data, long Timestamp)>? node))
        {
            long timeDiffMs = currentMs - node.Value.Timestamp;

            if (timeDiffMs <= _deduplicationWindowMs)
            {
                // Duplicate detected within window
                DuplicatesFiltered++;

                // Update timestamp for this frame (keep it fresh in cache)
                node.Value = (node.Value.Data, currentMs);

                return true;
            }
            else
            {
                // Outside window - treat as new frame
                // Remove old entry and add new one (effectively updates timestamp)
                _lruList.Remove(node);
                AddToCache(frameData, currentMs);

                return false;
            }
        }
        else
        {
            // New frame - add to cache
            AddToCache(frameData, currentMs);

            return false;
        }
    }

    /// <summary>
    /// Adds a frame to the cache and LRU list.
    /// Performs LRU eviction if cache size exceeds maxTrackedFrames.
    /// </summary>
    private void AddToCache(byte[] frameData, long timestamp)
    {
        // Check if we need to evict the oldest entry
        if (_nodeMap.Count >= _maxTrackedFrames)
        {
            EvictOldest();
        }

        // Add to LRU list and node map
        LinkedListNode<(byte[] Data, long Timestamp)> node = _lruList.AddLast((frameData, timestamp));
        _nodeMap[frameData] = node;
    }

    /// <summary>
    /// Evicts the oldest (least recently used) entry from the cache.
    /// </summary>
    private void EvictOldest()
    {
        if (_lruList.First == null)
        {
            return;
        }

        (byte[] oldestData, long _) = _lruList.First.Value;

        _nodeMap.Remove(oldestData);
        _lruList.RemoveFirst();

        CacheEvictions++;
    }

    /// <summary>
    /// Gets the current number of unique frames being tracked.
    /// </summary>
    public int CurrentCacheSize => _nodeMap.Count;

    /// <summary>
    /// Resets all statistics counters.
    /// </summary>
    public void ResetStatistics()
    {
        TotalFramesProcessed = 0;
        DuplicatesFiltered = 0;
        CacheEvictions = 0;
    }

    /// <summary>
    /// Clears the entire cache (for testing or reset scenarios).
    /// </summary>
    public void ClearCache()
    {
        _nodeMap.Clear();
        _lruList.Clear();
    }
}
