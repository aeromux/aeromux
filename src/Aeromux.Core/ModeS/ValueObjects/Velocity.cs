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

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents a velocity measurement with type-safe unit conversions.
/// Immutable value object implementing value equality and ordering semantics.
/// </summary>
/// <remarks>
/// <para>Value equality (IEquatable) is auto-implemented by the record type.</para>
/// <para>Implements IComparable for ordering - higher velocities are considered "greater".</para>
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// var cruiseSpeed = Velocity.FromKnots(450, VelocityType.GroundSpeed);
/// var taxiSpeed = Velocity.FromKnots(15, VelocityType.GroundSpeed);
/// bool isFaster = cruiseSpeed > taxiSpeed;  // true
///
/// // Filter high-speed aircraft
/// var fastAircraft = velocities.Where(v => v.Knots > 300);
/// </code>
/// </remarks>
public record Velocity : IComparable<Velocity>, IComparable
{
    private readonly int _knots;

    /// <summary>
    /// Gets the type of velocity measurement (GroundSpeed, TrueAirspeed, or IndicatedAirspeed).
    /// </summary>
    public VelocityType Type { get; init; }

    private Velocity(int knots, VelocityType type)
    {
        if (knots is < 0 or > 1500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(knots),
                knots,
                "Velocity must be between 0 and 1500 knots (0 allows stationary, 1500 covers supersonic)");
        }

        _knots = knots;
        Type = type;
    }

    /// <summary>
    /// Creates a velocity from knots.
    /// </summary>
    /// <param name="knots">Velocity in knots (0 to 1500).</param>
    /// <param name="type">Type of velocity measurement.</param>
    /// <returns>A Velocity instance.</returns>
    public static Velocity FromKnots(int knots, VelocityType type) => new(knots, type);

    /// <summary>
    /// Creates a velocity from kilometers per hour.
    /// Conversion: 1 knot = 1.852 km/h (exactly, by definition).
    /// </summary>
    /// <param name="kilometersPerHour">Velocity in km/h.</param>
    /// <param name="type">Type of velocity measurement.</param>
    /// <returns>A Velocity instance.</returns>
    public static Velocity FromKilometersPerHour(int kilometersPerHour, VelocityType type)
        => new((int)(kilometersPerHour / 1.852), type);

    /// <summary>
    /// Creates a velocity from miles per hour (statute miles).
    /// Conversion: 1 knot ≈ 1.15078 mph.
    /// </summary>
    /// <param name="milesPerHour">Velocity in mph (statute miles per hour).</param>
    /// <param name="type">Type of velocity measurement.</param>
    /// <returns>A Velocity instance.</returns>
    public static Velocity FromMilesPerHour(int milesPerHour, VelocityType type)
        => new((int)(milesPerHour / 1.15078), type);

    /// <summary>
    /// Gets the velocity in knots (nautical miles per hour).
    /// 1 knot = 1 nautical mile per hour = 1.852 km/h (exactly).
    /// </summary>
    public int Knots => _knots;

    /// <summary>
    /// Gets the velocity in kilometers per hour.
    /// Conversion: 1 knot = 1.852 km/h (exactly, by definition).
    /// </summary>
    public int KilometersPerHour => (int)(_knots * 1.852);

    /// <summary>
    /// Gets the velocity in miles per hour (statute miles).
    /// Conversion: 1 knot ≈ 1.15078 mph.
    /// </summary>
    public int MilesPerHour => (int)(_knots * 1.15078);

    /// <summary>
    /// Gets the velocity in meters per second.
    /// Conversion: 1 knot ≈ 0.514444 m/s (1.852 km/h ÷ 3600 s/h).
    /// </summary>
    public double MetersPerSecond => _knots * 0.514444;

    /// <summary>
    /// Compares this velocity to another.
    /// Higher velocities are considered "greater".
    /// </summary>
    /// <param name="other">The velocity to compare to.</param>
    /// <returns>
    /// Negative if this velocity is slower,
    /// zero if equal,
    /// positive if this velocity is faster.
    /// </returns>
    public int CompareTo(Velocity? other) => other is null ? 1 : _knots.CompareTo(other._knots);

    /// <summary>
    /// Compares this velocity to another object.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>
    /// Negative if this velocity is slower,
    /// zero if equal,
    /// positive if this velocity is faster.
    /// </returns>
    /// <exception cref="ArgumentException">If obj is not a Velocity.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is not Velocity other)
        {
            throw new ArgumentException($"Object must be of type {nameof(Velocity)}");
        }
        return CompareTo(other);
    }

    /// <summary>
    /// Determines if this velocity is less than another.
    /// </summary>
    public static bool operator <(Velocity left, Velocity right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines if this velocity is greater than another.
    /// </summary>
    public static bool operator >(Velocity left, Velocity right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Determines if this velocity is less than or equal to another.
    /// </summary>
    public static bool operator <=(Velocity left, Velocity right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Determines if this velocity is greater than or equal to another.
    /// </summary>
    public static bool operator >=(Velocity left, Velocity right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Returns a string representation of the velocity.
    /// </summary>
    /// <returns>String in format "450 kts (GroundSpeed)".</returns>
    public override string ToString()
        => $"{_knots} kts ({Type})";
}
