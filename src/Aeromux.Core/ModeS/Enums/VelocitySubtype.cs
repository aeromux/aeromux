namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Velocity subtype for Type Code 19 (Airborne Velocity) messages.
/// Indicates whether velocity is ground speed or airspeed, and subsonic or supersonic.
/// </summary>
/// <remarks>
/// Type Code 19 has 4 subtypes:
/// - Subtypes 1-2: Ground speed (East/West and North/South velocity components)
/// - Subtypes 3-4: Airspeed (heading and airspeed magnitude)
/// - Subtypes 2 and 4: Supersonic (4x multiplier for speed values)
///
/// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.9.
/// </remarks>
public enum VelocitySubtype
{
    /// <summary>
    /// Ground speed (subsonic, multiplier = 1).
    /// Provides East/West and North/South velocity components.
    /// Speed range: 0-1023 knots.
    /// </summary>
    GroundSpeedSubsonic = 1,

    /// <summary>
    /// Ground speed (supersonic, multiplier = 4).
    /// Provides East/West and North/South velocity components.
    /// Speed range: 0-4092 knots.
    /// </summary>
    GroundSpeedSupersonic = 2,

    /// <summary>
    /// Airspeed (subsonic, multiplier = 1).
    /// Provides heading and airspeed magnitude (IAS or TAS).
    /// Speed range: 0-1023 knots.
    /// </summary>
    AirspeedSubsonic = 3,

    /// <summary>
    /// Airspeed (supersonic, multiplier = 4).
    /// Provides heading and airspeed magnitude (IAS or TAS).
    /// Speed range: 0-4092 knots.
    /// </summary>
    AirspeedSupersonic = 4
}
