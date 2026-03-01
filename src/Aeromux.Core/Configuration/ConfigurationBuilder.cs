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

using Aeromux.Core.Configuration.Enums;
using RtlSdrManager.Modes;
using Serilog.Events;

namespace Aeromux.Core.Configuration;

/// <summary>
/// Builds application configuration using a hierarchy: Defaults → YAML → Command-line overrides.
/// This ensures command-line arguments have the highest priority, followed by YAML, then built-in defaults.
/// Encapsulates all configuration loading and merging logic.
/// </summary>
public class ConfigurationBuilder
{
    /// <summary>
    /// Builds configuration from settings using the hierarchy: Defaults → YAML → CLI overrides.
    /// This is the main entry point for configuration building.
    /// </summary>
    /// <param name="settings">Global settings containing config path and CLI overrides.</param>
    /// <param name="yamlLoader">The YAML configuration loader (injected for testability).</param>
    /// <returns>The fully built and merged configuration.</returns>
    /// <exception cref="FileNotFoundException">Thrown when user specifies --config but file doesn't exist.</exception>
    public AeromuxConfig BuildFromSettings(IGlobalSettings settings, IYamlConfigurationLoader yamlLoader)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(yamlLoader);

        // 1. Start with built-in defaults
        AeromuxConfig config = GetDefaults();

        // 2. Determine config file path
        string configPath = settings.ConfigPath ?? "aeromux.yaml";
        bool isDefaultPath = settings.ConfigPath == null;

        // 3. Try to load and merge YAML file
        if (File.Exists(configPath))
        {
            try
            {
                AeromuxConfig yamlConfig = yamlLoader.LoadFromFile(configPath);
                config = MergeYamlIntoConfig(config, yamlConfig);
            }
            catch (Exception ex)
            {
                // Log will happen in interceptor - we just throw here
                throw new InvalidOperationException($"Failed to load configuration from {configPath}", ex);
            }
        }
        else if (!isDefaultPath)
        {
            // User explicitly specified --config but file doesn't exist
            throw new FileNotFoundException($"Configuration file not found: {configPath}", configPath);
        }
        // else: aeromux.yaml doesn't exist - that's fine, use defaults

        // 4. Apply command-line overrides (highest priority)
        config = ApplyCommandLineOverrides(config, settings);

        return config;
    }

    /// <summary>
    /// Gets the default configuration values.
    /// These are used when no YAML file exists or values are not specified.
    /// </summary>
    private static AeromuxConfig GetDefaults()
    {
        return new AeromuxConfig
        {
            Logging = new LoggingConfig
            {
                Level = LogEventLevel.Information,
                Console = new ConsoleLoggingConfig
                {
                    Enabled = false,
                    Colored = false
                },
                File = new FileLoggingConfig
                {
                    Enabled = true,
                    Path = "logs/aeromux-.log",
                    RollingInterval = RollingInterval.Day,
                    RetainedFileCount = 7,
                    FileSizeLimitMb = 100
                }
            },
            Devices =
            [
                new DeviceConfig
                {
                    Name = "default",
                    DeviceIndex = 0,
                    TunerGain = 49.6,
                    GainMode = TunerGainModes.Manual,
                    PpmCorrection = 0,
                    Enabled = true
                }
            ],
            Network = new NetworkConfig
            {
                BeastPort = 30005,
                JsonPort = 30006,
                SbsPort = 30003,
                HttpPort = 8080,
                BindAddress = System.Net.IPAddress.Any,
                BeastOutputEnabled = true,
                JsonOutputEnabled = false,
                SbsOutputEnabled = false
            },
            Tracking = new TrackingConfig
            {
                AircraftTimeoutSeconds = 60,
                MaxHistorySize = 1000
            },
            Mlat = new MlatConfig
            {
                Enabled = true,
                InputPort = 30104
            },
            Database = new DatabaseConfig
            {
                Enabled = false,
                Path = null
            }
        };
    }

    /// <summary>
    /// Merges YAML configuration into the base configuration.
    /// YAML values override defaults, but CLI args (applied later) will override YAML.
    /// If a section is not specified in YAML (null), the base config section is preserved.
    /// </summary>
    private static AeromuxConfig MergeYamlIntoConfig(AeromuxConfig baseConfig, AeromuxConfig yamlConfig)
    {
        return new AeromuxConfig
        {
            Logging = yamlConfig.Logging ?? baseConfig.Logging,
            Devices = yamlConfig.Devices ?? baseConfig.Devices,
            Network = yamlConfig.Network ?? baseConfig.Network,
            Tracking = yamlConfig.Tracking ?? baseConfig.Tracking,
            Receiver = yamlConfig.Receiver ?? baseConfig.Receiver,
            Mlat = yamlConfig.Mlat ?? baseConfig.Mlat,
            Database = yamlConfig.Database ?? baseConfig.Database
        };
    }

    /// <summary>
    /// Applies command-line argument overrides to the configuration.
    /// CLI args have the highest priority and override both defaults and YAML.
    /// At this point, all config sections are guaranteed to be non-null from merge step.
    /// </summary>
    private static AeromuxConfig ApplyCommandLineOverrides(AeromuxConfig config, IGlobalSettings settings)
    {
        // Apply CLI overrides (highest priority)
        // All sections are non-null after merge, so use null-forgiving operator

        // Note: Network port overrides (--beast-port, --json-port, --sbs-port, --bind-address)
        // are daemon-specific and handled directly in DaemonCommand.ExecuteAsync()
        // using ValidatePort() and ValidateBindAddress() methods.

        if (settings.LogLevel.HasValue)
        {
            config.Logging!.Level = settings.LogLevel.Value;
        }

        if (settings.DatabasePath != null)
        {
            config.Database!.Path = settings.DatabasePath;
            config.Database.Enabled = true;
        }

        return config;
    }
}
