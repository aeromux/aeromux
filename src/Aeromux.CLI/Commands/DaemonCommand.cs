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
using Aeromux.CLI.Configuration;
using Aeromux.Core.Configuration;
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
/// Loads configuration, sets up logging, and will eventually manage SDR workers (Phase 1).
/// </summary>
public class DaemonCommand : Command<DaemonSettings>
{
    /// <summary>
    /// Executes the daemon command to start the Aeromux service.
    /// Configuration is already loaded by ConfigurationInterceptor and available via ConfigurationProvider.Current.
    /// </summary>
    /// <param name="context">The command context from Spectre.Console.Cli.</param>
    /// <param name="settings">Command settings (unused - only for Spectre.Console.Cli framework).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    public override int Execute(CommandContext context, DaemonSettings settings, CancellationToken cancellationToken)
    {
        // Validate settings parameter (required by CA1062)
        ArgumentNullException.ThrowIfNull(settings);

        Log.Information("Daemon command starting");

        Console.WriteLine("Aeromux daemon starting...");

        try
        {
            // Get configuration loaded by ConfigurationInterceptor
            var config = ConfigurationProvider.Current;

            // Check daemon-specific preconditions (business logic validation)
            CheckDaemonPreconditions(config);

            // TODO: Phase 1 will start SDR device workers here
            // Example:
            //   foreach (var deviceConfig in config.Sdr!.Devices.Where(d => d.Enabled))
            //   {
            //       var worker = new DeviceWorker(deviceConfig);
            //       worker.OpenDevice();
            //       worker.StartReceiving(cancellationToken);
            //   }
            Console.WriteLine("Service functionality not yet implemented - Phase 1");

            Log.Information("Daemon command completed (stub)");
            return 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("device") || ex.Message.Contains("port"))
        {
            // Daemon precondition checks failed
            Log.Error(ex, "Daemon preconditions not met");
            Console.WriteLine(ex.Message);
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
        if (config.Sdr?.Devices.Any(d => d.Enabled) != true)
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

        // TODO: Add checks for SBS and HTTP ports when those services are implemented
        Log.Debug("Daemon preconditions check passed");
    }
}
