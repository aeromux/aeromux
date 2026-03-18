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

using Aeromux.Core.Configuration;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Immutable validated configuration for the live command.
/// All values have been validated and resolved from CLI parameters and YAML config.
/// </summary>
public sealed record LiveValidatedConfig
{
    /// <summary>The fully loaded Aeromux configuration.</summary>
    public required AeromuxConfig Config { get; init; }

    /// <summary>Enabled SDR source configurations (empty when Beast-only mode).</summary>
    public required List<SdrSourceConfig> EnabledSdrSources { get; init; }

    /// <summary>Beast TCP input source configurations (empty when SDR-only mode).</summary>
    public required List<BeastSourceConfig> BeastSources { get; init; }

    /// <summary>Receiver configuration for distance calculation (both modes).</summary>
    public required ReceiverConfig? Receiver { get; init; }

    /// <summary>Tracking configuration for confidence filtering (both modes).</summary>
    public required TrackingConfig Tracking { get; init; }

    /// <summary>Whether SDR sources are active.</summary>
    public bool UseSdr => EnabledSdrSources.Count > 0;

    /// <summary>Whether Beast sources are active.</summary>
    public bool UseBeast => BeastSources.Count > 0;
}
