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

    /// <summary>Validated Beast protocol output port (1-65535).</summary>
    public required int BeastOutputPort { get; init; }

    /// <summary>Validated JSON streaming output port (1-65535).</summary>
    public required int JsonOutputPort { get; init; }

    /// <summary>Validated SBS protocol output port (1-65535).</summary>
    public required int SbsOutputPort { get; init; }

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

    /// <summary>Validated REST API port (1024-65535).</summary>
    public required int ApiPort { get; init; }

    /// <summary>Whether the REST API is enabled.</summary>
    public required bool ApiEnabled { get; init; }

    /// <summary>List of enabled SDR source configurations.</summary>
    public required List<SdrSourceConfig> EnabledSdrSources { get; init; }

    /// <summary>Beast TCP input source configurations.</summary>
    public required List<BeastSourceConfig> BeastSources { get; init; }

    /// <summary>Whether SDR sources are active.</summary>
    public bool UseSdr => EnabledSdrSources.Count > 0;

    /// <summary>Whether Beast sources are active.</summary>
    public bool UseBeast => BeastSources.Count > 0;
}
