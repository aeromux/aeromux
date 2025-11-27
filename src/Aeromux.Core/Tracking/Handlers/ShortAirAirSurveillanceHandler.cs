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
/// Handles ShortAirAirSurveillance messages (DF 0).
/// Updates flight status and altitude from ACAS (Airborne Collision Avoidance System) coordination messages.
/// </summary>
/// <remarks>
/// <para><strong>DF 0: Short Air-Air Surveillance (ACAS)</strong></para>
/// <para>
/// Used for ACAS/TCAS coordination between aircraft for collision avoidance.
/// Structure identical to DF 4 (Surveillance Altitude Reply), but semantic purpose is aircraft-to-aircraft
/// coordination rather than ground interrogation response. Represents less than 1% of Mode S message traffic.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Identification.FlightStatus: Airborne/ground status, alert conditions for collision avoidance calculations</item>
/// <item>Position.BarometricAltitude: Pressure altitude for vertical separation calculations in TCAS</item>
/// </list>
/// <para>
/// ACAS systems use these messages to coordinate Resolution Advisories (RAs) between aircraft.
/// Altitude information is critical for determining vertical separation and climb/descend advisories.
/// These messages enable aircraft to independently resolve conflicts without ground-based ATC intervention.
/// </para>
/// </remarks>
public sealed class ShortAirAirSurveillanceHandler : ITrackingHandler
{
    public Type MessageType => typeof(ShortAirAirSurveillance);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (ShortAirAirSurveillance)message;
        var changedFields = new HashSet<string>();
        TrackedIdentification identification = aircraft.Identification;
        TrackedPosition position = aircraft.Position;
        bool identChanged = false;
        bool posChanged = false;

        // Update FlightStatus from ACAS coordination message
        // Used by TCAS for collision avoidance calculations (threat assessment)
        if (identification.FlightStatus != msg.FlightStatus)
        {
            identification = identification with { FlightStatus = msg.FlightStatus };
            changedFields.Add($"{nameof(Aircraft.Identification)}.{nameof(TrackedIdentification.FlightStatus)}");
            identChanged = true;
        }

        // Update BarometricAltitude from ACAS coordination message
        // Critical for vertical separation calculations in TCAS Resolution Advisories
        // Used to determine whether to climb or descend to avoid collision
        if (msg.Altitude != null && position.BarometricAltitude != msg.Altitude)
        {
            position = position with { BarometricAltitude = msg.Altitude };
            changedFields.Add(nameof(Aircraft.Position));
            posChanged = true;
        }

        // Return updated aircraft state if anything changed
        if (identChanged || posChanged)
        {
            return (aircraft with { Identification = identification, Position = position }, changedFields);
        }

        return (aircraft, changedFields);
    }
}
