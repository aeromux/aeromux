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

namespace Aeromux.Core.Services;

/// <summary>
/// Pure mathematical service for calculating true heading from magnetic heading and declination.
/// </summary>
/// <remarks>
/// True heading is calculated by adding magnetic declination to magnetic heading.
/// Crab angle validation ensures heading/track difference doesn't exceed 45° (unrealistic wind effect).
/// This service has no side effects and does not manage Aircraft objects or caching.
/// </remarks>
internal static class TrueHeadingCalculator
{
    private const double MaxCrabAngleDegrees = 45.0;

    /// <summary>
    /// Calculate true heading from magnetic heading and declination.
    /// </summary>
    /// <param name="magneticHeading">Magnetic heading in degrees (0-360).</param>
    /// <param name="declination">Magnetic declination in degrees (positive East).</param>
    /// <param name="track">Optional ground track for crab angle validation.</param>
    /// <returns>True heading in degrees (0-360), or null if validation fails.</returns>
    public static double? Calculate(
        double magneticHeading,
        double declination,
        double? track = null)
    {
        // Calculate true heading: magnetic heading + declination
        // Normalize to 0-360 range
        double trueHeading = (magneticHeading + declination + 360.0) % 360.0;

        // Validate with crab angle if track provided
        // Crab angle is the difference between heading and track (caused by wind)
        // Values >= 45° are unrealistic and indicate bad data
        if (!track.HasValue)
        {
            return trueHeading;
        }

        double crabAngle = Math.Abs(NormalizeDifference(trueHeading, track.Value, 180.0));
        if (crabAngle >= MaxCrabAngleDegrees)
        {
            return null;  // Invalid - excessive crab angle
        }

        return trueHeading;
    }

    /// <summary>
    /// Normalize angular difference to range centered at 'center'.
    /// Handles 0°/360° wraparound correctly.
    /// </summary>
    /// <param name="angle1">First angle in degrees.</param>
    /// <param name="angle2">Second angle in degrees.</param>
    /// <param name="center">Center of normalization range (typically 180°).</param>
    /// <returns>Normalized difference in range [-center, +center].</returns>
    /// <example>
    /// NormalizeDifference(350, 10, 180) => -20 (not +340)
    /// NormalizeDifference(10, 350, 180) => +20 (not -340)
    /// </example>
    private static double NormalizeDifference(double angle1, double angle2, double center)
    {
        double diff = angle1 - angle2;
        while (diff > center)
        {
            diff -= 360.0;
        }

        while (diff < -center)
        {
            diff += 360.0;
        }

        return diff;
    }
}
