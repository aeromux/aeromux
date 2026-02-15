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
/// Interface for global settings to allow testability of ConfigurationBuilder.
/// This interface abstracts command-line arguments from the Spectre.Console.Cli framework,
/// enabling ConfigurationBuilder to be tested without framework dependencies.
/// </summary>
public interface IGlobalSettings
{
    /// <summary>
    /// Gets the path to the configuration file.
    /// Null means use default location (aeromux.yaml).
    /// </summary>
    string? ConfigPath { get; }

    /// <summary>
    /// Gets the logging level override.
    /// Null means use value from YAML or defaults.
    /// </summary>
    LogEventLevel? LogLevel { get; }
}
