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
/// Console output configuration for logging.
/// Enable for development, Docker containers, or systemd services.
/// </summary>
public class ConsoleLoggingConfig
{
    /// <summary>
    /// Gets or sets whether console logging is enabled.
    /// Enable for development, Docker containers, or systemd services.
    /// Default: false (file logging only for production)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets whether to use ANSI color codes for log levels.
    /// Disable if piping to log aggregators that don't support ANSI codes.
    /// Default: true
    /// </summary>
    public bool Colored { get; set; } = true;
}
