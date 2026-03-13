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
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Validates and resolves daemon configuration from CLI parameters, YAML config, and defaults.
/// Performs both value-level validation (port ranges, IP addresses) and business logic validation
/// (enabled devices, privileged ports, receiver location).
/// </summary>
public static class DaemonConfigValidator
{
    /// <summary>
    /// Validates all daemon configuration and returns an immutable validated config record.
    /// </summary>
    /// <param name="settings">CLI command settings.</param>
    /// <param name="config">Loaded Aeromux configuration.</param>
    /// <returns>Validated and resolved configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    public static DaemonValidatedConfig Validate(DaemonSettings settings, AeromuxConfig config)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(config);

        Log.Information("Starting device stream for all enabled devices");

        // Validate and resolve network configuration (priority: CLI > YAML > Default)
        int beastPort = ValidatePort(settings.BeastPort, config.Network!.BeastPort, "BeastPort");
        int jsonPort = ValidatePort(settings.JsonPort, config.Network.JsonPort, "JsonPort");
        int sbsPort = ValidatePort(settings.SbsPort, config.Network.SbsPort, "SbsPort");
        int apiPort = ValidatePort(settings.ApiPort, config.Network.ApiPort, "ApiPort");
        bool apiEnabled = ValidateOutputEnabled(settings.ApiEnabled, config.Network.ApiEnabled, "REST API");
        IPAddress bindAddress = ValidateBindAddress(settings.BindAddress, config.Network.BindAddress);
        Guid? receiverUuid = ValidateReceiverUuid(settings.ReceiverUuid, config.Receiver?.ReceiverUuid);
        var mlatConfig = MlatConfig.Validate(settings.MlatEnabled, settings.MlatInputPort, config.Mlat);

        // Validate and resolve output enabled flags (priority: CLI > YAML > Default)
        bool beastEnabled = ValidateOutputEnabled(
            settings.BeastOutputEnabled, config.Network.BeastOutputEnabled, "Beast");
        bool jsonEnabled = ValidateOutputEnabled(
            settings.JsonOutputEnabled, config.Network.JsonOutputEnabled, "JSON");
        bool sbsEnabled = ValidateOutputEnabled(
            settings.SbsOutputEnabled, config.Network.SbsOutputEnabled, "SBS");

        Log.Information(
            "Network configuration: Beast={BeastPort} ({BeastStatus}), JSON={JsonPort} ({JsonStatus}), SBS={SbsPort} ({SbsStatus}), API={ApiPort} ({ApiStatus}), Bind={BindAddress}, MLAT Input={MlatPort} ({MlatStatus})",
            beastPort, beastEnabled ? "enabled" : "disabled",
            jsonPort, jsonEnabled ? "enabled" : "disabled",
            sbsPort, sbsEnabled ? "enabled" : "disabled",
            apiPort, apiEnabled ? "enabled" : "disabled",
            bindAddress,
            mlatConfig.InputPort, mlatConfig.Enabled ? "enabled" : "disabled");

        // Check daemon-specific preconditions (business logic validation)
        CheckDaemonPreconditions(config, beastEnabled, jsonEnabled, sbsEnabled);

        var enabledDevices = config.Devices!.Where(d => d.Enabled).ToList();

