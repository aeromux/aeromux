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
/// Handles AirbornePosition messages (TC 9-18, 20-22) for aircraft in flight.
/// Updates position coordinates, altitude, and airborne status.
/// </summary>
/// <remarks>
/// <para><strong>Type Code ranges:</strong></para>
/// <list type="bullet">
/// <item>TC 9-18: Airborne position with barometric altitude (pressure altitude, standard setting 29.92 inHg)</item>
/// <item>TC 20-22: Airborne position with GNSS height (geometric altitude above WGS84 ellipsoid)</item>
/// </list>
/// <para>
/// This handler preserves both barometric and geometric altitude in separate fields without lossy merging.
/// Barometric altitude is used for ATC separation, while geometric altitude is more accurate but not used for traffic control.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Position.Coordinate: Geographic position decoded from airborne CPR encoding</item>
/// <item>Position.BarometricAltitude: Pressure altitude from TC 9-18 (if present)</item>
/// <item>Position.GeometricAltitude: GNSS altitude from TC 20-22 (if present)</item>
/// <item>Position.IsOnGround: Set to false (airborne)</item>
/// <item>Position.LastUpdate: Timestamp when position was updated</item>
/// </list>
/// </remarks>
public sealed class AirbornePositionHandler : ITrackingHandler
{
    public Type MessageType => typeof(AirbornePosition);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (AirbornePosition)message;
        var changedFields = new HashSet<string>();
        TrackedPosition position = aircraft.Position;
        bool positionChanged = false;

        // Update Coordinate from CPR decoding (if successfully decoded)
        // Airborne CPR encoding uses standard NL functions (different from surface CPR)
        if (msg.Position != null && position.Coordinate != msg.Position)
        {
            position = position with { Coordinate = msg.Position };
            positionChanged = true;
        }

        // Update altitude based on type - CRITICAL: Check altitude type to store in correct field
        // TC 9-18: Barometric altitude (pressure altitude, used for ATC separation)
        // TC 20-22: Geometric altitude (GNSS height, more accurate but not used for ATC)
        // Both types are preserved separately to avoid lossy merging
        if (msg.Altitude != null)
        {
            if (msg.Altitude.Type == ModeS.Enums.AltitudeType.Barometric &&
                position.BarometricAltitude != msg.Altitude)
            {
                // Store barometric altitude (standard pressure setting 29.92 inHg / 1013.25 hPa)
                position = position with { BarometricAltitude = msg.Altitude };
                positionChanged = true;
            }
            else if (msg.Altitude.Type == ModeS.Enums.AltitudeType.Geometric &&
                     position.GeometricAltitude != msg.Altitude)
            {
                // Store geometric altitude (GNSS height above WGS84 ellipsoid)
                // Typically 50-100 feet higher than barometric altitude
                position = position with { GeometricAltitude = msg.Altitude };
                positionChanged = true;
            }
        }

        // Update Single Antenna flag.
        // This field is encoded in bit 40 of Type Code 9-18 and 20-22 messages.
        if (msg.Antenna != null && position.Antenna != msg.Antenna)
        {
            position = position with { Antenna = msg.Antenna };
            positionChanged = true;
        }

        // Update IsOnGround status (airborne position messages always indicate in-flight)
        // Used to distinguish from surface position messages (TC 5-8) and filter ground traffic
        if (position.IsOnGround)
        {
            position = position with { IsOnGround = false };
            positionChanged = true;
        }

        // Update position LastUpdate timestamp if any position data changed
        if (positionChanged)
        {
            position = position with { LastUpdate = timestamp };
            changedFields.Add(nameof(Aircraft.Position));
        }

        // Return updated aircraft state if anything changed
        if (changedFields.Count > 0)
        {
            return (aircraft with { Position = position }, changedFields);
        }

        return (aircraft, changedFields);
    }
}
