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

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Metadata read from the <c>metadata</c> table inside an aeromux-db SQLite database.
/// </summary>
public class DatabaseMetadata
{
    /// <summary>
    /// Gets the database version string (e.g., <c>2026.1.w08_r1</c>).
    /// Read from the <c>db_version</c> key.
    /// </summary>
    public required string DbVersion { get; init; }

    /// <summary>
    /// Gets the build timestamp in ISO 8601 UTC format.
    /// Read from the <c>build_timestamp</c> key.
    /// </summary>
    public required string BuildTimestamp { get; init; }

    /// <summary>
    /// Gets the total number of aircraft records.
    /// Read from the <c>record_count</c> key.
    /// </summary>
    public required long RecordCount { get; init; }

    /// <summary>
    /// Gets the database schema version for compatibility checks.
    /// Read from the <c>schema_version</c> key.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Gets the version of the db-builder tool that generated this database.
    /// Read from the <c>tool_version</c> key.
    /// </summary>
    public required string ToolVersion { get; init; }
}
