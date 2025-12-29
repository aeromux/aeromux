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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles TargetStateAndStatus messages (TC 29) for autopilot/FMS intent, TCAS status, and data quality tracking.
/// Updates autopilot fields: SelectedAltitude, AltitudeSource, SelectedHeading, BarometricPressureSetting,
/// VerticalMode, HorizontalMode, AutopilotEngaged, VnavMode, LnavMode, AltitudeHoldMode, ApproachMode.
/// Updates ACAS fields: TcasOperational, TcasRaActive.
/// Updates DataQuality fields (V2 only): SIL_TC29, NACp_TC29, NICbaro_TC29.
/// </summary>
/// <remarks>
/// <para><strong>TC 29 has two versions with different field sets:</strong></para>
/// <list type="bullet">
/// <item>Version 1: VerticalMode, HorizontalMode, TcasRaActive, EmergencyPriority</item>
/// <item>Version 2: AutopilotEngaged, VnavMode, LnavMode, AltitudeHoldMode, ApproachMode,
/// BarometricPressureSetting, SIL, NACp, NICBaro</item>
/// <item>Shared: SelectedAltitude, AltitudeSource, SelectedHeading, TcasOperational</item>
/// </list>
/// <para>
/// This handler preserves fields from both versions using field-level merging.
/// Only updates fields that are present in the current message (doesn't null out V1 fields when receiving V2 or vice versa).
/// TCAS fields are now stored in TrackedAcas category instead of TrackedAutopilot.
/// V2 quality indicators (SIL, NACp, NICbaro) provide redundancy to TC 31, useful for cross-validation.
/// </para>
/// </remarks>
public sealed class TargetStateAndStatusHandler : ITrackingHandler
{
    public Type MessageType => typeof(TargetStateAndStatus);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (TargetStateAndStatus)message;
        TrackedAutopilot? existingAutopilot = aircraft.Autopilot;
        TrackedAcas? existingAcas = aircraft.Acas;

        // Create updated autopilot state with field-level merging
        // Only update fields present in this message, preserve others from existing state
        // Note: TCAS fields have been moved to TrackedAcas category
        var autopilot = new TrackedAutopilot
        {
            // Shared fields (V1 and V2)
            SelectedAltitude = msg.TargetAltitude ?? existingAutopilot?.SelectedAltitude,
            AltitudeSource = msg.AltitudeSource ?? existingAutopilot?.AltitudeSource,
            SelectedHeading = msg.TargetHeading ?? existingAutopilot?.SelectedHeading,

            // V2-only fields
            BarometricPressureSetting = msg.BarometricPressure ?? existingAutopilot?.BarometricPressureSetting,
            AutopilotEngaged = msg.AutopilotEngaged ?? existingAutopilot?.AutopilotEngaged,
            VnavMode = msg.VNAVMode ?? existingAutopilot?.VnavMode,
            LnavMode = msg.LNAVMode ?? existingAutopilot?.LnavMode,
            AltitudeHoldMode = msg.AltitudeHoldMode ?? existingAutopilot?.AltitudeHoldMode,
            ApproachMode = msg.ApproachMode ?? existingAutopilot?.ApproachMode,

            // V1-only fields
            VerticalMode = msg.VerticalMode ?? existingAutopilot?.VerticalMode,
            HorizontalMode = msg.HorizontalMode ?? existingAutopilot?.HorizontalMode,

            // Update timestamp
            LastUpdate = timestamp
        };

        // Create updated ACAS state with TC 29 TCAS fields
        // Preserve DF 0/DF 16 fields that TC 29 doesn't provide
        var acas = new TrackedAcas
        {
            // TC 29 TCAS fields
            TcasOperational = msg.TCASOperational ?? existingAcas?.TcasOperational,
            TcasRaActive = msg.TCASRaActive ?? existingAcas?.TcasRaActive,

            // Preserve DF 0/DF 16 fields
            SensitivityLevel = existingAcas?.SensitivityLevel,
            CrossLinkCapability = existingAcas?.CrossLinkCapability,
            ReplyInformation = existingAcas?.ReplyInformation,
            ResolutionAdvisoryTerminated = existingAcas?.ResolutionAdvisoryTerminated,
            MultipleThreatEncounter = existingAcas?.MultipleThreatEncounter,
            RacNotBelow = existingAcas?.RacNotBelow,
            RacNotAbove = existingAcas?.RacNotAbove,
            RacNotLeft = existingAcas?.RacNotLeft,
            RacNotRight = existingAcas?.RacNotRight,

            LastUpdate = timestamp
        };

        // TC 29 V2: Extract data quality indicators
        // These fields provide redundancy to TC 31 quality indicators, useful for cross-validation
        TrackedDataQuality? dataQuality = aircraft.DataQuality;
        if (msg.SIL.HasValue || msg.NACp.HasValue || msg.NICBaroIntegrity.HasValue)
        {
            dataQuality = dataQuality ?? new();
            dataQuality = dataQuality with
            {
                SIL_TC29 = msg.SIL ?? dataQuality.SIL_TC29,
                NACp_TC29 = msg.NACp ?? dataQuality.NACp_TC29,
                NICbaro_TC29 = msg.NICBaroIntegrity ?? dataQuality.NICbaro_TC29,
                LastUpdate = timestamp
            };
        }

        return aircraft with
        {
            Autopilot = autopilot,
            Acas = acas,
            DataQuality = dataQuality
        };
    }
}
