// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
    /// Geographic coordinates (latitude/longitude) from CPR decoding (TC 9-18, 20-22).
    /// Decoded from Compact Position Reporting (even/odd frame pairs or local decoding).
    /// Null if no position message received yet or CPR decoding failed.
    /// Note: CPR longitude decoding has known accuracy issues (Issue #005).
    /// </summary>
    public GeographicCoordinate? Coordinate { get; init; }

    /// <summary>
    /// Barometric (pressure) altitude above mean sea level (TC 9-18, DF 4).
    /// Standard altimeter setting (29.92 inHg / 1013.25 hPa).
    /// This is the altitude displayed to pilots and used for traffic separation.
    /// Null if no altitude data received.
    /// </summary>
    public Altitude? BarometricAltitude { get; init; }

    /// <summary>
    /// GNSS (GPS) altitude above WGS84 ellipsoid (TC 20-22).
    /// More accurate than barometric but not used for ATC separation.
    /// Typically 50-100 feet higher than barometric altitude.
    /// Null if no GNSS altitude data received (rare in ADS-B).
    /// </summary>
    public Altitude? GeometricAltitude { get; init; }

    /// <summary>
    /// Surface vs airborne status (from TC type code).
    /// True: aircraft on ground (TC 5-8).
    /// False: aircraft airborne (TC 9-18, 20-22).
    /// Used to filter ground traffic and apply different position decoding.
    /// </summary>
    public bool IsOnGround { get; init; }

    /// <summary>
    /// Navigation Accuracy Category for Position (TC 31 Operational Status).
    /// Indicates GPS accuracy: 0-11 scale (11 = &lt;3m, 10 = &lt;10m, 9 = &lt;30m, etc.).
    /// Extracted from OperationalStatus messages (TC 31) and preserved across position updates.
    /// Values are retained until a new TC 31 message provides updated accuracy information.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public NavigationAccuracyCategory? NACp { get; init; }

    /// <summary>
    /// Navigation Integrity Category for Barometric altitude (TC 31 Operational Status).
    /// Indicates whether barometric altitude is cross-checked with other sources.
    /// True = barometric altitude integrity verified, False = not verified.
    /// Extracted from OperationalStatus messages (TC 31) and preserved across position updates.
    /// Values are retained until a new TC 31 message provides updated integrity information.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public bool? NICbaro { get; init; }

    /// <summary>
    /// Source Integrity Level (TC 31 Operational Status).
    /// Indicates probability of position data exceeding containment radius.
    /// Scale: 0-3 (3 = highest integrity, &lt;10^-7 per hour).
    /// Extracted from OperationalStatus messages (TC 31) and preserved across position updates.
    /// Values are retained until a new TC 31 message provides updated integrity information.
    /// Null if no TC 31 message received yet.
    /// </summary>
    public SurveillanceIntegrityLevel? SIL { get; init; }

    /// <summary>
    /// Timestamp of last position update (when Coordinate or altitude was last set).
    /// Null if no position data received yet.
    /// Used for calculating "seen_pos" (seconds since last position update).
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