        return new DaemonValidatedConfig
        {
            Config = config,
            BeastPort = beastPort,
            JsonPort = jsonPort,
            SbsPort = sbsPort,
            ApiPort = apiPort,
            ApiEnabled = apiEnabled,
            BindAddress = bindAddress,
            ReceiverUuid = receiverUuid,
            MlatConfig = mlatConfig,
            BeastEnabled = beastEnabled,
            JsonEnabled = jsonEnabled,
            SbsEnabled = sbsEnabled,
            EnabledDevices = enabledDevices
        };
    }

    /// <summary>
    /// Validates and resolves port number from CLI parameter or config value.
    /// Priority order: CLI parameter > YAML config > Default value.
    ///
    /// TWO-TIER VALIDATION:
    /// This method validates 1-65535 (full TCP port range).
    /// CheckDaemonPreconditions enforces 1024-65535 (non-privileged ports).
    /// This allows flexibility while preventing accidental privileged port usage.
    /// </summary>
    /// <param name="cliPort">Optional port from CLI parameter.</param>
    /// <param name="configPort">Port from configuration file.</param>
    /// <param name="portName">Name of the port for error messages.</param>
    /// <returns>Validated port number.</returns>
    /// <exception cref="InvalidOperationException">Thrown when port is out of valid range.</exception>
    private static int ValidatePort(int? cliPort, int configPort, string portName)
    {
        int port = cliPort ?? configPort;

        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"{portName} must be between 1 and 65535 (got {port})");
        }

        return port;
    }

    /// <summary>
    /// Validates and resolves bind address from CLI parameter or config value.
    /// Priority order: CLI parameter > YAML config > Default value.
    ///
    /// BIND ADDRESS SEMANTICS:
    /// - IPAddress.Any (0.0.0.0): Binds to all network interfaces (accessible remotely)
    /// - IPAddress.Loopback (127.0.0.1): Binds to localhost only (local access only)
    /// - Specific IP (e.g., 192.168.1.100): Binds to specific network interface
    /// CLI accepts string format, config uses IPAddress type for type safety.
    /// </summary>
    /// <param name="cliBindAddress">Optional bind address from CLI parameter (string format).</param>
    /// <param name="configBindAddress">Bind address from configuration file (IPAddress type).</param>
    /// <returns>Validated IPAddress instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when bind address is invalid.</exception>
    private static IPAddress ValidateBindAddress(string? cliBindAddress, IPAddress configBindAddress)
    {
        // If CLI provided, parse and validate
        if (!string.IsNullOrEmpty(cliBindAddress))
        {
            if (!IPAddress.TryParse(cliBindAddress, out IPAddress? parsed))
            {
                throw new InvalidOperationException(
                    $"BindAddress '{cliBindAddress}' is not a valid IP address. " +
                    $"Examples: 0.0.0.0 (all interfaces), 127.0.0.1 (localhost), 192.168.1.100 (specific interface)");
            }

            return parsed;
        }

        // Use config value (already IPAddress from YAML deserialization)
        return configBindAddress;
    }

    /// <summary>
    /// Validates and resolves receiver UUID from CLI parameter or config value.
    /// Priority order: CLI parameter > YAML config.
    ///
    /// UUID VALIDATION:
    /// - Must be RFC 4122 compliant format (8-4-4-4-12 hex digits)
    /// - Examples: "550e8400-e29b-41d4-a716-446655440000"
    /// - Used for MLAT triangulation and receiver identification
    /// - Must be unique per receiver (shared UUIDs corrupt MLAT timing)
    /// </summary>
    /// <param name="cliReceiverUuid">Optional receiver UUID from CLI parameter (string format).</param>
    /// <param name="configReceiverUuid">Receiver UUID from configuration file (Guid? type).</param>
    /// <returns>Validated Guid instance, or null if not provided.</returns>
    /// <exception cref="InvalidOperationException">Thrown when UUID format is invalid.</exception>
    private static Guid? ValidateReceiverUuid(string? cliReceiverUuid, Guid? configReceiverUuid)
    {
        // If CLI provided, parse and validate
        if (!string.IsNullOrEmpty(cliReceiverUuid))
        {
            if (!Guid.TryParse(cliReceiverUuid, out Guid parsed))
            {
                throw new InvalidOperationException(
                    $"ReceiverUuid '{cliReceiverUuid}' is not a valid RFC 4122 UUID format. " +
                    $"Generate with: uuidgen (macOS/Linux), [guid]::NewGuid() (PowerShell), or https://www.uuidgenerator.net/");
            }

            return parsed;
        }

        // Use config value (already Guid? from YAML deserialization)
        return configReceiverUuid;
    }

    /// <summary>
    /// Validates and resolves output enabled flag from CLI parameter or config value.
    /// Priority order: CLI parameter > YAML config > Default value.
    ///
    /// ENABLE/DISABLE SEMANTICS:
    /// - true: Broadcaster is created, started, and listens on configured port
    /// - false: Broadcaster is NOT created, port is not listened on, clients cannot connect
    /// - Used to selectively enable output formats based on deployment requirements
    /// </summary>
    /// <param name="cliEnabled">Optional enabled flag from CLI parameter.</param>
    /// <param name="configEnabled">Enabled flag from configuration file.</param>
    /// <param name="formatName">Name of the format for logging (e.g., "Beast", "JSON").</param>
    /// <returns>Validated enabled flag.</returns>
    private static bool ValidateOutputEnabled(bool? cliEnabled, bool configEnabled, string formatName)
    {
        bool enabled = cliEnabled ?? configEnabled;
        Log.Debug("{Format} output {Status}", formatName, enabled ? "enabled" : "disabled");
        return enabled;
    }

    /// <summary>
    /// Checks daemon-specific preconditions (high-level business logic validation).
    /// Verifies that the daemon can operate with the loaded configuration.
    /// Device-specific validation (frequencies, gains, etc.) is done in DeviceWorker.OpenDevice().
    ///
    /// VALIDATION STRATEGY:
    /// - Devices: At least one must be enabled
    /// - Ports: Must be 1024-65535 (non-privileged, OS will detect conflicts on bind)
    /// - Only validates ports for ENABLED output formats
    /// - Receiver location: Optional, but if provided, lat/lon must both be specified
    /// Port conflict detection is deferred to OS (bind will fail if port is in use).
    /// </summary>
    /// <param name="config">The configuration to check.</param>
    /// <param name="beastEnabled">Whether Beast output is enabled.</param>
    /// <param name="jsonEnabled">Whether JSON output is enabled.</param>
    /// <param name="sbsEnabled">Whether SBS output is enabled.</param>
    /// <exception cref="InvalidOperationException">Thrown when daemon preconditions are not met.</exception>
    private static void CheckDaemonPreconditions(
        AeromuxConfig config,
        bool beastEnabled,
        bool jsonEnabled,
        bool sbsEnabled)
    {
        // Check SDR devices - at least one device must be enabled to run daemon
        if (config.Devices?.Any(d => d.Enabled) != true)
        {
            throw new InvalidOperationException(
                "Cannot start daemon: At least one SDR device must be enabled in configuration");
        }

        // Check network ports - Only validate ports for ENABLED outputs
        // Ports below 1024 require root/admin privileges, and 65535 is the maximum port number
        // Note: Port conflict validation is deferred to the OS (bind will fail if port is in use)
        if (beastEnabled && config.Network?.BeastPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"Cannot start daemon: Beast port must be between 1024 and 65535, but was {config.Network?.BeastPort}");
        }

        if (jsonEnabled && config.Network?.JsonPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"Cannot start daemon: JSON port must be between 1024 and 65535, but was {config.Network?.JsonPort}");
        }

        if (sbsEnabled && config.Network?.SbsPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"Cannot start daemon: SBS port must be between 1024 and 65535, but was {config.Network?.SbsPort}");
        }

        // ApiPort always validated (used by REST API)
        if (config.Network?.ApiPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"Cannot start daemon: API port must be between 1024 and 65535, but was {config.Network?.ApiPort}");
        }

        // Validate receiver location (optional, but validate if configured)
        if (config.Receiver != null)
        {
            if (config.Receiver.Latitude is < -90 or > 90)
            {
                throw new InvalidOperationException(
                    $"Cannot start daemon: Receiver latitude must be between -90 and +90 degrees, but was {config.Receiver.Latitude}");
            }

            if (config.Receiver.Longitude is < -180 or > 180)
            {
                throw new InvalidOperationException(
                    $"Cannot start daemon: Receiver longitude must be between -180 and +180 degrees, but was {config.Receiver.Longitude}");
            }

            // Both lat/lon must be provided together
            if (config.Receiver.Latitude.HasValue != config.Receiver.Longitude.HasValue)
            {
                throw new InvalidOperationException(
                    "Cannot start daemon: Receiver latitude and longitude must both be provided or both omitted");
            }

            // Log if configured
            if (config.Receiver.Latitude.HasValue && config.Receiver.Longitude.HasValue)
            {
                Log.Information("Receiver location configured: {Lat:F4}° {LatDir}, {Lon:F4}° {LonDir}",
                    Math.Abs(config.Receiver.Latitude.Value),
                    config.Receiver.Latitude.Value >= 0 ? "N" : "S",
                    Math.Abs(config.Receiver.Longitude.Value),
                    config.Receiver.Longitude.Value >= 0 ? "E" : "W");
            }

            // Log receiver UUID if configured
            // UUID identifies this receiver for MLAT triangulation and frame deduplication
            // Must be unique per receiver - shared UUIDs corrupt MLAT timing correlation
            if (config.Receiver.ReceiverUuid.HasValue)
            {
                Log.Information("Receiver UUID configured: {ReceiverUuid}", config.Receiver.ReceiverUuid.Value);
            }
        }
        else
        {
            Log.Warning("Receiver location not configured - TC 5-8 surface position decoding will be disabled");
        }

        // Note: Device-specific validation (centerFrequency, sampleRate, tunerGain, etc.)
        // is performed in DeviceWorker.OpenDevice() where the values are actually used.
        // This ensures single source of truth and proper error messages with device names.

        Log.Debug("Daemon preconditions check passed");
    }
}
