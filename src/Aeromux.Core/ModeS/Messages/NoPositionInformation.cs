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

using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// No position information message (ADS-B Extended Squitter, DF 17/18, TC 0).
/// </summary>
/// <remarks>
/// Type Code 0 indicates that no position information is available. The altitude field
/// (ME bits 9-20) can still carry a barometric altitude, encoded as in TC 9-18.
/// Reference: ICAO Annex 10, Volume IV, Table 2-2 (Type Codes).
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address (24-bit identifier).</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 17 or DF 18).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if single-bit error correction was applied during CRC validation.</param>
/// <param name="Altitude">Barometric altitude (null if the altitude field is zero or invalid).</param>
public sealed record NoPositionInformation(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected,
    Altitude? Altitude) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
