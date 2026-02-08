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

using Aeromux.CLI.Commands.Live;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Streaming;
using Serilog;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Live aircraft display command.
/// Supports standalone mode (direct RTL-SDR) and client mode (connect to Beast source).
/// Uses AircraftStateTracker for state management.
/// Client mode works with any Beast-compatible source (readsb, dump1090, aeromux daemon).
/// </summary>
public sealed class LiveCommand : AsyncCommand<LiveSettings>
{
    /// <summary>
    /// Executes the live aircraft display command in standalone or client mode.
    /// </summary>
    /// <param name="context">The command context from Spectre.Console CLI.</param>
    /// <param name="settings">Command settings including mode selection and connection parameters.</param>
    /// <param name="cancellationToken">Cancellation token to stop the TUI display.</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        LiveSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Log session separator for easy identification of new instances in log files
        LiveSessionReporter.LogSessionStart(settings.Connect?.IsSet == true);

        // Track session start time for summary statistics
        DateTime sessionStart = DateTime.UtcNow;

        try
        {
            // Validate and resolve all configuration upfront
            AeromuxConfig config = ConfigurationProvider.Current;
            LiveValidatedConfig validatedConfig = LiveConfigValidator.Validate(settings, config);

            int exitCode = validatedConfig.Mode == LiveMode.Client
                ? await RunClientModeAsync(validatedConfig, settings, sessionStart, cancellationToken)
                : await RunStandaloneModeAsync(validatedConfig, settings, sessionStart, cancellationToken);

            // Log session end separator
            LiveSessionReporter.LogSessionEnd(sessionStart);

            return exitCode;
        }
        catch (Exception ex)
        {
            return LiveExceptionHandler.HandleException(ex);
        }
    }

    /// <summary>
    /// Runs in standalone mode: Direct RTL-SDR access with local signal processing.
    /// </summary>
    /// <param name="validatedConfig">Validated configuration with enabled devices.</param>
    /// <param name="settings">Command settings for TUI display options.</param>
    /// <param name="sessionStart">Session start time for summary statistics.</param>
    /// <param name="ct">Cancellation token to stop device processing.</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    /// <remarks>
    /// Creates ReceiverStream for direct RTL-SDR access. Provides helpful error messages
    /// suggesting connection to daemon if device is busy.
    /// </remarks>
    private async Task<int> RunStandaloneModeAsync(
        LiveValidatedConfig validatedConfig,
        LiveSettings settings,
        DateTime sessionStart,
        CancellationToken ct)
    {
        Console.WriteLine("Aeromux live starting in standalone mode...");
        Console.WriteLine($"Aeromux live running with {validatedConfig.EnabledDevices.Count} device(s). Press ESC or Q to stop.");
        Console.WriteLine();

        try
        {
            // ReceiverStream implements IAsyncDisposable to ensure RTL-SDR device cleanup
            // Async using ensures StopAsync is called even on exceptions, releasing hardware
            await using var stream = new ReceiverStream(
                validatedConfig.EnabledDevices,
                validatedConfig.Tracking,
                validatedConfig.Receiver);

            await stream.StartAsync(ct);
            Log.Information("Device stream started");

            // Standalone mode: Pass receiver config for distance calculation
            var display = new LiveTuiDisplay();
            return await display.RunAsync(stream, settings, validatedConfig.Receiver, sessionStart, ct);
        }
        catch (Exception ex)
        {
            return LiveExceptionHandler.HandleStandaloneException(ex);
        }
    }

    /// <summary>
    /// Runs in client mode: Connect to Beast-compatible TCP source.
    /// </summary>
    /// <param name="validatedConfig">Validated configuration with host and port.</param>
    /// <param name="settings">Command settings for TUI display options.</param>
    /// <param name="sessionStart">Session start time for summary statistics.</param>
    /// <param name="ct">Cancellation token to stop the connection.</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    /// <remarks>
    /// Connects to any Beast-compatible server (readsb, dump1090, dump1090-fa, aeromux daemon).
    /// BeastStream includes IcaoConfidenceTracker to filter noise from real aircraft.
    /// </remarks>
    private async Task<int> RunClientModeAsync(
        LiveValidatedConfig validatedConfig,
        LiveSettings settings,
        DateTime sessionStart,
        CancellationToken ct)
    {
        string host = validatedConfig.Host!;
        int port = validatedConfig.Port!.Value;

        Console.WriteLine("Aeromux live starting in client mode...");
        Console.WriteLine($"Connecting to {host}:{port}...");
        Console.WriteLine("Aeromux live running, connected to a Beast daemon. Press ESC or Q to stop.");
        Console.WriteLine();

        try
        {
            // BeastStream includes IcaoConfidenceTracker to filter noise from real aircraft
            // TrackingConfig.ConfidenceLevel determines how many messages required to accept ICAO
            // This prevents displaying spurious aircraft from corrupted frames or interference
            await using var stream = new BeastStream(host, port, validatedConfig.Tracking);
            await stream.StartAsync(ct);

            Log.Information("Connected to Beast source: {Host}:{Port}", host, port);

            // Load receiver config if available (might be null in client mode)
            // If configured, allows distance calculation even when connected to remote source
            var display = new LiveTuiDisplay();
            return await display.RunAsync(stream, settings, validatedConfig.Receiver, sessionStart, ct);
        }
        catch (Exception ex)
        {
            return LiveExceptionHandler.HandleClientException(ex, host, port);
        }
    }
}
