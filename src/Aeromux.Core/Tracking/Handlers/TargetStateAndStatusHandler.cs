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
/// Handles TargetStateAndStatus messages (TC 29) for autopilot/FMS intent tracking.
/// Updates: SelectedAltitude, AltitudeSource, SelectedHeading, BarometricPressureSetting,
/// VerticalMode, HorizontalMode, AutopilotEngaged, VnavMode, LnavMode, AltitudeHoldMode,
/// ApproachMode, TcasOperational, TcasRaActive, LastUpdate
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
/// </para>
/// </remarks>
public sealed class TargetStateAndStatusHandler : ITrackingHandler
{
    public Type MessageType => typeof(TargetStateAndStatus);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (TargetStateAndStatus)message;
        var changedFields = new HashSet<string>();

        // Get existing autopilot state or create new one
        TrackedAutopilot? existing = aircraft.Autopilot;

        // Create updated autopilot state with field-level merging
        // Only update fields present in this message, preserve others from existing state
        var autopilot = new TrackedAutopilot
        {
            // Shared fields (V1 and V2)
            SelectedAltitude = msg.TargetAltitude ?? existing?.SelectedAltitude,
            AltitudeSource = msg.AltitudeSource ?? existing?.AltitudeSource,
            SelectedHeading = msg.TargetHeading ?? existing?.SelectedHeading,
            TcasOperational = msg.TcasOperational ?? existing?.TcasOperational,

            // V2-only fields
            BarometricPressureSetting = msg.BarometricPressure ?? existing?.BarometricPressureSetting,
            AutopilotEngaged = msg.AutopilotEngaged ?? existing?.AutopilotEngaged,
            VnavMode = msg.VnavMode ?? existing?.VnavMode,
            LnavMode = msg.LnavMode ?? existing?.LnavMode,
            AltitudeHoldMode = msg.AltitudeHoldMode ?? existing?.AltitudeHoldMode,
            ApproachMode = msg.ApproachMode ?? existing?.ApproachMode,

            // V1-only fields
            VerticalMode = msg.VerticalMode ?? existing?.VerticalMode,
            HorizontalMode = msg.HorizontalMode ?? existing?.HorizontalMode,
            TcasRaActive = msg.TcasRaActive ?? existing?.TcasRaActive,

            // Update timestamp
            LastUpdate = timestamp
        };

        // Mark as changed if any autopilot field changed
        if (!AutopilotEquals(existing, autopilot))
        {
            changedFields.Add(nameof(Aircraft.Autopilot));
        }

        return (aircraft with { Autopilot = autopilot }, changedFields);
    }

    /// <summary>
    /// Compares two TrackedAutopilot instances for equality (excluding LastUpdate).
    /// Returns true if all fields except LastUpdate are equal.
    /// </summary>
    private static bool AutopilotEquals(TrackedAutopilot? a, TrackedAutopilot? b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.SelectedAltitude == b.SelectedAltitude &&
               a.AltitudeSource == b.AltitudeSource &&
               a.SelectedHeading == b.SelectedHeading &&
               a.BarometricPressureSetting == b.BarometricPressureSetting &&
               a.VerticalMode == b.VerticalMode &&
               a.HorizontalMode == b.HorizontalMode &&
               a.AutopilotEngaged == b.AutopilotEngaged &&
               a.VnavMode == b.VnavMode &&
               a.LnavMode == b.LnavMode &&
               a.AltitudeHoldMode == b.AltitudeHoldMode &&
               a.ApproachMode == b.ApproachMode &&
               a.TcasOperational == b.TcasOperational &&
               a.TcasRaActive == b.TcasRaActive;
    }
}
