namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Transponder capability field (CA) from DF 11 (All-Call Reply) messages.
/// Indicates the operational capabilities and status of the Mode S transponder.
/// </summary>
/// <remarks>
/// The capability field is 3 bits (values 0-7) and indicates:
/// - Level of Mode S support (Level 1 = basic, Level 2+ = enhanced)
/// - On-ground vs airborne status (for Level 2+ transponders)
/// - SI (Surveillance Identifier) code capability
/// - Special conditions (DR field, flight status)
///
/// Level 1 transponders support only basic Mode S (DF 0, 4, 5, 11).
/// Level 2+ transponders support extended squitter and enhanced surveillance.
///
/// Reference: ICAO Annex 10, Volume IV, Chapter 3.
/// </remarks>
public enum TransponderCapability
{
    /// <summary>
    /// Level 1 transponder - basic Mode S capability.
    /// Supports only DF 0, 4, 5, 11 messages (no extended squitter).
    /// </summary>
    Level1 = 0,

    /// <summary>
    /// Reserved for future use (CA = 1).
    /// </summary>
    Reserved1 = 1,

    /// <summary>
    /// Reserved for future use (CA = 2).
    /// </summary>
    Reserved2 = 2,

    /// <summary>
    /// Reserved for future use (CA = 3).
    /// </summary>
    Reserved3 = 3,

    /// <summary>
    /// Level 2+ transponder, on-ground status.
    /// Enhanced Mode S with extended squitter capability (ADS-B).
    /// </summary>
    Level2PlusOnGround = 4,

    /// <summary>
    /// Level 2+ transponder, airborne status.
    /// Enhanced Mode S with extended squitter capability (ADS-B).
    /// </summary>
    Level2PlusAirborne = 5,

    /// <summary>
    /// Level 2+ transponder, on-ground or airborne (status uncertain).
    /// Enhanced Mode S with extended squitter capability (ADS-B).
    /// </summary>
    Level2PlusOnGroundOrAirborne = 6,

    /// <summary>
    /// Special capability code indicating one of:
    /// - Downlink Request (DR) field is not zero (interrogation response pending)
    /// - Flight status indicates alert/SPI condition (FS = 2, 3, 4, or 5)
    /// Can be airborne or on-ground.
    /// </summary>
    DRNotZeroOrSpecialFlightStatus = 7
}
