namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Surveillance Status (SS) field from ADS-B airborne position messages (TC 9-18, 20-22).
/// Indicates alert and SPI (Special Position Identification) status.
/// </summary>
/// <remarks>
/// The surveillance status is 2 bits (values 0-3) and indicates:
/// - No alert, no SPI (normal operation)
/// - Permanent alert (e.g., emergency, radio failure)
/// - Temporary alert (can be changed by ATC)
/// - SPI condition (special position identification pulse for ATC identification)
///
/// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.8.6.
/// </remarks>
public enum SurveillanceStatus
{
    /// <summary>
    /// No alert, no SPI, aircraft is in normal operation.
    /// </summary>
    NoAlertNoSPI = 0,

    /// <summary>
    /// Permanent alert (e.g., emergency condition, radio failure).
    /// This alert cannot be changed by ATC.
    /// </summary>
    PermanentAlert = 1,

    /// <summary>
    /// Temporary alert (can be changed by ATC request).
    /// </summary>
    TemporaryAlert = 2,

    /// <summary>
    /// SPI (Special Position Identification) condition.
    /// Used by ATC to identify a specific aircraft on radar display.
    /// </summary>
    SPI = 3
}
