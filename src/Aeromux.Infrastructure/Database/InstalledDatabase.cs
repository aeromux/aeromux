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

using Aeromux.Core.Database;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Represents an installed aeromux-db database file discovered on disk.
/// </summary>
public class InstalledDatabase
{
    /// <summary>
    /// Gets the full path to the database file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the database filename (e.g., <c>aeromux-db_2026.1.w08_r1.sqlite</c>).
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the version parsed from the filename.
    /// </summary>
    public required DatabaseVersion VersionFromFilename { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Gets the metadata read from the SQLite database.
    /// <c>null</c> if the file is corrupted or cannot be read.
    /// </summary>
    public DatabaseMetadata? Metadata { get; init; }
}
