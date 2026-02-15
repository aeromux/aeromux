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
/// Handles SurfacePosition messages (TC 5-8) for aircraft on the ground.
/// Updates position (Coordinate, IsOnGround) and surface velocity (GroundSpeed, GroundTrack).
/// Surface position messages use modified CPR encoding and provide ground movement data
/// essential for airport surface tracking and taxi operations.
/// </summary>
/// <remarks>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Position.Coordinate: Geographic position decoded from surface CPR</item>
/// <item>Position.IsOnGround: Always set to true for surface messages</item>
/// <item>Position.MovementCategory: Surface movement speed category for taxi operations</item>
/// <item>Position.LastUpdate: Timestamp when position was updated</item>
/// <item>Velocity.GroundSpeed: Surface movement speed (0-199 knots with non-linear quantization)</item>
/// <item>Velocity.GroundTrack: Surface movement direction (0-360° with 2.8125° resolution)</item>
/// <item>Velocity.LastUpdate: Timestamp when velocity was updated</item>
/// </list>
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
        ArgumentNullException.ThrowIfNull(frame);

        var msg = (SurfacePosition)message;

        // Update position with coordinate and ground status
        // Surface CPR uses modified NL functions different from airborne CPR
        // Surface messages always indicate ground
        // MovementCategory: Non-linear speed categories optimized for ground operations
        // PositionSource tracks where the position came from (Sdr, Beast, or Mlat)
        // HadMlatPosition is set to true if this is an MLAT position or was previously true
        TrackedPosition position = aircraft.Position with
        {
            Coordinate = msg.Position ?? aircraft.Position.Coordinate,
            IsOnGround = true,
            MovementCategory = msg.Movement,
            LastUpdate = timestamp,
            PositionSource = frame.Source,
            HadMlatPosition = aircraft.Position.HadMlatPosition || frame.Source == FrameSource.Mlat
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
