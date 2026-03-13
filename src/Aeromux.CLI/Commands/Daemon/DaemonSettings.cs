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

using System.ComponentModel;
using Aeromux.CLI.Configuration;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Settings for the daemon command, capturing command-line options.
/// Inherits global --config option from GlobalSettings.
/// </summary>
public class DaemonSettings : GlobalSettings
{
    [CommandOption("--beast-port")]
    [Description("Beast protocol port (default: 30005, dump1090-compatible)")]
    public int? BeastPort { get; set; }

    [CommandOption("--json-port")]
    [Description("JSON streaming port (default: 30006, web-friendly)")]
    public int? JsonPort { get; set; }

    [CommandOption("--sbs-port")]
    [Description("SBS protocol port (default: 30003, VRS-compatible)")]
    public int? SbsPort { get; set; }

    [CommandOption("--beast-output-enabled")]
    [Description("Enable Beast binary protocol output (default: true)")]
    public bool? BeastOutputEnabled { get; set; }

    [CommandOption("--json-output-enabled")]
    [Description("Enable JSON streaming output (default: false)")]
    public bool? JsonOutputEnabled { get; set; }

    [CommandOption("--sbs-output-enabled")]
    [Description("Enable SBS BaseStation protocol output (default: false)")]
    public bool? SbsOutputEnabled { get; set; }

    [CommandOption("--bind-address")]
    [Description(
        "IP address to bind to (default: 0.0.0.0 for all interfaces)")]
    public string? BindAddress { get; set; } // CLI uses string, parsed to IPAddress in validation

    [CommandOption("--api-port")]
    [Description("REST API port (default: 8080)")]
    public int? ApiPort { get; set; }

    [CommandOption("--api-enabled")]
    [Description("Enable REST API (default: true)")]
    public bool? ApiEnabled { get; set; }

    [CommandOption("--mlat-enabled")]
    [Description("Enable MLAT input from mlat-client (default: true)")]
    public bool? MlatEnabled { get; set; }

    [CommandOption("--mlat-input-port")]
    [Description("MLAT Beast input port (default: 30104)")]
    public int? MlatInputPort { get; set; }

    [CommandOption("--receiver-uuid")]
    [Description("Receiver UUID for MLAT triangulation (format: RFC 4122)")]
    public string? ReceiverUuid { get; set; }
}
