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
/// Configuration for the daemon's automatic aircraft-database updates.
/// Nested under <see cref="DatabaseConfig"/> as <c>database.autoUpdate</c>; daemon-only, YAML-only.
/// Only takes effect when database enrichment is enabled and its directory is writable.
/// </summary>
public class DatabaseAutoUpdateConfig
{
    /// <summary>
    /// Minimum allowed check interval, in hours. Guards against a hot loop from a misconfigured value.
    /// </summary>
    public const int MinCheckIntervalHours = 1;

    /// <summary>
    /// Maximum allowed check interval, in hours (one year). Guards against <see cref="TimeSpan.FromHours"/> overflow.
    /// </summary>
    public const int MaxCheckIntervalHours = 24 * 365;

    /// <summary>
    /// Gets or sets whether the daemon automatically checks for and installs newer databases.
    /// Default: <c>true</c> (gated on enrichment being enabled and the database directory writable).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to run a check shortly after the daemon starts, in addition to the interval.
    /// Default: <c>true</c>.
    /// </summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets how often, in hours of uptime, to re-check for a newer database. Default: <c>24</c>.
    /// The check is monotonic from the previous check, not a fixed wall-clock time.
    /// </summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether superseded database files are removed after a successful auto-update.
    /// When <c>true</c> (default), only the newly installed database is kept, so the directory does not
    /// accumulate old files under unattended operation. Set <c>false</c> to keep previous files (e.g. for
    /// archival or manual rollback). Applies to the daemon auto-updater only; the <c>database update</c>
    /// command never deletes files.
    /// </summary>
    public bool PruneOldDatabases { get; set; } = true;

    /// <summary>
    /// Returns a fresh, validated copy of the given configuration. A <c>null</c> input resolves to the
    /// built-in default (enabled) — <c>omitted == default == on</c> — and the interval is clamped to
    /// [<see cref="MinCheckIntervalHours"/>, <see cref="MaxCheckIntervalHours"/>].
    /// Never mutates the input instance.
    /// </summary>
    /// <param name="config">The raw configuration (possibly <c>null</c> when the YAML omits <c>autoUpdate</c>).</param>
    /// <returns>A new, clamped configuration.</returns>
    public static DatabaseAutoUpdateConfig Resolve(DatabaseAutoUpdateConfig? config)
    {
        DatabaseAutoUpdateConfig source = config ?? new DatabaseAutoUpdateConfig();
        return new DatabaseAutoUpdateConfig
        {
            Enabled = source.Enabled,
            CheckOnStartup = source.CheckOnStartup,
            CheckIntervalHours = Math.Clamp(source.CheckIntervalHours, MinCheckIntervalHours, MaxCheckIntervalHours),
            PruneOldDatabases = source.PruneOldDatabases
        };
    }
}
