namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Aircraft category derived from Type Code (TC) and Category (CA) fields in TC 1-4 messages.
/// Used for wake vortex separation and aircraft classification.
/// </summary>
/// <remarks>
/// The category is encoded as a combination of:
/// - Type Code (TC): 1-4 (determines general class)
/// - Category field (CA): 0-7 (3 bits, specific category within class)
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
    NoInformation = 10,

    // TC 2: Surface Emergency Vehicle / Surface Service Vehicle
    /// <summary>
    /// Surface emergency vehicle (TC 2, CA 1).
    /// </summary>
    SurfaceEmergencyVehicle = 21,

    /// <summary>
    /// Surface service vehicle (TC 2, CA 3).
    /// </summary>
    SurfaceServiceVehicle = 23,

    // TC 3: Ground Obstruction
    /// <summary>
    /// Point obstacle (TC 3, CA 1).
    /// </summary>
    GroundObstaclePoint = 31,

    /// <summary>
    /// Cluster obstacle (TC 3, CA 2).
    /// </summary>
    GroundObstacleCluster = 32,

    /// <summary>
    /// Line obstacle (TC 3, CA 3).
    /// </summary>
    GroundObstacleLine = 33,

    // TC 4: Aircraft (Wake Vortex Categories)
    /// <summary>
    /// Light aircraft, maximum takeoff weight less than 7,000 kg (TC 4, CA 1).
    /// Examples: Cessna 172, Piper Cherokee, small general aviation aircraft.
    /// </summary>
    Light = 41,

    /// <summary>
    /// Small aircraft, 7,000 kg to 34,000 kg (TC 4, CA 2).
    /// Examples: Citation jets, King Air, regional turboprops.
    /// </summary>
    Small = 42,

    /// <summary>
    /// Large aircraft, 34,000 kg to 136,000 kg (TC 4, CA 3).
    /// Examples: Boeing 737, Airbus A320, most commercial airliners.
    /// </summary>
    Large = 43,

    /// <summary>
    /// High vortex large aircraft (TC 4, CA 4).
    /// Examples: Boeing 757 (known for strong wake turbulence).
    /// </summary>
    HighVortexLarge = 44,

    /// <summary>
    /// Heavy aircraft, 136,000 kg or more (TC 4, CA 5).
    /// Examples: Boeing 747, 777, Airbus A380, large freighters.
    /// </summary>
    Heavy = 45,

    /// <summary>
    /// High performance aircraft, speed greater than 5g acceleration capability (TC 4, CA 6).
    /// Examples: Military fighters, high-performance jets.
    /// </summary>
    HighPerformance = 46,

    /// <summary>
    /// Rotorcraft / Helicopter (TC 4, CA 7).
    /// Examples: All helicopters regardless of size.
    /// </summary>
    Rotorcraft = 47,

    // Additional TC 4 categories (CA 0 and undefined)
    /// <summary>
    /// No category information for aircraft (TC 4, CA 0).
    /// Aircraft type but category not specified.
    /// </summary>
    AircraftNoCategory = 40,

    /// <summary>
    /// Reserved or undefined category.
    /// Used for TC/CA combinations not defined in the specification.
    /// </summary>
    Reserved = 0
}
