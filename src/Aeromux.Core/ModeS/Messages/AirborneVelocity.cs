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
/// Airborne velocity message with speed, heading, and vertical rate.
/// Corresponds to Type Code 19 in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <remarks>
/// Velocity, Heading, VerticalRate, and NACv may be null if not available in the message.
/// Subtype indicates Ground Speed (1-2) or Airspeed (3-4), with supersonic variants.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Velocity">Velocity (null if not available).</param>
/// <param name="Heading">Heading in degrees (0-360, null if not available).</param>
/// <param name="VerticalRate">Vertical rate in feet/minute (null if not available, negative = descending).</param>
/// <param name="Subtype">Velocity subtype (ground speed vs airspeed, subsonic vs supersonic).</param>
/// <param name="NACv">Navigation Accuracy Category for Velocity (bits 43-45, null if not available).</param>
/// <param name="GeometricBarometricDelta">
/// Difference between geometric (GNSS) altitude and barometric altitude in feet.
/// Extracted from TC 19 ME bits 49-56 (SDif + dAlt fields).
/// Positive values indicate GNSS altitude is above barometric (typical: +50 to +100 feet).
/// Formula: Δh = s × (n - 1) × 25 feet, where s=±1 based on SDif bit, n is dAlt magnitude.
/// Range: ±25 to ±3,150 feet. Null if unavailable (dAlt = 0 or 127).
/// Used to derive geometric altitude when TC 20-22 messages are unavailable.
/// </param>
public sealed record AirborneVelocity(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected,
    Velocity? Velocity,
    double? Heading,
    int? VerticalRate,
    VelocitySubtype Subtype,
    NavigationAccuracyCategoryVelocity? NACv,
    int? GeometricBarometricDelta) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
