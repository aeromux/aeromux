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
/// Comm-B identity reply message (DF 21).
/// Response to Comm-B interrogation containing squawk code + 56-bit BDS register data.
/// ALL BDS codes (1,0/1,7/2,0/3,0/4,0/4,4/4,5/5,0/5,3/6,0) implemented.
/// </summary>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 21).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="SquawkCode">Squawk code as 4-digit octal string.</param>
/// <param name="FlightStatus">Flight status (airborne/ground and alert conditions).</param>
/// <param name="DownlinkRequest">Downlink request field (0-31).</param>
/// <param name="UtilityMessage">Utility message field (IIS + IDS).</param>
/// <param name="BdsCode">Inferred BDS register code.</param>
/// <param name="BdsData">Parsed BDS data (specific to BDS code, null if unknown/empty).</param>
public sealed record CommBIdentityReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected,
    string SquawkCode,
    FlightStatus FlightStatus,
    int DownlinkRequest,
    int UtilityMessage,
    BdsCode BdsCode,
    BdsData? BdsData) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
