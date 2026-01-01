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
/// Reply Information (RI) from DF 0 and DF 16 messages.
/// Indicates either ACAS operational capabilities or maximum airspeed capability.
/// </summary>
/// <remarks>
/// The RI field serves dual purposes:
/// - Values 0, 2, 3, 4: ACAS operational status
/// - Values 8-14: Maximum airspeed capability
/// Values 1, 5, 6, 7, 15 are reserved/undefined.
/// </remarks>
public enum AcasReplyInformation
{
    /// <summary>
    /// No operating ACAS (value 0, binary 0000).
    /// Aircraft does not have ACAS or system is not operational.
    /// </summary>
    NoAcas = 0,

    /// <summary>
    /// ACAS with resolution capability inhibited (value 2, binary 0010).
    /// ACAS system is present but resolution advisories are currently inhibited.
    /// </summary>
    ResolutionCapabilityInhibited = 2,

    /// <summary>
    /// ACAS with vertical-only resolution capability (value 3, binary 0011).
    /// ACAS can issue vertical-only resolution advisories (climb/descend).
    /// </summary>
    VerticalOnlyResolutionCapability = 3,

    /// <summary>
    /// ACAS with vertical and horizontal resolution capability (value 4, binary 0100).
    /// ACAS can issue both vertical and horizontal resolution advisories.
    /// </summary>
    VerticalAndHorizontalResolutionCapability = 4,

    /// <summary>
    /// No maximum airspeed data available (value 8, binary 1000).
    /// </summary>
    NoMaximumAirspeedData = 8,

    /// <summary>
    /// Maximum airspeed less than 75 knots (value 9, binary 1001).
    /// </summary>
    MaximumAirspeedLessThan75Knots = 9,

    /// <summary>
    /// Maximum airspeed 75-150 knots (value 10, binary 1010).
    /// </summary>
    MaximumAirspeed75To150Knots = 10,

    /// <summary>
    /// Maximum airspeed 150-300 knots (value 11, binary 1011).
    /// </summary>
    MaximumAirspeed150To300Knots = 11,

    /// <summary>
    /// Maximum airspeed 300-600 knots (value 12, binary 1100).
    /// </summary>
    MaximumAirspeed300To600Knots = 12,

    /// <summary>
    /// Maximum airspeed 600-1200 knots (value 13, binary 1101).
    /// </summary>
    MaximumAirspeed600To1200Knots = 13,

    /// <summary>
    /// Maximum airspeed greater than 1200 knots (value 14, binary 1110).
    /// </summary>
    MaximumAirspeedGreaterThan1200Knots = 14

    // Values 1, 5, 6, 7, 15: Reserved/undefined
}
