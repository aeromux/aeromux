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
/// Emitter category derived from the TC (Type Code) and CA (Category) fields in TC 1-4 messages.
/// Used for wake vortex separation and aircraft classification.
/// </summary>
/// <remarks>
/// Each type code selects a category set and CA (3 bits) the entry within it:
/// - TC 4: set A, aircraft by wake vortex class
/// - TC 3: set B, gliders, balloons, parachutists, ultralights, UAVs and space vehicles
/// - TC 2: set C, surface vehicles and obstacles
/// - TC 1: set D, reserved
///
/// CA 0 means no category information in every set. Values are TC × 10 + CA.
///
/// Wake vortex separation is the spacing required between aircraft to avoid turbulence
/// created by wingtip vortices from preceding aircraft.
///
/// Reference: RTCA DO-260B, §2.2.3.2.5.2 (Emitter Category).
/// </remarks>
public enum AircraftCategory
{
    /// <summary>
    /// No category information (CA 0 in any set).
    /// Common with older transponders that don't report category.
    /// </summary>
    [JsonStringEnumMemberName("No Information")]
    NoInformation = 10,

    // TC 2 (set C): surface vehicles and obstacles
    /// <summary>
    /// Surface emergency vehicle (TC 2, CA 1).
    /// </summary>
    [JsonStringEnumMemberName("Surface Emergency Vehicle")]
    SurfaceEmergencyVehicle = 21,

    /// <summary>
    /// Surface service vehicle (TC 2, CA 2).
    /// </summary>
    [JsonStringEnumMemberName("Surface Service Vehicle")]
    SurfaceServiceVehicle = 22,

    /// <summary>
    /// Point obstacle, including tethered balloons (TC 2, CA 3).
    /// </summary>
    [JsonStringEnumMemberName("Point Obstacle")]
    PointObstacle = 23,

    /// <summary>
    /// Cluster obstacle (TC 2, CA 4).
    /// </summary>
    [JsonStringEnumMemberName("Cluster Obstacle")]
    ClusterObstacle = 24,

    /// <summary>
    /// Line obstacle (TC 2, CA 5).
    /// </summary>
    [JsonStringEnumMemberName("Line Obstacle")]
    LineObstacle = 25,

    // TC 3 (set B): unpowered, lighter-than-air, unmanned and space vehicles
    /// <summary>
    /// Glider or sailplane (TC 3, CA 1).
    /// </summary>
    [JsonStringEnumMemberName("Glider / Sailplane")]
    Glider = 31,

    /// <summary>
    /// Lighter-than-air aircraft: balloon or airship (TC 3, CA 2).
    /// </summary>
    [JsonStringEnumMemberName("Lighter-than-air")]
    LighterThanAir = 32,

    /// <summary>
    /// Parachutist or skydiver (TC 3, CA 3).
    /// </summary>
    [JsonStringEnumMemberName("Parachutist / Skydiver")]
    Parachutist = 33,

    /// <summary>
    /// Ultralight, hang-glider or paraglider (TC 3, CA 4).
    /// </summary>
    [JsonStringEnumMemberName("Ultralight / Hang-glider / Paraglider")]
    Ultralight = 34,

    /// <summary>
    /// Unmanned aerial vehicle (TC 3, CA 6).
    /// </summary>
    [JsonStringEnumMemberName("Unmanned Aerial Vehicle")]
    UnmannedAerialVehicle = 36,

    /// <summary>
    /// Space or trans-atmospheric vehicle (TC 3, CA 7).
    /// </summary>
    [JsonStringEnumMemberName("Space / Trans-atmospheric Vehicle")]
    SpaceVehicle = 37,

    // TC 4 (set A): aircraft by wake vortex category
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

    /// <summary>
    /// Reserved category: all of set D (TC 1), and set B CA 5 and set C CA 6-7.
    /// </summary>
    [JsonStringEnumMemberName("Reserved")]
    Reserved = 0
}
