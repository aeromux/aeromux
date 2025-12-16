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
/// Operational status message (DF 17/18, TC 31).
/// Contains aircraft version, capabilities, and accuracy parameters.
/// Priority 3: Implements essential fields (version, NACp, NACv, NICbaro, SIL).
/// </summary>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Subtype">Airborne or surface operational status.</param>
/// <param name="Version">ADS-B protocol version (DO-260, DO-260A, DO-260B, etc.).</param>
/// <param name="NACp">Navigation Accuracy Category for Position (horizontal accuracy).</param>
/// <param name="NACv">Navigation Accuracy Category for Velocity (velocity accuracy, 0-7 scale).</param>
/// <param name="NICBaroAltitudeIntegrity">Barometric altitude integrity flag (cross-checked with GNSS).</param>
/// <param name="SIL">Surveillance Integrity Level (probability of position error).</param>
public sealed record OperationalStatus(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    OperationalStatusSubtype Subtype,
    AdsbVersion Version,
    NavigationAccuracyCategory NACp,
    int NACv,
    bool NICBaroAltitudeIntegrity,
    SurveillanceIntegrityLevel SIL) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
