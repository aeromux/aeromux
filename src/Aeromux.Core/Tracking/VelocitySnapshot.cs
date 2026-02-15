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
/// Velocity snapshot record for historical tracking.
/// Immutable time-stamped velocity measurement with airborne and surface velocity data.
/// Stored in CircularBuffer for speed graphs, acceleration analysis, and performance profiling.
/// Supports both airborne (TC 19) and surface (TC 5-8) velocity sources in a single snapshot.
/// </summary>
/// <param name="Timestamp">UTC timestamp when this velocity was observed</param>
/// <param name="Velocity">Airborne velocity from TC 19 (ground speed, true airspeed, or IAS based on VelocitySubtype), null if only surface data available</param>
/// <param name="Heading">True heading in degrees (0-359.9, null if unavailable) from TC 19 subtype 3-4 - direction nose points</param>
/// <param name="Track">Ground track angle in degrees (0-359.9, null if unavailable) from TC 19 subtype 1-2 - direction of movement over ground</param>
/// <param name="GroundSpeed">Surface ground speed from TC 5-8 (0-199 knots, null if unavailable) - airport surface movement during taxi</param>
/// <param name="GroundTrack">Surface ground track from TC 5-8 (0-360° with 2.8125° resolution, null if unavailable) - taxi direction on ground</param>
/// <param name="VerticalRate">Vertical rate in feet per minute (positive = climbing, negative = descending, null if unavailable) from TC 19</param>
/// <param name="VelocitySubtype">Velocity subtype from TC 19: GroundSpeedSubsonic/Supersonic or AirspeedSubsonic/Supersonic, null if only surface data</param>
public sealed record VelocitySnapshot(
    DateTime Timestamp,
    Velocity? Velocity,
    double? Heading,
    double? Track,
    Velocity? GroundSpeed,
    double? GroundTrack,
    int? VerticalRate,
    VelocitySubtype? VelocitySubtype
);
