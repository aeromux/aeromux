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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Velocity subtype for Type Code 19 (Airborne Velocity) messages.
/// Indicates whether velocity is ground speed or airspeed, and subsonic or supersonic.
/// </summary>
/// <remarks>
/// Type Code 19 has 4 subtypes:
/// - Subtypes 1-2: Ground speed (East/West and North/South velocity components)
/// - Subtypes 3-4: Airspeed (heading and airspeed magnitude)
/// - Subtypes 2 and 4: Supersonic (4x multiplier for speed values)
///
/// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.9.
/// </remarks>
public enum VelocitySubtype
{
    /// <summary>
    /// Ground speed (subsonic, multiplier = 1).
    /// Provides East/West and North/South velocity components.
    /// Speed range: 0-1023 knots.
    /// </summary>
    GroundSpeedSubsonic = 1,

    /// <summary>
    /// Ground speed (supersonic, multiplier = 4).
    /// Provides East/West and North/South velocity components.
    /// Speed range: 0-4092 knots.
    /// </summary>
    GroundSpeedSupersonic = 2,

    /// <summary>
    /// Airspeed (subsonic, multiplier = 1).
    /// Provides heading and airspeed magnitude.
    /// Airspeed can be IAS (Indicated Airspeed) or TAS (True Airspeed) depending on aircraft equipment.
    /// Speed range: 0-1023 knots.
    /// </summary>
    AirspeedSubsonic = 3,

    /// <summary>
    /// Airspeed (supersonic, multiplier = 4).
    /// Provides heading and airspeed magnitude.
    /// Airspeed can be IAS (Indicated Airspeed) or TAS (True Airspeed) depending on aircraft equipment.
    /// Speed range: 0-4092 knots.
    /// </summary>
    AirspeedSupersonic = 4
}
