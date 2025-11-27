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
/// Handles AircraftIdentification messages (TC 1-4).
/// Updates aircraft callsign (flight number or registration) and emitter category.
/// </summary>
/// <remarks>
/// <para><strong>Type Code meanings:</strong></para>
/// <list type="bullet">
/// <item>TC 1: No category information available</item>
/// <item>TC 2: Surface vehicle emitter (ground service vehicles, emergency vehicles)</item>
/// <item>TC 3: Ground obstruction (fixed obstacles on airport surface)</item>
/// <item>TC 4: Aircraft emitter with wake vortex category (Light, Small, Large, Large High Vortex, Heavy, High Performance, Rotorcraft)</item>
/// </list>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Identification.Callsign: Flight number (e.g., "UAL123") or registration (e.g., "N12345"), 8 characters max, space-padded</item>
/// <item>Identification.Category: Aircraft wake vortex category, used for separation requirements and display icons</item>
/// </list>
/// <para>
/// Callsign is the primary human-readable identifier displayed in UIs.
/// Category determines wake turbulence separation minima and icon selection in displays.
/// </para>
/// </remarks>
public sealed class AircraftIdentificationHandler : ITrackingHandler
{
    public Type MessageType => typeof(AircraftIdentification);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (AircraftIdentification)message;
        var changedFields = new HashSet<string>();
        TrackedIdentification identification = aircraft.Identification;

        // Update Callsign (flight number or aircraft registration)
        // Primary human-readable identifier for the aircraft
        // Format: 8 characters max, space-padded (e.g., "UAL1234 ", "N12345  ")
        if (identification.Callsign != msg.Callsign)
        {
            identification = identification with { Callsign = msg.Callsign };
            changedFields.Add($"{nameof(Aircraft.Identification)}.{nameof(TrackedIdentification.Callsign)}");
        }

        // Update Category (wake vortex category / emitter type)
        // Used for: wake turbulence separation requirements, display icon selection, traffic filtering
        // Categories: NoInfo, Light, Small, Large, LargeHighVortex, Heavy, HighPerformance, Rotorcraft, etc.
        if (identification.Category != msg.Category)
        {
            identification = identification with { Category = msg.Category };
            changedFields.Add($"{nameof(Aircraft.Identification)}.{nameof(TrackedIdentification.Category)}");
        }

        // Return updated aircraft state if anything changed
        if (changedFields.Count > 0)
        {
            return (aircraft with { Identification = identification }, changedFields);
        }

        return (aircraft, changedFields);
    }
}
