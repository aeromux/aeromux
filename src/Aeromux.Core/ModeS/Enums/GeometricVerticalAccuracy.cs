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
/// Represents the Geometric Vertical Accuracy (GVA) from Aircraft Operational Status Messages.
/// Indicates the vertical position accuracy of the aircraft.
/// Reference: DO-260B, Table A-2-73a
/// </summary>
public enum GeometricVerticalAccuracy
{
    /// <summary>
    /// Unknown vertical accuracy or GVA ≥ 150 meters.
    /// </summary>
    [JsonStringEnumMemberName("Unknown or ≥ 150 m")]
    UnknownOrGreaterThanOrEqual150Meters = 0,

    /// <summary>
    /// Vertical accuracy is less than 150 meters.
    /// </summary>
    [JsonStringEnumMemberName("< 150 m")]
    LessThan150Meters = 1,

    /// <summary>
    /// Vertical accuracy is less than 45 meters.
    /// </summary>
    [JsonStringEnumMemberName("< 45 m")]
    LessThan45Meters = 2,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved = 3
}
