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
/// Source of the vertical rate in an airborne velocity message (TC 19, ME bit 36).
/// </summary>
/// <remarks>
/// The two sources can differ noticeably: a barometric rate follows pressure altitude, a
/// GNSS rate follows height above the WGS84 ellipsoid.
/// </remarks>
public enum VerticalRateSource
{
    /// <summary>
    /// Geometric vertical rate from GNSS (source bit 0).
    /// </summary>
    [JsonStringEnumMemberName("GNSS")]
    Geometric = 0,

    /// <summary>
    /// Barometric vertical rate from the air data system (source bit 1).
    /// </summary>
    [JsonStringEnumMemberName("Barometric")]
    Barometric = 1
}
