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

using Aeromux.CLI.Commands.Live;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Streaming;
using Serilog;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Live aircraft display command.
/// Supports unified input model: SDR sources, Beast sources, or both aggregated.
/// Uses AircraftStateTracker for state management.
/// </summary>
public sealed class LiveCommand : AsyncCommand<LiveSettings>
{
    /// <summary>
    /// Executes the live aircraft display command with unified input sources.
    /// </summary>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        LiveSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Log session separator for easy identification of new instances in log files
        LiveSessionReporter.LogSessionStart(settings);

        // Track session start time for summary statistics
        DateTime sessionStart = DateTime.UtcNow;

        try
        {
            // Validate and resolve all configuration upfront
            AeromuxConfig config = ConfigurationProvider.Current;
            LiveValidatedConfig validatedConfig = LiveConfigValidator.Validate(settings, config);

            // Print startup summary
            StartupSummaryPrinter.PrintLiveSummary(validatedConfig);

            // Build running message
            string sourcesSummary = BuildSourcesSummary(validatedConfig);
            Console.WriteLine($"Aeromux live running with {sourcesSummary}. Press Q to quit.");
            Console.WriteLine();

            try
            {
                // Create unified ReceiverStream with both SDR and Beast source lists
                await using var stream = new ReceiverStream(
                    validatedConfig.UseSdr ? validatedConfig.EnabledSdrSources : null,
                    validatedConfig.Tracking,
                    validatedConfig.Receiver,
                    beastSourceConfigs: validatedConfig.UseBeast ? validatedConfig.BeastSources : null);

                await stream.StartAsync(cancellationToken);
                Log.Information("Stream started");

                var display = new LiveTuiDisplay();
                int exitCode = await display.RunAsync(stream, settings, validatedConfig.Receiver, sessionStart, cancellationToken);

                return exitCode;
            }
            catch (Exception ex)
            {
                return LiveExceptionHandler.HandleStreamException(ex);
            }
            finally
            {
                LiveSessionReporter.LogSessionEnd(sessionStart);
            }
        }
        catch (Exception ex)
        {
            return LiveExceptionHandler.HandleException(ex);
        }
    }

    /// <summary>
    /// Builds a human-readable summary of active sources for the startup message.
    /// </summary>
    private static string BuildSourcesSummary(LiveValidatedConfig config)
    {
        var parts = new List<string>();

        if (config.UseSdr)
        {
            parts.Add($"{config.EnabledSdrSources.Count} SDR source(s)");
        }

        if (config.UseBeast)
        {
            parts.Add($"{config.BeastSources.Count} Beast source(s)");
        }

        return string.Join(" and ", parts);
    }
}
