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
/// Surface movement (ground speed) bands of the 7-bit movement field in surface position
/// messages (TC 5-8).
/// </summary>
/// <remarks>
/// The members are speed bands, not raw field values; <c>MessageParser.DecodeMovement</c>
/// maps the raw value to its band and to a speed in knots.
/// Raw encoding: 0 no information, 1 stopped, 2-8 below 1 kt (0.125 kt steps),
/// 9-12 1-2 kt (0.25 kt), 13-38 2-15 kt (0.5 kt), 39-93 15-70 kt (1 kt),
/// 94-108 70-100 kt (2 kt), 109-123 100-175 kt (5 kt), 124 175 kt or more, 125-127 reserved.
/// Reference: ICAO Doc 9871 (surface position format).
/// </remarks>
public enum SurfaceMovement
{
    /// <summary>No information available (raw 0).</summary>
    [JsonStringEnumMemberName("No Information")]
    NoInformation = 0,

    /// <summary>Aircraft stopped, ground speed below 0.125 kt (raw 1).</summary>
    [JsonStringEnumMemberName("Stopped")]
    Stopped = 1,

    /// <summary>Ground speed 0.125 to 1 kt (raw 2-8).</summary>
    [JsonStringEnumMemberName("< 1 kts")]
    Below1Kt = 2,

    /// <summary>Ground speed 1 to 2 kt (raw 9-12).</summary>
    [JsonStringEnumMemberName("1-2 kts")]
    From1To2Kt = 3,

    /// <summary>Ground speed 2 to 15 kt (raw 13-38).</summary>
    [JsonStringEnumMemberName("2-15 kts")]
    From2To15Kt = 4,

    /// <summary>Ground speed 15 to 70 kt (raw 39-93).</summary>
    [JsonStringEnumMemberName("15-70 kts")]
    From15To70Kt = 5,

    /// <summary>Ground speed 70 to 100 kt (raw 94-108).</summary>
    [JsonStringEnumMemberName("70-100 kts")]
    From70To100Kt = 6,

    /// <summary>Ground speed 100 to 175 kt (raw 109-123).</summary>
    [JsonStringEnumMemberName("100-175 kts")]
    From100To175Kt = 7,

    /// <summary>Ground speed 175 kt or more (raw 124).</summary>
    [JsonStringEnumMemberName("≥ 175 kts")]
    AtLeast175Kt = 8,

    /// <summary>Reserved values (raw 125-127).</summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved = 9
}
