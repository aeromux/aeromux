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

using System.Text.Json.Serialization;

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Aircraft category derived from TC (Type Code) and CA (Category) fields in TC 1-4 messages.
/// Used for wake vortex separation and aircraft classification.
/// </summary>
/// <remarks>
/// The category is encoded as a combination of:
/// - Type Code (TC): 1-4 (determines general class)
/// - Category field (CA): 0-7 (3 bits, specific category within class)
///
/// Wake vortex separation is the spacing required between aircraft to avoid turbulence
/// created by wingtip vortices from preceding aircraft.
///
/// Reference: ICAO Doc 9871 (Technical Provisions for Mode S Services and Extended Squitter).
/// </remarks>
public enum AircraftCategory
{
    // TC 1: Reserved / No Category Information
    /// <summary>
    /// No category information available (TC 1, CA 0).
    /// Common with older transponders that don't report category.
    /// </summary>
    [JsonStringEnumMemberName("No Information")]
    NoInformation = 10,

    // TC 2: Surface Emergency Vehicle / Surface Service Vehicle
    /// <summary>
    /// Surface emergency vehicle (TC 2, CA 1).
    /// </summary>
    [JsonStringEnumMemberName("Surface Emergency Vehicle")]
    SurfaceEmergencyVehicle = 21,

    /// <summary>
    /// Surface service vehicle (TC 2, CA 3).
    /// </summary>
    [JsonStringEnumMemberName("Surface Service Vehicle")]
    SurfaceServiceVehicle = 23,

    // TC 3: Ground Obstruction
    /// <summary>
    /// Point obstacle (TC 3, CA 1).
    /// </summary>
    [JsonStringEnumMemberName("Ground Obstacle (Point)")]
    GroundObstaclePoint = 31,

    /// <summary>
    /// Cluster obstacle (TC 3, CA 2).
    /// </summary>
    [JsonStringEnumMemberName("Ground Obstacle (Cluster)")]
    GroundObstacleCluster = 32,

    /// <summary>
    /// Line obstacle (TC 3, CA 3).
    /// </summary>
    [JsonStringEnumMemberName("Ground Obstacle (Line)")]
    GroundObstacleLine = 33,

    // TC 4: Aircraft (Wake Vortex Categories)
    /// <summary>
    /// Light aircraft, maximum takeoff weight less than 7,000 kg (TC 4, CA 1).
    /// Examples: Cessna 172, Piper Cherokee, small general aviation aircraft.
    /// </summary>
    [JsonStringEnumMemberName("Light")]
    Light = 41,

    /// <summary>
    /// Small aircraft, 7,000 kg to 34,000 kg (TC 4, CA 2).
    /// Examples: Citation jets, King Air, regional turboprops.
    /// </summary>
    [JsonStringEnumMemberName("Small")]
    Small = 42,

    /// <summary>
    /// Large aircraft, 34,000 kg to 136,000 kg (TC 4, CA 3).
    /// Examples: Boeing 737, Airbus A320, most commercial airliners.
    /// </summary>
    [JsonStringEnumMemberName("Large")]
    Large = 43,

    /// <summary>
    /// High vortex large aircraft (TC 4, CA 4).
    /// Examples: Boeing 757 (known for strong wake turbulence).
    /// </summary>
    [JsonStringEnumMemberName("High Vortex Large")]
    HighVortexLarge = 44,

    /// <summary>
    /// Heavy aircraft, 136,000 kg or more (TC 4, CA 5).
    /// Examples: Boeing 747, 777, Airbus A380, large freighters.
    /// </summary>
    [JsonStringEnumMemberName("Heavy")]
    Heavy = 45,

    /// <summary>
    /// High performance aircraft, capable of sustained maneuvers exceeding 5g acceleration (TC 4, CA 6).
    /// Where g = gravitational acceleration (9.8 m/s² or 32.2 ft/s²).
    /// Examples: Military fighters, high-performance jets.
    /// </summary>
    [JsonStringEnumMemberName("High Performance")]
    HighPerformance = 46,

    /// <summary>
    /// Rotorcraft / Helicopter (TC 4, CA 7).
    /// Examples: All helicopters regardless of size.
    /// </summary>
    [JsonStringEnumMemberName("Rotorcraft")]
    Rotorcraft = 47,

    // Additional TC 4 categories (CA 0 and undefined)
    /// <summary>
    /// No category information for aircraft (TC 4, CA 0).
    /// Aircraft type but category not specified.
    /// </summary>
    [JsonStringEnumMemberName("No Category")]
    AircraftNoCategory = 40,

    /// <summary>
    /// Reserved or undefined category.
    /// Used for TC/CA combinations not defined in the specification.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved = 0
}
