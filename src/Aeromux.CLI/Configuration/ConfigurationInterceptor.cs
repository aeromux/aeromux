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

using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Configuration;
using Serilog;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Configuration;

/// <summary>
/// Command interceptor that builds configuration before any command executes.
/// Uses configuration hierarchy: Command-line args > YAML file > Built-in defaults.
/// Runs after Spectre.Console.Cli parses arguments but before command execution.
/// Sets ConfigurationProvider.Current for global access.
/// </summary>
public class ConfigurationInterceptor : ICommandInterceptor
{
    /// <summary>
    /// Intercepts command execution to build configuration.
    /// Delegates all configuration logic to ConfigurationBuilder.
    /// </summary>
    /// <param name="context">Command context with parsed settings.</param>
    /// <param name="settings">Parsed command settings (contains ConfigPath and CLI overrides).</param>
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        // Cast to GlobalSettings to access configuration options
        // All command settings inherit from GlobalSettings, so this is safe
        if (settings is not GlobalSettings globalSettings)
        {
            Log.Warning("Command settings do not inherit from GlobalSettings, skipping config load");
            return;
        }

        try
        {
            // Build configuration using hierarchy: Defaults → YAML → CLI
            // All logic is in ConfigurationBuilder (separation of concerns)
            var builder = new ConfigurationBuilder();
            var loader = new YamlConfigurationLoader();
            AeromuxConfig config = builder.BuildFromSettings(globalSettings, loader);

            // Store configuration globally for access by any class
            ConfigurationProvider.Current = config;

            // Configure logging from the final merged configuration
            // Config sections are guaranteed non-null after building
            Program.ConfigureLogging(config.Logging!);

            // Log final configuration values
            Log.Debug("Configuration loaded: BeastPort={BeastPort}, LogLevel={LogLevel}",
                config.Network!.BeastPort,
                config.Logging!.Level);
        }
        catch (Exception ex)
        {
            // Log with specific message based on exception type
            string message = ex switch
            {
                FileNotFoundException => "Configuration file not found",
                InvalidOperationException => "Failed to load configuration",
                _ => "Unexpected error building configuration"
            };
            Log.Error(ex, message);
            throw;
        }
    }

    /// <summary>
    /// Intercepts command execution asynchronously (not used).
    /// </summary>
    public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
    {
        // Not needed for configuration loading
    }
}
