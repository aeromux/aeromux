using System;

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
/// var cruiseAlt = Altitude.FromFeet(35000, AltitudeType.Barometric);  // FL350
/// var approachAlt = Altitude.FromFeet(5000, AltitudeType.Barometric);
/// bool isHigher = cruiseAlt > approachAlt;  // true
///
/// // Sorting by altitude
/// var altitudes = new[] { cruiseAlt, approachAlt }.OrderBy(a => a);
/// </code>
/// </remarks>
public record Altitude : IEquatable<Altitude>, IComparable<Altitude>, IComparable
{
    private readonly int _feet;

    /// <summary>
    /// Gets the type of altitude measurement (Barometric, Geometric, or Ground).
    /// </summary>
    public AltitudeType Type { get; init; }

    private Altitude(int feet, AltitudeType type)
    {
        if (feet is < -2000 or > 60000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(feet),
                feet,
                "Altitude must be between -2000 and 60000 feet (-2000 allows Dead Sea, 60000 allows FL600)");
        }

        _feet = feet;
        Type = type;
    }

    /// <summary>
    /// Creates an altitude from feet.
    /// </summary>
    /// <param name="feet">Altitude in feet (-2000 to 60000).</param>
    /// <param name="type">Type of altitude measurement.</param>
    /// <returns>An Altitude instance.</returns>
    public static Altitude FromFeet(int feet, AltitudeType type) => new(feet, type);

    /// <summary>
    /// Creates an altitude from meters.
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
    /// </summary>
    public int Meters => (int)(_feet * 0.3048);

    /// <summary>
    /// Gets the flight level (altitude / 100).
    /// Example: 35000 feet = FL350.
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
        if (other is null) return 1;
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
        if (obj is null) return 1;
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
        => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines if this altitude is greater than another.
    /// </summary>
    public static bool operator >(Altitude left, Altitude right)
        => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines if this altitude is less than or equal to another.
    /// </summary>
    public static bool operator <=(Altitude left, Altitude right)
        => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines if this altitude is greater than or equal to another.
    /// </summary>
    public static bool operator >=(Altitude left, Altitude right)
        => left.CompareTo(right) >= 0;

    /// <summary>
    /// Returns a string representation of the altitude.
    /// </summary>
    /// <returns>String in format "35000 ft (Barometric, FL350)".</returns>
    public override string ToString()
        => $"{_feet} ft ({Type}, FL{FlightLevel})";
}
