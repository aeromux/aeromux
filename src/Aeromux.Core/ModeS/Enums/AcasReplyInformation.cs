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
/// ACAS Reply Information (RI) from DF 0 and DF 16 messages.
/// Indicates the type of reply to interrogating aircraft and ACAS operational capabilities.
/// </summary>
/// <remarks>
/// Reference: https://mode-s.org/1090mhz/content/mode-s/4-acas.html
/// Only values 0, 2, 3, 7 are valid ACAS values. Other values are not part of ACAS.
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
    /// ACAS with vertical and horizontal resolution capability (value 7, binary 0111).
    /// ACAS can issue both vertical and horizontal resolution advisories.
    /// </summary>
    VerticalAndHorizontalResolutionCapability = 7

    // Values 1, 4, 5, 6, 8-15: Not part of ACAS specification
}
