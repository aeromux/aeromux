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
/// Specifies the type of altitude measurement.
/// Different altitude types are used for different aviation purposes.
/// </summary>
public enum AltitudeType
{
    /// <summary>
    /// Barometric altitude (pressure altitude based on standard pressure 1013.25 hPa / 29.92 inHg).
    /// Most common for airborne operations and ATC separation.
    /// Varies with atmospheric pressure changes; same physical height shows different barometric readings
    /// depending on weather. All aircraft in an area use the same pressure reference for safe separation.
    /// Source: TC 9-18, DF 4, DF 20.
    /// </summary>
    Barometric,

    /// <summary>
    /// Geometric altitude (GNSS-based height above WGS84 ellipsoid).
    /// WGS84 (World Geodetic System 1984) is the standard Earth reference ellipsoid used by GPS.
    /// More accurate than barometric for absolute position reporting but not used for ATC separation.
    /// Typically 50-100 feet higher than barometric altitude due to geoid-ellipsoid separation.
    /// Source: TC 20-22, derived from TC 19 delta.
    /// </summary>
    Geometric,

    /// <summary>
    /// Ground level (0 feet altitude, used for surface position reports).
    /// Indicates aircraft is on the ground (taxiing, parked, or ground operations).
    /// Source: TC 5-8 (surface position messages).
    /// </summary>
    Ground
}
