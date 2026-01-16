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

using System.ComponentModel;
using System.Net;
using Aeromux.CLI.Configuration;
using Aeromux.Core.Configuration;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Aeromux.Infrastructure.Network;
using Aeromux.Infrastructure.Network.Enums;
using RtlSdrManager.Exceptions;
using Serilog;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Settings for the daemon command, capturing command-line options.
/// Inherits global --config option from GlobalSettings.
/// </summary>
public class DaemonSettings : GlobalSettings
{
    [CommandOption("--beast-port")]
    [Description("Beast protocol port (default: 30005, dump1090-compatible)")]
    public int? BeastPort { get; set; }

    [CommandOption("--json-port")]
    [Description("JSON streaming port (default: 30006, web-friendly)")]
    public int? JsonPort { get; set; }

    [CommandOption("--sbs-port")]
    [Description("SBS protocol port (default: 30003, VRS-compatible)")]
    public int? SbsPort { get; set; }

    [CommandOption("--beast-output-enabled")]
    [Description("Enable Beast binary protocol output (default: true)")]
    public bool? BeastOutputEnabled { get; set; }

    [CommandOption("--json-output-enabled")]
    [Description("Enable JSON streaming output (default: false)")]
    public bool? JsonOutputEnabled { get; set; }

    [CommandOption("--sbs-output-enabled")]
    [Description("Enable SBS BaseStation protocol output (default: false)")]
    public bool? SbsOutputEnabled { get; set; }

    [CommandOption("--bind-address")]
    [Description(
        "IP address to bind to (default: 0.0.0.0 for all interfaces)")]
    public string? BindAddress { get; set; } // CLI uses string, parsed to IPAddress in validation

    [CommandOption("--mlat-enabled")]
    [Description("Enable MLAT input from mlat-client (default: true)")]
    public bool? MlatEnabled { get; set; }

    [CommandOption("--mlat-input-port")]
    [Description("MLAT Beast input port (default: 30104)")]
    public int? MlatInputPort { get; set; }

    [CommandOption("--receiver-uuid")]
    [Description("Receiver UUID for MLAT triangulation (format: RFC 4122)")]
    public string? ReceiverUuid { get; set; }
}

