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

using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles AirbornePosition messages (TC 9-18, 20-22) for aircraft in flight.
/// Updates position coordinates, altitude, and airborne status.
/// </summary>
/// <remarks>
/// <para><strong>Type Code ranges:</strong></para>
/// <list type="bullet">
/// <item>TC 9-18: Airborne position with barometric altitude (pressure altitude, standard setting 29.92 inHg)</item>
/// <item>TC 20-22: Airborne position with GNSS height (geometric altitude above WGS84 ellipsoid)</item>
/// </list>
/// <para>
/// This handler preserves both barometric and geometric altitude in separate fields without lossy merging.
/// Barometric altitude is used for ATC separation, while geometric altitude is more accurate but not used for traffic control.
/// </para>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Position.Coordinate: Geographic position decoded from airborne CPR encoding</item>
/// <item>Position.BarometricAltitude: Pressure altitude from TC 9-18 (if present)</item>
/// <item>Position.GeometricAltitude: GNSS altitude from TC 20-22 (if present)</item>
/// <item>Position.IsOnGround: Set to false (airborne)</item>
/// <item>Position.LastUpdate: Timestamp when position was updated</item>
/// <item>Position.ImplausibleCount: Speed/distance plausibility counter (reset on good position, incremented on bad)</item>
/// </list>
/// </remarks>
public sealed class AirbornePositionHandler : ITrackingHandler
{
    /// <summary>
    /// Default airborne speed assumption (knots) when no velocity data is available.
    /// 900 knots covers fast jets and provides a generous upper bound for plausibility checks.
    /// </summary>
    private const int DefaultAirborneSpeedKnots = 900;

    /// <summary>
    /// Number of consecutive implausible positions required before accepting a new position anyway.
    /// Prevents a stale good position from permanently blocking updates when the aircraft has legitimately moved.
    /// </summary>
    private const int PositionPersistenceThreshold = 4;

    /// <summary>
    /// Time threshold (seconds) after which the speed check is bypassed.
    /// Computed as 50% of the configured aircraft timeout. If the last position is older than this,
    /// the position is considered stale and the new position is accepted unconditionally.
    /// </summary>
    private readonly double _speedCheckBypassSeconds;

    /// <summary>
    /// Initializes the handler with tracking configuration for speed check threshold calculation.
    /// </summary>
    /// <param name="trackingConfig">Tracking configuration providing aircraft timeout for speed check bypass threshold.</param>
    public AirbornePositionHandler(TrackingConfig trackingConfig)
    {
        ArgumentNullException.ThrowIfNull(trackingConfig);
        _speedCheckBypassSeconds = trackingConfig.AircraftTimeoutSeconds / 2.0;
    }

    public Type MessageType => typeof(AirbornePosition);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(frame);

        var msg = (AirbornePosition)message;
        TrackedPosition position = aircraft.Position;

        // Update altitude based on type - CRITICAL: Check altitude type to store in correct field
        // TC 9-18: Barometric altitude (pressure altitude, used for ATC separation)
        // TC 20-22: Geometric altitude (GNSS height, more accurate but not used for ATC)
        // Both types are preserved separately to avoid lossy merging
        Altitude? barometricAltitude = position.BarometricAltitude;
        Altitude? geometricAltitude = position.GeometricAltitude;
        int? geometricBarometricDelta = position.GeometricBarometricDelta;

        if (msg.Altitude != null)
        {
            switch (msg.Altitude.Type)
            {
                case AltitudeType.Barometric:
                {
                    // Store barometric altitude from TC 9-18
                    barometricAltitude = msg.Altitude;

                    // Always derive geometric altitude from latest barometric + cached delta
                    // This ensures geometric altitude stays in sync with changing barometric altitude,
                    // which varies with atmospheric pressure and QNH settings, while geometric altitude
                    // remains absolute relative to WGS84 ellipsoid. Recalculation is necessary because
                    // barometric altitude changes frequently (every TC 9-18 message) but delta updates
                    // are less frequent (only in TC 19). By deriving from barometric + delta, we maintain
                    // accurate geometric altitude without requiring constant TC 19 updates.
                    // Strategy: Recalculate on every barometric update (TC 9-18), not on delta updates (TC 19)
                    if (geometricBarometricDelta != null)
                    {
                        int derivedGeometricFeet = barometricAltitude.Feet + geometricBarometricDelta.Value;
                        geometricAltitude = Altitude.FromFeet(derivedGeometricFeet, AltitudeType.Geometric);
                    }

                    break;
                }
                case AltitudeType.Geometric:
                {
                    // Store geometric altitude from TC 20-22 (latest explicit value)
                    geometricAltitude = msg.Altitude;

                    // Reverse-calculate delta for future derivations
                    // Formula: delta = geometric - barometric
                    if (barometricAltitude != null)
                    {
                        geometricBarometricDelta = geometricAltitude.Feet - barometricAltitude.Feet;
                    }

                    break;
                }
            }
        }

