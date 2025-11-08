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

using Aeromux.CLI.Configuration;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Sdr;
using Serilog;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Settings for the daemon command, capturing command-line options.
/// Inherits global --config option from GlobalSettings.
/// </summary>
public class DaemonSettings : GlobalSettings
{
    // No additional settings for daemon command yet
    // Global --config option is inherited from GlobalSettings
}

/// <summary>
/// Main daemon command for running Aeromux as a continuous service.
/// Manages SDR device workers, receives IQ samples, and will eventually demodulate/decode Mode S messages.
/// </summary>
/// <remarks>
/// Phase 1 (Complete): Opens RTL-SDR devices and receives IQ samples.
/// Future phases: Demodulation, decoding, TCP broadcasting, HTTP API.
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
    public override async Task<int> ExecuteAsync(CommandContext context, DaemonSettings settings, CancellationToken cancellationToken)
    {
        // Validate settings parameter (required by CA1062)
        ArgumentNullException.ThrowIfNull(settings);

        // Log session separator for easy identification of new instances in log files
        Log.Information("========================================");
        Log.Information("Aeromux Daemon Starting");
        Log.Information("Session: {SessionStart:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("========================================");

        Console.WriteLine("Aeromux daemon starting...");

        try
        {
            // Get configuration loaded by ConfigurationInterceptor
            AeromuxConfig config = ConfigurationProvider.Current;

            // Check daemon-specific preconditions (business logic validation)
            CheckDaemonPreconditions(config);

            Log.Information("Starting SDR device workers");

            // Initialize and start SDR device workers for all enabled devices
            var deviceWorkers = new List<DeviceWorker>();

            foreach (DeviceConfig deviceConfig in config.Devices!.Where(d => d.Enabled))
            {
                var worker = new DeviceWorker(deviceConfig, config.Tracking!, config.Receiver);

                try
                {
                    worker.OpenDevice();
                    worker.StartReceiving(cancellationToken);
                    deviceWorkers.Add(worker);

                    Log.Information("Started SDR device worker: '{DeviceName}' (index: {DeviceIndex})",
                        deviceConfig.Name, deviceConfig.DeviceIndex);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to start device worker: '{DeviceName}' (index: {DeviceIndex})",
                        deviceConfig.Name, deviceConfig.DeviceIndex);
                    // Clean up partially initialized worker before re-throwing
                    worker.Dispose();
                    throw;
                }
            }

            Log.Information("All SDR device workers started. Count={DeviceCount}", deviceWorkers.Count);

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
                Console.WriteLine($"Aeromux daemon running with {deviceWorkers.Count} device(s). Press Ctrl+C to stop.");
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

                // Cleanup on shutdown
                Console.WriteLine();
                Console.WriteLine("Shutting down device workers...");
                Log.Information("Shutting down device workers...");

                foreach (DeviceWorker worker in deviceWorkers)
                {
                    worker.Dispose();
                }

                Console.WriteLine("All device workers stopped.");
                Log.Information("All device workers stopped");

                // Log session end separator
                Log.Information("========================================");
                Log.Information("Aeromux Daemon Stopped");
                Log.Information("Session End: {SessionEnd:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
                Log.Information("========================================");
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
    /// Checks daemon-specific preconditions (high-level business logic validation).
    /// Verifies that the daemon can operate with the loaded configuration.
    /// Device-specific validation (frequencies, gains, etc.) is done in DeviceWorker.OpenDevice().
    /// </summary>
    /// <param name="config">The configuration to check.</param>
    /// <exception cref="InvalidOperationException">Thrown when daemon preconditions are not met.</exception>
    private static void CheckDaemonPreconditions(AeromuxConfig config)
    {
        // Check SDR devices - at least one device must be enabled to run daemon
        if (config.Devices?.Any(d => d.Enabled) != true)
        {
            throw new InvalidOperationException(
                "Cannot start daemon: At least one SDR device must be enabled in configuration");
        }

        // Check network ports - Beast port must be in valid range
        // Ports below 1024 require root/admin privileges, and 65535 is the maximum port number
        if (config.Network?.BeastPort is < 1024 or > 65535)
        {
            throw new InvalidOperationException(
                $"Cannot start daemon: Beast port must be between 1024 and 65535, but was {config.Network?.BeastPort}");
        }

        // TODO: Phase 6+ - Add validation for SBS port (30003) and HTTP port (8080)
        // Validate range 1024-65535, check no port conflicts between services

        // Validate receiver location (optional, but validate if configured)
        Log.Debug("Checking receiver configuration: IsNull={IsNull}", config.Receiver == null);
        if (config.Receiver != null)
        {
            Log.Debug("Receiver config present: Lat={Lat}, Lon={Lon}, Alt={Alt}, Name={Name}",
                config.Receiver.Latitude, config.Receiver.Longitude,
                config.Receiver.Altitude, config.Receiver.Name);
        }

        if (config.Receiver != null)
        {
            if (config.Receiver.Latitude.HasValue)
            {
                if (config.Receiver.Latitude < -90 || config.Receiver.Latitude > 90)
                {
                    throw new InvalidOperationException(
                        $"Cannot start daemon: Receiver latitude must be between -90 and +90 degrees, but was {config.Receiver.Latitude}");
                }
            }

            if (config.Receiver.Longitude.HasValue)
            {
                if (config.Receiver.Longitude < -180 || config.Receiver.Longitude > 180)
                {
                    throw new InvalidOperationException(
                        $"Cannot start daemon: Receiver longitude must be between -180 and +180 degrees, but was {config.Receiver.Longitude}");
                }
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
