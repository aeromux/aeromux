namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Specifies the type of altitude measurement.
/// </summary>
public enum AltitudeType
{
    /// <summary>
    /// Barometric altitude (based on standard pressure 1013.25 hPa).
    /// Most common for airborne operations.
    /// </summary>
    Barometric,

    /// <summary>
    /// Geometric altitude (GPS-based, height above WGS84 ellipsoid).
    /// More accurate for position reporting.
    /// </summary>
    Geometric,

    /// <summary>
    /// Ground level (0 feet, used for surface position reports).
    /// </summary>
    Ground
}
