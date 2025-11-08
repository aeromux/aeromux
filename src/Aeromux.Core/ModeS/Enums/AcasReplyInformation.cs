namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// ACAS Reply Information (RI) from DF 16 messages.
/// Indicates the operational state of the ACAS system.
/// </summary>
/// <remarks>
/// Reference: ICAO Annex 10, Volume IV, Table 3-13.
/// Only values 0, 2, 3, 4 are valid. Values 1, 5, 6, 7 are reserved.
/// </remarks>
public enum AcasReplyInformation
{
    /// <summary>
    /// No operating ACAS (value 0).
    /// Aircraft does not have ACAS or system is not operational.
    /// </summary>
    NoAcas = 0,

    /// <summary>
    /// ACAS with Resolution Advisory active (value 2).
    /// Aircraft is currently executing a resolution advisory (climb/descend command).
    /// </summary>
    ResolutionAdvisoryActive = 2,

    /// <summary>
    /// ACAS with Vertical-only Resolution Advisory active (value 3).
    /// Aircraft is executing a vertical-only RA (no horizontal component).
    /// </summary>
    VerticalOnlyRA = 3,

    /// <summary>
    /// ACAS with Resolution Advisory terminated (value 4).
    /// RA has recently ended, aircraft returning to normal flight.
    /// </summary>
    ResolutionAdvisoryTerminated = 4

    // Values 1, 5, 6, 7: Reserved (not defined by ICAO)
}
