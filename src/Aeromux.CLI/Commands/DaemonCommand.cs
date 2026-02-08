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

using Aeromux.CLI.Commands.Daemon;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Streaming;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

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

        DaemonSessionSummaryReporter.LogSessionStart();
        Console.WriteLine("Aeromux daemon starting...");

        // Track session start time for summary statistics
        DateTime sessionStart = DateTime.UtcNow;

        try
        {
            // Get configuration loaded by ConfigurationInterceptor
            AeromuxConfig config = ConfigurationProvider.Current;

            // Validate and resolve all configuration
            DaemonValidatedConfig validatedConfig = DaemonConfigValidator.Validate(settings, config);

            // Create and start all daemon services (receiver stream, aircraft tracker, broadcasters)
            var orchestrator = new DaemonOrchestrator(validatedConfig);
            try
            {
                await orchestrator.StartAsync(cancellationToken);

                // Wait for shutdown signal (CTRL+C or SIGTERM)
                Console.WriteLine(
                    $"Aeromux daemon running with {orchestrator.DeviceCount} device(s). Press Ctrl+C to stop.");
                using var shutdown = new DaemonShutdownCoordinator(cancellationToken);
                await shutdown.WaitForShutdownAsync();

                // Collect statistics before disposal (stream still alive)
                StreamStatistics? stats = orchestrator.GetStatistics();

                // Ordered shutdown: broadcasters -> stream -> tracker
                await orchestrator.DisposeAsync();

                // Display session summary with aggregated statistics from all devices
                DaemonSessionSummaryReporter.LogSessionSummary(sessionStart, stats);
                DaemonSessionSummaryReporter.LogSessionEnd();

                return 0;
            }
            finally
            {
                // Ensures cleanup on exception paths (e.g., StartAsync or broadcaster failure)
                // Idempotent: no-op if DisposeAsync was already called above
                await orchestrator.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            return DaemonExceptionHandler.HandleException(ex);
        }
    }
}