/// <summary>
/// Main daemon command for running Aeromux as a continuous service.
/// Manages RTL-SDR devices, demodulates Mode S signals, decodes ADS-B messages,
/// and broadcasts data to multiple clients via TCP (Beast/JSON/SBS formats).
/// </summary>
/// <remarks>
/// Device management, demodulation, decoding, ICAO confidence tracking.
/// TCP broadcasting (Beast/JSON/SBS), multi-device support, network configuration.
/// Aircraft state tracking infrastructure.
/// </remarks>
public class DaemonCommand : AsyncCommand<DaemonSettings>
{
    /// <summary>
    /// Executes the daemon command to start the Aeromux service.
    /// Configuration is already loaded by ConfigurationInterceptor and available via ConfigurationProvider.Current.
    /// </summary>
    /// <param name="context">The command context from Spectre.Console.Cli.</param>
    /// <param name="settings">Command settings (unused - only for Spectre.Console.Cli framework).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, DaemonSettings settings,
        CancellationToken cancellationToken)
    {
        // Validate settings parameter (required by CA1062)
        ArgumentNullException.ThrowIfNull(settings);

        // Log session separator for easy identification of new instances in log files
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Aeromux Daemon Starting");
        Log.Information("Session: {SessionStart:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("═══════════════════════════════════════════════════════════════");

        Console.WriteLine("Aeromux daemon starting...");

        // Track session start time for summary statistics
        DateTime sessionStart = DateTime.UtcNow;

        try
        {
            // Get configuration loaded by ConfigurationInterceptor
            AeromuxConfig config = ConfigurationProvider.Current;

            Log.Information("Starting device stream for all enabled devices");

            // Validate and resolve network configuration (priority: CLI > YAML > Default)
            int beastPort = ValidatePort(settings.BeastPort, config.Network!.BeastPort, "BeastPort");
            int jsonPort = ValidatePort(settings.JsonPort, config.Network.JsonPort, "JsonPort");
            int sbsPort = ValidatePort(settings.SbsPort, config.Network.SbsPort, "SbsPort");
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
                "Network configuration: Beast={BeastPort} ({BeastStatus}), JSON={JsonPort} ({JsonStatus}), SBS={SbsPort} ({SbsStatus}), Bind={BindAddress}, MLAT Input={MlatPort} ({MlatStatus})",
                beastPort, beastEnabled ? "enabled" : "disabled",
                jsonPort, jsonEnabled ? "enabled" : "disabled",
                sbsPort, sbsEnabled ? "enabled" : "disabled",
                bindAddress,
                mlatConfig.InputPort, mlatConfig.Enabled ? "enabled" : "disabled");

            // Check daemon-specific preconditions (business logic validation)
            CheckDaemonPreconditions(config, beastEnabled, jsonEnabled, sbsEnabled);

            // Create ReceiverStream (uninitialized - devices not opened yet)
            var enabledDevices = config.Devices!.Where(d => d.Enabled).ToList();

            var receiverStream = new ReceiverStream(
                enabledDevices,
                config.Tracking!,
                config.Receiver,
                mlatConfig);

            Log.Information("Device stream created. Devices={DeviceCount}", enabledDevices.Count);

            // CRITICAL STARTUP ORDER:
            // Start ReceiverStream FIRST (opens RTL-SDR devices and begins internal broadcasting)
            // This MUST complete before TcpBroadcasters call Subscribe()
            // ReceiverStream.StartAsync() initializes the internal broadcaster task and makes Subscribe() available
            await receiverStream.StartAsync(cancellationToken);
            Log.Information("Device stream started");

            // Create centralized aircraft state tracker for all devices
            // Tracks aircraft across multiple RTL-SDR devices (automatic deduplication by ICAO)
            var aircraftTracker = new AircraftStateTracker(config.Tracking!);

            // Subscribe to aircraft lifecycle events for operational visibility
            // Logs new aircraft to track what's being received and help diagnose coverage issues
            aircraftTracker.OnAircraftAdded += (sender, e) =>
            {
                Aircraft aircraft = e.Aircraft;
                Log.Information("New aircraft: ICAO={Icao}, Callsign={Callsign}",
                    aircraft.Identification.ICAO,
                    aircraft.Identification.Callsign ?? "Unknown");
            };

            // Log significant updates (position, altitude, velocity changes)
            aircraftTracker.OnAircraftUpdated += (sender, e) =>
            {
                Aircraft prev = e.Previous;
                Aircraft curr = e.Updated;

                // Only log if position or velocity actually changed to reduce log noise
                // OnAircraftUpdated fires on EVERY frame, but we only care about significant state changes
                bool positionChanged = prev.Position.Coordinate != curr.Position.Coordinate ||
                                      prev.Position.BarometricAltitude != curr.Position.BarometricAltitude;
                bool velocityChanged = prev.Velocity.GroundSpeed != curr.Velocity.GroundSpeed ||
                                      prev.Velocity.Speed != curr.Velocity.Speed;

                if (positionChanged || velocityChanged)
                {
                    Log.Debug("Aircraft update: ICAO={Icao}, Position={Position}, Alt={Altitude}, Speed={Velocity}",
                        curr.Identification.ICAO,
                        curr.Position.Coordinate,
                        curr.Position.BarometricAltitude,
                        curr.Velocity.GroundSpeed ?? curr.Velocity.Speed);
                }
            };

            aircraftTracker.StartConsuming(receiverStream.Subscribe(), cancellationToken);
            Log.Information("Aircraft state tracker started");

            // Create and start TCP broadcasters conditionally based on enabled flags
            // Each TcpBroadcaster.StartAsync() will call Subscribe() on the device stream
            // This gives each broadcaster its own channel reader for independent consumption
            // Receiver UUID passed to Beast broadcaster enables MLAT identification (sent as 0xe3 message)
            //
            // IMPORTANT: Staggered startup with 50ms delays between broadcasters
            // This prevents a race condition in .NET's Socket.ValidateBlockingMode() on macOS ARM64
            // where concurrent socket initialization can corrupt internal state fields, causing AccessViolationException
            TcpBroadcaster? beastBroadcaster = null;
            TcpBroadcaster? jsonBroadcaster = null;
            TcpBroadcaster? sbsBroadcaster = null;

            if (beastEnabled)
            {
                beastBroadcaster = new TcpBroadcaster(
                    beastPort,
                    bindAddress,
                    receiverStream,
                    BroadcastFormat.Beast,
                    receiverUuid);
                await beastBroadcaster.StartAsync(cancellationToken);
                Log.Information("Beast broadcaster started on {BindAddress}:{Port}", bindAddress, beastPort);
                await Task.Delay(50, cancellationToken); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition (AccessViolationException)
            }

            if (jsonEnabled)
            {
                jsonBroadcaster = new TcpBroadcaster(
                    jsonPort,
                    bindAddress,
                    receiverStream,
                    BroadcastFormat.Json,
                    receiverUuid: null, // JSON doesn't use receiver UUID (Beast only)
                    aircraftTracker: aircraftTracker); // Required for JSON format
                await jsonBroadcaster.StartAsync(cancellationToken);
                Log.Information("JSON broadcaster started on {BindAddress}:{Port} (aircraft mode, 1s rate limit)", bindAddress, jsonPort);
                await Task.Delay(50, cancellationToken); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition (AccessViolationException)
            }

            if (sbsEnabled)
            {
                sbsBroadcaster = new TcpBroadcaster(
                    sbsPort,
                    bindAddress,
                    receiverStream,
                    BroadcastFormat.Sbs,
                    receiverUuid: null, // SBS doesn't use receiver UUID (Beast only)
                    aircraftTracker: aircraftTracker); // Required for SBS format
                await sbsBroadcaster.StartAsync(cancellationToken);
                Log.Information("SBS broadcaster started on {BindAddress}:{Port}", bindAddress, sbsPort);
                await Task.Delay(50, cancellationToken); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition (AccessViolationException)
            }

            int enabledCount = (beastEnabled ? 1 : 0) + (jsonEnabled ? 1 : 0) + (sbsEnabled ? 1 : 0);
            if (enabledCount == 0)
            {
                Log.Warning("All TCP output formats disabled - no broadcasters started");
            }
            Log.Information("TCP broadcasters started: {Count} format(s) enabled", enabledCount);

            // Create linked CTS to handle both interactive (CTRL+C) and service (SIGTERM) shutdown
            using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken shutdownToken = shutdownCts.Token; // Capture token, not CTS

            // Handle CTRL+C in interactive mode (when running in terminal)
            // This doesn't fire when running as a systemd service (no console)
            ConsoleCancelEventHandler? cancelHandler = null;
            if (!Console.IsInputRedirected) // Only set up if running interactively
            {
                // Capture the CTS in a local variable before creating the lambda
                // This avoids capturing the 'using' variable directly
                CancellationTokenSource localCts = shutdownCts;
                cancelHandler = (_, e) =>
                {
                    Log.Information("CTRL+C received - requesting shutdown");
                    e.Cancel = true; // Prevent immediate process termination
                    try
                    {
                        localCts.Cancel(); // Trigger graceful shutdown
                    }
                    catch (ObjectDisposedException)
                    {
                        // CTS already disposed - shutdown already in progress
                    }
                };
                Console.CancelKeyPress += cancelHandler;
            }

            try
            {
                Console.WriteLine(
                    $"Aeromux daemon running with {enabledDevices.Count} device(s). Press Ctrl+C to stop.");
                Log.Debug("Entering wait loop for cancellation");

                try
                {
                    // Wait for shutdown signal from either CTRL+C or systemctl stop (SIGTERM)
                    await Task.Delay(Timeout.Infinite, shutdownToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation - graceful shutdown
                    Log.Information("Shutdown signal received");
                }

                // GRACEFUL SHUTDOWN ORDER:
                // Dispose in correct order to ensure clean resource cleanup
                Console.WriteLine();
                Console.WriteLine("Shutting down TCP broadcasters, tracker, and device stream...");
                Log.Information("Shutting down TCP broadcasters, tracker, and device stream...");

                // Step 1: Stop TCP broadcasters first (null-safe disposal)
                // Each DisposeAsync waits for background tasks then disposes clients
                // This unsubscribes from device stream and stops consuming data
                if (beastBroadcaster != null)
                {
                    await beastBroadcaster.DisposeAsync();
                }

                if (jsonBroadcaster != null)
                {
                    await jsonBroadcaster.DisposeAsync();
                }

                if (sbsBroadcaster != null)
                {
                    await sbsBroadcaster.DisposeAsync();
                }

                // Step 2: Stop device stream
                // Closes RTL-SDR devices and completes internal broadcast channel
                // This will complete the trackerChannel, causing the tracker's consumer task to finish
                await receiverStream.DisposeAsync();

                // Step 3: Dispose aircraft tracker
                // Tracker.Dispose() waits for consumer task to complete, then disposes cleanup timer
                aircraftTracker.Dispose();
                Log.Information("Aircraft state tracker stopped");

                Console.WriteLine("All device workers and TCP broadcasters stopped.");
                Log.Information("All device workers and TCP broadcasters stopped");

                // Display session summary with aggregated statistics from all devices
                TimeSpan sessionDuration = DateTime.UtcNow - sessionStart;
                StreamStatistics? stats = receiverStream.GetStatistics();
                if (stats != null)
                {
                    Log.Information("═══════════════════════════════════════════════════════════════");
                    Log.Information("Aeromux Session Summary");
                    Log.Information("═══════════════════════════════════════════════════════════════");
                    Log.Information("Session duration: {Duration}", sessionDuration.ToString(@"hh\:mm\:ss"));
                    Log.Information("Total frames: {TotalFrames:N0}", stats.TotalFrames);
                    Log.Information("Valid frames: {ValidFrames:N0}", stats.ValidFrames);
                    Log.Information("Corrected frames: {CorrectedFrames:N0}", stats.CorrectedFrames);
                    Log.Information("Messages parsed: {ParsedMessages:N0}", stats.ParsedMessages);
                    Log.Information("MLAT frames: {MlatFrames:N0}", stats.MlatFrames);
                    Log.Information("═══════════════════════════════════════════════════════════════");
                }

                // Log session end separator
                Log.Information("═══════════════════════════════════════════════════════════════");
                Log.Information("Aeromux Daemon Stopped");
                Log.Information("Session End: {SessionEnd:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
                Log.Information("═══════════════════════════════════════════════════════════════");
            }
            finally
            {
                // Unregister CTRL+C handler if it was set up
                if (cancelHandler != null)
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
            }

            return 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("device") || ex.Message.Contains("port"))
        {
            // Daemon precondition checks failed
            Log.Error(ex, "Daemon preconditions not met");
            Console.WriteLine(ex.Message);
            return 1;
        }
        catch (RtlSdrLibraryExecutionException ex)
        {
            Log.Error(ex, "RTL-SDR device already in use");
            Console.WriteLine("Error: Cannot open RTL-SDR device (already in use)");
            Console.WriteLine("This usually means another instance is running.");
            Console.WriteLine("Try:");
            Console.WriteLine("  1. Connect to daemon: aeromux live --connect localhost:30005");
            Console.WriteLine("  2. Stop daemon: aeromux daemon stop");
            return 1;
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("RtlSdr") && ex.Message.Contains("not found"))
        {
            // RTL-SDR device not found
            Log.Error(ex, "RTL-SDR device not found");
            Console.WriteLine("Error: RTL-SDR device not found. Please check:");
            Console.WriteLine("  1. Device is connected via USB");
            Console.WriteLine("  2. Drivers are installed (librtlsdr)");
            Console.WriteLine("  3. Device index is correct in configuration");
            Console.WriteLine("  4. Run 'rtl_test' to verify device detection");
            return 1;
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("RtlSdr"))
        {
            // Other RTL-SDR errors
            Log.Error(ex, "RTL-SDR error");
            Console.WriteLine($"RTL-SDR Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            // Unexpected errors
            Log.Error(ex, "Failed to start daemon");
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
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

        // HttpPort always validated (not part of this feature)
        if (config.Network?.HttpPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"Cannot start daemon: HTTP port must be between 1024 and 65535, but was {config.Network?.HttpPort}");
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
