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
/// Handles LongAirAirSurveillance messages (DF 16) for ACAS Resolution Advisory tracking.
/// Updates: SensitivityLevel, ReplyInformation, ResolutionAdvisoryTerminated,
/// MultipleThreatEncounter, RAC fields (RacNotBelow/Above/Left/Right), BarometricAltitude
/// </summary>
/// <remarks>
/// <para><strong>DF 16: Long Air-Air Surveillance (ACAS Coordination)</strong></para>
/// <para>
/// Long Air-Air Surveillance provides detailed TCAS/ACAS coordination data during collision avoidance.
/// Unlike DF 0 (basic ACAS status), DF 16 includes Resolution Advisory Complement (RAC) fields
/// that specify prohibited maneuvers - which directions (vertical/horizontal) the pilot must avoid
/// to maintain safe separation from threat aircraft.
/// </para>
/// <para><strong>Message Structure:</strong></para>
/// <list type="bullet">
/// <item>DF 16 contains a Message Vertical (MV) field with ACAS coordination data</item>
/// <item>RAC fields only valid when MV field is valid (AcasValid = true, VDS = 0x30)</item>
/// <item>Altitude field provides barometric altitude for vertical separation calculations</item>
/// </list>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Acas.SensitivityLevel: ACAS sensitivity level (0-7)</item>
/// <item>Acas.ReplyInformation: Current ACAS operational state</item>
/// <item>Acas.TcasRaActive: Derived from ReplyInformation (RA active if ResolutionAdvisoryActive or VerticalOnlyRA)</item>
/// <item>Acas.ResolutionAdvisoryTerminated: RA termination flag (MV bit 59)</item>
/// <item>Acas.MultipleThreatEncounter: Multiple threats flag (MV bit 60)</item>
/// <item>Acas.RacNotBelow: Prohibition on descending (MV bit 55)</item>
/// <item>Acas.RacNotAbove: Prohibition on climbing (MV bit 56)</item>
/// <item>Acas.RacNotLeft: Prohibition on left turn (MV bit 57)</item>
/// <item>Acas.RacNotRight: Prohibition on right turn (MV bit 58)</item>
/// <item>Position.BarometricAltitude: Pressure altitude (if present)</item>
/// </list>
/// <para><strong>Field Preservation:</strong></para>
/// <para>
/// This handler uses field-level merging to preserve ACAS data from other sources:
/// - TcasOperational from TC 29 is preserved (DF 16 doesn't provide this)
/// - CrossLinkCapability from DF 0 is preserved (DF 16 doesn't provide this)
/// </para>
/// </remarks>
public sealed class LongAirAirSurveillanceHandler : ITrackingHandler
{
    public Type MessageType => typeof(LongAirAirSurveillance);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (LongAirAirSurveillance)message;
        TrackedAcas? existing = aircraft.Acas;

        // === Update ACAS state with DF 16 fields ===
        // Field-level merging: preserve TC 29 and DF 0 fields not provided by DF 16

        var acas = new TrackedAcas
        {
            // Operational status
            TcasOperational = existing?.TcasOperational,  // From TC 29 (preserve)
            SensitivityLevel = msg.SensitivityLevel,      // From DF 16 (update)
            CrossLinkCapability = existing?.CrossLinkCapability,  // From DF 0 (preserve)

            // Resolution Advisory state
            ReplyInformation = msg.ReplyInformation,  // From DF 16 (update)

            // Derive TcasRaActive from ReplyInformation
            // RA is active if state is ResolutionAdvisoryActive or VerticalOnlyRA
            TcasRaActive = msg.ReplyInformation == AcasReplyInformation.ResolutionAdvisoryActive ||
                          msg.ReplyInformation == AcasReplyInformation.VerticalOnlyRA,

            ResolutionAdvisoryTerminated = msg.ResolutionAdvisoryTerminated,  // From DF 16 MV field
            MultipleThreatEncounter = msg.MultipleThreatEncounter,  // From DF 16 MV field

            // Resolution Advisory Complement (RAC) - only valid when msg.AcasValid = true
            // These fields specify prohibited maneuvers during collision avoidance
            RacNotBelow = msg.RacNotBelow,  // Do not descend below threat
            RacNotAbove = msg.RacNotAbove,  // Do not climb above threat
            RacNotLeft = msg.RacNotLeft,    // Do not turn left
            RacNotRight = msg.RacNotRight,  // Do not turn right

            LastUpdate = timestamp
        };

        // === Update altitude if available ===
        // DF 16 altitude is critical for TCAS vertical separation calculations
        TrackedPosition position = aircraft.Position with
        {
            BarometricAltitude = msg.Altitude ?? aircraft.Position.BarometricAltitude
        };

        // Return updated aircraft state
        return aircraft with { Acas = acas, Position = position };
    }
}
