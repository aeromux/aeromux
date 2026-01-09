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
/// Aircraft identification and category (callsign) message.
/// Corresponds to Type Code 1-4 in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <remarks>
/// TC value determines the general class:
/// - TC 1: No category information
/// - TC 2: Surface vehicles
/// - TC 3: Ground obstructions
/// - TC 4: Aircraft (with wake vortex categories: Light, Small, Large, Heavy, etc.)
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Callsign">Aircraft callsign (flight number), 8 characters, trimmed.</param>
/// <param name="Category">Aircraft category (wake vortex classification and vehicle type).</param>
public sealed record AircraftIdentification(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected,
    string Callsign,
    AircraftCategory Category) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
