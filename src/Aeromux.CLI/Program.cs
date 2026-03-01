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

using Aeromux.CLI.Commands;
using Aeromux.CLI.Configuration;
using Aeromux.Core.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Spectre.Console.Cli;
using RollingInterval = Serilog.RollingInterval;

namespace Aeromux.CLI;

/// <summary>
/// Main entry point for the Aeromux CLI application.
/// Configures Spectre.Console command infrastructure and Serilog logging.
/// </summary>
internal abstract class Program
{
    /// <summary>
    /// Application entry point. Configures CLI commands and runs the command infrastructure.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    private static async Task<int> Main(string[] args)
    {
        // Bootstrap logging with minimal config
        // Only logs warnings and errors during startup before full configuration is loaded
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Debug("Aeromux starting.");

            // Configure Spectre.Console CLI with available commands
            // Configuration loading happens in ConfigurationInterceptor after argument parsing
            var app = new CommandApp();

            app.Configure(config =>
            {
                config.SetApplicationName("aeromux");

                // Enable strict parsing to reject unknown options (typos like --connects instead of --connect)
                config.Settings.StrictParsing = true;

                // Use custom help provider (configures plain styles and adds header)
                config.SetHelpProvider(new AeromuxHelpProvider(config.Settings));

                // Use interceptor to load configuration before commands execute
                // This handles the global --config option for all commands
                config.SetInterceptor(new ConfigurationInterceptor());

                // Daemon command - main service mode for continuous operation
                config.AddCommand<DaemonCommand>("daemon")
                      .WithDescription("Start the Aeromux service")
                      .WithExample("daemon")
                      .WithExample("daemon", "--config", "custom.yaml")
                      .WithExample("daemon", "--beast-output-enabled", "--beast-port", "30005")
                      .WithExample("daemon", "--sbs-output-enabled", "--sbs-port", "30003")
                      .WithExample("daemon", "--json-output-enabled", "--json-port", "30006")
                      .WithExample("daemon", "--beast-output-enabled", "--beast-port", "30005", "--bind-address", "192.168.1.1");

                // Live command - real-time aircraft display with TUI
                config.AddCommand<LiveCommand>("live")
                      .WithDescription("Live aircraft display (TUI)")
                      .WithExample("live")
                      .WithExample("live", "--standalone")
                      .WithExample("live", "--standalone", "--config", "custom.yaml")
                      .WithExample("live", "--connect")
                      .WithExample("live", "--connect", "--config", "custom.yaml")
                      .WithExample("live", "--connect", "localhost:30005")
                      .WithExample("live", "--connect", "localhost:30005",  "--config", "custom.yaml")
                      .WithExample("live", "--connect", "192.168.1.100:30005")
                      .WithExample("live", "--connect", "192.168.1.100:30005", "--config", "custom.yaml");

                // Version command - displays version information
                config.AddCommand<VersionCommand>("version")
                      .WithDescription("Display version information")
                      .WithExample("version")
                      .WithExample("version", "--details");

                // Database command - manages aircraft metadata database
                config.AddCommand<DatabaseCommand>("database")
                      .WithDescription("Manage the aircraft metadata database")
                      .WithExample("database", "info")
                      .WithExample("database", "info", "--database", "artifacts/db/")
                      .WithExample("database", "update", "--database", "artifacts/db/")
                      .WithExample("database", "update", "--config", "aeromux.yaml");

                // TODO: Phase 9 will add validate and doctor commands
            });

            // Run the CLI and return the exit code from the executed command
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            // Log fatal errors that prevent the application from running
            Log.Fatal(ex, "Application terminated unexpectedly.");
            return 1;
        }
        finally
        {
            // Ensure all log messages are flushed to output before exit
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Reconfigures Serilog from YAML configuration.
    /// Called after loading configuration file to apply logging settings.
    /// </summary>
    /// <param name="loggingConfig">The logging configuration from YAML.</param>
    public static void ConfigureLogging(LoggingConfig loggingConfig)
    {
        // Build new logger configuration from YAML settings
        LoggerConfiguration logConfig = new LoggerConfiguration()
            .MinimumLevel.Is(loggingConfig.Level)
            // Reduce noise from Microsoft and System namespaces
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            // Add contextual enrichers for structured logging
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId();

        // Configure console sink if enabled
        if (loggingConfig.Console.Enabled)
        {
            if (loggingConfig.Console.Colored)
            {
                // Colored output with ANSI codes for better readability during development
                logConfig.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: AnsiConsoleTheme.Code);
            }
            else
            {
                // Plain text output for production or piped output
                logConfig.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
            }
        }

        // Configure file sink if enabled
        if (loggingConfig.File.Enabled)
        {
            // Map our RollingInterval enum to Serilog's RollingInterval enum
            RollingInterval rollingInterval = loggingConfig.File.RollingInterval switch
            {
                Core.Configuration.Enums.RollingInterval.Hour => RollingInterval.Hour,
                Core.Configuration.Enums.RollingInterval.Day => RollingInterval.Day,
                Core.Configuration.Enums.RollingInterval.Month => RollingInterval.Month,
                _ => RollingInterval.Day
            };

            // Configure file output with rotation and retention
            logConfig.WriteTo.File(
                path: loggingConfig.File.Path,
                rollingInterval: rollingInterval,
                retainedFileCountLimit: loggingConfig.File.RetainedFileCount,
                fileSizeLimitBytes: loggingConfig.File.FileSizeLimitMb * 1024 * 1024,
                // File output includes full timestamps for analysis
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
        }

        // Replace the bootstrap logger with the fully configured logger
        Log.Logger = logConfig.CreateLogger();
        Log.Information("Logging reconfigured from YAML: Level={LogLevel}, Console={ConsoleEnabled}, File={FileEnabled}",
            loggingConfig.Level, loggingConfig.Console.Enabled, loggingConfig.File.Enabled);
    }
}
