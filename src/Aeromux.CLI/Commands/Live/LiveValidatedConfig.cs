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
/// Defines the operating mode for the Live command.
/// </summary>
public enum LiveMode
{
    /// <summary>Direct RTL-SDR access with local signal processing.</summary>
    Standalone,

    /// <summary>Connect to a Beast-compatible TCP source.</summary>
    Client
}

/// <summary>
/// Immutable validated configuration for the live command.
/// All values have been validated and resolved from CLI parameters and YAML config.
/// </summary>
public sealed record LiveValidatedConfig
{
    /// <summary>The fully loaded Aeromux configuration.</summary>
    public required AeromuxConfig Config { get; init; }

    /// <summary>The resolved operating mode (standalone or client).</summary>
    public required LiveMode Mode { get; init; }

    /// <summary>Enabled device configurations (standalone mode only, empty for client mode).</summary>
    public required List<DeviceConfig> EnabledDevices { get; init; }

    /// <summary>Beast source host (client mode only).</summary>
    public required string? Host { get; init; }

    /// <summary>Beast source port (client mode only).</summary>
    public required int? Port { get; init; }

    /// <summary>Receiver configuration for distance calculation (both modes).</summary>
    public required ReceiverConfig? Receiver { get; init; }

    /// <summary>Tracking configuration for confidence filtering (both modes).</summary>
    public required TrackingConfig Tracking { get; init; }
}
