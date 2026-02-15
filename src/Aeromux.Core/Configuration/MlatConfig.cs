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

namespace Aeromux.Core.Configuration;

/// <summary>
/// MLAT (Multilateration) input configuration for receiving MLAT-computed positions.
/// Aeromux acts as a data provider to mlat-client (sends frames on port 30005)
/// and receives MLAT-computed positions back from mlat-client via Beast protocol.
/// </summary>
/// <remarks>
/// MLAT Flow:
/// RTL-SDR → Aeromux → Beast (30005) → mlat-client → MLAT Server
///                                              ↓
/// Aeromux ← Beast (30104) ← mlat-client (MLAT positions)
///     ↓
/// TcpBroadcasters → Consumers (tar1090, VRS, etc.)
///
/// MLAT frames are:
/// - Pre-validated by MLAT network (no confidence tracking needed)
/// - Pre-deduplicated by mlat-client (no frame deduplication needed)
/// - Marked with FrameSource.Mlat for downstream consumers
/// - ICAOs from MLAT frames are marked as confident to help SDR workers
/// </remarks>
public class MlatConfig
{
    /// <summary>
    /// Gets or sets whether MLAT input is enabled.
    /// When enabled, Aeromux listens for Beast-encoded MLAT frames from mlat-client.
    /// Default: true (MLAT enabled by default).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the TCP port to listen for Beast-encoded MLAT frames.
    /// This is where mlat-client sends MLAT-computed positions back to Aeromux.
    /// Default: 30104 (dump1090/readsb standard MLAT input port).
    /// Valid range: 1024-65535.
    /// </summary>
    public int InputPort { get; set; } = 30104;

    /// <summary>
    /// Validates and builds MLAT configuration from CLI arguments and config file.
    /// Priority order: CLI arguments > YAML config > Defaults.
    /// </summary>
    /// <param name="cliEnabled">CLI --mlat-enabled value (highest priority).</param>
    /// <param name="cliPort">CLI --mlat-input-port value (highest priority).</param>
    /// <param name="configMlat">YAML configuration value (middle priority).</param>
    /// <returns>Validated MlatConfig with all values resolved.</returns>
    /// <exception cref="InvalidOperationException">Thrown when MLAT input port is invalid.</exception>
    public static MlatConfig Validate(bool? cliEnabled, int? cliPort, MlatConfig? configMlat)
    {
        // Priority: CLI > YAML > Default
        bool enabled = cliEnabled ?? configMlat?.Enabled ?? true;
        int port = cliPort ?? configMlat?.InputPort ?? 30104;

        // Validate port range (1024-65535 for non-privileged ports)
        if (port is < 1024 or > 65535)
        {
            throw new InvalidOperationException($"MLAT input port must be 1024-65535, got {port}");
        }

        return new MlatConfig { Enabled = enabled, InputPort = port };
    }
}
