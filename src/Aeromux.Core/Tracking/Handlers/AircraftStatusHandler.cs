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
/// Handles AircraftStatus messages (TC 28).
/// Updates squawk code and emergency/priority status for safety-critical monitoring.
/// </summary>
/// <remarks>
/// <para><strong>TC 28 subtypes:</strong></para>
/// <list type="bullet">
/// <item>Subtype 0: No information available</item>
/// <item>Subtype 1: Emergency/priority status with squawk code</item>
/// <item>Subtype 2: TCAS Resolution Advisory (not fully implemented yet)</item>
/// </list>
/// <para><strong>Special squawk codes:</strong></para>
/// <list type="bullet">
/// <item>7700: General emergency (mechanical failure, medical emergency, etc.)</item>
/// <item>7600: Radio communication failure (lost comms with ATC)</item>
/// <item>7500: Unlawful interference (hijacking)</item>
/// <item>1200: VFR (Visual Flight Rules, uncontrolled airspace)</item>
/// </list>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Identification.Squawk: 4-digit octal transponder code (ATC identification and special codes)</item>
/// <item>Identification.EmergencyState: Emergency condition (NoEmergency, GeneralEmergency, MedicalEmergency, MinimumFuel, NoCommunications, UnlawfulInterference, Downed)</item>
/// </list>
/// <para>
/// Emergency state triggers immediate alerts and priority handling in ATC systems.
/// Squawk codes are assigned by ATC for identification and coordination.
/// </para>
/// </remarks>
public sealed class AircraftStatusHandler : ITrackingHandler
{
    public Type MessageType => typeof(AircraftStatus);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (AircraftStatus)message;
        var changedFields = new HashSet<string>();
        TrackedIdentification identification = aircraft.Identification;

        // Update Squawk code (4-digit octal transponder code)
        // Used for: ATC identification, special condition signaling (7700/7600/7500)
        // Format: 4 octal digits (0-7 for each digit), e.g., "7700", "1200", "0035"
        if (msg.SquawkCode != null && identification.Squawk != msg.SquawkCode)
        {
            identification = identification with { Squawk = msg.SquawkCode };
            changedFields.Add($"{nameof(Aircraft.Identification)}.{nameof(TrackedIdentification.Squawk)}");
        }

        // Update EmergencyState (critical safety information)
        // States: NoEmergency, GeneralEmergency, MedicalEmergency, MinimumFuel,
        //         NoCommunications, UnlawfulInterference, Downed
        // Triggers: UI alerts, priority display, logging, notification systems
        if (msg.EmergencyState.HasValue && identification.EmergencyState != msg.EmergencyState.Value)
        {
            identification = identification with { EmergencyState = msg.EmergencyState.Value };
            changedFields.Add($"{nameof(Aircraft.Identification)}.{nameof(TrackedIdentification.EmergencyState)}");
        }

        // Return updated aircraft state if anything changed
        if (changedFields.Count > 0)
        {
            return (aircraft with { Identification = identification }, changedFields);
        }

        return (aircraft, changedFields);
    }
}
