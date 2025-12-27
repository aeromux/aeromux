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
/// Operational status message (DF 17/18, TC 31).
/// Contains aircraft capabilities, operational modes, version, and accuracy parameters.
/// Supports ADS-B Version 0, 1, and 2 (DO-260, DO-260A, DO-260B).
/// </summary>
/// <remarks>
/// This message provides information about the aircraft's ADS-B capabilities and current operational status.
/// The content varies based on the ADS-B version and whether the aircraft is airborne or on the surface.
///
/// Version differences:
/// - Version 0 (DO-260): Basic TCAS and CDTI capability reporting
/// - Version 1 (DO-260A): Adds ARV, TS, TC, UAT capabilities, NACv, NIC supplements
/// - Version 2 (DO-260B): Enhanced accuracy reporting with GVA, NICbaro, SIL supplement
///
/// Reference: https://mode-s.org/1090mhz/content/ads-b/6-operation-status.html
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address (24-bit unique identifier).</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 17 for ADS-B, DF 18 for TIS-B/ADS-R).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied to the message.</param>
/// <param name="Subtype">Message subtype: Airborne or Surface operational status (bits 38-40).</param>
/// <param name="CapabilityClass">Capability Class (CC) codes indicating aircraft capabilities (bits 41-56, version-dependent, nullable).</param>
/// <param name="OperationalMode">Operational Mode (OM) codes indicating current operational state (bits 57-72, version-dependent, nullable).</param>
/// <param name="AircraftLengthAndWidth">Aircraft/vehicle dimensions for surface operations (Surface subtype only, nullable).</param>
/// <param name="Version">ADS-B protocol version: DO-260 (0), DO-260A (1), DO-260B (2), etc. (3 bits, bits 73-75).</param>
/// <param name="NICSupplementA">NIC Supplement-A for position accuracy decoding (1 bit, bit 86).</param>
/// <param name="NACp">Navigation Accuracy Category for Position - horizontal accuracy (4 bits, bits 77-80).</param>
/// <param name="GeometricVerticalAccuracy">Geometric (GNSS) vertical accuracy (GVA, 2 bits, bits 81-82, Airborne only, nullable).</param>
/// <param name="SIL">Source Integrity Level - probability of position error (2 bits, bits 83-84).</param>
/// <param name="NICbaro">Barometric Altitude Integrity Code - cross-check status (1 bit, bit 85, Airborne only, nullable).</param>
/// <param name="TargetHeading">Track/Heading indicator for surface vehicles: Track (0) or Heading (1) (1 bit, bit 85, Surface only).</param>
/// <param name="HRD">Horizontal Reference Direction: True North (0) or Magnetic North (1) (1 bit, bit 86).</param>
/// <param name="SILSupplement">SIL Supplement indicating per-hour or per-sample basis (1 bit, bit 87).</param>
public sealed record OperationalStatus(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    OperationalStatusSubtype Subtype,
    AdsbVersion Version,
    CapabilityClass? CapabilityClass,
    OperationalMode? OperationalMode,
    AircraftLengthAndWidth? AircraftLengthAndWidth,
    bool? NICSupplementA,
    NavigationAccuracyCategoryPosition? NACp,
    GeometricVerticalAccuracy? GeometricVerticalAccuracy,
    SourceIntegrityLevel? SIL,
    BarometricAltitudeIntegrityCode? NICbaro,
    TargetHeadingType? TargetHeading,
    HorizontalReferenceDirection? HRD,
    SilSupplement? SILSupplement) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
