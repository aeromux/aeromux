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

using Aeromux.Core.Timing;

namespace Aeromux.Core.Tests.Timing;

/// <summary>
/// Test implementation of ITimeProvider that returns fixed or incrementing timestamps.
/// </summary>
/// <remarks>
/// Enables deterministic testing by providing controllable timestamp behavior:
/// <list type="bullet">
/// <item>Fixed mode: Always returns the same timestamp</item>
/// <item>Auto-increment mode: Returns timestamp + increment on each call</item>
/// </list>
/// Thread-safe for reads when auto-increment is disabled.
/// </remarks>
public sealed class FixedTimeProvider : ITimeProvider
{
    private DateTime _currentTime;
    private readonly TimeSpan _increment;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a FixedTimeProvider that returns the specified timestamp.
    /// </summary>
    /// <param name="fixedTime">The timestamp to return from GetCurrentTimestamp()</param>
    /// <param name="autoIncrement">Optional increment to add after each call (default: no increment)</param>
    public FixedTimeProvider(DateTime fixedTime, TimeSpan? autoIncrement = null)
    {
        _currentTime = fixedTime;
        _increment = autoIncrement ?? TimeSpan.Zero;
    }

    /// <summary>
    /// Gets the current timestamp (fixed or auto-incremented).
    /// </summary>
    /// <returns>The current timestamp value</returns>
    /// <remarks>
    /// If auto-increment is enabled, each call advances the internal time
    /// by the increment amount. This is thread-safe via lock.
    /// </remarks>
    public DateTime GetCurrentTimestamp()
    {
        if (_increment == TimeSpan.Zero)
        {
            // No increment - return fixed time (no lock needed)
            return _currentTime;
        }

        // Auto-increment enabled - need lock for thread-safety
        lock (_lock)
        {
            DateTime result = _currentTime;
            _currentTime += _increment;
            return result;
        }
    }

    /// <summary>
    /// Manually sets the current time (useful for testing time progression).
    /// </summary>
    /// <param name="newTime">The new timestamp value</param>
    public void SetTime(DateTime newTime)
    {
        lock (_lock)
        {
            _currentTime = newTime;
        }
    }

    /// <summary>
    /// Manually advances the current time by the specified amount.
    /// </summary>
    /// <param name="delta">Amount to advance the time</param>
    public void Advance(TimeSpan delta)
    {
        lock (_lock)
        {
            _currentTime += delta;
        }
    }
}
