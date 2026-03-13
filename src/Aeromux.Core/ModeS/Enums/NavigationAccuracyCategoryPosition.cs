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
/// Navigation Accuracy Category for Position (NACp) from Operational Status message (TC 31).
/// Indicates the 95% horizontal accuracy of the reported position.
/// </summary>
/// <remarks>
/// NACp quantifies position integrity using EPU (Estimated Position Uncertainty), defined as
/// the radius of a circle centered on the reported position where the true position lies
/// with 95% probability. Lower EPU values indicate higher accuracy.
///
/// Reference: RTCA DO-260B, Table 2-14 (NACp values and EPU thresholds).
/// NACp field is 4 bits (values 0-15).
/// Higher NACp values indicate better position accuracy (lower EPU).
/// Example: NACp=11 means EPU &lt; 3 meters (precision GPS with DGPS/RTK).
/// </remarks>
public enum NavigationAccuracyCategoryPosition
{
    /// <summary>
    /// EPU ≥ 18.52 km (10 NM) or unknown.
    /// Lowest accuracy or position source unavailable.
    /// </summary>
    [JsonStringEnumMemberName("Unknown")]
    Unknown = 0,

    /// <summary>
    /// EPU &lt; 18.52 km (10 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 10 NM")]
    LessThan10NM = 1,

    /// <summary>
    /// EPU &lt; 7.408 km (4 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 4 NM")]
    LessThan4NM = 2,

    /// <summary>
    /// EPU &lt; 3.704 km (2 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 2 NM")]
    LessThan2NM = 3,

    /// <summary>
    /// EPU &lt; 1.852 km (1 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 1 NM")]
    LessThan1NM = 4,

    /// <summary>
    /// EPU &lt; 0.926 km (0.5 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 0.5 NM")]
    LessThan05NM = 5,

    /// <summary>
    /// EPU &lt; 0.556 km (0.3 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 0.3 NM")]
    LessThan03NM = 6,

    /// <summary>
    /// EPU &lt; 0.185 km (0.1 NM).
    /// </summary>
    [JsonStringEnumMemberName("< 0.1 NM")]
    LessThan01NM = 7,

    /// <summary>
    /// EPU &lt; 0.093 km (0.05 NM / 93 meters).
    /// </summary>
    [JsonStringEnumMemberName("< 93 m")]
    LessThan93m = 8,

    /// <summary>
    /// EPU &lt; 30 meters.
    /// Good GPS accuracy.
    /// </summary>
    [JsonStringEnumMemberName("< 30 m")]
    LessThan30m = 9,

    /// <summary>
    /// EPU &lt; 10 meters.
    /// High-quality GPS with SBAS augmentation.
    /// </summary>
    [JsonStringEnumMemberName("< 10 m")]
    LessThan10m = 10,

    /// <summary>
    /// EPU &lt; 3 meters.
    /// Precision GPS (DGPS, RTK, or multi-constellation).
    /// </summary>
    [JsonStringEnumMemberName("< 3 m")]
    LessThan3m = 11,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved12 = 12,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved13 = 13,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved14 = 14,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved15 = 15
}
