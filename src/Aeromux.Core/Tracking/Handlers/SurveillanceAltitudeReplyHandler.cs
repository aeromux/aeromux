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
/// Handles SurveillanceAltitudeReply messages (DF 4).
/// Updates flight status and barometric altitude from Mode S surveillance interrogation responses.
/// </summary>
/// <remarks>
/// <para><strong>DF 4: Surveillance Altitude Reply</strong></para>
/// <para>
/// Response to Mode S altitude interrogations from ground stations (secondary surveillance radar).
/// Contains barometric altitude and flight status flags. Represents 5-10% of Mode S message traffic.
/// Primarily used for transponder-equipped aircraft without ADS-B capability.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Identification.FlightStatus: Airborne/ground status, alert conditions, SPI (Special Position Identification) flag</item>
/// <item>Position.BarometricAltitude: Pressure altitude from transponder (25-foot or 100-foot increments)</item>
/// </list>
/// <para>
/// FlightStatus includes: AirborneNormal, OnGroundNormal, AirborneAlert, OnGroundAlert, AlertSPI, NoAlertSPI.
/// Altitude encoding uses Q-bit (25-foot increments), Gillham code (100-foot), or metric altitude.
/// </para>
/// </remarks>
public sealed class SurveillanceAltitudeReplyHandler : ITrackingHandler
{
    public Type MessageType => typeof(SurveillanceAltitudeReply);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (SurveillanceAltitudeReply)message;

        // Update FlightStatus from surveillance reply
        // Indicates: airborne/ground, alert condition, SPI (ident button pressed)
        // Used for ground/airborne discrimination and alert monitoring
        TrackedIdentification identification = aircraft.Identification with
        {
            FlightStatus = msg.FlightStatus
        };

        // Update BarometricAltitude from transponder reply
        // Encoding: Q-bit (25-foot increments), Gillham code (100-foot), or metric
        // This provides altitude for non-ADS-B equipped aircraft
        TrackedPosition position = aircraft.Position with
        {
            BarometricAltitude = msg.Altitude ?? aircraft.Position.BarometricAltitude
        };

        return aircraft with { Identification = identification, Position = position };
    }
}
