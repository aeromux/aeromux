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

using Aeromux.Core.Configuration;
using Serilog;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Validates and resolves live command configuration from CLI parameters and YAML config.
/// Determines operating mode, validates devices or connection string, and logs configuration.
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
    /// Validates mutual exclusivity of --standalone and --connect flags, determines operating mode,
    /// and performs mode-specific validation (device availability for standalone, connection string
    /// parsing for client). Throws InvalidOperationException for all validation failures, which
    /// LiveExceptionHandler.HandleException maps to user-friendly error messages.
    /// </remarks>
    public static LiveValidatedConfig Validate(LiveSettings settings, AeromuxConfig config)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(config);

        // Validate mutual exclusivity of --standalone and --connect
        if (settings is { Standalone: true, Connect.IsSet: true })
        {
            throw new InvalidOperationException(
                "Cannot use both --standalone and --connect (mutually exclusive)");
        }

        // Determine mode
        bool isClientMode = settings.Connect?.IsSet == true;
        LiveMode mode = isClientMode ? LiveMode.Client : LiveMode.Standalone;

        // Mode-specific validation
        List<DeviceConfig> enabledDevices = [];
        string? host = null;
        int? port = null;

        if (isClientMode)
        {
            (host, port) = LiveConnectionStringParser.Parse(settings.Connect!);
            Log.Information("Starting Live command in client mode");
            Log.Information("Connecting to Beast source: {Host}:{Port}", host, port);
        }
        else
        {
            Log.Information("Starting Live command in standalone mode");
            enabledDevices = config.Devices!.Where(d => d.Enabled).ToList();

            if (enabledDevices.Count == 0)
            {
                throw new InvalidOperationException(
                    "No enabled devices found in configuration");
            }

            Log.Information("Device stream created. Devices={DeviceCount}", enabledDevices.Count);
        }

        // Log tracking config (both modes)
        Log.Information("Tracking config: ConfidenceLevel={Level}, IcaoTimeout={Timeout}s",
            config.Tracking!.ConfidenceLevel, config.Tracking.IcaoTimeoutSeconds);

        // Log receiver location if configured (both modes)
        LogReceiverLocation(config.Receiver);

        return new LiveValidatedConfig
        {
            Config = config,
            Mode = mode,
            EnabledDevices = enabledDevices,
            Host = host,
            Port = port,
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
