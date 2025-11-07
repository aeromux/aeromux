namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Flight Status (FS) field from Mode S surveillance replies (DF 0, 4, 5, 16, 20, 21).
/// Indicates airborne/ground status and alert conditions.
/// </summary>
/// <remarks>
/// The flight status is 3 bits (values 0-7) and encodes:
/// - Airborne vs on-ground status
/// - Alert status (no alert, temporary alert, permanent alert)
/// - SPI (Special Position Identification) condition
///
/// Reference: ICAO Annex 10, Volume IV, Chapter 3.
/// </remarks>
public enum FlightStatus
{
    /// <summary>
    /// Airborne, no alert, no SPI.
    /// Normal flight operation.
    /// </summary>
    AirborneNormal = 0,

    /// <summary>
    /// On ground, no alert, no SPI.
    /// Aircraft on ground (parked, taxiing, etc.).
    /// </summary>
    OnGroundNormal = 1,

    /// <summary>
    /// Airborne, alert (emergency).
    /// Permanent alert condition (e.g., emergency, radio failure).
    /// </summary>
    AirborneAlert = 2,

    /// <summary>
    /// On ground, alert (emergency).
    /// Permanent alert condition while on ground.
    /// </summary>
    OnGroundAlert = 3,

    /// <summary>
    /// Alert, SPI condition (airborne or on ground).
    /// Special position identification pulse for ATC.
    /// </summary>
    AlertSPI = 4,

    /// <summary>
    /// No alert, SPI condition (airborne or on ground).
    /// Special position identification without alert.
    /// </summary>
    NoAlertSPI = 5,

    /// <summary>
    /// Reserved.
    /// </summary>
    Reserved6 = 6,

    /// <summary>
    /// Not assigned.
    /// </summary>
    NotAssigned = 7
}
