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

using System.Text.Json.Serialization;

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Represents the Single Antenna (SA) flag in ADS-B airborne position messages.
/// Indicates whether the aircraft is equipped with diversity antenna capability.
/// </summary>
/// <remarks>
/// This field is encoded in bit 40 of Type Code 9-18 and 20-22 messages.
/// Diversity antennas use multiple antennas to improve signal reception and reliability.
/// Reference: ICAO Annex 10, Volume IV, and DO-260B specification.
/// </remarks>
public enum AntennaFlag
{
    /// <summary>
    /// Aircraft equipped with diversity antenna capability (multiple antennas for improved signal reception).
    /// Corresponds to Single Antenna (SA) flag = 0 in ADS-B airborne position messages.
    /// </summary>
    [JsonStringEnumMemberName("Diversity")]
    DiversityAntenna = 0,

    /// <summary>
    /// Aircraft equipped with single antenna only (no diversity capability).
    /// Corresponds to Single Antenna (SA) flag = 1 in ADS-B airborne position messages.
    /// </summary>
    [JsonStringEnumMemberName("Single")]
    SingleAntenna = 1,
}
