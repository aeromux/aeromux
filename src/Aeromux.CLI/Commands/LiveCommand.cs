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
using System.Net.Sockets;
using Aeromux.CLI.Configuration;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using RtlSdrManager.Exceptions;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Optional connection string that supports both flag usage (--connect) and value usage (--connect HOST:PORT).
/// When used as flag, defaults to empty string which ParseConnectionString interprets as localhost:30005.
/// </summary>
public sealed class OptionalConnectionString : IFlagValue
{
    private string _value = string.Empty;
    private bool _isSet;

    public object? Value
    {
        get => _value;
        set => _value = value?.ToString() ?? string.Empty;
    }

    public bool IsSet
    {
        get => _isSet;
        set => _isSet = value;
    }

    public Type Type => typeof(string);

    public static OptionalConnectionString FromFlag() =>
        new() { _value = string.Empty, _isSet = true };

    public static OptionalConnectionString FromValue(string value) =>
        new() { _value = value, _isSet = true };

    public static implicit operator string?(OptionalConnectionString? connection) =>
        connection?._value;
}

/// <summary>
/// Settings for the Live command.
/// </summary>
public sealed class LiveSettings : GlobalSettings
{
    [CommandOption("--standalone")]
    [Description("Run in standalone mode (process RTL-SDR directly)")]
    public bool Standalone { get; set; }

    [CommandOption("--connect [ADDRESS]")]
    [Description("Connect to Beast-compatible source (default: localhost:30005)")]
    public OptionalConnectionString? Connect { get; set; }
}

/// <summary>
/// Live aircraft display command.
/// Supports standalone mode (direct RTL-SDR) and client mode (connect to Beast source).
/// Uses AircraftStateTracker for state management.
/// Client mode works with any Beast-compatible source (readsb, dump1090, aeromux daemon).
/// </summary>
public sealed class LiveCommand : AsyncCommand<LiveSettings>
{
    // Unit enums for display conversions
    private enum DistanceUnit { Miles, Kilometers }
    private enum AltitudeUnit { Feet, Meters }
    private enum SpeedUnit { Knots, KilometersPerHour, MilesPerHour }

