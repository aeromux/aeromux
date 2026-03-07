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

namespace Aeromux.CLI.Commands.Device;

/// <summary>
/// Settings for the device command.
/// Inherits global options (<c>--config</c>, <c>--log-level</c>, <c>--database</c>) from <see cref="GlobalSettings"/>.
/// </summary>
public class DeviceSettings : GlobalSettings
{
    /// <summary>
    /// Gets or sets whether to display detailed tuner parameters by opening each device.
    /// </summary>
    [CommandOption("-v|--verbose")]
    [Description("Show detailed tuner parameters (opens each device)")]
    public bool Verbose { get; set; }
}
