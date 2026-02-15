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
/// Aircraft Status message subtype (TC 28).
/// Indicates the type of status information contained in the message.
/// </summary>
/// <remarks>
/// Reference: ICAO Annex 10, Volume IV, 3.1.2.8.8 (Aircraft Status).
/// Subtype field is 3 bits (values 0-7), but only 0-2 are currently defined.
/// Values 3-7 are reserved for future use.
/// </remarks>
public enum AircraftStatusSubtype
{
    /// <summary>
    /// No information available (subtype 0).
    /// Status message present but no specific information provided.
    /// </summary>
    NoInformation = 0,

    /// <summary>
    /// Emergency/priority status (subtype 1).
    /// Contains emergency state (7 standard codes) and squawk code.
    /// Emergency states: none, general, medical, no radio, unlawful interference, downed, reserved.
    /// </summary>
    EmergencyPriority = 1,

    /// <summary>
    /// TCAS/ACAS Resolution Advisory (subtype 2).
    /// Indicates active TCAS Resolution Advisory with detailed RA information.
    /// Contains threat identity, RA flags, and complementary/corrective advisories.
    /// </summary>
    TcasResolutionAdvisory = 2

    // Values 3-7: Reserved for future use (not yet defined by ICAO)
}
