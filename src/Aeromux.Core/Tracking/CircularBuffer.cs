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
/// Thread-safe circular buffer with fixed capacity.
/// Implements a ring buffer where the oldest entries are automatically overwritten when full.
/// Used for historical data tracking (position, altitude, velocity snapshots over time).
/// Provides O(1) add operation and O(n) retrieval in chronological order.
/// </summary>
/// <typeparam name="T">Type of items to store (typically snapshot records)</typeparam>
/// <remarks>
/// Thread-safety: All public methods use lock for synchronization.
/// Memory efficiency: Fixed allocation, no dynamic resizing or GC pressure.
/// </remarks>
public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private readonly long[] _sequenceIds;  // Parallel array storing sequence ID per slot
    private readonly int _capacity;
    private int _head;    // Next write position (wraps around using modulo)
    private int _count;   // Current item count (0 to _capacity)
    private long _nextSequenceId = 1;      // Next sequence ID to assign (starts at 1)
    private readonly Lock _lock = new();

    /// <summary>
    /// Creates a new circular buffer with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of items to store</param>
    /// <exception cref="ArgumentException">Thrown if capacity is not positive</exception>
    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be positive", nameof(capacity));
        }

        _capacity = capacity;
        _buffer = new T[capacity];
        _sequenceIds = new long[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Adds an item to the buffer. If full, overwrites the oldest entry.
    /// Thread-safe and lock-protected. O(1) time complexity.
    /// </summary>
    /// <param name="item">Item to add to the buffer</param>
    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _sequenceIds[_head] = _nextSequenceId++;
            _head = (_head + 1) % _capacity;  // Wrap around
            if (_count < _capacity)
            {
                _count++;  // Buffer not yet full, increment count
            }
            // If full, _count stays at _capacity, and we've overwritten the oldest entry
        }
    }

    /// <summary>
    /// Gets all items in chronological order (oldest to newest).
    /// Returns a snapshot copy (thread-safe for concurrent reads).
    /// Creates a new array allocation - safe to iterate without locking.
    /// </summary>
    /// <returns>Array of all items in chronological order, or empty array if buffer empty</returns>
    public T[] GetAll()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }

            var result = new T[_count];

            // Calculate starting position for chronological order:
            // - If buffer not yet full: start from index 0
            // - If buffer is full: start from _head (which points to oldest overwritten entry)
            int start = (_count < _capacity) ? 0 : _head;

            for (int i = 0; i < _count; i++)
            {
                result[i] = _buffer[(start + i) % _capacity];
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the most recent N items in chronological order (oldest to newest among the N).
    /// If buffer contains fewer items than requested, returns all available items.
    /// Thread-safe snapshot copy.
    /// </summary>
    /// <param name="count">Number of recent items to retrieve</param>
    /// <returns>Array of most recent items in chronological order, or empty array if buffer empty</returns>
    /// <example>
    /// Buffer: [1, 2, 3, 4, 5] with capacity=5, GetRecent(3) returns [3, 4, 5]
    /// </example>
    public T[] GetRecent(int count)
    {
        lock (_lock)
        {
            int actualCount = Math.Min(count, _count);
            if (actualCount == 0)
            {
                return Array.Empty<T>();
            }

            var result = new T[actualCount];
            // Calculate start position: go back actualCount positions from head
            int start = (_head - actualCount + _capacity) % _capacity;

            for (int i = 0; i < actualCount; i++)
            {
                result[i] = _buffer[(start + i) % _capacity];
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the current number of items in the buffer (0 to Capacity).
    /// Thread-safe property access.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Gets the maximum capacity of the buffer (fixed at construction).
    /// This value never changes after initialization.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Sequence ID of the oldest entry in the buffer, or null when empty.
    /// Thread-safe property access.
    /// </summary>
    public long? MinSequenceId
    {
        get
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    return null;
                }

                int start = (_count < _capacity) ? 0 : _head;
                return _sequenceIds[start];
            }
        }
    }

    /// <summary>
    /// Sequence ID of the newest entry in the buffer, or null when empty.
    /// Thread-safe property access.
    /// </summary>
    public long? MaxSequenceId
    {
        get
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    return null;
                }

                int newest = (_head - 1 + _capacity) % _capacity;
                return _sequenceIds[newest];
            }
        }
    }

    /// <summary>
    /// Gets all items with their sequence IDs in chronological order (oldest to newest).
    /// Returns a snapshot copy (thread-safe for concurrent reads).
    /// </summary>
    /// <returns>Array of tuples with items and sequence IDs, or empty array if buffer empty</returns>
    public (T Item, long SequenceId)[] GetAllWithSequenceIds()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                return Array.Empty<(T, long)>();
            }

            var result = new (T, long)[_count];
            int start = (_count < _capacity) ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _capacity;
                result[i] = (_buffer[idx], _sequenceIds[idx]);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the most recent N items with their sequence IDs in chronological order.
    /// If buffer contains fewer items than requested, returns all available items.
    /// Thread-safe snapshot copy.
    /// </summary>
    /// <param name="count">Number of recent items to retrieve</param>
    /// <returns>Array of tuples with items and sequence IDs, or empty array if buffer empty</returns>
    public (T Item, long SequenceId)[] GetRecentWithSequenceIds(int count)
    {
        lock (_lock)
        {
            int actualCount = Math.Min(count, _count);
            if (actualCount == 0)
            {
                return Array.Empty<(T, long)>();
            }

            var result = new (T, long)[actualCount];
            int start = (_head - actualCount + _capacity) % _capacity;
            for (int i = 0; i < actualCount; i++)
            {
                int idx = (start + i) % _capacity;
                result[i] = (_buffer[idx], _sequenceIds[idx]);
            }

            return result;
        }
    }

    /// <summary>
    /// Returns entries with sequence ID greater than afterSequenceId, ordered oldest to newest.
    /// If afterSequenceId is below the minimum, all entries are returned.
    /// If afterSequenceId is at or above the maximum, an empty array is returned.
    /// Thread-safe snapshot copy.
    /// </summary>
    /// <param name="afterSequenceId">Return only entries with sequence ID greater than this value</param>
    /// <returns>Array of tuples with qualifying items and sequence IDs</returns>
    public (T Item, long SequenceId)[] GetAfter(long afterSequenceId)
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                return [];
            }

            int start = (_count < _capacity) ? 0 : _head;

            // Sequence IDs are monotonically increasing and the buffer is in chronological
            // order, so once we find the first qualifying entry, all subsequent entries
            // also qualify.
            int firstMatch = -1;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _capacity;
                if (_sequenceIds[idx] > afterSequenceId)
                {
                    firstMatch = i;
                    break;
                }
            }

            if (firstMatch < 0)
            {
                return [];
            }

            int resultCount = _count - firstMatch;
            var result = new (T, long)[resultCount];
            for (int i = 0; i < resultCount; i++)
            {
                int idx = (start + firstMatch + i) % _capacity;
                result[i] = (_buffer[idx], _sequenceIds[idx]);
            }

            return result;
        }
    }
}
