// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft position information group.
/// Contains coordinates, altitude, ground state, and accuracy metrics.
/// Sources: TC 9-18, 20-22 (ADS-B Airborne/Surface Position), DF 4 (Surveillance Altitude Reply), TC 31 (Operational Status).
/// </summary>
public sealed record TrackedPosition
{
    /// <summary>
    /// Geographic coordinates (latitude/longitude) from CPR (Compact Position Reporting) decoding (TC 9-18, 20-22).
    /// CPR encoding compresses position into 17 bits each for latitude and longitude, requiring
    /// paired even/odd frames for global decoding or receiver location for local decoding.
    /// Null if no position message received yet or CPR decoding failed.
    /// </summary>
    public GeographicCoordinate? Coordinate { get; init; }

    /// <summary>
    /// Barometric (pressure) altitude above mean sea level in feet (TC 9-18, DF 4).
    /// Standard altimeter setting (29.92 inHg / 1013.25 hPa) defines reference pressure.
    /// This is the altitude displayed to pilots and used for ATC traffic separation.
    /// Changes with atmospheric pressure; same physical altitude shows different barometric
    /// altitude readings depending on weather conditions and QNH setting.
    /// Null if no altitude data received.
    /// </summary>
    public Altitude? BarometricAltitude { get; init; }

    /// <summary>
    /// GNSS (Global Navigation Satellite System, e.g., GPS/Galileo) altitude above WGS84 ellipsoid (TC 20-22).
    /// WGS84 (World Geodetic System 1984) is the standard Earth reference ellipsoid used by GPS.
    /// Geometric altitude is more accurate than barometric but not used for ATC separation.
    /// Typically 50-100 feet higher than barometric altitude (geoid-ellipsoid separation).
    /// Null if no GNSS altitude data received (TC 20-22 messages are less common than TC 9-18).
    /// </summary>
    public Altitude? GeometricAltitude { get; init; }

    /// <summary>
    /// Cached difference between geometric (GNSS) altitude and barometric altitude in feet.
    /// Derived from TC 19 (Airborne Velocity) ME bits 49-56 (SDif + dAlt fields).
    /// Positive values indicate GNSS altitude is above barometric (typical: +50 to +100 feet).
    /// Formula: Δh = s × (n - 1) × 25 feet, where s=±1 based on SDif bit, n is dAlt magnitude.
    /// Range: ±25 to ±3,150 feet.
    /// Used to derive GeometricAltitude when TC 20-22 messages are unavailable:
    /// GeometricAltitude = BarometricAltitude + GeometricBarometricDelta.
    /// Null if no TC 19 message with valid delta has been received.
    /// </summary>
    public int? GeometricBarometricDelta { get; init; }

    /// <summary>
    /// Surface vs airborne status (from TC type code).
    /// True: aircraft on ground (TC 5-8).
    /// False: aircraft airborne (TC 9-18, 20-22).
    /// Used to filter ground traffic and apply different position decoding.
    /// </summary>
    public bool IsOnGround { get; init; }

    /// <summary>
    /// Surface movement speed category (TC 5-8 surface position messages only).
    /// Provides categorized speed ranges for ground operations:
    /// - Stationary: No movement.
    /// - Speed0_125To1: 0.125 to 1 kt.
    /// - Speed1To2: 1 to 2 kt.
    /// - And so on up to Speed150To175 and GreaterThan175.
    /// These non-linear categories are optimized for taxiing and ground operations.
    /// Null if no surface position message received, or if aircraft is airborne.
    /// </summary>
    public SurfaceMovement? MovementCategory { get; init; }

    /// <summary>
    /// Represents the Single Antenna (SA) flag in ADS-B airborne position messages.
    /// Indicates whether the aircraft is equipped with diversity antenna capability.
    /// </summary>
    public AntennaFlag? Antenna { get; init; }

    /// <summary>
    /// NACp (Navigation Accuracy Category for Position) from TC 31 Operational Status.
    /// Indicates GNSS position accuracy on a 0-11 scale based on EPU (Estimated Position Uncertainty):
    /// 11 = &lt;3m (precision GPS), 10 = &lt;10m (high-quality GPS), 9 = &lt;30m (standard GPS), etc.
    /// Higher values indicate better accuracy. Used by ATC for determining separation minima.
    /// Extracted from OperationalStatus messages (TC 31) and preserved across position updates.
    /// Values are retained until a new TC 31 message provides updated accuracy information.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public NavigationAccuracyCategoryPosition? NACp { get; init; }

    /// <summary>
    /// NICbaro (Navigation Integrity Category for Barometric altitude) from TC 31 Operational Status.
    /// Indicates whether barometric altitude is cross-checked with other sensors for integrity.
    /// True = barometric altitude has been verified against GNSS altitude or other reference,
    ///        providing confidence that the pressure sensor is working correctly.
    /// False = barometric altitude is not cross-checked, single-source measurement only.
    /// Extracted from OperationalStatus messages (TC 31) and preserved across position updates.
    /// Values are retained until a new TC 31 message provides updated integrity information.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public bool? NICbaro { get; init; }

    /// <summary>
    /// SIL (Source Integrity Level) from TC 31 Operational Status.
    /// Indicates the probability per flight hour that reported position exceeds its stated
    /// accuracy (containment radius) without detection. Scale: 0-3, where:
    /// - SIL 3: &lt; 1×10⁻⁷ per hour (highest integrity, SBAS/GBAS augmented systems)
    /// - SIL 2: &lt; 1×10⁻⁵ per hour (high integrity, multi-constellation GNSS)
    /// - SIL 1: &lt; 1×10⁻³ per hour (moderate integrity, basic GNSS)
    /// - SIL 0: Unknown or &gt; 1×10⁻³ per hour (low/unknown integrity)
    /// Critical for ATC decision-making on how much to trust reported positions.
    /// Extracted from OperationalStatus messages (TC 31) and preserved across position updates.
    /// Values are retained until a new TC 31 message provides updated integrity information.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public SourceIntegrityLevel? SIL { get; init; }

    /// <summary>
    /// Timestamp of last position update (when Coordinate or altitude was last set).
    /// Null if no position data received yet.
    /// Used for calculating "seen_pos" (seconds since last position update).
    /// </summary>
    public DateTime? LastUpdate { get; init; }

    /// <summary>
    /// Source of the most recent position update (Sdr, Beast, or Mlat).
    /// Indicates where the current Coordinate came from:
    /// - Sdr: Direct reception from local SDR (Software-Defined Radio)
    /// - Beast: Received from network Beast protocol feed
    /// - Mlat: MLAT (Multilateration) - position calculated from time-of-arrival differences at multiple receivers
    /// Null if no position data received yet.
    /// Used by consumers (JSON, UI) to distinguish MLAT-derived positions from direct ADS-B reception.
    /// </summary>
    public FrameSource? PositionSource { get; init; }

    /// <summary>
    /// True if this aircraft has ever received an MLAT (Multilateration) derived position.
    /// MLAT calculates position from time-of-arrival differences at multiple ground receivers,
    /// used for aircraft not equipped with ADS-B position transmitters (Mode-S only aircraft).
    /// Used for consistent UI display (e.g., different color for MLAT-capable aircraft)
    /// without flickering when positions alternate between SDR and MLAT sources.
    /// Once set to true, this flag is never reset to false during the aircraft's tracking lifetime.
    /// </summary>
    public bool HadMlatPosition { get; init; }
}
