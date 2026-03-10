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

using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft identification information group.
/// Contains all identity-related fields: ICAO address, callsign, squawk, category, emergency state.
/// Sources: TC 1-4 (ADS-B Identification), BDS 2,0 (Comm-B Identification), TC 28 (Aircraft Status), DF 5 (Surveillance Identity Reply).
/// </summary>
public sealed record TrackedIdentification
{
    /// <summary>
    /// 24-bit ICAO (International Civil Aviation Organization) address uniquely identifying the aircraft (always present).
    /// Each aircraft is assigned a unique ICAO address by its country of registration.
    /// Format: 6-character uppercase hex string (e.g., "440CF8").
    /// This is the primary key for aircraft tracking.
    /// </summary>
    public required string ICAO { get; init; }

    /// <summary>
    /// Flight identification / callsign (TC 1-4, BDS 2,0).
    /// Format: 8-character string, may contain flight number or registration.
    /// Example: "UAL1234 " or "N12345  " (space-padded).
    /// Null if not yet received.
    /// </summary>
    public string? Callsign { get; init; }

    /// <summary>
    /// Mode A code / squawk (TC 28, DF 5).
    /// Format: 4-digit octal string (0-7 for each digit).
    /// Example: "7700" (emergency), "1200" (VFR - Visual Flight Rules), "7600" (lost comms).
    /// Used by ATC for aircraft identification and emergency signaling.
    /// Null if not yet received.
    /// </summary>
    public string? Squawk { get; init; }

    /// <summary>
    /// Aircraft category / emitter type (TC 1-4).
    /// Indicates aircraft size and type: light, small, large, heavy, etc.
    /// Used for wake turbulence separation and display categorization.
    /// Null if not yet received.
    /// </summary>
    public AircraftCategory? Category { get; init; }

    /// <summary>
    /// Emergency status (TC 28).
    /// Indicates emergency condition: None, General, Medical, MinFuel, NoComms, Unlawful, Downed.
    /// Default: NoEmergency if not explicitly set.
    /// </summary>
    public EmergencyState EmergencyState { get; init; }

    /// <summary>
    /// Flight status from Mode S surveillance replies (DF 0, 4, 5, 16, 20, 21).
    /// Encodes airborne/ground status, alert conditions, and SPI (Special Position Identification - pilot-activated IDENT pulse).
    /// Null if no surveillance reply received yet.
    /// Values: AirborneNormal, OnGroundNormal, AirborneAlert, OnGroundAlert, AlertSPI, NoAlertSPI.
    /// </summary>
    public FlightStatus? FlightStatus { get; init; }

    /// <summary>
    /// ADS-B version from TC 31 (Operational Status).
    /// Values: 0 (DO-260), 1 (DO-260A), 2 (DO-260B/C), etc.
    /// Indicates ADS-B equipment capability level and supported features.
    /// This represents aircraft capability metadata, not internal tracking statistics.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public AdsbVersion? Version { get; init; }
}
