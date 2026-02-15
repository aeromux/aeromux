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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Short air-air surveillance message (ACAS coordination).
/// Corresponds to Downlink Format 0.
/// </summary>
/// <remarks>
/// Used for ACAS (Airborne Collision Avoidance System) coordination between aircraft.
/// DF 0 is an ACAS reply containing altitude and ACAS status fields (VS, CC, SL, RI).
/// Unlike DF 4 (ground surveillance), DF 0 uses ACAS-specific fields for collision avoidance.
/// Traffic: Less than 1% of Mode S messages.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 0).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Altitude">Decoded altitude (null if unavailable or invalid).</param>
/// <param name="VerticalStatus">Vertical status (Airborne or Ground).</param>
/// <param name="CrossLinkCapability">True if aircraft supports DF 16 coordination replies.</param>
/// <param name="SensitivityLevel">ACAS sensitivity level (0-7, where 0=inoperative).</param>
/// <param name="ReplyInformation">ACAS operational state (0=no ACAS, 2=RA active, 3=vertical RA, 4=RA terminated).</param>
public sealed record ShortAirAirSurveillance(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected,
    Altitude? Altitude,
    VerticalStatus VerticalStatus,
    bool CrossLinkCapability,
    int SensitivityLevel,
    AcasReplyInformation ReplyInformation) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
