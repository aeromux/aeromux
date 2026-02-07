// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Specifies the type of velocity measurement.
/// Different velocity types serve different purposes in aviation operations.
/// </summary>
public enum VelocityType
{
    /// <summary>
    /// Ground speed (speed relative to the ground, accounting for wind effects).
    /// Used for navigation, flight planning, and ETA calculations.
    /// GS = TAS + wind component (can be higher or lower than TAS depending on wind).
    /// Source: TC 19 (subtype 1-2), TC 5-8 (surface), BDS 5,0.
    /// </summary>
    GroundSpeed,

    /// <summary>
    /// True airspeed (speed relative to the air mass, does not account for wind).
    /// Used for aircraft performance calculations, fuel burn, and wind determination.
    /// TAS = IAS corrected for altitude and temperature (always higher than IAS at altitude).
    /// Source: TC 19 (subtype 3-4), BDS 5,0, BDS 5,3.
    /// </summary>
    TrueAirspeed,

    /// <summary>
    /// Indicated airspeed (speed shown on cockpit instruments, uncorrected for altitude/temperature).
    /// Used for aircraft control and structural limits (stall speed, max speed).
    /// IAS is the direct pitot-static reading, most relevant for aerodynamic forces on the aircraft.
    /// At sea level ISA conditions: IAS = TAS, but at altitude: IAS &lt; TAS.
    /// Source: BDS 5,3, BDS 6,0.
    /// </summary>
    IndicatedAirspeed
}