        // Determine new coordinate with speed check + persistence filtering
        // This prevents CPR decode errors (from bit corruption passing CRC) from producing
        // wildly incorrect positions that appear as long straight lines on the map
        GeographicCoordinate? newCoordinate = position.Coordinate;
        int implausibleCount = position.ImplausibleCount;

        if (msg.Position != null)
        {
            if (position.Coordinate == null || position.LastUpdate == null)
            {
                // First position — accept unconditionally
                newCoordinate = msg.Position;
                implausibleCount = 0;
            }
            else
            {
                double elapsed = (timestamp - position.LastUpdate.Value).TotalSeconds;
                if (IsPositionPlausible(position.Coordinate, msg.Position, elapsed, aircraft.Velocity.Speed?.Knots))
                {
                    // Plausible — accept
                    newCoordinate = msg.Position;
                    implausibleCount = 0;
                }
                else if (implausibleCount >= PositionPersistenceThreshold)
                {
                    // Too many consecutive implausible positions — invalidate current position
                    // rather than accepting the potentially bad new one. The next position
                    // (good or bad) will be accepted as "first position" via the null check above.
                    // This breaks the feedback loop where persistence alternately accepts
                    // bad and good positions, creating zigzag tracks on the map.
                    newCoordinate = null;
                    implausibleCount = 0;
                }
                else
                {
                    // Implausible — keep old coordinate, increment counter
                    implausibleCount++;
                }
            }
        }

        // Update position with all fields
        // Single Antenna flag from bit 40
        // IsOnGround always false for airborne messages
        // PositionSource tracks where the position came from (Sdr, Beast, or Mlat)
        // HadMlatPosition is set to true if this is an MLAT position or was previously true
        position = position with
        {
            Coordinate = newCoordinate,
            BarometricAltitude = barometricAltitude,
            GeometricAltitude = geometricAltitude,
            GeometricBarometricDelta = geometricBarometricDelta,
            Antenna = msg.Antenna ?? position.Antenna,
            IsOnGround = false,
            LastUpdate = newCoordinate != position.Coordinate ? timestamp : position.LastUpdate,
            PositionSource = frame.Source,
            HadMlatPosition = position.HadMlatPosition || frame.Source == FrameSource.Mlat,
            ImplausibleCount = implausibleCount
        };

        return aircraft with { Position = position };
    }

    /// <summary>
    /// Checks whether a position update is physically plausible based on distance vs. speed and elapsed time.
    /// Returns true if the position should be accepted, false if it appears to be a CPR decode error.
    /// </summary>
    /// <param name="previous">The aircraft's current known position.</param>
    /// <param name="candidate">The newly decoded position to validate.</param>
    /// <param name="elapsedSeconds">Seconds since the last position update.</param>
    /// <param name="speedKnots">Aircraft's known speed in knots, or null if unavailable.</param>
    /// <returns>True if the position change is plausible given the aircraft's speed and elapsed time.</returns>
    private bool IsPositionPlausible(
        GeographicCoordinate previous,
        GeographicCoordinate candidate,
        double elapsedSeconds,
        int? speedKnots)
    {
        // Stale position — skip speed check, accept unconditionally
        if (elapsedSeconds > _speedCheckBypassSeconds)
        {
            return true;
        }

        int speed = speedKnots ?? DefaultAirborneSpeedKnots;

        // Max distance aircraft could travel: (elapsed + 1s jitter tolerance) * speed in NM
        double maxDistanceNm = (elapsedSeconds + 1.0) * speed / 3600.0;

        return previous.DistanceToNauticalMiles(candidate) <= maxDistanceNm;
    }
}
