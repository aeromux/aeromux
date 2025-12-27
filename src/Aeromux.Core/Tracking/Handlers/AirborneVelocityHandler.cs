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
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles AirborneVelocity messages (TC 19) for aircraft in flight.
/// Updates: Speed, Heading, Track, VerticalRate, VelocitySubtype, LastUpdate
/// </summary>
/// <remarks>
/// <para><strong>Important distinction in TC 19 message encoding:</strong></para>
/// <list type="bullet">
/// <item>Ground speed messages (subtype 1-2): Message "Heading" field contains Track angle (direction of movement)</item>
/// <item>Airspeed messages (subtype 3-4): Message "Heading" field contains true Heading (direction nose points)</item>
/// </list>
/// <para>
/// This handler correctly separates these into TrackedVelocity.Heading and TrackedVelocity.Track.
/// Note: TrackedVelocity.Track (from TC 19) is different from TrackedVelocity.GroundTrack (from TC 5-8 surface messages).
/// Track is airborne ground track accounting for wind, while GroundTrack is surface taxi direction.
/// </para>
/// </remarks>
public sealed class AirborneVelocityHandler : ITrackingHandler
{
    public Type MessageType => typeof(AirborneVelocity);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (AirborneVelocity)message;

        // Extract heading vs track based on velocity subtype
        // TC 19 message encoding quirk: the "Heading" field has different meanings:
        // - Ground speed messages (subtype 1-2): Field contains track angle (direction of movement over ground)
        // - Airspeed messages (subtype 3-4): Field contains true heading (direction nose points)
        // We decode this correctly into separate Heading and Track fields
        double? heading = null;
        double? track = null;

        if (msg.Subtype is VelocitySubtype.GroundSpeedSubsonic or VelocitySubtype.GroundSpeedSupersonic)
        {
            // Ground speed: extract track angle (direction of movement accounting for wind)
            track = msg.Heading;
        }
        else if (msg.Subtype is VelocitySubtype.AirspeedSubsonic or VelocitySubtype.AirspeedSupersonic)
        {
            // Airspeed: extract true heading (direction nose points)
            heading = msg.Heading;
        }

        // Update velocity with all fields from TC 19 message
        // TC 19 messages always contain complete airborne velocity information when present
        // Note: GroundSpeed and GroundTrack remain unchanged (those come from TC 5-8 surface messages)
        var velocity = new TrackedVelocity
        {
            Speed = msg.Velocity,                                      // Airborne velocity (ground speed or airspeed)
            Heading = heading,                                         // True heading (subtype 3-4 only)
            Track = track,                                             // Ground track angle (subtype 1-2 only)
            VerticalRate = msg.VerticalRate,                           // Climb/descent rate
            VelocitySubtype = msg.Subtype,                             // Velocity source and speed range
            NACv = msg.NACv,                                           // Navigation accuracy category for velocity
            LastUpdate = msg.Velocity != null ? timestamp : null       // Update timestamp only if velocity present
        };

        return aircraft with { Velocity = velocity };
    }
}
