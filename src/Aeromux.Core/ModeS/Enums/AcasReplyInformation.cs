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
/// ACAS Reply Information (RI) from DF 16 messages.
/// Indicates the operational state of the ACAS system.
/// </summary>
/// <remarks>
/// Reference: ICAO Annex 10, Volume IV, Table 3-13.
/// Only values 0, 2, 3, 4 are valid. Values 1, 5, 6, 7 are reserved.
/// </remarks>
public enum AcasReplyInformation
{
    /// <summary>
    /// No operating ACAS (value 0).
    /// Aircraft does not have ACAS or system is not operational.
    /// </summary>
    NoAcas = 0,

    /// <summary>
    /// ACAS with Resolution Advisory active (value 2).
    /// Aircraft is currently executing a resolution advisory (climb/descend command).
    /// </summary>
    ResolutionAdvisoryActive = 2,

    /// <summary>
    /// ACAS with Vertical-only Resolution Advisory active (value 3).
    /// Aircraft is executing a vertical-only RA (no horizontal component).
    /// </summary>
    VerticalOnlyRA = 3,

    /// <summary>
    /// ACAS with Resolution Advisory terminated (value 4).
    /// RA has recently ended, aircraft returning to normal flight.
    /// </summary>
    ResolutionAdvisoryTerminated = 4

    // Values 1, 5, 6, 7: Reserved (not defined by ICAO)
}
