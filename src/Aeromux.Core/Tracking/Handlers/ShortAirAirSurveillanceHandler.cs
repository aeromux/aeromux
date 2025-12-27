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
/// Handles ShortAirAirSurveillance messages (DF 0) for ACAS/TCAS coordination.
/// Updates flight status, altitude, and ACAS operational data.
/// </summary>
/// <remarks>
/// <para><strong>DF 0: Short Air-Air Surveillance (ACAS)</strong></para>
/// <para>
/// Used for ACAS/TCAS coordination between aircraft for collision avoidance.
/// Unlike DF 4 (Surveillance Altitude Reply), DF 0 uses ACAS-specific fields (VS, CC, SL, RI)
/// for aircraft-to-aircraft coordination. Represents less than 1% of Mode S message traffic.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Identification.FlightStatus: Mapped from VerticalStatus (Airborne/Ground), no alert/SPI information</item>
/// <item>Position.BarometricAltitude: Pressure altitude for vertical separation calculations in TCAS</item>
/// <item>Acas.SensitivityLevel: ACAS sensitivity level (0-7)</item>
/// <item>Acas.CrossLinkCapability: DF 16 coordination support flag</item>
/// <item>Acas.ReplyInformation: ACAS operational state and RA status</item>
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

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (ShortAirAirSurveillance)message;
        TrackedAcas? existingAcas = aircraft.Acas;

        // Map VerticalStatus to FlightStatus for tracking purposes
        // DF 0 (ACAS) uses VerticalStatus, but tracking system uses FlightStatus for consistency
        // No alert or SPI information available in DF 0, so map to "Normal" states
        FlightStatus mappedFlightStatus = msg.VerticalStatus == VerticalStatus.Airborne
            ? FlightStatus.AirborneNormal
            : FlightStatus.OnGroundNormal;

        TrackedIdentification identification = aircraft.Identification with
        {
            FlightStatus = mappedFlightStatus
        };

        TrackedPosition position = aircraft.Position with
        {
            BarometricAltitude = msg.Altitude ?? aircraft.Position.BarometricAltitude
        };

        // Update ACAS state with DF 0 fields
        // Preserve TC 29 and DF 16 fields that DF 0 doesn't provide
        var acas = new TrackedAcas
        {
            // DF 0 ACAS fields
            SensitivityLevel = msg.SensitivityLevel,
            CrossLinkCapability = msg.CrossLinkCapability,
            ReplyInformation = msg.ReplyInformation,

            // Preserve TC 29 fields (TcasOperational, TcasRaActive)
            TcasOperational = existingAcas?.TcasOperational,
            TcasRaActive = existingAcas?.TcasRaActive,

            // Preserve DF 16 fields (RAC, MTE, RAT)
            ResolutionAdvisoryTerminated = existingAcas?.ResolutionAdvisoryTerminated,
            MultipleThreatEncounter = existingAcas?.MultipleThreatEncounter,
            RacNotBelow = existingAcas?.RacNotBelow,
            RacNotAbove = existingAcas?.RacNotAbove,
            RacNotLeft = existingAcas?.RacNotLeft,
            RacNotRight = existingAcas?.RacNotRight,

            LastUpdate = timestamp
        };

        return aircraft with { Identification = identification, Position = position, Acas = acas };
    }
}
