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

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents capability-related fields from Aircraft Operational Status (BDS 6,5) ADS-B messages.
/// These fields describe the aircraft's installed capabilities and technical specifications.
/// </summary>
public record CapabilityClass
{
    /// <summary>
    /// Indicates whether TCAS (Traffic Collision Avoidance System) is operational.
    /// </summary>
    public bool? TcasOperational { get; init; }

    /// <summary>
    /// Indicates whether the aircraft has CDTI (Cockpit Display of Traffic Information) capability.
    /// </summary>
    public bool? CdtiCapability { get; init; }

    /// <summary>
    /// Indicates whether the aircraft supports ADS-B 1090ES (1090 MHz Extended Squitter) transmission.
    /// </summary>
    public bool? Adsb1090EsCapability { get; init; }

    /// <summary>
    /// Indicates whether the aircraft has ARV (Air-Referenced Velocity) reporting capability.
    /// </summary>
    public bool? ArvCapability { get; init; }

    /// <summary>
    /// Indicates whether the aircraft supports TS (Target State) reporting.
    /// </summary>
    public bool? TsCapability { get; init; }

    /// <summary>
    /// Indicates the aircraft's Trajectory Change (TC) reporting capability level.
    /// Defines how the aircraft reports intended trajectory changes.
    /// </summary>
    public TrajectoryChangeReportCapability? TcCapabilityLevel { get; init; }

    /// <summary>
    /// Indicates whether the aircraft supports UAT (Universal Access Transceiver) transmission.
    /// UAT operates on 978 MHz and is primarily used in the United States.
    /// </summary>
    public bool? UatCapability { get; init; }

    /// <summary>
    /// Position Offset Applied (POA) flag.
    /// Indicates whether a position offset has been applied for privacy or security reasons.
    /// </summary>
    public bool? Poa { get; init; }

    /// <summary>
    /// Indicates whether the aircraft transmits on 1090ES at low power (B2 Low).
    /// Used for reduced interference in dense traffic areas.
    /// </summary>
    public bool? B2Low { get; init; }

    /// <summary>
    /// Navigation Accuracy Category for Velocity (NACv).
    /// Indicates the accuracy of the aircraft's velocity vector information.
    /// </summary>
    public NavigationAccuracyCategoryVelocity? Nacv { get; init; }

    /// <summary>
    /// NIC Supplement-C bit.
    /// Used in combination with NIC (Navigation Integrity Category) to determine
    /// the horizontal containment radius for position accuracy.
    /// </summary>
    public bool? NicSupplementC { get; init; }
}
