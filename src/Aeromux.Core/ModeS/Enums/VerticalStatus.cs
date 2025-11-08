namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Vertical status from ACAS messages (DF 0, DF 16).
/// Indicates whether the aircraft is airborne or on the ground.
/// </summary>
public enum VerticalStatus
{
    /// <summary>
    /// Aircraft is airborne (value 0).
    /// </summary>
    Airborne = 0,

    /// <summary>
    /// Aircraft is on the ground (value 1).
    /// </summary>
    Ground = 1
}
