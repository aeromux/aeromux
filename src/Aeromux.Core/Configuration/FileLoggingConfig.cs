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

using Aeromux.Core.Configuration.Enums;

namespace Aeromux.Core.Configuration;

/// <summary>
/// File output configuration for logging with rotation support.
/// </summary>
public class FileLoggingConfig
{
    /// <summary>
    /// Gets or sets whether file logging is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the log file path pattern.
    /// The dash character will be replaced with date/time based on rolling interval.
    /// Default: "logs/aeromux-.log"
    /// </summary>
    public string Path { get; set; } = "logs/aeromux-.log";

    /// <summary>
    /// Gets or sets the file rotation interval.
    /// Default: Day
    /// </summary>
    public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;

    /// <summary>
    /// Gets or sets the number of old log files to retain.
    /// Older files will be automatically deleted.
    /// Default: 7
    /// </summary>
    public int RetainedFileCount { get; set; } = 7;

    /// <summary>
    /// Gets or sets the maximum log file size in megabytes.
    /// When exceeded, a new file will be created.
    /// Default: 100 MB
    /// </summary>
    public int FileSizeLimitMb { get; set; } = 100;
}
