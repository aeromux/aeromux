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

using System.ComponentModel;
using Aeromux.Core.Configuration;
using Serilog.Events;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Configuration;

/// <summary>
/// Base settings class for all Aeromux commands.
/// Contains global options that apply to the entire application.
/// These options follow a configuration hierarchy: CLI args > YAML > Defaults.
/// </summary>
public class GlobalSettings : CommandSettings, IGlobalSettings
{
    /// <summary>
    /// Gets the path to the configuration file.
    /// If not specified, defaults to "aeromux.yaml" in the current directory.
    /// This is a global option that can be used with any command.
    /// </summary>
    [CommandOption("--config")]
    [Description("Path to configuration file")]
    public string? ConfigPath { get; init; }

    /// <summary>
    /// Override for Beast protocol TCP port.
    /// If specified, overrides the value from YAML and defaults.
    /// </summary>
    [CommandOption("--beast-port")]
    [Description("Beast protocol TCP port (default: 30002)")]
    public int? BeastPort { get; init; }

    /// <summary>
    /// Override for SBS BaseStation protocol TCP port.
    /// If specified, overrides the value from YAML and defaults.
    /// </summary>
    [CommandOption("--sbs-port")]
    [Description("SBS BaseStation protocol TCP port (default: 30003)")]
    public int? SbsPort { get; init; }

    /// <summary>
    /// Override for HTTP API and web interface port.
    /// If specified, overrides the value from YAML and defaults.
    /// </summary>
    [CommandOption("--http-port")]
    [Description("HTTP API and web interface port (default: 8080)")]
    public int? HttpPort { get; init; }

    /// <summary>
    /// Override for logging level.
    /// If specified, overrides the value from YAML and defaults.
    /// Valid values: Verbose, Debug, Information, Warning, Error, Fatal.
    /// </summary>
    [CommandOption("--log-level")]
    [Description("Logging level (Verbose|Debug|Information|Warning|Error|Fatal)")]
    public LogEventLevel? LogLevel { get; init; }
}
