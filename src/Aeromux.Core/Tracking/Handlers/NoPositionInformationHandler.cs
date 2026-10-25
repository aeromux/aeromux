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
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles NoPositionInformation messages (TC 0), which carry a barometric altitude but no position.
/// </summary>
/// <remarks>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Position.BarometricAltitude: Pressure altitude, when the message carries one</item>
/// <item>Position.GeometricAltitude: Derived from the barometric altitude and the cached TC 19 delta, as for TC 9-18</item>
/// </list>
/// <para>
/// The coordinate, ground status and position update time are left alone, because the
/// message carries no position.
/// </para>
/// </remarks>
public sealed class NoPositionInformationHandler : ITrackingHandler
{
    public Type MessageType => typeof(NoPositionInformation);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (NoPositionInformation)message;
        if (msg.Altitude == null)
        {
            return aircraft;
        }

        // Same transmitter as TC 9-18, so the geometric altitude is derived the same way
        Altitude? geometricAltitude = AirbornePositionHandler.DeriveGeometricAltitude(
            msg.Altitude, aircraft.Position.GeometricBarometricDelta);

        TrackedPosition position = aircraft.Position with
        {
            BarometricAltitude = msg.Altitude,
            GeometricAltitude = geometricAltitude ?? aircraft.Position.GeometricAltitude
        };

        return aircraft with { Position = position };
    }
}
