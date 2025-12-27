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

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (SurfacePosition)message;

        // Update position with coordinate and ground status
        // Surface CPR uses modified NL functions different from airborne CPR
        // Surface messages always indicate ground
        TrackedPosition position = aircraft.Position with
        {
            Coordinate = msg.Position ?? aircraft.Position.Coordinate,
            IsOnGround = true,
            LastUpdate = timestamp
        };

        // Update velocity with ground movement data
        // GroundSpeed: Non-linear quantization: 0-199 knots with higher precision at lower speeds
        // GroundTrack: 0-360° with 2.8125° resolution (360/128 quantization)
        Velocity? groundSpeed = msg.GroundSpeed.HasValue
            ? Velocity.FromKnots(msg.GroundSpeed.Value, VelocityType.GroundSpeed)
            : aircraft.Velocity.GroundSpeed;

        TrackedVelocity velocity = aircraft.Velocity with
        {
            GroundSpeed = groundSpeed,
            GroundTrack = msg.GroundTrack ?? aircraft.Velocity.GroundTrack,
            LastUpdate = timestamp
        };

        return aircraft with { Position = position, Velocity = velocity };
    }
}
