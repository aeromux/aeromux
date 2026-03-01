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

using System.Text.RegularExpressions;

namespace Aeromux.Core.Database;

/// <summary>
/// Parses and compares calendar-based database version strings.
/// Format: <c>YYYY.Q.wWW_rR</c> (e.g., <c>2026.1.w08_r1</c>).
/// </summary>
public partial class DatabaseVersion : IComparable<DatabaseVersion>, IEquatable<DatabaseVersion>
{
    /// <summary>
    /// Gets the year component of the version.
    /// </summary>
    public int Year { get; }

    /// <summary>
    /// Gets the quarter component of the version (1-4).
    /// </summary>
    public int Quarter { get; }

    /// <summary>
    /// Gets the week component of the version (1-53).
    /// </summary>
    public int Week { get; }

    /// <summary>
    /// Gets the revision component of the version.
    /// </summary>
    public int Revision { get; }

    /// <summary>
    /// Gets the original version string.
    /// </summary>
    public string VersionString { get; }

    private DatabaseVersion(int year, int quarter, int week, int revision, string versionString)
    {
        Year = year;
        Quarter = quarter;
        Week = week;
        Revision = revision;
        VersionString = versionString;
    }

    /// <summary>
    /// Tries to parse a version string in the format <c>YYYY.Q.wWW_rR</c>.
    /// </summary>
    /// <param name="versionString">The version string to parse.</param>
    /// <param name="version">The parsed version, or <c>null</c> if parsing fails.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    public static bool TryParse(string? versionString, out DatabaseVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return false;
        }

        Match match = VersionPattern().Match(versionString);
        if (!match.Success)
        {
            return false;
        }

        int year = int.Parse(match.Groups["year"].Value);
        int quarter = int.Parse(match.Groups["quarter"].Value);
        int week = int.Parse(match.Groups["week"].Value);
        int revision = int.Parse(match.Groups["revision"].Value);

        version = new DatabaseVersion(year, quarter, week, revision, versionString);
        return true;
    }

    /// <summary>
    /// Extracts a version string from a database filename.
    /// Expected format: <c>aeromux-db_YYYY.Q.wWW_rR.sqlite</c>.
    /// </summary>
    /// <param name="fileName">The filename to extract the version from.</param>
    /// <param name="version">The parsed version, or <c>null</c> if extraction fails.</param>
    /// <returns><c>true</c> if extraction succeeded; otherwise <c>false</c>.</returns>
    public static bool TryParseFromFilename(string fileName, out DatabaseVersion? version)
    {
        version = null;
        Match match = FilenamePattern().Match(fileName);
        if (!match.Success)
        {
            return false;
        }

        return TryParse(match.Groups["version"].Value, out version);
    }

    /// <inheritdoc />
    public int CompareTo(DatabaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int result = Year.CompareTo(other.Year);
        if (result != 0)
        {
            return result;
        }

        result = Quarter.CompareTo(other.Quarter);
        if (result != 0)
        {
            return result;
        }

        result = Week.CompareTo(other.Week);
        if (result != 0)
        {
            return result;
        }

        return Revision.CompareTo(other.Revision);
    }

    /// <inheritdoc />
    public bool Equals(DatabaseVersion? other)
    {
        if (other is null)
        {
            return false;
        }

        return Year == other.Year && Quarter == other.Quarter && Week == other.Week && Revision == other.Revision;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DatabaseVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Year, Quarter, Week, Revision);

    /// <inheritdoc />
    public override string ToString() => VersionString;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(DatabaseVersion? left, DatabaseVersion? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(DatabaseVersion? left, DatabaseVersion? right) => !(left == right);

    /// <summary>Less-than operator.</summary>
    public static bool operator <(DatabaseVersion? left, DatabaseVersion? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    /// <summary>Greater-than operator.</summary>
    public static bool operator >(DatabaseVersion? left, DatabaseVersion? right) =>
        left is not null && left.CompareTo(right) > 0;

    /// <summary>Less-than-or-equal operator.</summary>
    public static bool operator <=(DatabaseVersion? left, DatabaseVersion? right) => !(left > right);

    /// <summary>Greater-than-or-equal operator.</summary>
    public static bool operator >=(DatabaseVersion? left, DatabaseVersion? right) => !(left < right);

    [GeneratedRegex(@"^(?<year>\d{4})\.(?<quarter>[1-4])\.w(?<week>\d{2})_r(?<revision>\d+)$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^aeromux-db_(?<version>\d{4}\.[1-4]\.w\d{2}_r\d+)\.sqlite$")]
    private static partial Regex FilenamePattern();
}
