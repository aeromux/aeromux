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

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents a geographic coordinate (latitude, longitude) with aviation-specific calculations.
/// Immutable value object implementing value equality.
/// </summary>
/// <remarks>
/// Does NOT implement IComparable - geographic coordinates have no natural single-axis ordering.
/// Use DistanceTo() or BearingTo() methods for positional comparisons.
/// </remarks>
/// <param name="Latitude">Latitude in decimal degrees (-90 to +90, negative is South).</param>
/// <param name="Longitude">Longitude in decimal degrees (-180 to +180, negative is West).</param>
public record GeographicCoordinate(double Latitude, double Longitude)
{
    /// <summary>
    /// Latitude in decimal degrees (-90 to +90).
    /// </summary>
    public double Latitude { get; init; } = ValidateLatitude(Latitude);

    /// <summary>
    /// Longitude in decimal degrees (-180 to +180).
    /// </summary>
    public double Longitude { get; init; } = ValidateLongitude(Longitude);

    private static double ValidateLatitude(double latitude)
    {
        if (latitude is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be between -90 and +90 degrees");
        }
        return latitude;
    }

    private static double ValidateLongitude(double longitude)
    {
        if (longitude is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be between -180 and +180 degrees");
        }
        return longitude;
    }

    /// <summary>
    /// Calculates the great-circle distance to another coordinate using the Haversine formula.
    /// </summary>
    /// <remarks>
    /// The Haversine formula calculates the shortest distance between two points on a sphere,
    /// accounting for Earth's curvature. Accurate for distances up to approximately 10,000 nautical miles.
    /// For very long distances or high-precision requirements, consider geodesic calculations.
    /// </remarks>
    /// <param name="other">The destination coordinate.</param>
    /// <returns>Distance in nautical miles.</returns>
    public double DistanceToNauticalMiles(GeographicCoordinate other)
    {
        ArgumentNullException.ThrowIfNull(other);

        const double earthRadiusNm = 3440.065; // Earth's radius in nautical miles

        double lat1Rad = Latitude * Math.PI / 180.0;
        double lat2Rad = other.Latitude * Math.PI / 180.0;
        double deltaLatRad = (other.Latitude - Latitude) * Math.PI / 180.0;
        double deltaLonRad = (other.Longitude - Longitude) * Math.PI / 180.0;

        double a = (Math.Sin(deltaLatRad / 2.0) * Math.Sin(deltaLatRad / 2.0)) +
                   (Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLonRad / 2.0) * Math.Sin(deltaLonRad / 2.0));
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return earthRadiusNm * c;
    }

    /// <summary>
    /// Calculates the great-circle distance to another coordinate.
    /// </summary>
    /// <param name="other">The destination coordinate.</param>
    /// <returns>Distance in kilometers.</returns>
    public double DistanceToKilometers(GeographicCoordinate other)
        => DistanceToNauticalMiles(other) * 1.852;

    /// <summary>
    /// Calculates the great-circle distance to another coordinate.
    /// </summary>
    /// <param name="other">The destination coordinate.</param>
    /// <returns>Distance in statute miles.</returns>
    public double DistanceToMiles(GeographicCoordinate other)
        => DistanceToNauticalMiles(other) * 1.15078;

    /// <summary>
    /// Calculates the initial bearing (forward azimuth) to another coordinate.
    /// </summary>
    /// <param name="other">The destination coordinate.</param>
    /// <returns>Bearing in degrees (0-360, where 0 is North, 90 is East).</returns>
    public double BearingTo(GeographicCoordinate other)
    {
        ArgumentNullException.ThrowIfNull(other);

        double lat1Rad = Latitude * Math.PI / 180.0;
        double lat2Rad = other.Latitude * Math.PI / 180.0;
        double deltaLonRad = (other.Longitude - Longitude) * Math.PI / 180.0;

        double y = Math.Sin(deltaLonRad) * Math.Cos(lat2Rad);
        double x = (Math.Cos(lat1Rad) * Math.Sin(lat2Rad)) -
                   (Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLonRad));
        double bearing = Math.Atan2(y, x) * 180.0 / Math.PI;

        return (bearing + 360.0) % 360.0;
    }

    /// <summary>
    /// Formats the coordinate as decimal degrees.
    /// </summary>
    /// <param name="precision">Number of decimal places (default: 4).</param>
    /// <returns>String in format "37.7749° N, 122.4194° W".</returns>
    public string ToDecimalDegrees(int precision = 4)
    {
        string latHemisphere = Latitude >= 0 ? "N" : "S";
        string lonHemisphere = Longitude >= 0 ? "E" : "W";

        return $"{Math.Abs(Latitude).ToString($"F{precision}")}° {latHemisphere}, " +
               $"{Math.Abs(Longitude).ToString($"F{precision}")}° {lonHemisphere}";
    }

    /// <summary>
    /// Returns a string representation of the coordinate.
    /// </summary>
    /// <returns>String in format "37.7749° N, 122.4194° W".</returns>
    public override string ToString() => ToDecimalDegrees();
}
