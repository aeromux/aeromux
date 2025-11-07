namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// CPR (Compact Position Reporting) format field.
/// Indicates whether the CPR-encoded position is an even or odd frame.
/// </summary>
/// <remarks>
/// CPR encoding requires paired even and odd frames for global position decoding.
/// The format bit (F) determines which frame type:
/// - Even frames (F=0): Use NZ = 60 for latitude zones
/// - Odd frames (F=1): Use NZ = 59 for latitude zones
///
/// Both frame types are needed for unambiguous position calculation.
/// The most recent frame determines which latitude/longitude values to use.
///
/// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.8.
/// </remarks>
public enum CprFormat
{
    /// <summary>
    /// Even frame (F=0).
    /// Uses 60 latitude zones for CPR decoding.
    /// </summary>
    Even = 0,

    /// <summary>
    /// Odd frame (F=1).
    /// Uses 59 latitude zones for CPR decoding.
    /// </summary>
    Odd = 1
}
