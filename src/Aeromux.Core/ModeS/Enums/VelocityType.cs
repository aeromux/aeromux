namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Specifies the type of velocity measurement.
/// </summary>
public enum VelocityType
{
    /// <summary>
    /// Ground speed (speed relative to the ground).
    /// Accounts for wind effects.
    /// </summary>
    GroundSpeed,

    /// <summary>
    /// True airspeed (speed relative to the air mass).
    /// Does not account for wind.
    /// </summary>
    TrueAirspeed,

    /// <summary>
    /// Indicated airspeed (speed shown on cockpit instruments).
    /// Uncorrected for altitude/temperature.
    /// </summary>
    IndicatedAirspeed
}
