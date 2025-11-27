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
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles SurfacePosition messages (TC 5-8) for aircraft on the ground.
/// Updates position (Coordinate, IsOnGround) and surface velocity (GroundSpeed, GroundTrack).
/// Surface position messages use modified CPR encoding and provide ground movement data
/// essential for airport surface tracking and taxi operations.
/// </summary>
/// <remarks>
/// Updated fields:
/// - Position.Coordinate: Geographic position decoded from surface CPR
/// - Position.IsOnGround: Always set to true for surface messages
/// - Position.LastUpdate: Timestamp when position was updated
/// - Velocity.GroundSpeed: Surface movement speed (0-199 knots with non-linear quantization)
/// - Velocity.GroundTrack: Surface movement direction (0-360° with 2.8125° resolution)
/// - Velocity.LastUpdate: Timestamp when velocity was updated
/// </remarks>
public sealed class SurfacePositionHandler : ITrackingHandler
{
    public Type MessageType => typeof(SurfacePosition);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (SurfacePosition)message;
        var changedFields = new HashSet<string>();
        TrackedPosition position = aircraft.Position;
        TrackedVelocity velocity = aircraft.Velocity;
        bool positionChanged = false;
        bool velocityChanged = false;

        // Update Coordinate from CPR decoding (if successfully decoded)
        // Surface CPR uses modified NL functions different from airborne CPR
        if (msg.Position != null && position.Coordinate != msg.Position)
        {
            position = position with { Coordinate = msg.Position };
            positionChanged = true;
        }

        // Update IsOnGround status (surface messages always indicate ground)
        // Used to filter ground traffic and apply surface-specific logic
        if (position.IsOnGround != true)
        {
            position = position with { IsOnGround = true };
            positionChanged = true;
        }

        // Update position LastUpdate timestamp if any position data changed
        if (positionChanged)
        {
            position = position with { LastUpdate = timestamp };
            changedFields.Add(nameof(Aircraft.Position));
        }

        // Update GroundSpeed from surface movement field (if available)
        // Non-linear quantization: 0-199 knots with higher precision at lower speeds
        // Converted to Velocity value object for type safety and unit conversions
        if (msg.GroundSpeed.HasValue)
        {
            var groundSpeed = Velocity.FromKnots(msg.GroundSpeed.Value, VelocityType.GroundSpeed);
            if (!Equals(velocity.GroundSpeed, groundSpeed))
            {
                velocity = velocity with { GroundSpeed = groundSpeed };
                velocityChanged = true;
            }
        }

        // Update GroundTrack direction (if available)
        // 0-360° with 2.8125° resolution (360/128 quantization)
        // Indicates direction of surface movement during taxi operations
        if (msg.GroundTrack.HasValue && !Equals(velocity.GroundTrack, msg.GroundTrack.Value))
        {
            velocity = velocity with { GroundTrack = msg.GroundTrack.Value };
            velocityChanged = true;
        }

        // Update velocity LastUpdate timestamp if any velocity data changed
        if (velocityChanged)
        {
            velocity = velocity with { LastUpdate = timestamp };
            changedFields.Add(nameof(Aircraft.Velocity));
        }

        // Return updated aircraft state if anything changed
        if (changedFields.Count > 0)
        {
            return (aircraft with { Position = position, Velocity = velocity }, changedFields);
        }

        return (aircraft, changedFields);
    }
}
