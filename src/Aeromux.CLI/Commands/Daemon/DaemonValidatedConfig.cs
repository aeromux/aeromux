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

using System.Net;
using Aeromux.Core.Configuration;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Immutable validated configuration for the daemon command.
/// All values have been validated and resolved from CLI parameters, YAML config, and defaults.
/// </summary>
public sealed record DaemonValidatedConfig
{
    /// <summary>The fully loaded Aeromux configuration.</summary>
    public required AeromuxConfig Config { get; init; }

    /// <summary>Validated Beast protocol port (1-65535).</summary>
    public required int BeastPort { get; init; }

    /// <summary>Validated JSON streaming port (1-65535).</summary>
    public required int JsonPort { get; init; }

    /// <summary>Validated SBS protocol port (1-65535).</summary>
    public required int SbsPort { get; init; }

    /// <summary>Validated bind address for TCP listeners.</summary>
    public required IPAddress BindAddress { get; init; }

    /// <summary>Optional receiver UUID for MLAT triangulation.</summary>
    public required Guid? ReceiverUuid { get; init; }

    /// <summary>Validated MLAT configuration.</summary>
    public required MlatConfig MlatConfig { get; init; }

    /// <summary>Whether Beast output format is enabled.</summary>
    public required bool BeastEnabled { get; init; }

    /// <summary>Whether JSON output format is enabled.</summary>
    public required bool JsonEnabled { get; init; }

    /// <summary>Whether SBS output format is enabled.</summary>
    public required bool SbsEnabled { get; init; }

    /// <summary>List of enabled device configurations.</summary>
    public required List<DeviceConfig> EnabledDevices { get; init; }
}
