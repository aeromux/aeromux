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
/// Target state and status message (DF 17/18, TC 29).
/// Contains autopilot target parameters and flight management system settings.
/// Full implementation of both Version 1 and Version 2.
/// </summary>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Subtype">Message version (Version1 or Version2).</param>
/// <param name="TargetAltitude">Target/selected altitude (null if not available).</param>
/// <param name="AltitudeSource">Altitude source (V2: MCP/FCU or FMS, V1: varies).</param>
/// <param name="TargetHeading">Target/selected heading in degrees (null if not available).</param>
/// <param name="BarometricPressure">Barometric pressure setting in millibars (null if not available, V2 only).</param>
/// <param name="VerticalMode">Vertical navigation mode (V1 only: None, Acquiring, or CapturingOrMaintaining).</param>
/// <param name="HorizontalMode">Horizontal navigation mode (V1 only: None, Acquiring, or CapturingOrMaintaining).</param>
/// <param name="AutopilotEngaged">Autopilot engaged (V2 only).</param>
/// <param name="VNAVMode">VNAV mode engaged (V2 only).</param>
/// <param name="AltitudeHoldMode">Altitude hold mode engaged (V2 only).</param>
/// <param name="ApproachMode">Approach mode engaged (V2 only).</param>
/// <param name="LNAVMode">LNAV mode engaged (V2 only).</param>
/// <param name="TCASOperational">TCAS/ACAS operational.</param>
/// <param name="TCASRaActive">TCAS RA active (V1 only).</param>
/// <param name="EmergencyPriority">Emergency state (V1 only).</param>
/// <param name="SIL">Surveillance Integrity Level (V2 only).</param>
/// <param name="NACp">Navigation Accuracy Category for Position (V2 only).</param>
/// <param name="NICBaroIntegrity">Barometric altitude integrity flag (V2 only).</param>
public sealed record TargetStateAndStatus(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    TargetStateSubtype Subtype,
    Altitude? TargetAltitude,
    AltitudeSource? AltitudeSource,
    double? TargetHeading,
    TargetHeadingType? TargetHeadingType,
    double? BarometricPressure,
    VerticalMode? VerticalMode,
    HorizontalMode? HorizontalMode,
    bool? AutopilotEngaged,
    bool? VNAVMode,
    bool? AltitudeHoldMode,
    bool? ApproachMode,
    bool? LNAVMode,
    bool? TCASOperational,
    bool? TCASRaActive,
    EmergencyState? EmergencyPriority,
    SourceIntegrityLevel? SIL,
    NavigationAccuracyCategoryPosition? NACp,
    BarometricAltitudeIntegrityCode? NICBaroIntegrity) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
