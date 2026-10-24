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

using System.Globalization;

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents a latitude-longitude bounding box, such as the region a map view shows.
/// Immutable value object implementing value equality.
/// </summary>
/// <remarks>
/// <para>
/// A box whose <see cref="West"/> edge is east of its <see cref="East"/> edge crosses
/// the 180° meridian: it runs east from <see cref="West"/> to 180°, then on from
/// -180° to <see cref="East"/>. Map views over the Pacific produce such boxes, and
/// treating them as ordinary ranges would match nothing at all.
/// </para>
/// <para>
/// Unlike <see cref="GeographicCoordinate"/>, invalid edges are reported through
/// <see cref="TryCreate"/> rather than an exception, because a box usually comes from
/// a client request, where a bad value is an expected input to reject, not a fault.
/// </para>
/// </remarks>
/// <param name="South">Southern latitude bound in decimal degrees.</param>
/// <param name="West">Western longitude bound in decimal degrees.</param>
/// <param name="North">Northern latitude bound in decimal degrees.</param>
/// <param name="East">Eastern longitude bound in decimal degrees.</param>
public readonly record struct GeographicBounds(double South, double West, double North, double East)
{
    /// <summary>
    /// Whether the box crosses the 180° meridian (its western edge is east of its eastern edge).
    /// </summary>
    public bool CrossesAntimeridian => West > East;

    /// <summary>
    /// Validates the four edges and creates a box from them.
    /// </summary>
    /// <param name="south">Southern latitude bound in decimal degrees.</param>
    /// <param name="west">Western longitude bound in decimal degrees.</param>
    /// <param name="north">Northern latitude bound in decimal degrees.</param>
    /// <param name="east">Eastern longitude bound in decimal degrees.</param>
    /// <param name="bounds">The created box when validation succeeds; default otherwise.</param>
    /// <param name="error">A human-readable reason when validation fails; null otherwise.</param>
    /// <returns><c>true</c> if every edge is finite, in range, and south is below north.</returns>
    public static bool TryCreate(
        double south, double west, double north, double east,
        out GeographicBounds bounds, out string? error)
    {
        bounds = default;

        if (!double.IsFinite(south) || !double.IsFinite(west) ||
            !double.IsFinite(north) || !double.IsFinite(east))
        {
            error = "Invalid bounds values: all values must be finite numbers";
            return false;
        }

        if (south >= north)
        {
            error = string.Create(CultureInfo.InvariantCulture,
                $"Invalid bounds: south ({south}) must be less than north ({north})");
            return false;
        }

        if (south < -90 || north > 90)
        {
            error = "Invalid bounds: latitude must be between -90 and 90";
            return false;
        }

        if (west is < -180 or > 180 || east is < -180 or > 180)
        {
            error = "Invalid bounds: longitude must be between -180 and 180";
            return false;
        }

        bounds = new GeographicBounds(south, west, north, east);
        error = null;
        return true;
    }

    /// <summary>
    /// Whether a point lies inside the box, edges included.
    /// </summary>
    /// <param name="latitude">Point latitude in decimal degrees.</param>
    /// <param name="longitude">Point longitude in decimal degrees.</param>
    /// <returns><c>true</c> if the point is within the box.</returns>
    public bool Contains(double latitude, double longitude)
    {
        if (latitude < South || latitude > North)
        {
            return false;
        }

        return CrossesAntimeridian
            ? longitude >= West || longitude <= East
            : longitude >= West && longitude <= East;
    }

    /// <summary>
    /// Whether a coordinate lies inside the box, edges included.
    /// </summary>
    /// <param name="coordinate">The coordinate to test.</param>
    /// <returns><c>true</c> if the coordinate is within the box.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="coordinate"/> is null.</exception>
    public bool Contains(GeographicCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return Contains(coordinate.Latitude, coordinate.Longitude);
    }

    /// <summary>
    /// Whether a rectangle overlaps the box, touching edges included.
    /// </summary>
    /// <param name="south">Rectangle southern latitude in decimal degrees.</param>
    /// <param name="west">Rectangle western longitude in decimal degrees.</param>
    /// <param name="north">Rectangle northern latitude in decimal degrees.</param>
    /// <param name="east">Rectangle eastern longitude in decimal degrees (not less than <paramref name="west"/>).</param>
    /// <returns><c>true</c> if the rectangle and the box share any area or edge.</returns>
    public bool Intersects(double south, double west, double north, double east)
    {
        if (north < South || south > North)
        {
            return false;
        }

        // A box across the meridian is the union of [West, 180] and [-180, East], so the
        // rectangle overlaps it if it reaches past either inner edge.
        return CrossesAntimeridian
            ? east >= West || west <= East
            : east >= West && west <= East;
    }
}
