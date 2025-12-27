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
/// </summary>
public enum VelocityType
{
    /// <summary>
    /// Ground speed (speed relative to the ground).
    /// Accounts for wind effects.
    /// </summary>
    GroundSpeed,

    /// <summary>
    /// True airspeed (speed relative to the air mass).
    /// Does not account for wind.
    /// </summary>
    TrueAirspeed,

    /// <summary>
    /// Indicated airspeed (speed shown on cockpit instruments).
    /// Uncorrected for altitude/temperature.
    /// </summary>
    IndicatedAirspeed
}
