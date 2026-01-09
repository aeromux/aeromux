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

using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Surveillance identity reply message.
/// Corresponds to Downlink Format 5.
/// </summary>
/// <remarks>
/// Response to Mode S identity interrogation from ground stations.
/// Contains squawk code (4-digit octal transponder code) and flight status.
/// Special squawk codes: 7700 (emergency), 7600 (radio failure), 7500 (hijack).
/// Traffic: 5-10% of Mode S messages.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 5).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="SquawkCode">Squawk code as 4-digit octal string (e.g., "7700" for emergency).</param>
/// <param name="FlightStatus">Flight status (airborne/ground and alert conditions).</param>
public sealed record SurveillanceIdentityReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected,
    string SquawkCode,
    FlightStatus FlightStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
