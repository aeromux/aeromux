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
/// Represents metadata about a GitHub release of the aeromux-db database.
/// </summary>
public class GitHubReleaseInfo
{
    /// <summary>
    /// Gets the release tag name (e.g., <c>2026.1.w08_r1</c>).
    /// </summary>
    public required string TagName { get; init; }

    /// <summary>
    /// Gets the release publication date.
    /// </summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Gets the SQLite asset filename (e.g., <c>aeromux-db_2026.1.w08_r1.sqlite</c>).
    /// </summary>
    public required string AssetName { get; init; }

    /// <summary>
    /// Gets the asset file size in bytes.
    /// </summary>
    public required long AssetSize { get; init; }

    /// <summary>
    /// Gets the asset download URL.
    /// </summary>
    public required string AssetUrl { get; init; }

    /// <summary>
    /// Gets the SHA-256 digest of the asset (format: <c>sha256:&lt;hex&gt;</c>).
    /// </summary>
    public required string AssetDigest { get; init; }
}
