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
                var worker = new DeviceWorker(deviceConfig);

                try
                {
                    worker.OpenDevice();
                    worker.StartReceiving(cancellationToken);
                    deviceWorkers.Add(worker);

                    Log.Information("Started SDR device worker: {DeviceName}", deviceConfig.Name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to start device worker: {DeviceName}", deviceConfig.Name);
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
    /// Checks daemon-specific preconditions (business logic validation).
    /// Verifies that the daemon can operate with the loaded configuration.
    /// This is separate from technical config validation (format, syntax) done by ConfigurationBuilder.
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

        // Validate each enabled device configuration
        foreach (DeviceConfig device in config.Devices!.Where(d => d.Enabled))
        {
            // Validate center frequency range
            // RTL-SDR devices typically support 24-1766 MHz (R820T/R820T2 tuners)
            if (device.CenterFrequency <= 0)
            {
                throw new InvalidOperationException(
                    $"Cannot start daemon: Device '{device.Name}' has invalid centerFrequency: {device.CenterFrequency} MHz (must be > 0)");
            }

            if (device.CenterFrequency < 24)
            {
                Log.Warning(
                    "Device {DeviceName}: centerFrequency {Frequency} MHz is below typical minimum (24 MHz)",
                    device.Name, device.CenterFrequency);
            }

            if (device.CenterFrequency > 1766)
            {
                throw new InvalidOperationException(
                    $"Cannot start daemon: Device '{device.Name}' has invalid centerFrequency: {device.CenterFrequency} MHz (must be <= 1766 MHz for R820T/R820T2 tuners)");
            }

            // Validate sample rate range
            // RTL-SDR maximum is ~3.2 MSPS, but practical maximum is 2.4 MSPS
            if (device.SampleRate <= 0)
            {
                throw new InvalidOperationException(
                    $"Cannot start daemon: Device '{device.Name}' has invalid sampleRate: {device.SampleRate} MHz (must be > 0)");
            }

            if (device.SampleRate > 3.2)
            {
                throw new InvalidOperationException(
                    $"Cannot start daemon: Device '{device.Name}' has invalid sampleRate: {device.SampleRate} MHz (maximum is 3.2 MHz)");
            }

            if (device.SampleRate > 2.4)
            {
                Log.Warning(
                    "Device {DeviceName}: sampleRate {SampleRate} MHz exceeds recommended maximum (2.4 MHz) - sample drops may occur",
                    device.Name, device.SampleRate);
            }

            // Warn about unusual tuner gain values
            if (device.TunerGain < 0 || device.TunerGain > 50)
            {
                Log.Warning(
                    "Device {DeviceName}: tunerGain {Gain} dB is outside typical range (0-50 dB)",
                    device.Name, device.TunerGain);
            }

            // Note: GainMode enum validation happens during YAML deserialization (technical validation)
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
        Log.Debug("Daemon preconditions check passed");
    }
}
