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
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Airborne position message with altitude and CPR (Compact Position Reporting) encoded location.
/// Corresponds to Type Code 9-18 (barometric) and 20-22 (GNSS) in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <remarks>
/// Position may be null if CPR decoding is not yet possible (requires paired even/odd frames).
/// CPR encoding reduces position data to 17 bits each for latitude and longitude.
/// CprLat, CprLon, and CprFormat are always present for later decoding.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Position">Decoded geographic position (null if not yet decoded).</param>
/// <param name="Altitude">Decoded altitude (null if not available).</param>
/// <param name="Antenna">Single Antenna flag (aircraft equipped with diversity or non-diversity antenna).</param>
/// <param name="SurveillanceStatus">Surveillance status (alert and SPI conditions).</param>
public sealed record AirbornePosition(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    GeographicCoordinate? Position,
    Altitude? Altitude,
    AntennaFlag? Antenna,
    SurveillanceStatus SurveillanceStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
