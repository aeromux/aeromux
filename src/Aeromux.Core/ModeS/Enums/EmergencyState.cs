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
/// Emergency state codes from TC 28 (Aircraft Status) messages.
/// Indicates the type of emergency or priority condition reported by the aircraft.
/// </summary>
/// <remarks>
/// Emergency squawk codes are 4-digit transponder codes pilots enter to signal specific emergencies to ATC.
/// These standardized codes trigger immediate alerts and priority handling by air traffic control.
/// </remarks>
public enum EmergencyState
{
    /// <summary>
    /// No emergency (0).
    /// Normal operations.
    /// </summary>
    [JsonStringEnumMemberName("No Emergency")]
    NoEmergency = 0,

    /// <summary>
    /// General emergency - squawk 7700 (1).
    /// Indicates any emergency condition not covered by other specific codes (e.g., engine failure, fire, medical).
    /// Triggers immediate ATC priority handling and emergency services coordination.
    /// </summary>
    [JsonStringEnumMemberName("General Emergency")]
    GeneralEmergency = 1,

    /// <summary>
    /// Lifeguard/medical emergency (2).
    /// Aircraft carrying urgent medical patients or organs.
    /// May request priority routing and expedited handling.
    /// </summary>
    [JsonStringEnumMemberName("Lifeguard/Medical")]
    LifeguardMedical = 2,

    /// <summary>
    /// Minimum fuel (3).
    /// Aircraft has reached minimum fuel state, may need priority landing.
    /// Not yet an emergency but requires expedited handling to prevent fuel exhaustion.
    /// </summary>
    [JsonStringEnumMemberName("Minimum Fuel")]
    MinimumFuel = 3,

    /// <summary>
    /// No communications - squawk 7600 (4).
    /// Loss of radio communication with ATC.
    /// Aircraft will follow standard lost-comm procedures.
    /// </summary>
    [JsonStringEnumMemberName("No Communications")]
    NoCommunications = 4,

    /// <summary>
    /// Unlawful interference (hijack) - squawk 7500 (5).
    /// Aircraft hijacking or unlawful seizure.
    /// Triggers immediate security and law enforcement response.
    /// </summary>
    [JsonStringEnumMemberName("Unlawful Interference")]
    UnlawfulInterference = 5,

    /// <summary>
    /// Downed aircraft (6).
    /// Aircraft has crashed or made forced landing.
    /// Triggers emergency response and search and rescue operations.
    /// </summary>
    [JsonStringEnumMemberName("Downed Aircraft")]
    DownedAircraft = 6,

    /// <summary>
    /// Reserved (7).
    /// Reserved for future use.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved = 7
}
