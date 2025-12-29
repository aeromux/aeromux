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
/// Handles AllCallReply messages (DF 11).
/// Updates transponder capability level from Mode S all-call interrogation responses.
/// </summary>
/// <remarks>
/// <para><strong>DF 11: All-Call Reply</strong></para>
/// <para>
/// Response to Mode S all-call interrogations used for aircraft acquisition.
/// Announces ICAO address and basic transponder capability level.
/// Does not include altitude, position, or velocity data.
/// Primarily used by ground stations to discover aircraft in the surveillance area.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Capabilities.TransponderLevel: Mode S transponder capability level (Level 1-5)</item>
/// </list>
/// <para>
/// Capability levels indicate Mode S support:
/// - Level 1: No Mode S capability (Mode A/C only)
/// - Level 2-4: Mode S with varying capability levels
/// - Level 5: Full Mode S with enhanced surveillance
/// This field provides baseline capability information for all Mode S equipped aircraft.
/// </para>
/// </remarks>
public sealed class AllCallReplyHandler : ITrackingHandler
{
    public Type MessageType => typeof(AllCallReply);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (AllCallReply)message;

        // Initialize or update Capabilities category
        // TransponderLevel indicates Mode S capability (Level 1-5)
        // This is the baseline capability information from all-call acquisition
        TrackedCapabilities capabilities = aircraft.Capabilities ?? new();
        capabilities = capabilities with
        {
            TransponderLevel = msg.Capability,
            LastUpdate = timestamp
        };

        return aircraft with { Capabilities = capabilities };
    }
}
