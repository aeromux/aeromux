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

using System.Diagnostics;

namespace Aeromux.Core.Timing;

/// <summary>
/// Provides high-precision timestamps using Stopwatch for sub-millisecond accuracy.
/// </summary>
/// <remarks>
/// <para>
/// This implementation anchors to system time (DateTime.UtcNow) at construction
/// and uses Stopwatch.Elapsed for high-resolution time measurement. This approach
/// combines absolute time reference with sub-millisecond precision.
/// </para>
/// <para><b>Precision Characteristics:</b></para>
/// <list type="bullet">
/// <item>Anchor time: ~1-15ms precision (limited by DateTime.UtcNow OS granularity)</item>
/// <item>Elapsed time: Sub-microsecond precision (Stopwatch uses high-frequency timer)</item>
/// <item>Combined: Sub-millisecond precision for frame-to-frame timing</item>
/// <item>Monotonic: Always increasing, immune to NTP clock adjustments during operation</item>
/// </list>
/// <para><b>MLAT Readiness:</b></para>
/// <para>
/// This precision is sufficient for MLAT client participation when combined with
/// NTP-synchronized system time. For improved accuracy, GPS PPS synchronization
/// can be added by periodically re-anchoring to GPS-disciplined system time.
/// </para>
/// <para><b>Thread Safety:</b></para>
/// <para>
/// Stopwatch.Elapsed is thread-safe for reads. Multiple threads can safely call
/// GetCurrentTimestamp() concurrently without synchronization.
/// </para>
/// </remarks>
public sealed class StopwatchTimeProvider : ITimeProvider
{
    /// <summary>
    /// Anchor time from which elapsed time is measured (set at construction).
    /// </summary>
    private readonly DateTime _anchorTime;

    /// <summary>
    /// High-precision stopwatch for measuring elapsed time since anchor.
    /// </summary>
    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// Creates a new StopwatchTimeProvider anchored to current system time.
    /// </summary>
    /// <remarks>
    /// The anchor time is captured via DateTime.UtcNow and the stopwatch
    /// is started immediately. The two operations are sequential to minimize
    /// skew between anchor and stopwatch start.
    /// </remarks>
    public StopwatchTimeProvider()
    {
        _anchorTime = DateTime.UtcNow;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Gets the current timestamp with sub-millisecond precision.
    /// </summary>
    /// <returns>
    /// Absolute UTC timestamp combining anchor time with high-precision elapsed time
    /// </returns>
    /// <remarks>
    /// Calculation: anchor time + stopwatch elapsed ticks.
    /// This preserves DateTime.Ticks precision (100ns resolution) while benefiting
    /// from Stopwatch's high-frequency timer for accurate elapsed time measurement.
    /// </remarks>
    public DateTime GetCurrentTimestamp() =>
        _anchorTime.AddTicks(_stopwatch.Elapsed.Ticks);
}
