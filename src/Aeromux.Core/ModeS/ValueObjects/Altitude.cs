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

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents an altitude measurement with type-safe unit conversions.
/// Immutable value object implementing value equality and ordering semantics.
/// </summary>
/// <remarks>
/// <para>Value equality (IEquatable) is auto-implemented by the record type.</para>
/// <para>Implements IComparable for ordering - higher altitudes are considered "greater".</para>
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// var cruiseAlt = Altitude.FromFeet(35000, AltitudeType.Barometric);  // FL350 (Flight Level 350)
/// var approachAlt = Altitude.FromFeet(5000, AltitudeType.Barometric);
/// bool isHigher = cruiseAlt > approachAlt;  // true
///
/// // Sorting by altitude
/// var altitudes = new[] { cruiseAlt, approachAlt }.OrderBy(a => a);
/// </code>
/// </remarks>
public record Altitude : IComparable<Altitude>, IComparable
{
    private readonly int _feet;

    /// <summary>
    /// Gets the type of altitude measurement (Barometric, Geometric, or Ground).
    /// </summary>
    public AltitudeType Type { get; init; }

    private Altitude(int feet, AltitudeType type)
    {
        if (feet is < -2000 or > 126700)
        {
            throw new ArgumentOutOfRangeException(
                nameof(feet),
                feet,
                "Altitude must be between -2000 and 126700 feet " +
                "(-2000 allows Dead Sea, 126700 is Gillham code maximum, " +
                "where Gillham is the Gray code altitude encoding used in Mode A/C replies)");
        }

        _feet = feet;
        Type = type;
    }

    /// <summary>
    /// Creates an altitude from feet.
    /// </summary>
    /// <param name="feet">Altitude in feet (-2000 to 126700).</param>
    /// <param name="type">Type of altitude measurement.</param>
    /// <returns>An Altitude instance.</returns>
    public static Altitude FromFeet(int feet, AltitudeType type) => new(feet, type);

    /// <summary>
    /// Creates an altitude from meters.
    /// Conversion: 1 foot = 0.3048 meters (exactly, by international definition).
    /// </summary>
    /// <param name="meters">Altitude in meters.</param>
    /// <param name="type">Type of altitude measurement.</param>
    /// <returns>An Altitude instance.</returns>
    public static Altitude FromMeters(int meters, AltitudeType type)
        => new((int)(meters / 0.3048), type);

    /// <summary>
    /// Gets the altitude in feet.
    /// </summary>
    public int Feet => _feet;

    /// <summary>
    /// Gets the altitude in meters.
    /// Conversion: 1 foot = 0.3048 meters (exactly).
    /// </summary>
    public int Meters => (int)(_feet * 0.3048);

    /// <summary>
    /// Gets the flight level (altitude / 100).
    /// FL (Flight Level) is the altitude in hundreds of feet on standard pressure (1013.25 hPa).
    /// Example: 35000 feet = FL350.
    /// Used for high-altitude traffic separation above transition altitude.
    /// </summary>
    public int FlightLevel => _feet / 100;

    /// <summary>
    /// Compares this altitude to another.
    /// Higher altitudes are considered "greater".
    /// </summary>
    /// <param name="other">The altitude to compare to.</param>
    /// <returns>
    /// Negative if this altitude is lower,
    /// zero if equal,
    /// positive if this altitude is higher.
    /// </returns>
    public int CompareTo(Altitude? other)
    {
        if (other is null)
        {
            return 1;
        }

        return _feet.CompareTo(other._feet);
    }

    /// <summary>
    /// Compares this altitude to another object.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>
    /// Negative if this altitude is lower,
    /// zero if equal,
    /// positive if this altitude is higher.
    /// </returns>
    /// <exception cref="ArgumentException">If obj is not an Altitude.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is not Altitude other)
        {
            throw new ArgumentException($"Object must be of type {nameof(Altitude)}");
        }
        return CompareTo(other);
    }

    /// <summary>
    /// Determines if this altitude is less than another.
    /// </summary>
    public static bool operator <(Altitude left, Altitude right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines if this altitude is greater than another.
    /// </summary>
    public static bool operator >(Altitude left, Altitude right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Determines if this altitude is less than or equal to another.
    /// </summary>
    public static bool operator <=(Altitude left, Altitude right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Determines if this altitude is greater than or equal to another.
    /// </summary>
    public static bool operator >=(Altitude left, Altitude right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Returns a string representation of the altitude.
    /// </summary>
    /// <returns>String in format "35000 ft (Barometric, FL350)".</returns>
    public override string ToString()
        => $"{_feet} ft ({Type}, FL{FlightLevel})";
}
