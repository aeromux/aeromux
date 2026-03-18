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
using Serilog;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Validates and resolves live command configuration from CLI parameters and YAML config.
/// Supports unified input model: SDR sources, Beast sources, or both aggregated.
/// </summary>
public static class LiveConfigValidator
{
    /// <summary>
    /// Validates all live command configuration and returns an immutable validated config record.
    /// </summary>
    /// <param name="settings">CLI command settings.</param>
    /// <param name="config">Loaded Aeromux configuration.</param>
    /// <returns>Validated and resolved configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    /// <remarks>
    /// Input source resolution follows CLI > YAML > Default priority per setting:
    /// - CLI --beast-source replaces YAML beastSources entirely.
    /// - --sdr-source or default (no Beast) enables SDR from YAML sdrSources.
    /// - Both can be active simultaneously for aggregated mode.
    /// </remarks>
    public static LiveValidatedConfig Validate(LiveSettings settings, AeromuxConfig config)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(config);

        // 1. Resolve Beast sources (CLI > YAML)
        List<BeastSourceConfig> beastSources;
        bool hasBeastFromCli = settings.BeastSource is { Length: > 0 };

        if (hasBeastFromCli)
        {
            // CLI --beast-source replaces YAML beastSources entirely
            beastSources = ConnectionStringParser.ParseMultiple(settings.BeastSource);
        }
        else if (config.BeastSources is { Count: > 0 })
        {
            // Use YAML beastSources
            beastSources = config.BeastSources;
        }
        else
        {
            beastSources = [];
        }

        // 2. Resolve SDR usage
        // SDR is implied (default) when no Beast sources are configured.
        // When Beast sources exist, SDR requires explicit --sdr-source flag.
        bool hasBeast = beastSources.Count > 0;
        bool useSdr = settings.SdrSource || !hasBeast;

        // 3. Validate SDR sources
        List<SdrSourceConfig> enabledSdrSources = [];
        if (useSdr)
        {
            enabledSdrSources = config.SdrSources?.Where(d => d.Enabled).ToList() ?? [];

            if (enabledSdrSources.Count == 0)
            {
                throw new InvalidOperationException(
                    "No enabled SDR sources found in configuration. " +
                    "Add SDR sources to sdrSources in YAML, or use --beast-source for Beast-only mode.");
            }
        }

        // 4. Validate at least one input source
        if (enabledSdrSources.Count == 0 && beastSources.Count == 0)
        {
            throw new InvalidOperationException(
                "No input sources configured. Specify --sdr-source, --beast-source, or configure sources in YAML.");
        }

        // Log configuration
        if (enabledSdrSources.Count > 0)
        {
            Log.Information("SDR sources: {Count} enabled", enabledSdrSources.Count);
        }

        if (beastSources.Count > 0)
        {
            foreach (BeastSourceConfig beast in beastSources)
            {
                Log.Information("Beast source: {Host}:{Port}", beast.Host, beast.Port);
            }
        }

        Log.Information("Tracking config: ConfidenceLevel={Level}, IcaoTimeout={Timeout}s",
            config.Tracking!.ConfidenceLevel, config.Tracking.IcaoTimeoutSeconds);

        LogReceiverLocation(config.Receiver);

        return new LiveValidatedConfig
        {
            Config = config,
            EnabledSdrSources = enabledSdrSources,
            BeastSources = beastSources,
            Receiver = config.Receiver,
            Tracking = config.Tracking
        };
    }

    /// <summary>
    /// Logs receiver location status for distance calculation.
    /// </summary>
    /// <param name="receiver">Receiver configuration, or null if not configured.</param>
    private static void LogReceiverLocation(ReceiverConfig? receiver)
    {
        if (receiver?.Latitude.HasValue == true && receiver?.Longitude.HasValue == true)
        {
            Log.Information("Receiver location configured: {Lat:F4}° {LatDir}, {Lon:F4}° {LonDir}",
                Math.Abs(receiver.Latitude!.Value),
                receiver.Latitude.Value >= 0 ? "N" : "S",
                Math.Abs(receiver.Longitude!.Value),
                receiver.Longitude.Value >= 0 ? "E" : "W");
        }
        else
        {
            Log.Warning("Receiver location not configured - distance calculation disabled");
        }
    }
}
