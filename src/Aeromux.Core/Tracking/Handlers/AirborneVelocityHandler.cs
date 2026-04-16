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
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles AirborneVelocity messages (TC 19) for aircraft in flight.
/// Updates: Speed, VerticalRate, VelocitySubtype, NACv, LastUpdate,
/// and conditionally Track (subtype 1-2) or Heading (subtype 3-4).
/// </summary>
/// <remarks>
/// <para><strong>Important distinction in TC 19 message encoding:</strong></para>
/// <list type="bullet">
/// <item>Ground speed messages (subtype 1-2): Message "Heading" field contains Track angle (direction of movement)</item>
/// <item>Airspeed messages (subtype 3-4): Message "Heading" field contains true Heading (direction nose points)</item>
/// </list>
/// <para>
/// This handler updates only the directional field relevant to the current subtype,
/// preserving the other from previous state. This avoids Track/Heading flickering
/// when an aircraft alternates between subtypes.
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

        // Update velocity with TC 19 message fields while preserving Comm-B and surface data.
        // Heading and Track are NOT set here — they are preserved from previous state.
        // Preserve from other handlers: IndicatedAirspeed, TrueAirspeed, TrackAngle (Comm-B BDS 5,0/5,3/6,0)
        //                               GroundSpeed, GroundTrack (Surface Position TC 5-8)
        TrackedVelocity velocity = aircraft.Velocity with
        {
            Speed = msg.Velocity,                                      // Airborne velocity from TC 19
            VerticalRate = msg.VerticalRate,                           // Climb/descent rate from TC 19
            VelocitySubtype = msg.Subtype,                             // Velocity source and speed range
            NACv = msg.NACv,                                           // Navigation accuracy category for velocity
            LastUpdate = msg.Velocity != null ? timestamp : null       // Update timestamp only if velocity present
        };

        // TC 19 message encoding quirk: the "Heading" field has different meanings per subtype.
        // Only update the directional field this subtype provides — the other is preserved
        // from the previous state by the `with` expression above. This prevents alternating
        // subtype 1-2 and 3-4 messages from nullifying each other's Track/Heading values.
        velocity = msg.Subtype switch
        {
            VelocitySubtype.GroundSpeedSubsonic or VelocitySubtype.GroundSpeedSupersonic
                => velocity with { Track = msg.Heading },              // Ground track angle (subtype 1-2)
            VelocitySubtype.AirspeedSubsonic or VelocitySubtype.AirspeedSupersonic
                => velocity with { Heading = msg.Heading },            // True heading (subtype 3-4)
            _ => velocity
        };

        // Cache geometric-barometric delta from TC 19 for geometric altitude derivation
        // Note: We only save the delta here; geometric altitude will be recalculated
        // when new barometric altitude arrives from TC 9-18
        TrackedPosition position = aircraft.Position;
        if (msg.GeometricBarometricDelta != null)
        {
            position = position with
            {
                GeometricBarometricDelta = msg.GeometricBarometricDelta.Value
            };
        }

        return aircraft with
        {
            Velocity = velocity,
            Position = position
        };
    }
}
