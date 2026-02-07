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

namespace Aeromux.Core.Services;

/// <summary>
/// Calculates wind speed and direction from aircraft velocity vectors.
/// </summary>
/// <remarks>
/// <para>
/// Implements the vector triangle method for wind calculation from aircraft motion data.
/// The velocity triangle relates three vectors:
/// </para>
/// <list type="number">
/// <item>True Airspeed (TAS) vector: Aircraft velocity through the air mass (magnitude + heading)</item>
/// <item>Ground Speed (GS) vector: Aircraft velocity over the ground (magnitude + track)</item>
/// <item>Wind vector: Air mass velocity over the ground (magnitude + direction)</item>
/// </list>
/// <para>
/// Relationship: GS Vector = TAS Vector + Wind Vector
/// Therefore: Wind Vector = GS Vector - TAS Vector
/// </para>
/// <para>
/// The crab angle (difference between heading and track) indicates wind drift. When an aircraft
/// points in one direction (heading) but travels in another (track), wind is causing the drift.
/// Wind components are resolved into headwind/tailwind and crosswind using trigonometry,
/// then combined via Pythagorean theorem to determine wind speed and direction.
/// </para>
/// <para>
/// Wind direction follows meteorological convention: direction the wind is coming FROM (0-360°).
/// For example, a 270° wind blows from the west toward the east.
/// </para>
/// <para>
/// Implementation inspired by readsb (https://github.com/wiedehopf/readsb)
/// by Mitre Corporation and wiedehopf. Thank you for the reference implementation.
/// </para>
/// </remarks>
public static class WindCalculator
{
    /// <summary>
    /// Maximum valid wind speed in knots.
    /// </summary>
    private const double MaxWindSpeed = 250.0;

    /// <summary>
    /// Calculates wind speed and direction from velocity vectors.
    /// </summary>
    /// <param name="trueHeading">True heading in degrees (0-360)</param>
    /// <param name="track">Ground track in degrees (0-360)</param>
    /// <param name="tas">True airspeed in knots</param>
    /// <param name="groundSpeed">Ground speed in knots</param>
    /// <param name="windSpeed">Calculated wind speed in knots (output)</param>
    /// <param name="windDirection">Calculated wind direction in degrees (output, direction wind is coming from)</param>
    /// <returns>True if calculation succeeded and result is valid, false otherwise</returns>
    public static bool Calculate(
        double trueHeading,
        double track,
        double tas,
        double groundSpeed,
        out double windSpeed,
        out double windDirection)
    {
        windSpeed = 0;
        windDirection = 0;

        double headingRad = DegreesToRadians(trueHeading);
        double trackRad = DegreesToRadians(track);

        // Crab angle indicates wind drift (difference between where aircraft points vs where it goes)
        double crabAngle = NormalizeDifference(headingRad, trackRad, 0);

        // Resolve wind into headwind and crosswind components using velocity triangle
        double headwindComponent = tas - (Math.Cos(crabAngle) * groundSpeed);
        double crosswindComponent = Math.Sin(crabAngle) * groundSpeed;

        // Calculate wind magnitude from components
        double windMagnitude = Math.Sqrt((headwindComponent * headwindComponent) + (crosswindComponent * crosswindComponent));

        if (windMagnitude >= MaxWindSpeed)
        {
            return false;
        }

        // Determine wind direction (meteorological convention: direction wind comes FROM)
        double windDirectionRad = headingRad + Math.Atan2(crosswindComponent, headwindComponent);
        double windDirectionDeg = NormalizeAngle(RadiansToDegrees(windDirectionRad));

        windSpeed = windMagnitude;
        windDirection = windDirectionDeg;
        return true;
    }

    /// <summary>
    /// Normalizes the difference between two angles to the range [-center-π, center+π).
    /// Handles angle wraparound correctly.
    /// </summary>
    /// <param name="angle1">First angle in radians</param>
    /// <param name="angle2">Second angle in radians</param>
    /// <param name="center">Center point for normalization (typically 0)</param>
    /// <returns>Normalized difference in radians</returns>
    private static double NormalizeDifference(double angle1, double angle2, double center)
    {
        double diff = angle1 - angle2;

        // Handle angle wraparound (e.g., 350° - 10° = -20° should be 340°)
        while (diff >= center + Math.PI)
        {
            diff -= 2 * Math.PI;
        }
        while (diff < center - Math.PI)
        {
            diff += 2 * Math.PI;
        }

        return diff;
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 360) degrees.
    /// </summary>
    /// <param name="degrees">Angle in degrees</param>
    /// <returns>Normalized angle in degrees</returns>
    private static double NormalizeAngle(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0)
        {
            degrees += 360.0;
        }
        return degrees;
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
