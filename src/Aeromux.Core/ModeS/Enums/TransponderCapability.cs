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
/// Transponder capability field (CA - Capability) from DF 11 (All-Call Reply) messages.
/// Indicates the operational capabilities and status of the Mode S transponder.
/// </summary>
/// <remarks>
/// The capability field is 3 bits (values 0-7) and indicates:
/// - Level of Mode S support (Level 1 = basic, Level 2+ = enhanced)
/// - On-ground vs airborne status (for Level 2+ transponders)
/// - SI (Surveillance Identifier) code capability
/// - Special conditions (DR - Downlink Request field, FS - Flight Status)
///
/// Level 1 transponders support only basic Mode S (DF 0, 4, 5, 11).
/// Level 2+ transponders support extended squitter and enhanced surveillance.
///
/// Reference: ICAO Annex 10, Volume IV, Chapter 3.
/// </remarks>
public enum TransponderCapability
{
    /// <summary>
    /// Level 1 transponder - basic Mode S capability.
    /// Supports only DF 0, 4, 5, 11 messages (no extended squitter).
    /// </summary>
    [JsonStringEnumMemberName("Level 1")]
    Level1 = 0,

    /// <summary>
    /// Reserved for future use (CA = 1).
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved1 = 1,

    /// <summary>
    /// Reserved for future use (CA = 2).
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved2 = 2,

    /// <summary>
    /// Reserved for future use (CA = 3).
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved3 = 3,

    /// <summary>
    /// Level 2+ transponder, on-ground status.
    /// Enhanced Mode S with extended squitter capability (ADS-B).
    /// </summary>
    [JsonStringEnumMemberName("Level 2+ (On Ground)")]
    Level2PlusOnGround = 4,

    /// <summary>
    /// Level 2+ transponder, airborne status.
    /// Enhanced Mode S with extended squitter capability (ADS-B).
    /// </summary>
    [JsonStringEnumMemberName("Level 2+ (Airborne)")]
    Level2PlusAirborne = 5,

    /// <summary>
    /// Level 2+ transponder, on-ground or airborne (status uncertain).
    /// Enhanced Mode S with extended squitter capability (ADS-B).
    /// </summary>
    [JsonStringEnumMemberName("Level 2+ (On Ground or Airborne)")]
    Level2PlusOnGroundOrAirborne = 6,

    /// <summary>
    /// Special capability code indicating one of:
    /// - Downlink Request (DR) field is not zero (interrogation response pending)
    /// - Flight status indicates alert/SPI condition (FS = 2, 3, 4, or 5)
    /// Can be airborne or on-ground.
    /// </summary>
    [JsonStringEnumMemberName("DR \u2260 0 or Special Flight Status")]
    DRNotZeroOrSpecialFlightStatus = 7
}
