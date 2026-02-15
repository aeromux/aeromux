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

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles SurveillanceIdentityReply messages (DF 5).
/// Updates flight status and squawk code from Mode S identity interrogation responses.
/// </summary>
/// <remarks>
/// <para><strong>DF 5: Surveillance Identity Reply</strong></para>
/// <para>
/// Response to Mode S identity interrogations from ground stations (secondary surveillance radar).
/// Contains squawk code (Mode A code) and flight status flags. Represents 5-10% of Mode S message traffic.
/// Essential for non-ADS-B transponders to provide identification to ATC.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Identification.FlightStatus: Airborne/ground status, alert conditions, SPI flag</item>
/// <item>Identification.Squawk: 4-digit octal transponder code assigned by ATC</item>
/// </list>
/// <para><strong>Special squawk codes monitored:</strong></para>
/// <list type="bullet">
/// <item>7700: General emergency</item>
/// <item>7600: Radio communication failure</item>
/// <item>7500: Unlawful interference (hijacking)</item>
/// <item>1200: VFR (Visual Flight Rules, US)</item>
/// </list>
/// <para>
/// Squawk codes are the primary identification method for non-ADS-B equipped aircraft.
/// </para>
/// </remarks>
public sealed class SurveillanceIdentityReplyHandler : ITrackingHandler
{
    public Type MessageType => typeof(SurveillanceIdentityReply);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (SurveillanceIdentityReply)message;

        // Update FlightStatus from surveillance reply
        // Same as DF 4: airborne/ground, alert, SPI flags
        //
        // Update Squawk code from identity reply
        // 4-digit octal transponder code: primary identification for non-ADS-B aircraft
        // Special codes (7700/7600/7500) trigger alerts in ATC systems
        TrackedIdentification identification = aircraft.Identification with
        {
            FlightStatus = msg.FlightStatus,
            Squawk = msg.SquawkCode
        };

        return aircraft with { Identification = identification };
    }
}
