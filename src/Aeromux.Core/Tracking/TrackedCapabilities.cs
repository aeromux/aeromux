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
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft system capabilities and configuration metadata.
/// Contains transponder capabilities, ADS-B capabilities, Comm-B capabilities, and physical configuration.
/// Sources: DF 11 (All-Call Reply), TC 31 (Operational Status), BDS 1,0 (Data Link Capability), BDS 1,7 (GICB Capability).
/// </summary>
public sealed record TrackedCapabilities
{
    /// <summary>
    /// Transponder capability level (DF 11 All-Call Reply).
    /// Indicates Mode S support level and operational status (Level 1-5).
    /// Level 1: No Mode S capability (Mode A/C only).
    /// Level 2-4: Mode S with varying capability levels.
    /// Level 5: Full Mode S with enhanced surveillance.
    /// Null if DF 11 not yet received.
    /// </summary>
    public TransponderCapability? TransponderLevel { get; init; }

    /// <summary>
    /// Indicates whether TCAS (Traffic Collision Avoidance System) is operational (TC 31 CapabilityClass).
    /// True if TCAS is installed and operational, false if not operational or not installed.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? TcasCapability { get; init; }

    /// <summary>
    /// Indicates whether the aircraft has CDTI (Cockpit Display of Traffic Information) capability (TC 31 CapabilityClass).
    /// CDTI displays traffic information to the flight crew.
    /// True if CDTI is available, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? CockpitDisplayTraffic { get; init; }

    /// <summary>
    /// Indicates whether the aircraft supports ADS-B 1090ES (1090 MHz Extended Squitter) transmission (TC 31 CapabilityClass).
    /// True if 1090ES transmission is supported, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? Adsb1090ES { get; init; }

    /// <summary>
    /// Indicates whether the aircraft has ARV (Air-Referenced Velocity) reporting capability (TC 31 CapabilityClass).
    /// ARV provides airspeed and heading information.
    /// True if ARV reporting is available, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field (Version 0 does not include ARV).
    /// </summary>
    public bool? AirReferencedVelocity { get; init; }

    /// <summary>
    /// Indicates whether the aircraft supports TS (Target State) reporting (TC 31 CapabilityClass).
    /// Target State provides autopilot selected altitudes and headings (TC 29).
    /// True if TS reporting is supported, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field (Version 0 does not include TS).
    /// </summary>
    public bool? TargetStateReporting { get; init; }

    /// <summary>
    /// Trajectory Change (TC) reporting capability level (TC 31 CapabilityClass).
    /// Indicates how the aircraft reports intended trajectory changes:
    /// - Level 0: No trajectory change reporting.
    /// - Level 1: Report trajectory change via TC (Type Code) messages.
    /// - Level 2: Multiple trajectory change reports.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field (Version 0 does not include TC).
    /// </summary>
    public TrajectoryChangeReportCapability? TrajectoryChangeLevel { get; init; }

    /// <summary>
    /// Indicates whether the aircraft supports UAT (Universal Access Transceiver) transmission (TC 31 CapabilityClass).
    /// UAT operates on 978 MHz and is primarily used in the United States.
    /// True if UAT transmission is supported, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field (Version 0 does not include UAT).
    /// </summary>
    public bool? Uat978Support { get; init; }

    /// <summary>
    /// Position Offset Applied (POA) flag (TC 31 CapabilityClass).
    /// Indicates whether a position offset has been applied for privacy or security reasons.
    /// True if position offset is applied, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? PositionOffsetApplied { get; init; }

    /// <summary>
    /// Indicates whether the aircraft transmits on 1090ES at low power (B2 Low) (TC 31 CapabilityClass).
    /// Low power transmission is used for reduced interference in dense traffic areas.
    /// True if low power mode is active, false if not.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? LowPower1090ES { get; init; }

    /// <summary>
    /// Navigation Accuracy Category for Velocity (NACv) from TC 31 CapabilityClass.
    /// Indicates the accuracy of the aircraft's velocity vector information.
    /// Range: Unknown (0) to LessThan0.3MetersPerSecond (7).
    /// Note: This is different from NACv in TC 19 (Airborne Velocity), which is message-specific.
    /// This value represents the aircraft's overall velocity accuracy capability.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field (Version 0 does not include NACv).
    /// </summary>
    public NavigationAccuracyCategoryVelocity? NACv { get; init; }

    /// <summary>
    /// NIC Supplement-C bit (TC 31 CapabilityClass).
    /// Used in combination with NIC (Navigation Integrity Category) to determine
    /// the horizontal containment radius for position accuracy.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field (Version 0 does not include NIC Supplement-C).
    /// </summary>
    public bool? NICSupplementC { get; init; }

    /// <summary>
    /// Data link capability bits (BDS 1,0 Comm-B register).
    /// 16-bit capability flags indicating support for various Comm-A, Comm-B, Comm-C, and Comm-D services.
    /// Each bit represents a specific data link service or protocol support.
    /// Null if BDS 1,0 not yet received (requires ground interrogation for Comm-B replies).
    /// </summary>
    public int? DataLinkCapabilityBits { get; init; }

    /// <summary>
    /// Supported BDS registers bitmask (BDS 1,7 GICB Capability Report).
    /// 56-bit bitmask where each bit indicates support for a specific BDS register.
    /// Allows interrogators to determine which Comm-B registers the aircraft supports,
    /// enabling intelligent interrogation strategies.
    /// Null if BDS 1,7 not yet received (requires ground interrogation for Comm-B replies).
    /// </summary>
    public ulong? SupportedBdsRegisters { get; init; }

    /// <summary>
    /// Aircraft/vehicle physical dimensions (TC 31 Operational Status, surface subtype only).
    /// Provides length and width categories for surface operations (A0-A7, B0-B7).
    /// Used for ground traffic management and taxiway spacing.
    /// Null if TC 31 surface operational status not yet received, or if aircraft is airborne.
    /// </summary>
    public AircraftLengthAndWidth? Dimensions { get; init; }

    /// <summary>
    /// Timestamp of the most recent capability update.
    /// Updated whenever any capability field changes.
    /// Null if no capability data has been received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
