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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles OperationalStatus messages (TC 31, Version 0/1/2).
/// Extracts comprehensive aircraft capabilities, operational modes, data quality indicators, and ADS-B version.
/// </summary>
/// <remarks>
/// <para><strong>TC 31 provides complete equipment capability and operational status information:</strong></para>
/// <list type="bullet">
/// <item>ADS-B Version: Equipment capability level (DO-260, DO-260A, DO-260B)</item>
/// <item>Capability Class (CC): Aircraft capabilities (TCAS, CDTI, ADS-B 1090ES, ARV, TS, TC, UAT, POA, B2Low, NACv, NIC Supplement-C)</item>
/// <item>Operational Mode (OM): Current operational state (TCAS RA, IDENT, ATC services, antenna config, SDA, GPS offsets)</item>
/// <item>Data Quality: NACp, NIC Supplement-A, GVA, SIL, NICbaro, HRD, SIL Supplement, Target Heading Type</item>
/// <item>Physical Configuration: Aircraft length and width (surface only)</item>
/// </list>
/// <para><strong>Updated categories:</strong></para>
/// <list type="bullet">
/// <item>Identification.Version: ADS-B protocol version (moved from Status per user requirement)</item>
/// <item>Capabilities: All CapabilityClass fields (11 fields) + Dimensions</item>
/// <item>OperationalMode: All OperationalMode fields (7 fields)</item>
/// <item>DataQuality: GeometricVerticalAccuracy, NICSupplementA, SILSupplement, HorizontalReference, HeadingType</item>
/// <item>Position: NACp, NICbaro, SIL (preserved for backward compatibility)</item>
/// </list>
/// <para>
/// This handler now tracks ~30 fields from TC 31, providing complete metadata coverage.
/// TC 31 messages are transmitted less frequently than position/velocity (typically every 5-10 seconds).
/// </para>
/// <para><strong>Reference:</strong> https://mode-s.org/1090mhz/content/ads-b/6-operation-status.html</para>
/// </remarks>
public sealed class OperationalStatusHandler : ITrackingHandler
{
    public Type MessageType => typeof(OperationalStatus);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (OperationalStatus)message;

        // === IDENTIFICATION: ADS-B Version ===
        TrackedIdentification identification = aircraft.Identification with
        {
            Version = msg.Version
        };

        // === CAPABILITIES: CapabilityClass fields + Dimensions ===
        TrackedCapabilities? capabilities = aircraft.Capabilities ?? new();

        // Extract all CapabilityClass fields if available
        if (msg.CapabilityClass is not null)
        {
            capabilities = capabilities with
            {
                TCASCapability = msg.CapabilityClass.TCASOperational,
                CockpitDisplayTraffic = msg.CapabilityClass.CDTICapability,
                ADSB1090ES = msg.CapabilityClass.ADSB1090ESCapability,
                AirReferencedVelocity = msg.CapabilityClass.ARVCapability,
                TargetStateReporting = msg.CapabilityClass.TSCapability,
                TrajectoryChangeLevel = msg.CapabilityClass.TCCapabilityLevel,
                UAT978Support = msg.CapabilityClass.UATCapability,
                PositionOffsetApplied = msg.CapabilityClass.POA,
                LowPower1090ES = msg.CapabilityClass.B2Low,
                NACv = msg.CapabilityClass.NACv,
                NICSupplementC = msg.CapabilityClass.NICSupplementC
            };
        }

        // Physical dimensions (surface operations only)
        capabilities = capabilities with
        {
            Dimensions = msg.AircraftLengthAndWidth ?? capabilities.Dimensions,
            LastUpdate = timestamp
        };

        // === OPERATIONAL MODE: Runtime operational status ===
        TrackedOperationalMode? operationalMode = aircraft.OperationalMode ?? new();

        // Extract all OperationalMode fields if available
        if (msg.OperationalMode is not null)
        {
            operationalMode = operationalMode with
            {
                TCASRAActive = msg.OperationalMode.TCASRAActive,
                IdentSwitchActive = msg.OperationalMode.IdentSwitchActive,
                ReceivingATCServices = msg.OperationalMode.ATCServices,
                SingleAntenna = msg.OperationalMode.SingleAntenna,
                SystemDesignAssurance = msg.OperationalMode.SDA,
                GPSLateralOffset = msg.OperationalMode.GPSLatOffset,
                GPSLongitudinalOffset = msg.OperationalMode.GPSLongOffset,
                LastUpdate = timestamp
            };
        }

        // === DATA QUALITY: Quality indicators and integrity levels ===
        TrackedDataQuality? dataQuality = aircraft.DataQuality ?? new();
        dataQuality = dataQuality with
        {
            GeometricVerticalAccuracy = msg.GeometricVerticalAccuracy ?? dataQuality.GeometricVerticalAccuracy,
            NICSupplementA = msg.NICSupplementA ?? dataQuality.NICSupplementA,
            SILSupplement = msg.SILSupplement ?? dataQuality.SILSupplement,
            HorizontalReference = msg.HRD ?? dataQuality.HorizontalReference,
            HeadingType = msg.TargetHeading ?? dataQuality.HeadingType,
            LastUpdate = timestamp
        };

        // === POSITION: Data quality metrics ===
        // NACp: Navigation Accuracy Category for Position (horizontal GPS accuracy)
        //       Scale 0-11: 11=<3m, 10=<10m, 9=<30m, 8=<92.6m, ..., 0=unknown
        // NICbaro: Barometric Altitude Integrity Code (1-bit, indicates cross-check status)
        //          Enum: NotCrossChecked (0) or CrossCheckedOrNonGilham (1)
        //          Converted to bool: true if cross-checked, false if not, null if unavailable
        // SIL: Source Integrity Level (probability of error containment, bits 83-84)
        //      Scale 0-3: 3=<10^-7 per hour (highest), 2=<10^-5, 1=<10^-3, 0=unknown

        // Convert NICbaro from enum to bool for storage
        bool? nicBaroValue = msg.NICbaro.HasValue
            ? msg.NICbaro.Value == ModeS.Enums.BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham
            : null;

        TrackedPosition position = aircraft.Position with
        {
            NACp = msg.NACp,
            NICbaro = nicBaroValue,
            SIL = msg.SIL
        };

        return aircraft with
        {
            Identification = identification,
            Capabilities = capabilities,
            OperationalMode = operationalMode,
            DataQuality = dataQuality,
            Position = position
        };
    }
}
