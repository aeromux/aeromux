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

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Settings for the Live command.
/// </summary>
public sealed class LiveSettings : GlobalSettings
{
    /// <summary>
    /// Gets or sets whether to run in standalone mode with direct RTL-SDR access.
    /// </summary>
    [CommandOption("--standalone")]
    [Description("Run in standalone mode (process RTL-SDR directly)")]
    public bool Standalone { get; set; }

    /// <summary>
    /// Gets or sets the connection string for Beast-compatible source.
    /// </summary>
    [CommandOption("--connect [ADDRESS]")]
    [Description("Connect to Beast-compatible source (default: localhost:30005)")]
    public LiveOptionalConnectionString? Connect { get; set; }
}
