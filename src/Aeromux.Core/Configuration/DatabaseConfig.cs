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

namespace Aeromux.Core.Configuration;

/// <summary>
/// Configuration for the aircraft metadata database.
/// Controls whether aeromux uses an SQLite database for enriching decoded aircraft data
/// with registrations, types, operators, and manufacturers.
/// </summary>
public class DatabaseConfig
{
    /// <summary>
    /// Gets or sets whether database enrichment is enabled at runtime.
    /// When <c>false</c>, aeromux operates without database enrichment.
    /// The <c>database</c> command actions (<c>update</c>, <c>info</c>) work regardless of this setting.
    /// Default: <c>false</c>. Providing <c>--database</c> on the CLI implicitly sets this to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the directory where the database file is stored.
    /// Supports both relative paths (resolved against the working directory) and absolute paths.
    /// Can be overridden via the <c>--database</c> CLI option.
    /// </summary>
    public string? Path { get; set; }
}
