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
/// Aircraft historical data group.
/// Contains three circular buffers for time-series position, altitude, and velocity tracking.
/// Each buffer: configurable max size (default 1000 entries), ~96 KB per aircraft total.
/// Used for trail visualization (planned to be introduced later), time-series graphs, and performance analysis.
/// Buffers may be null if history tracking disabled in configuration to save memory.
/// </summary>
/// <remarks>
/// Memory usage (per aircraft with all buffers enabled, 1000 entries each):
/// - PositionHistory: ~32 KB (GeographicCoordinate + DateTime + int? per entry)
/// - AltitudeHistory: ~32 KB (Altitude + DateTime + enum per entry)
/// - VelocityHistory: ~32 KB (Velocity + DateTime + doubles per entry)
/// Total: ~96 KB per fully-tracked aircraft
/// </remarks>
public sealed record TrackedHistory
{
    /// <summary>
    /// Position snapshots over time (lat/lon coordinates with timestamps).
    /// Null if position history tracking disabled in configuration.
    /// Used for drawing aircraft trails on maps and analyzing flight paths.
    /// </summary>
    public CircularBuffer<PositionSnapshot>? PositionHistory { get; init; }

    /// <summary>
    /// Altitude snapshots over time (altitude values with timestamps and source).
    /// Null if altitude history tracking disabled in configuration.
    /// Used for altitude graphs, climb/descent rate analysis, and approach visualization.
    /// </summary>
    public CircularBuffer<AltitudeSnapshot>? AltitudeHistory { get; init; }

    /// <summary>
    /// Velocity snapshots over time (speed, heading, vertical rate with timestamps).
    /// Null if velocity history tracking disabled in configuration.
    /// Used for speed graphs, acceleration analysis, and performance profiling.
    /// </summary>
    public CircularBuffer<VelocitySnapshot>? VelocityHistory { get; init; }
}
