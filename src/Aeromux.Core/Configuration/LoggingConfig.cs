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

using Serilog.Events;

namespace Aeromux.Core.Configuration;

/// <summary>
/// Structured logging configuration for console and file outputs.
/// </summary>
public class LoggingConfig
{
    /// <summary>
    /// Gets or sets the minimum log level to output.
    /// Uses Serilog's LogEventLevel enum directly.
    /// Default: Information
    /// </summary>
    public LogEventLevel Level { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Gets or sets the console logging configuration.
    /// </summary>
    public ConsoleLoggingConfig Console { get; set; } = new();

    /// <summary>
    /// Gets or sets the file logging configuration.
    /// </summary>
    public FileLoggingConfig File { get; set; } = new();
}
