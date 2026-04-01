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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Combined state snapshot for historical tracking.
/// Captures position, altitude, and velocity in a single record at each position update.
/// Stored in CircularBuffer for correlated trail visualization without client-side merging.
/// </summary>
/// <param name="Timestamp">UTC timestamp when position was updated (snapshot trigger)</param>
/// <param name="Position">Geographic coordinate — always non-null (triggered by position update)</param>
/// <param name="NACp">Navigation Accuracy Category for Position, null if unavailable</param>
/// <param name="Altitude">Current altitude in feet (barometric preferred, geometric fallback), null if unavailable</param>
/// <param name="AltitudeType">Type of altitude value: Barometric or Geometric, null if no altitude</param>
/// <param name="Speed">Airborne velocity from TC 19, null if unavailable</param>
/// <param name="Heading">True heading from TC 19 subtype 3-4 in degrees, null if unavailable</param>
/// <param name="Track">Ground track angle from TC 19 subtype 1-2 in degrees, null if unavailable</param>
/// <param name="SpeedOnGround">Surface ground speed from TC 5-8, null if unavailable</param>
/// <param name="TrackOnGround">Surface ground track from TC 5-8 in degrees, null if unavailable</param>
/// <param name="VerticalRate">Vertical rate in feet/min from TC 19, null if unavailable</param>
public sealed record StateSnapshot(
    DateTime Timestamp,
    GeographicCoordinate Position,
    NavigationAccuracyCategoryPosition? NACp,
    int? Altitude,
    AltitudeType? AltitudeType,
    Velocity? Speed,
    double? Heading,
    double? Track,
    Velocity? SpeedOnGround,
    double? TrackOnGround,
    int? VerticalRate
);