    /// <summary>
    /// Executes the live aircraft display command in standalone or client mode.
    /// </summary>
    /// <param name="context">The command context from Spectre.Console CLI.</param>
    /// <param name="settings">Command settings including mode selection and connection parameters.</param>
    /// <param name="cancellationToken">Cancellation token to stop the TUI display.</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    /// <exception cref="ArgumentException">Thrown when both --standalone and --connect are specified.</exception>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        LiveSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Log session separator for easy identification of new instances in log files
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Aeromux Live Command Starting");
        Log.Information("Mode: {Mode}", settings.Connect?.IsSet == true ? "Client" : "Standalone");
        Log.Information("Session: {SessionStart:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("═══════════════════════════════════════════════════════════════");

        // Track session start time for summary statistics
        DateTime sessionStart = DateTime.UtcNow;

        // Validate mutual exclusivity
        if (settings.Standalone && settings.Connect?.IsSet == true)
        {
            Log.Error("Both --standalone and --connect specified (mutually exclusive)");
            Console.WriteLine("Error: Cannot use both --standalone and --connect");
            return 1;
        }

        try
        {
            int exitCode;
            if (settings.Connect?.IsSet == true)
            {
                exitCode = await RunClientModeAsync(settings, sessionStart, cancellationToken);
            }
            else
            {
                // Standalone mode (default or explicit --standalone)
                exitCode = await RunStandaloneModeAsync(settings, sessionStart, cancellationToken);
            }

            // Log session end separator
            TimeSpan sessionDuration = DateTime.UtcNow - sessionStart;
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("Aeromux Live Command Stopped");
            Log.Information("Session duration: {Duration}", sessionDuration.ToString(@"hh\:mm\:ss"));
            Log.Information("Session End: {SessionEnd:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
            Log.Information("═══════════════════════════════════════════════════════════════");

            return exitCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in Live command");
            HandleError(ex, settings.Connect != null);
            return 1;
        }
    }

    /// <summary>
    /// Runs in standalone mode: Direct RTL-SDR access with local signal processing.
    /// </summary>
    /// <param name="settings">Command settings (not currently used in standalone mode).</param>
    /// <param name="sessionStart">Session start time for summary statistics.</param>
    /// <param name="ct">Cancellation token to stop device processing.</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    /// <exception cref="InvalidOperationException">Thrown when RTL-SDR device is already in use.</exception>
    /// <exception cref="Exception">Thrown when no RTL-SDR devices are found.</exception>
    /// <remarks>
    /// Creates DeviceStream for direct RTL-SDR access. Provides helpful error messages
    /// suggesting connection to daemon if device is busy.
    /// </remarks>
    private async Task<int> RunStandaloneModeAsync(LiveSettings settings, DateTime sessionStart, CancellationToken ct)
    {
        Log.Information("Starting Live command in standalone mode");
        Console.WriteLine("Aeromux live starting in standalone mode...");

        AeromuxConfig config = ConfigurationProvider.Current;
        var enabledDevices = config.Devices!.Where(d => d.Enabled).ToList();

        if (enabledDevices.Count == 0)
        {
            Log.Error("No enabled devices found in configuration");
            Console.WriteLine("Error: No enabled devices found in configuration");
            return 1;
        }

        Log.Information("Device stream created. Devices={DeviceCount}", enabledDevices.Count);
        Log.Information("Tracking config: ConfidenceLevel={Level}, IcaoTimeout={Timeout}s",
            config.Tracking!.ConfidenceLevel, config.Tracking.IcaoTimeoutSeconds);

        // Log receiver location if configured
        if (config.Receiver?.Latitude.HasValue == true && config.Receiver?.Longitude.HasValue == true)
        {
            Log.Information("Receiver location configured: {Lat:F4}° {LatDir}, {Lon:F4}° {LonDir}",
                Math.Abs(config.Receiver.Latitude.Value),
                config.Receiver.Latitude.Value >= 0 ? "N" : "S",
                Math.Abs(config.Receiver.Longitude.Value),
                config.Receiver.Longitude.Value >= 0 ? "E" : "W");
        }
        else
        {
            Log.Warning("Receiver location not configured - distance calculation disabled");
        }

        Console.WriteLine($"Aeromux live running with {enabledDevices.Count} device(s). Press ESC or Q to stop.");
        Console.WriteLine();

        try
        {
            await using var stream = new DeviceStream(
                enabledDevices,
                config.Tracking!,
                config.Receiver);

            await stream.StartAsync(ct);
            Log.Information("Device stream started");

            // Standalone mode: Pass receiver config for distance calculation
            return await DisplayFramesAsync(stream, settings, config.Receiver, sessionStart, ct);
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
            Log.Error(ex, "RTL-SDR device not found");
            Console.WriteLine("Error: RTL-SDR device not found");
            Console.WriteLine("Please check:");
            Console.WriteLine("  1. Device is connected via USB");
            Console.WriteLine("  2. Drivers are installed (librtlsdr)");
            Console.WriteLine("  3. Run 'rtl_test' to verify device detection");
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
    /// Runs in client mode: Connect to Beast-compatible TCP source.
    /// </summary>
    /// <param name="settings">Command settings containing connection string.</param>
    /// <param name="sessionStart">Session start time for summary statistics.</param>
    /// <param name="ct">Cancellation token to stop the connection.</param>
    /// <returns>Exit code: 0 for success, 1 for error.</returns>
    /// <exception cref="SocketException">Thrown when connection to Beast source is refused.</exception>
    /// <remarks>
    /// Connects to any Beast-compatible server (readsb, dump1090, dump1090-fa, aeromux daemon).
    /// BeastStream includes IcaoConfidenceTracker to filter noise from real aircraft.
    /// </remarks>
    private async Task<int> RunClientModeAsync(LiveSettings settings, DateTime sessionStart, CancellationToken ct)
    {
        // Parse connection string (default: localhost:30005)
        (string host, int port) = ParseConnectionString(settings.Connect!);

        Log.Information("Starting Live command in client mode");
        Log.Information("Connecting to Beast source: {Host}:{Port}", host, port);

        Console.WriteLine("Aeromux live starting in client mode...");
        Console.WriteLine($"Connecting to {host}:{port}...");

        // Load config for TrackingConfig (needed for IcaoConfidenceTracker)
        AeromuxConfig config = ConfigurationProvider.Current;

        Log.Information("Tracking config: ConfidenceLevel={Level}, IcaoTimeout={Timeout}s",
            config.Tracking!.ConfidenceLevel, config.Tracking.IcaoTimeoutSeconds);

        // Log receiver location if configured (useful for distance calc even in client mode)
        if (config.Receiver?.Latitude.HasValue == true && config.Receiver?.Longitude.HasValue == true)
        {
            Log.Information("Receiver location configured: {Lat:F4}° {LatDir}, {Lon:F4}° {LonDir}",
                Math.Abs(config.Receiver.Latitude.Value),
                config.Receiver.Latitude.Value >= 0 ? "N" : "S",
                Math.Abs(config.Receiver.Longitude.Value),
                config.Receiver.Longitude.Value >= 0 ? "E" : "W");
        }
        else
        {
            Log.Warning("Receiver location not configured - distance calculation disabled");
        }

        Console.WriteLine("Aeromux live running, connected to a Beast daemon. Press ESC or Q to stop.");
        Console.WriteLine();

        try
        {
            // Create BeastStream with TrackingConfig for confidence filtering
            await using var stream = new BeastStream(host, port, config.Tracking!);
            await stream.StartAsync(ct);

            Log.Information("Connected to Beast source: {Host}:{Port}", host, port);

            // Load receiver config if available (might be null in client mode)
            // If configured, allows distance calculation even when connected to remote source
            return await DisplayFramesAsync(stream, settings, config.Receiver, sessionStart, ct);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            Log.Error(ex, "Connection timeout to Beast source: {Host}:{Port}", host, port);
            Console.WriteLine("Error: Connection timeout (5 seconds)");
            Console.WriteLine($"Cannot connect to {host}:{port}");
            Console.WriteLine("Please check:");
            Console.WriteLine("  - Host address is correct");
            Console.WriteLine("  - Port is correct (default Beast port is 30005)");
            Console.WriteLine("  - Beast source is running and accessible");
            return 1;
        }
        catch (SocketException ex)
        {
            Log.Error(ex, "Failed to connect to Beast source: {Host}:{Port}", host, port);
            Console.WriteLine("Error: Connection refused");
            Console.WriteLine("Beast source is not running or not accessible.");
            Console.WriteLine("Examples:");
            Console.WriteLine("  - Start readsb: readsb --net");
            Console.WriteLine("  - Start aeromux: aeromux daemon");
            return 1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to Beast source: {Host}:{Port}", host, port);
            Console.WriteLine("Error: Other");
            Console.WriteLine("Other error has happened during connection to the Beast source.");
            return 1;
        }
    }

    /// <summary>
    /// Displays frames with TUI (Terminal User Interface).
    /// </summary>
    /// <param name="stream">Frame stream (DeviceStream or BeastStream) providing ProcessedFrame data.</param>
    /// <param name="settings">Command settings (not currently used).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <param name="sessionStart">Session start time for summary statistics.</param>
    /// <param name="ct">Cancellation token to stop the TUI display.</param>
    /// <returns>Exit code: always returns 0 on normal exit.</returns>
    /// <remarks>
    /// Implements a flicker-free TUI using Spectre.Console Live display with 1-second refresh rate.
    /// Includes workaround for Spectre.Console resize bug (see GitHub issue #356).
    /// Tracker runs in background; display polls GetAllAircraft() every 1 second.
    /// Keyboard input is checked every 50ms for responsive navigation.
    /// </remarks>
    private async Task<int> DisplayFramesAsync(
        IFrameStream stream,
        LiveSettings settings,
        ReceiverConfig? receiverConfig,
        DateTime sessionStart,
        CancellationToken ct)
    {
        // Create local AircraftStateTracker (follows DaemonCommand pattern)
        AeromuxConfig config = ConfigurationProvider.Current;
        var tracker = new AircraftStateTracker(config.Tracking!);

        // Create linked cancellation token for tracker consumer task
        // This allows us to cancel the tracker independently when user quits
        using var trackerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Subscribe to aircraft lifecycle events for operational visibility
        tracker.OnAircraftAdded += (sender, e) =>
        {
            Aircraft aircraft = e.Aircraft;
            Log.Information("New aircraft: ICAO={Icao}, Callsign={Callsign}",
                aircraft.Identification.ICAO,
                aircraft.Identification.Callsign ?? "Unknown");
        };

        // Log significant updates (position, altitude, velocity changes)
        tracker.OnAircraftUpdated += (sender, e) =>
        {
            Aircraft prev = e.Previous;
            Aircraft curr = e.Updated;

            // Only log if position or velocity actually changed to reduce log noise
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

        // IMPORTANT: Tracker runs in background, consuming frames automatically
        // Uses trackerCts.Token so we can cancel it independently before disposal
        tracker.StartConsuming(stream.Subscribe(), trackerCts.Token);
        Log.Information("Aircraft state tracker started");

        AnsiConsole.Clear();

        // TUI state
        string? selectedIcao = null; // Track selected aircraft by ICAO (not row index)
        int selectedRow;
        bool showingDetails = false;
        DistanceUnit distanceUnit = DistanceUnit.Miles;
        AltitudeUnit altitudeUnit = AltitudeUnit.Feet;
        SpeedUnit speedUnit = SpeedUnit.Knots;

        // Track terminal size for resize detection (workaround for Spectre.Console bug)
        int lastWidth;
        int lastHeight;

        Console.CursorVisible = false;

        Log.Debug("TUI display started");
        Log.Debug("Terminal size: {Width}x{Height}", Console.WindowWidth, Console.WindowHeight);

        // WORKAROUND: Spectre.Console has a known bug where Live display gets corrupted on resize
        // See: https://github.com/spectreconsole/spectre.console/discussions/356
        // Solution: Detect resize, exit Live display, clear screen, restart
        try
        {
            bool needsRestart = false;
            bool shouldQuit = false;

            while (!ct.IsCancellationRequested && !shouldQuit)
            {
                // Clear screen before starting new Live display session (especially after resize)
                if (needsRestart)
                {
                    Log.Debug("Terminal resize detected: {Width}x{Height}", Console.WindowWidth, Console.WindowHeight);
                    AnsiConsole.Clear();
                    needsRestart = false;
                }

                // Capture current terminal size for this Live display session
                lastWidth = Console.WindowWidth;
                lastHeight = Console.WindowHeight;

                await AnsiConsole.Live(new Text("Initializing..."))
                    .AutoClear(false) // Don't auto-clear, we manage content
                    .StartAsync(async ctx =>
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            // Poll tracker for current state (tracker updates in background)
                            // Sort by ICAO for stable, predictable order
                            var sortedAircraft = tracker.GetAllAircraft()
                                .OrderBy(a => a.Identification.ICAO)
                                .ToList();

                            // Find selected aircraft by ICAO and update row index
                            if (selectedIcao != null)
                            {
                                int foundIndex = sortedAircraft.FindIndex(a => a.Identification.ICAO == selectedIcao);
                                if (foundIndex >= 0)
                                {
                                    selectedRow = foundIndex;
                                }
                                else
                                {
                                    // Selected aircraft expired, select first available
                                    selectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[0].Identification.ICAO : null;
                                    selectedRow = 0;
                                }
                            }
                            else
                            {
                                // Initial selection
                                selectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[0].Identification.ICAO : null;
                                selectedRow = 0;
                            }

                            // WORKAROUND: Detect terminal resize to fix Spectre.Console Live display corruption
                            // Exit Live display to clear corrupted buffer, outer loop will restart
                            int currentWidth = Console.WindowWidth;
                            int currentHeight = Console.WindowHeight;
                            if (currentWidth != lastWidth || currentHeight != lastHeight)
                            {
                                needsRestart = true;
                                return; // Exit Live display, outer loop will clear and restart
                            }

                            // Update display with current view (flicker-free for normal updates)
                            if (showingDetails && sortedAircraft.Count > 0)
                            {
                                ctx.UpdateTarget(BuildDetailView(sortedAircraft[selectedRow], distanceUnit, altitudeUnit, speedUnit, receiverConfig));
                            }
                            else
                            {
                                ctx.UpdateTarget(BuildTable(sortedAircraft, stream.GetStatistics(), selectedRow, distanceUnit, altitudeUnit, speedUnit, receiverConfig));
                            }
                            ctx.Refresh();

                            // Check for keyboard input with timeout (non-blocking poll every 50ms)
                            DateTime startTime = DateTime.UtcNow;
                            while ((DateTime.UtcNow - startTime).TotalMilliseconds < 1000 && !ct.IsCancellationRequested)
                            {
                                if (Console.KeyAvailable)
                                {
                                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                                    if (showingDetails)
                                    {
                                        // Detail view keyboard handling
                                        switch (key.Key)
                                        {
                                            case ConsoleKey.Escape:
                                                showingDetails = false;
                                                break;
                                            case ConsoleKey.Q:
                                                shouldQuit = true;
                                                return; // Exit Live display
                                        }
                                    }
                                    else
                                    {
                                        // Table view keyboard handling
                                        if (!HandleTableKeyboard(key, sortedAircraft, ref selectedIcao, ref selectedRow, ref showingDetails, ref distanceUnit, ref altitudeUnit, ref speedUnit))
                                        {
                                            shouldQuit = true;
                                            return; // Exit Live display
                                        }
                                    }

                                    // Re-render immediately after keyboard input for responsive feel
                                    break;
                                }

                                // Small delay to avoid busy-waiting
                                await Task.Delay(50, ct);
                            }
                        }
                    });

                // If we exited due to resize, continue outer loop to restart Live display
                // If we exited due to quit, ct.IsCancellationRequested will be true and we'll exit
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            Log.Information("TUI display cancelled");
        }
        finally
        {
            Console.CursorVisible = true;

            // CRITICAL: Cancel tracker consumer task BEFORE disposing
            // This prevents ObjectDisposedException during shutdown
            await trackerCts.CancelAsync();

            // Now safe to dispose tracker (consumer task will complete gracefully)
            tracker.Dispose();
            Log.Information("Aircraft state tracker stopped");

            // Display session summary with statistics
            TimeSpan sessionDuration = DateTime.UtcNow - sessionStart;
            StreamStatistics? stats = stream.GetStatistics();

            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("Live Command Session Summary");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("Session duration: {Duration}", sessionDuration.ToString(@"hh\:mm\:ss"));
            Log.Information("Mode: {Mode}", settings.Connect?.IsSet == true ? "Client" : "Standalone");

            if (stats != null)
            {
                Log.Information("Total frames: {TotalFrames:N0}", stats.TotalFrames);
                Log.Information("Valid frames: {ValidFrames:N0}", stats.ValidFrames);
                Log.Information("Corrected frames: {CorrectedFrames:N0}", stats.CorrectedFrames);
                Log.Information("Messages parsed: {ParsedMessages:N0}", stats.ParsedMessages);
            }
            else
            {
                Log.Information("Statistics not available (client mode)");
            }

            Log.Information("Total aircraft tracked: {AircraftCount}", tracker.GetAllAircraft().Count);
            Log.Information("═══════════════════════════════════════════════════════════════");
        }

        return 0;
    }

    /// <summary>
    /// Handles keyboard input for table view.
    /// </summary>
    /// <param name="key">The console key information from keyboard input.</param>
    /// <param name="sortedAircraft">List of aircraft sorted by ICAO for stable display order.</param>
    /// <param name="selectedIcao">Currently selected aircraft ICAO (updated on navigation).</param>
    /// <param name="selectedRow">Currently selected row index (updated on navigation).</param>
    /// <param name="showingDetails">Detail view toggle (set true when Enter is pressed).</param>
    /// <param name="distanceUnit">Distance unit setting (toggled by D key).</param>
    /// <param name="altitudeUnit">Altitude unit setting (toggled by A key).</param>
    /// <param name="speedUnit">Speed unit setting (cycled by S key).</param>
    /// <returns>False if user pressed Q or Escape (quit), true to continue.</returns>
    private bool HandleTableKeyboard(
        ConsoleKeyInfo key,
        List<Aircraft> sortedAircraft,
        ref string? selectedIcao,
        ref int selectedRow,
        ref bool showingDetails,
        ref DistanceUnit distanceUnit,
        ref AltitudeUnit altitudeUnit,
        ref SpeedUnit speedUnit)
    {
        // Calculate available rows dynamically based on terminal height
        const int headerLines = 0;
        const int footerLines = 2;
        const int tableHeaderLines = 1;
        const int padding = 3;

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                selectedRow = Math.Max(0, selectedRow - 1);
                selectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[selectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.DownArrow:
                selectedRow = Math.Min(sortedAircraft.Count - 1, selectedRow + 1);
                selectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[selectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.LeftArrow:
            case ConsoleKey.PageUp:
                // Jump up by viewport size
                selectedRow = Math.Max(0, selectedRow - availableRows);
                selectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[selectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.RightArrow:
            case ConsoleKey.PageDown:
                // Jump down by viewport size
                selectedRow = Math.Min(sortedAircraft.Count - 1, selectedRow + availableRows);
                selectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[selectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.Enter:
                // Show detail view for selected aircraft
                if (sortedAircraft.Count > 0)
                {
                    showingDetails = true;
                }
                break;
            case ConsoleKey.D:
                // Toggle distance unit (miles <-> kilometers)
                distanceUnit = distanceUnit == DistanceUnit.Miles
                    ? DistanceUnit.Kilometers
                    : DistanceUnit.Miles;
                Log.Debug("Distance unit changed to: {Unit}", distanceUnit);
                break;
            case ConsoleKey.A:
                // Toggle altitude unit (feet <-> meters)
                altitudeUnit = altitudeUnit == AltitudeUnit.Feet
                    ? AltitudeUnit.Meters
                    : AltitudeUnit.Feet;
                Log.Debug("Altitude unit changed to: {Unit}", altitudeUnit);
                break;
            case ConsoleKey.S:
                // Cycle through speed units (knots -> km/h -> mph -> knots)
                speedUnit = speedUnit switch
                {
                    SpeedUnit.Knots => SpeedUnit.KilometersPerHour,
                    SpeedUnit.KilometersPerHour => SpeedUnit.MilesPerHour,
                    SpeedUnit.MilesPerHour => SpeedUnit.Knots,
                    _ => SpeedUnit.Knots
                };
                Log.Debug("Speed unit changed to: {Unit}", speedUnit);
                break;
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                Log.Information("User quit requested (Q/ESC)");
                return false; // Quit
        }

        return true; // Continue
    }

    /// <summary>
    /// Builds aircraft table with dynamic viewport.
    /// </summary>
    /// <param name="sortedAircraft">Aircraft list sorted by ICAO for stable display order.</param>
    /// <param name="stats">Stream statistics from DeviceStream, or null in client mode.</param>
    /// <param name="selectedRow">Index of currently selected row for highlighting.</param>
    /// <param name="distanceUnit">Unit to display distances (miles or kilometers).</param>
    /// <param name="altitudeUnit">Unit to display altitudes (feet or meters).</param>
    /// <param name="speedUnit">Unit to display speeds (knots, km/h, or mph).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <returns>Spectre.Console Table with aircraft data, footer, and fixed 120-character width.</returns>
    private Table BuildTable(
        List<Aircraft> sortedAircraft,
        StreamStatistics? stats,
        int selectedRow,
        DistanceUnit distanceUnit,
        AltitudeUnit altitudeUnit,
        SpeedUnit speedUnit,
        ReceiverConfig? receiverConfig)
    {
        // Calculate available rows dynamically based on terminal height
        const int headerLines = 1;
        const int footerLines = 2;
        const int tableHeaderLines = 1;
        const int padding = 3;

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);
        int halfViewport = availableRows / 2;

        // Calculate viewport (centered on selection)
        int viewportStart = Math.Max(0, selectedRow - halfViewport);
        int viewportEnd = Math.Min(sortedAircraft.Count, viewportStart + availableRows);

        // Adjust if at end of list (show full viewport)
        if (viewportEnd - viewportStart < availableRows && sortedAircraft.Count >= availableRows)
        {
            viewportStart = Math.Max(0, viewportEnd - availableRows);
        }

        // Create table with dynamic width based on terminal size
        Table table = new Table()
            .Border(TableBorder.Square)
            .Title("AIRCRAFT LIST - Aeromux", new Style(decoration:Decoration.Bold));

        // Add ICAO column with fixed width using correct Spectre.Console API (lambda configuration)
        table.AddColumn("[bold]ICAO[/]", col => col.Width(6).NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Callsign[/]", col => col.Width(8).NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Altitude[/]", col => col.Width(8).RightAligned().NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Vertical[/]", col => col.Width(10).RightAligned().NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Distance[/]", col => col.Width(9).RightAligned().NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Speed[/]", col => col.Width(8).RightAligned().NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Messages[/]", col => col.Width(8).RightAligned().NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Signal[/]", col => col.Width(6).RightAligned().NoWrap().PadLeft(1).PadRight(1));
        table.AddColumn("[bold]Last seen[/]", col => col.Width(9).RightAligned().NoWrap().PadLeft(1).PadRight(1));

        // Render aircraft in viewport
        for (int i = viewportStart; i < viewportEnd; i++)
        {
            Aircraft aircraft = sortedAircraft[i];
            TimeSpan age = DateTime.UtcNow - aircraft.Status.LastSeen;
            bool isSelected = i == selectedRow;

            // Format callsign (or N/A if not known)
            string callsign = aircraft.Identification.Callsign ?? "N/A";
            callsign = callsign.PadRight(8);

            // Format altitude based on selected unit (or N/A if not known)
            string altitude = "N/A";
            Altitude? altitudeValue = aircraft.Position.BarometricAltitude ?? aircraft.Position.GeometricAltitude;
            if (altitudeValue != null)
            {
                if (altitudeUnit == AltitudeUnit.Feet)
                {
                    altitude = $"{altitudeValue.Feet.ToString()} ft";
                }
                else // Meters
                {
                    double meters = altitudeValue.Feet * 0.3048;
                    altitude = $"{((int)meters).ToString()} m";
                }
            }
            altitude = altitude.PadLeft(8);

            // Format vertical rate (or N/A if not known)
            string verticalRate = "N/A";
            int? verticalRateValue = aircraft.Velocity.VerticalRate;
            if (verticalRateValue != null)
            {
                verticalRate = $"{verticalRateValue} ft/m";
            }
            verticalRate = verticalRate.PadLeft(10);

            // Calculate distance (if receiver location configured and aircraft has position)
            string distance = "N/A";
            if (receiverConfig?.Latitude.HasValue == true &&
                receiverConfig?.Longitude.HasValue == true &&
                aircraft.Position.Coordinate != null)
            {
                var receiverLocation = new GeographicCoordinate(
                    receiverConfig.Latitude.Value,
                    receiverConfig.Longitude.Value);

                double distanceValue = distanceUnit == DistanceUnit.Miles
                    ? receiverLocation.DistanceToMiles(aircraft.Position.Coordinate)
                    : receiverLocation.DistanceToKilometers(aircraft.Position.Coordinate);

                string unitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
                distance = $"{distanceValue:F1} {unitLabel}";
            }
            distance = distance.PadLeft(9);

            // Format speed based on selected unit (or N/A if not known)
            string speed = "N/A";
            Velocity? speedValue = aircraft.Velocity.Speed ?? aircraft.Velocity.GroundSpeed;
            if (speedValue != null)
            {
                double displaySpeed = speedUnit switch
                {
                    SpeedUnit.Knots => speedValue.Knots,
                    SpeedUnit.KilometersPerHour => speedValue.Knots * 1.852,
                    SpeedUnit.MilesPerHour => speedValue.Knots * 1.15078,
                    _ => speedValue.Knots
                };

                string unitLabel = speedUnit switch
                {
                    SpeedUnit.Knots => "kts",
                    SpeedUnit.KilometersPerHour => "km/h",
                    SpeedUnit.MilesPerHour => "mph",
                    _ => "kts"
                };

                speed = $"{displaySpeed:F0} {unitLabel}";
            }
            speed = speed.PadLeft(8);

            // Format signal strength
            string signal = aircraft.Status.SignalStrength != 0
                ? $"{aircraft.Status.SignalStrength:F1}"
                : "  N/A";
            signal = signal.PadLeft(6);

            // Format last seen: XX.Xs ago (right-align number to 4 chars: 2 digits + dot + 1 decimal)
            string lastSeenNumber = $"{age.TotalSeconds:F1}s ago".PadLeft(9);

            // Format messages: right-align to 6 chars
            string messages = $"{aircraft.Status.TotalMessages}".PadLeft(8);

            // Apply selection highlighting (inverted colors)
            if (isSelected)
            {
                table.AddRow(
                    $"[black on white]{aircraft.Identification.ICAO}[/]",
                    $"[black on white]{callsign}[/]",
                    $"[black on white]{altitude}[/]",
                    $"[black on white]{verticalRate}[/]",
                    $"[black on white]{distance}[/]",
                    $"[black on white]{speed}[/]",
                    $"[black on white]{messages}[/]",
                    $"[black on white]{signal}[/]",
                    $"[black on white]{lastSeenNumber}[/]");
            }
            else
            {
                table.AddRow(
                    aircraft.Identification.ICAO,
                    callsign,
                    altitude,
                    verticalRate,
                    distance,
                    speed,
                    messages,
                    signal,
                    lastSeenNumber);
            }
        }

        // Fill remaining rows with empty content to always use full terminal height
        int rowsRendered = viewportEnd - viewportStart;
        int emptyRowsNeeded = availableRows - rowsRendered;
        for (int i = 0; i < emptyRowsNeeded; i++)
        {
            table.AddRow("", "", "", "", "", "", "", "", "");
        }

        // Build footer (2 rows with left/right alignment)
        string distUnitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
        string altUnitLabel = altitudeUnit == AltitudeUnit.Feet ? "ft" : "m";
        string speedUnitLabel = speedUnit switch
        {
            SpeedUnit.Knots => "kts",
            SpeedUnit.KilometersPerHour => "km/h",
            SpeedUnit.MilesPerHour => "mph",
            _ => "kts"
        };

        // Footer row 1: left and right sections
        string footerRow1Left = $"[bold]Aircraft:[/] {sortedAircraft.Count} | [bold]Selected:[/] {selectedRow + 1}/{sortedAircraft.Count} | [bold]Viewport:[/] {viewportStart + 1}-{viewportEnd}";
        string footerRow1Right = $"[bold]Dist:[/] {distUnitLabel} | [bold]Alt:[/] {altUnitLabel} | [bold]Spd:[/] {speedUnitLabel}";

        // Footer row 2: left and right sections
        string footerRow2Left = "[bold]↑/↓[/]: Row, [bold]←/→[/]: Page";
        string footerRow2Right = "[bold]ENTER[/]: Details, [bold]D/A/S[/]: Units, [bold]Q[/]: Quit";

        // Calculate spacing for right alignment (100 chars total width - border chars)
        // Table width is 100, but caption appears inside borders, so usable width is 96
        int usableWidth = 96;

        string footerRow1 = footerRow1Left + new string(' ', Math.Max(1, usableWidth - footerRow1Left.Length + 27 - footerRow1Right.Length + 27)) + footerRow1Right;
        string footerRow2 = footerRow2Left + new string(' ', Math.Max(1, usableWidth - footerRow2Left.Length + 18 - footerRow2Right.Length + 27)) + footerRow2Right;

        string footer = footerRow1 + "\n" + footerRow2;

        table.Caption(footer, new Style(foreground: Color.Grey));

        // Return table for Live display
        return table;
    }

    /// <summary>
    /// Builds detailed view for a single aircraft as a table (matching main table width).
    /// </summary>
    /// <param name="aircraft">Aircraft to display detailed information for.</param>
    /// <param name="distanceUnit">Unit to display distances (miles or kilometers).</param>
    /// <param name="altitudeUnit">Unit to display altitudes (feet or meters).</param>
    /// <param name="speedUnit">Unit to display speeds (knots, km/h, or mph).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <returns>Spectre.Console Table with detailed aircraft information and fixed 120-character width.</returns>
    private Table BuildDetailView(
        Aircraft aircraft,
        DistanceUnit distanceUnit,
        AltitudeUnit altitudeUnit,
        SpeedUnit speedUnit,
        ReceiverConfig? receiverConfig)
    {
        // Calculate available rows dynamically based on terminal height
        const int headerLines = 1;
        const int footerLines = 2;
        const int tableHeaderLines = 1;
        const int padding = 3;

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);
        int usedRows = 0;

        // Create table with same fixed 98 character width as main table
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Width(100)
            .Title($"AIRCRAFT DETAIL ({aircraft.Identification.ICAO}) - Aeromux")
            .AddColumn(new TableColumn("[bold]Field[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Value[/]").NoWrap());

        // === IDENTIFICATION ===
        table.AddRow("[bold]IDENTIFICATION[/]", "");
        table.AddRow("ICAO Address", aircraft.Identification.ICAO);
        table.AddRow("Callsign", aircraft.Identification.Callsign ?? "N/A");
        table.AddRow("Category", aircraft.Identification.Category?.ToString() ?? "N/A");
        table.AddRow("Squawk", aircraft.Identification.Squawk ?? "N/A");
        table.AddRow("Emergency", aircraft.Identification.EmergencyState.ToString());
        table.AddRow("Flight Status", aircraft.Identification.FlightStatus?.ToString() ?? "N/A");
        table.AddRow("ADS-B Version", aircraft.Identification.Version?.ToString() ?? "N/A");
        table.AddRow("", "");
        usedRows += 9;

        // === STATUS ===
        table.AddRow("[bold]STATUS[/]", "");
        table.AddRow("First Seen", aircraft.Status.FirstSeen.ToString("HH:mm:ss"));
        table.AddRow("Last Seen", $"{(DateTime.UtcNow - aircraft.Status.LastSeen).TotalSeconds:F1}s ago");
        table.AddRow("Total Messages", aircraft.Status.TotalMessages.ToString());
        table.AddRow("Position Msgs", aircraft.Status.PositionMessages.ToString());
        table.AddRow("Velocity Msgs", aircraft.Status.VelocityMessages.ToString());
        table.AddRow("ID Messages", aircraft.Status.IdentificationMessages.ToString());
        table.AddRow("Signal Strength", $"{aircraft.Status.SignalStrength:F1} dBFS");
        table.AddRow("", "");
        usedRows += 9;

        // === POSITION ===
        table.AddRow("[bold]POSITION[/]", "");
        usedRows += 1;
        if (aircraft.Position.Coordinate != null)
        {
            table.AddRow("Latitude", $"{aircraft.Position.Coordinate.Latitude:F6}°");
            table.AddRow("Longitude", $"{aircraft.Position.Coordinate.Longitude:F6}°");

            // Distance
            if (receiverConfig?.Latitude.HasValue == true && receiverConfig?.Longitude.HasValue == true)
            {
                var receiverLocation = new GeographicCoordinate(
                    receiverConfig.Latitude.Value,
                    receiverConfig.Longitude.Value);

                double dist = distanceUnit == DistanceUnit.Miles
                    ? receiverLocation.DistanceToMiles(aircraft.Position.Coordinate)
                    : receiverLocation.DistanceToKilometers(aircraft.Position.Coordinate);

                string unitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
                table.AddRow("Distance", $"{dist:F1} {unitLabel}");
            }
            else
            {
                table.AddRow("Distance", "N/A (no receiver location)");
            }

            usedRows += 3;
        }
        else
        {
            table.AddRow("Position", "N/A");

            usedRows += 1;
        }

        // Barometric altitude
        if (aircraft.Position.BarometricAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                table.AddRow("Baro Altitude", $"{aircraft.Position.BarometricAltitude.Feet:F0} ft");
            }
            else
            {
                double meters = aircraft.Position.BarometricAltitude.Feet * 0.3048;
                table.AddRow("Baro Altitude", $"{meters:F0} m");
            }
        }
        else
        {
            table.AddRow("Baro Altitude", "N/A");
        }
        usedRows += 1;

        // Geometric altitude
        if (aircraft.Position.GeometricAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                table.AddRow("Geo Altitude", $"{aircraft.Position.GeometricAltitude.Feet:F0} ft");
            }
            else
            {
                double meters = aircraft.Position.GeometricAltitude.Feet * 0.3048;
                table.AddRow("Geo Altitude", $"{meters:F0} m");
            }
        }
        usedRows += 1;

        table.AddRow("On Ground", aircraft.Position.IsOnGround.ToString());
        table.AddRow("NACp", aircraft.Position.NACp?.ToString() ?? "N/A");
        table.AddRow("SIL", aircraft.Position.SIL?.ToString() ?? "N/A");
        table.AddRow("", "");
        usedRows += 3;

        // === VELOCITY ===
        table.AddRow("[bold]VELOCITY[/]", "");
        usedRows += 1;
        Velocity? speedValue = aircraft.Velocity.Speed ?? aircraft.Velocity.GroundSpeed;
        if (speedValue != null)
        {
            double displaySpeed = speedUnit switch
            {
                SpeedUnit.Knots => speedValue.Knots,
                SpeedUnit.KilometersPerHour => speedValue.Knots * 1.852,
                SpeedUnit.MilesPerHour => speedValue.Knots * 1.15078,
                _ => speedValue.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            string velocityTypeStr = aircraft.Velocity.VelocitySubtype switch
            {
                VelocitySubtype.GroundSpeedSubsonic or VelocitySubtype.GroundSpeedSupersonic => "Ground Speed",
                VelocitySubtype.AirspeedSubsonic or VelocitySubtype.AirspeedSupersonic => "Airspeed",
                _ => "Unknown"
            };

            table.AddRow("Speed", $"{displaySpeed:F0} {unitLabel} ({velocityTypeStr})");
        }
        else
        {
            table.AddRow("Speed", "N/A");
        }
        usedRows += 1;

        if (aircraft.Velocity.Heading != null)
        {
            table.AddRow("Heading", $"{aircraft.Velocity.Heading:F1}°");
            usedRows += 1;
        }


        if (aircraft.Velocity.Track != null)
        {
            table.AddRow("Track", $"{aircraft.Velocity.Track:F1}°");
            usedRows += 1;
        }

        if (aircraft.Velocity.GroundTrack != null)
        {
            table.AddRow("Ground Track", $"{aircraft.Velocity.GroundTrack:F1}°");
            usedRows += 1;
        }

        if (aircraft.Velocity.VerticalRate != null)
        {
            table.AddRow("Vertical Rate", $"{aircraft.Velocity.VerticalRate:F0} ft/min");
            usedRows += 1;
        }

        // Fill remaining rows with empty content to always use full terminal height
        int emptyRowsNeeded = availableRows - usedRows;
        for (int i = 0; i < emptyRowsNeeded; i++)
        {
            table.AddRow("", "");
        }

        // Build footer (2 rows with left/right alignment)
        string distUnitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
        string altUnitLabel = altitudeUnit == AltitudeUnit.Feet ? "ft" : "m";
        string speedUnitLabel = speedUnit switch
        {
            SpeedUnit.Knots => "kts",
            SpeedUnit.KilometersPerHour => "km/h",
            SpeedUnit.MilesPerHour => "mph",
            _ => "kts"
        };

        // Footer row 1: left and right sections
        string footerRow1Left = $"[bold]Aircraft:[/] {aircraft.Identification.ICAO}";
        string footerRow1Right = $"[bold]Dist:[/] {distUnitLabel} | [bold]Alt:[/] {altUnitLabel} | [bold]Spd:[/] {speedUnitLabel}";

        // Footer row 2: left and right sections
        string footerRow2Left = "[bold]ESC[/]: Return to the table view";
        string footerRow2Right = "[bold]Q[/]: Quit";

        // Calculate spacing for right alignment (100 chars total width - border chars)
        // Table width is 100, but caption appears inside borders, so usable width is 96
        int usableWidth = 96;

        string footerRow1 = footerRow1Left + new string(' ', Math.Max(1, usableWidth - footerRow1Left.Length + 9 - footerRow1Right.Length + 27)) + footerRow1Right;
        string footerRow2 = footerRow2Left + new string(' ', Math.Max(1, usableWidth - footerRow2Left.Length + 9 - footerRow2Right.Length + 9)) + footerRow2Right;

        string footer = footerRow1 + "\n" + footerRow2;

        table.Caption(footer, new Style(foreground: Color.Grey));

        // Return table for Live display
        return table;
    }

    /// <summary>
    /// Parses connection string into (host, port) tuple.
    /// Supports: "HOST:PORT", ":PORT", "PORT", "HOST", "IP", or empty (defaults to localhost:30005)
    /// </summary>
    /// <param name="connectString">Connection string in format "HOST:PORT", ":PORT", "PORT", "HOST", "IP", or null for default.</param>
    /// <returns>Tuple of (host, port) parsed from connection string, or ("localhost", 30005) if null/empty.</returns>
    /// <exception cref="ArgumentException">Thrown when port number is invalid or format is incorrect.</exception>
    private (string host, int port) ParseConnectionString(string? connectString)
    {
        // Default if just --connect (no value)
        if (string.IsNullOrWhiteSpace(connectString))
        {
            return ("localhost", 30005);
        }

        // Parse HOST:PORT or just PORT or just HOST/IP
        string[] parts = connectString.Split(':');

        switch (parts.Length)
        {
            case 1:
            {
                // Could be just port (30005) or just host (192.168.1.1 or example.com)
                string value = parts[0].TrimStart(':');

                // Try to parse as port number first
                if (int.TryParse(value, out int port) && port is > 0 and <= 65535)
                {
                    // It's a port number - use localhost
                    return ("localhost", port);
                }

                // It's a hostname or IP address - validate and use default port
                if (IsValidHost(value))
                {
                    return (value, 30005);
                }

                Console.WriteLine($"Error: Invalid hostname or IP address '{value}'");
                throw new ArgumentException($"Invalid hostname or IP address: {value}");
            }
            case 2:
            {
                // HOST:PORT
                string host = parts[0];

                // Validate host
                if (!IsValidHost(host))
                {
                    Console.WriteLine($"Error: Invalid hostname or IP address '{host}'");
                    throw new ArgumentException($"Invalid hostname or IP address: {host}");
                }

                // Validate port
                if (int.TryParse(parts[1], out int port) && port is > 0 and <= 65535)
                {
                    return (host, port);
                }

                Console.WriteLine($"Error: Invalid port number '{parts[1]}'");
                throw new ArgumentException($"Invalid port number: {parts[1]}");

            }
            default:
                // Too many colons (e.g., host:port:extra)
                Console.WriteLine($"Error: Invalid connection string '{connectString}'");
                Console.WriteLine("Expected format: HOST:PORT or just PORT");
                throw new ArgumentException($"Invalid connection string format: {connectString}");
        }
    }

    /// <summary>
    /// Validates whether a string is a valid hostname or IP address.
    /// </summary>
    /// <param name="host">The hostname or IP address to validate.</param>
    /// <returns>True if the host is valid, false otherwise.</returns>
    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // Check if it's a valid IP address (IPv4 or IPv6)
        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        // Check if it's a valid DNS hostname
        // Uri.CheckHostName validates DNS naming rules
        UriHostNameType hostType = Uri.CheckHostName(host);
        return hostType == UriHostNameType.Dns;
    }

    /// <summary>
    /// Handles and displays errors.
    /// </summary>
    /// <param name="ex">The exception to display.</param>
    /// <param name="clientMode">True if running in client mode, false for standalone mode.</param>
    private void HandleError(Exception ex, bool clientMode) =>
        Console.WriteLine(ex);
}
