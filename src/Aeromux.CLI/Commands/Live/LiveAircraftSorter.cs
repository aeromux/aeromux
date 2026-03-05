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
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Filters and sorts aircraft for the live TUI table view.
/// Supports dynamic column sorting with null-last semantics and ICAO tiebreaker,
/// plus case-insensitive substring search across ICAO and callsign fields.
/// </summary>
internal static class LiveAircraftSorter
{
    /// <summary>
    /// Filters aircraft by search term (if provided) and sorts by the specified column and direction.
    /// </summary>
    /// <param name="aircraft">All tracked aircraft from the state tracker.</param>
    /// <param name="sortColumn">Column to sort by.</param>
    /// <param name="sortDirection">Sort direction (ascending or descending).</param>
    /// <param name="searchInput">Search term for filtering, or null/empty to show all aircraft.</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <returns>Filtered and sorted list of aircraft.</returns>
    public static List<Aircraft> SortAndFilter(
        List<Aircraft> aircraft,
        SortColumn sortColumn,
        SortDirection sortDirection,
        string? searchInput,
        ReceiverConfig? receiverConfig)
    {
        // Filter by search term (case-insensitive substring match on ICAO or callsign)
        IEnumerable<Aircraft> filtered = aircraft;
        if (!string.IsNullOrEmpty(searchInput))
        {
            filtered = aircraft.Where(a =>
                a.Identification.ICAO.Contains(searchInput, StringComparison.OrdinalIgnoreCase) ||
                (a.Identification.Callsign?.Contains(searchInput, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Build receiver location once for distance sorting
        GeographicCoordinate? receiverLocation = null;
        if (receiverConfig?.Latitude.HasValue == true && receiverConfig?.Longitude.HasValue == true)
        {
            receiverLocation = new GeographicCoordinate(
                receiverConfig.Latitude.Value,
                receiverConfig.Longitude.Value);
        }

        // Cache distances when sorting by distance to avoid recomputation during comparisons
        Dictionary<string, double?>? distanceCache = null;
        if (sortColumn == SortColumn.Distance)
        {
            distanceCache = new Dictionary<string, double?>();
            foreach (Aircraft a in filtered)
            {
                distanceCache[a.Identification.ICAO] = GetDistance(a, receiverLocation);
            }
            // Materialize filtered list to avoid multiple enumeration after cache population
            filtered = filtered.ToList();
        }

        int directionMultiplier = sortDirection == SortDirection.Ascending ? 1 : -1;

        return filtered
            .OrderBy(a => a, Comparer<Aircraft>.Create((x, y) =>
            {
                int result = sortColumn switch
                {
                    SortColumn.ICAO => string.Compare(x.Identification.ICAO, y.Identification.ICAO, StringComparison.OrdinalIgnoreCase),
                    SortColumn.Callsign => CompareNullable(x.Identification.Callsign, y.Identification.Callsign),
                    SortColumn.Altitude => CompareNullable(GetAltitude(x), GetAltitude(y)),
                    SortColumn.Vertical => CompareNullable(x.Velocity.VerticalRate, y.Velocity.VerticalRate),
                    SortColumn.Distance => CompareNullable(
                        distanceCache != null ? distanceCache[x.Identification.ICAO] : GetDistance(x, receiverLocation),
                        distanceCache != null ? distanceCache[y.Identification.ICAO] : GetDistance(y, receiverLocation)),
                    SortColumn.Speed => CompareNullable(GetSpeed(x), GetSpeed(y)),
                    _ => 0
                };

                // Apply direction for non-null comparisons (null-last is direction-independent)
                if (result != 0 && !IsNullLastResult(result))
                {
                    result *= directionMultiplier;
                }

                // Tiebreaker: ICAO ascending (FR-SORT-06)
                if (result == 0)
                {
                    result = string.Compare(x.Identification.ICAO, y.Identification.ICAO, StringComparison.OrdinalIgnoreCase);
                }

                return result;
            }))
            .ToList();
    }

    /// <summary>
    /// Sentinel value returned by CompareNullable to indicate a null-last ordering.
    /// Must be outside the normal comparison range of -1, 0, 1.
    /// </summary>
    private const int NullLastSentinel = int.MaxValue;

    private static bool IsNullLastResult(int result) =>
        result == NullLastSentinel || result == -NullLastSentinel;

    /// <summary>
    /// Compares two nullable values with null-last semantics.
    /// Returns a sentinel value for null comparisons so the caller can skip direction reversal.
    /// </summary>
    private static int CompareNullable<T>(T? x, T? y) where T : struct, IComparable<T>
    {
        if (x.HasValue && y.HasValue)
        {
            return x.Value.CompareTo(y.Value);
        }

        if (!x.HasValue && !y.HasValue)
        {
            return 0;
        }

        return x.HasValue ? -NullLastSentinel : NullLastSentinel;  // null sorts last
    }

    /// <summary>
    /// Compares two nullable strings with null-last semantics.
    /// </summary>
    private static int CompareNullable(string? x, string? y)
    {
        if (x != null && y != null)
        {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        if (x == null && y == null)
        {
            return 0;
        }

        return x != null ? -NullLastSentinel : NullLastSentinel;
    }

    private static int? GetAltitude(Aircraft a) =>
        (a.Position.BarometricAltitude ?? a.Position.GeometricAltitude)?.Feet;

    private static double? GetSpeed(Aircraft a) =>
        (a.Velocity.Speed ?? a.Velocity.GroundSpeed)?.Knots;

    private static double? GetDistance(Aircraft a, GeographicCoordinate? receiverLocation)
    {
        if (receiverLocation == null || a.Position.Coordinate == null)
        {
            return null;
        }

        return receiverLocation.DistanceToMiles(a.Position.Coordinate);
    }
}
