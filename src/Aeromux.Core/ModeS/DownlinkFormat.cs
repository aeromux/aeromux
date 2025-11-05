namespace Aeromux.Core.ModeS;

/// <summary>
/// Mode S Downlink Format (DF) field values.
/// The DF field occupies the first 5 bits of every Mode S message.
/// </summary>
/// <remarks>
/// Determines message type and length:
/// - Short frames (56 bits): DF 0, 4, 5, 11, 16, 24
/// - Long frames (112 bits): DF 17, 18, 19, 20, 21
///
/// Reference: ICAO Annex 10, Volume IV
/// </remarks>
public enum DownlinkFormat
{
    /// <summary>
    /// DF 0: Short air-air surveillance (ACAS)
    /// Length: 56 bits
    /// </summary>
    ShortAirAirSurveillance = 0,

    /// <summary>
    /// DF 4: Surveillance altitude reply
    /// Length: 56 bits
    /// Contains: Altitude code
    /// </summary>
    SurveillanceAltitudeReply = 4,

    /// <summary>
    /// DF 5: Surveillance identity reply
    /// Length: 56 bits
    /// Contains: Squawk code
    /// </summary>
    SurveillanceIdentityReply = 5,

    /// <summary>
    /// DF 11: All-call reply
    /// Length: 56 bits
    /// Contains: Aircraft capability and ICAO address
    /// </summary>
    AllCallReply = 11,

    /// <summary>
    /// DF 16: Long air-air surveillance (ACAS)
    /// Length: 56 bits
    /// </summary>
    LongAirAirSurveillance = 16,

    /// <summary>
    /// DF 17: Extended squitter (ADS-B)
    /// Length: 112 bits
    /// Contains: Position, velocity, identification, status
    /// Most common ADS-B message type
    /// </summary>
    ExtendedSquitter = 17,

    /// <summary>
    /// DF 18: Extended squitter non-transponder (TIS-B, ADS-R)
    /// Length: 112 bits
    /// Similar to DF 17 but from ground stations or non-Mode S aircraft
    /// </summary>
    ExtendedSquitterNonTransponder = 18,

    /// <summary>
    /// DF 19: Military extended squitter
    /// Length: 112 bits
    /// </summary>
    MilitaryExtendedSquitter = 19,

    /// <summary>
    /// DF 20: Comm-B altitude reply
    /// Length: 112 bits
    /// Contains: Altitude + 56-bit data block
    /// </summary>
    CommBAltitudeReply = 20,

    /// <summary>
    /// DF 21: Comm-B identity reply
    /// Length: 112 bits
    /// Contains: Squawk + 56-bit data block
    /// </summary>
    CommBIdentityReply = 21,

    /// <summary>
    /// DF 24: Comm-D extended length message
    /// Length: 56 bits (can extend to 112 bits with continuation)
    /// Note: Treated as 56-bit short frame for basic detection
    /// </summary>
    CommDExtendedLength = 24
}
