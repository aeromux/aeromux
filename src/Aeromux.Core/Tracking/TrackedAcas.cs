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

using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft ACAS/TCAS collision avoidance system information group.
/// Contains operational status, resolution advisory state, and threat coordination data.
/// </summary>
/// <remarks>
/// <para><strong>Data Sources:</strong></para>
/// <list type="bullet">
/// <item>DF 0 (Short Air-Air Surveillance): Sensitivity level, cross-link capability, reply information</item>
/// <item>DF 16 (Long Air-Air Surveillance): Full Resolution Advisory data with RAC fields</item>
/// <item>TC 29 (Target State and Status): TCAS operational status</item>
/// </list>
/// <para>
/// ACAS (Airborne Collision Avoidance System) / TCAS (Traffic Alert and Collision Avoidance System)
/// provides automatic traffic monitoring and resolution advisories to prevent mid-air collisions.
/// </para>
/// <para>
/// Resolution Advisory Complement (RAC) fields specify prohibited maneuvers during collision avoidance,
/// indicating which directions (vertical/horizontal) the pilot must avoid to maintain safe separation.
/// </para>
/// </remarks>
public sealed record TrackedAcas
{
    // ========================================
    // Operational Status
    // ========================================

    /// <summary>
    /// TCAS operational status (from TC 29).
    /// True if TCAS is operational and actively monitoring traffic.
    /// False if TCAS is inoperative or not installed.
    /// Null if TC 29 message not received yet.
    /// </summary>
    public bool? TcasOperational { get; init; }

    /// <summary>
    /// ACAS sensitivity level (from DF 0, DF 16).
    /// Range: 0-7 where 0 = inoperative, 1-7 = increasing sensitivity levels.
    /// Determines intruder detection range and Resolution Advisory trigger thresholds.
    /// Higher sensitivity provides earlier warnings but may increase false alerts.
    /// Null if no ACAS messages (DF 0 or DF 16) received yet.
    /// </summary>
    public int? SensitivityLevel { get; init; }

    /// <summary>
    /// Cross-link capability flag (from DF 0 only).
    /// True if aircraft supports DF 16 coordination replies for ACAS-to-ACAS coordination.
    /// False if aircraft only supports DF 0 (basic ACAS without coordination).
    /// Null if DF 0 message not received yet.
    /// </summary>
    public bool? CrossLinkCapability { get; init; }

    // ========================================
    // Resolution Advisory State
    // ========================================

    /// <summary>
    /// ACAS reply information - operational state indicator (from DF 0, DF 16).
    /// Indicates current ACAS/TCAS state and whether Resolution Advisory is active.
    /// Values:
    /// - NoAcas (0): No ACAS installed
    /// - ResolutionAdvisoryActive (2): RA currently active (collision avoidance maneuver)
    /// - VerticalOnlyRA (3): RA active, vertical-only guidance
    /// - ResolutionAdvisoryTerminated (4): RA recently terminated, returning to normal flight
    /// Null if no ACAS messages received yet.
    /// </summary>
    public AcasReplyInformation? ReplyInformation { get; init; }

    /// <summary>
    /// TCAS Resolution Advisory active flag (from TC 29 V1, DF 16).
    /// True if TCAS RA is currently active, requiring pilot to execute collision avoidance maneuver.
    /// False if no RA active (normal flight).
    /// Sources: TC 29 Version 1 (TcasRaActive field) or derived from DF 16 ReplyInformation.
    /// Null if neither TC 29 V1 nor DF 16 received.
    /// </summary>
    public bool? TcasRaActive { get; init; }

    /// <summary>
    /// Resolution Advisory terminated flag (from DF 16 only, MV field bit 59).
    /// True if RA recently terminated, aircraft returning to normal flight path.
    /// False if RA ongoing or no recent RA.
    /// Only valid when DF 16 MV field is valid (AcasValid = true).
    /// Null if DF 16 not received or MV field invalid.
    /// </summary>
    public bool? ResolutionAdvisoryTerminated { get; init; }

    /// <summary>
    /// Multiple threat encounter flag (from DF 16 only, MV field bit 60).
    /// True if ACAS has detected multiple simultaneous threats requiring coordinated avoidance.
    /// False if single threat or no threats.
    /// Only valid when DF 16 MV field is valid (AcasValid = true).
    /// Null if DF 16 not received or MV field invalid.
    /// </summary>
    public bool? MultipleThreatEncounter { get; init; }

    // ========================================
    // Resolution Advisory Complement (RAC) - DF 16 only
    // ========================================

    /// <summary>
    /// RAC: Do not pass below threat altitude (from DF 16 only, MV field bit 55).
    /// True if Resolution Advisory prohibits descending below the threat aircraft.
    /// Indicates pilot should climb or maintain altitude, not descend.
    /// Only valid when DF 16 MV field is valid (AcasValid = true).
    /// Null if DF 16 not received or MV field invalid.
    /// </summary>
    public bool? RacNotBelow { get; init; }

    /// <summary>
    /// RAC: Do not pass above threat altitude (from DF 16 only, MV field bit 56).
    /// True if Resolution Advisory prohibits climbing above the threat aircraft.
    /// Indicates pilot should descend or maintain altitude, not climb.
    /// Only valid when DF 16 MV field is valid (AcasValid = true).
    /// Null if DF 16 not received or MV field invalid.
    /// </summary>
    public bool? RacNotAbove { get; init; }

    /// <summary>
    /// RAC: Do not turn left of threat (from DF 16 only, MV field bit 57).
    /// True if Resolution Advisory prohibits turning left relative to the threat aircraft.
    /// Indicates pilot should turn right or maintain heading, not turn left.
    /// Only valid when DF 16 MV field is valid (AcasValid = true).
    /// Null if DF 16 not received or MV field invalid.
    /// </summary>
    public bool? RacNotLeft { get; init; }

    /// <summary>
    /// RAC: Do not turn right of threat (from DF 16 only, MV field bit 58).
    /// True if Resolution Advisory prohibits turning right relative to the threat aircraft.
    /// Indicates pilot should turn left or maintain heading, not turn right.
    /// Only valid when DF 16 MV field is valid (AcasValid = true).
    /// Null if DF 16 not received or MV field invalid.
    /// </summary>
    public bool? RacNotRight { get; init; }

    // ========================================
    // Timestamp
    // ========================================

    /// <summary>
    /// Timestamp of last ACAS/TCAS data update.
    /// Updated whenever any ACAS field is modified by incoming messages (DF 0, DF 16, TC 29).
    /// Null if no ACAS data received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
