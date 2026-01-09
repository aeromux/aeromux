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
/// </summary>
/// <remarks>
/// When used as flag, defaults to empty string which ParseConnectionString interprets as localhost:30005.
/// Implements IFlagValue to enable Spectre.Console.Cli's flag parsing behavior.
/// </remarks>
public sealed class OptionalConnectionString : IFlagValue
{
    private string _value = string.Empty;
    private bool _isSet;

    /// <summary>
    /// Gets or sets the connection string value.
    /// </summary>
    public object? Value
    {
        get => _value;
        set => _value = value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets whether the flag was explicitly set by the user.
    /// </summary>
    public bool IsSet
    {
        get => _isSet;
        set => _isSet = value;
    }

    /// <summary>
    /// Gets the underlying type of the value (always string).
    /// </summary>
    public Type Type => typeof(string);

    /// <summary>
    /// Creates an instance for flag usage without a value (--connect).
    /// </summary>
    /// <returns>An OptionalConnectionString with empty value and IsSet=true.</returns>
    public static OptionalConnectionString FromFlag() =>
        new() { _value = string.Empty, _isSet = true };

    /// <summary>
    /// Creates an instance with a specific connection string value (--connect HOST:PORT).
    /// </summary>
    /// <param name="value">The connection string value.</param>
    /// <returns>An OptionalConnectionString with the specified value and IsSet=true.</returns>
    public static OptionalConnectionString FromValue(string value) =>
        new() { _value = value, _isSet = true };

    /// <summary>
    /// Implicitly converts OptionalConnectionString to string for convenient usage.
    /// </summary>
    /// <param name="connection">The OptionalConnectionString to convert.</param>
    /// <returns>The connection string value, or null if connection is null.</returns>
    public static implicit operator string?(OptionalConnectionString? connection) =>
        connection?._value;
}

/// <summary>
/// Settings for the Live command.
/// </summary>
public sealed class LiveSettings : GlobalSettings
{
    /// <summary>
    /// Gets or sets whether to run in standalone mode with direct RTL-SDR access.
    /// </summary>
    [CommandOption("--standalone")]
    [Description("Run in standalone mode (process RTL-SDR directly)")]
    public bool Standalone { get; set; }

    /// <summary>
    /// Gets or sets the connection string for Beast-compatible source.
    /// </summary>
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
        if (settings is { Standalone: true, Connect.IsSet: true })
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
            // DeviceStream implements IAsyncDisposable to ensure RTL-SDR device cleanup
            // Async using ensures StopAsync is called even on exceptions, releasing hardware
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
            // BeastStream includes IcaoConfidenceTracker to filter noise from real aircraft
            // TrackingConfig.ConfidenceLevel determines how many messages required to accept ICAO
            // This prevents displaying spurious aircraft from corrupted frames or interference
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
        int detailViewSelectedRow = 0;  // Track scroll position in detail view
        List<DetailRow>? currentDetailRows = null;  // Store for keyboard navigation
        DistanceUnit distanceUnit = DistanceUnit.Miles;
        AltitudeUnit altitudeUnit = AltitudeUnit.Feet;
        SpeedUnit speedUnit = SpeedUnit.Knots;

        // Track terminal size for resize detection (workaround for Spectre.Console bug)
        // Spectre.Console Live display gets corrupted on resize, requiring restart
        // Store size at Live display start, check each iteration for changes
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

                            // Track selection by ICAO (not row index) to maintain selection stability
                            // Aircraft list changes every second (timeouts, new aircraft), so row indices shift
                            // ICAO remains constant for each aircraft, ensuring selection persists across refreshes
                            if (selectedIcao != null)
                            {
                                int foundIndex = sortedAircraft.FindIndex(a => a.Identification.ICAO == selectedIcao);
                                if (foundIndex >= 0)
                                {
                                    selectedRow = foundIndex;
                                }
                                else
                                {
                                    // Selected aircraft expired (timeout), select first available
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
                                (Table detailTable, List<DetailRow> detailRows) = BuildDetailView(
                                    sortedAircraft[selectedRow],
                                    distanceUnit,
                                    altitudeUnit,
                                    speedUnit,
                                    receiverConfig,
                                    detailViewSelectedRow);

                                currentDetailRows = detailRows;  // Store for keyboard handling
                                ctx.UpdateTarget(detailTable);
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
                                        if (currentDetailRows != null)
                                        {
                                            if (!HandleDetailKeyboard(key, currentDetailRows, ref detailViewSelectedRow, ref showingDetails))
                                            {
                                                shouldQuit = true;
                                                return;  // Quit
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Table view keyboard handling
                                        if (!HandleTableKeyboard(key, sortedAircraft, ref selectedIcao, ref selectedRow, ref showingDetails, ref detailViewSelectedRow, ref distanceUnit, ref altitudeUnit, ref speedUnit))
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
    /// <param name="detailViewSelectedRow">Detail view selected row index (reset to 1 when entering detail view).</param>
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
        ref int detailViewSelectedRow,
        ref DistanceUnit distanceUnit,
        ref AltitudeUnit altitudeUnit,
        ref SpeedUnit speedUnit)
    {
        // Calculate available viewport rows based on terminal height
        // Layout: title (0) + table header (1) + data rows + footer (2) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 0;        // No title row in main table
        const int footerLines = 2;        // Two-line footer with navigation hints
        const int tableHeaderLines = 1;   // Column header row
        const int padding = 3;            // Border and spacing overhead

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
                    detailViewSelectedRow = 1;  // Start at first data row (row 0 is always a header)
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
    /// Handles keyboard input for detail view.
    /// </summary>
    /// <param name="key">The console key information.</param>
    /// <param name="allRows">All detail rows (needed to check for section headers).</param>
    /// <param name="selectedRow">Currently selected row (updated on navigation).</param>
    /// <param name="showingDetails">Detail view toggle (set false when Escape pressed).</param>
    /// <returns>False if user pressed Q (quit), true to continue.</returns>
    private bool HandleDetailKeyboard(
        ConsoleKeyInfo key,
        List<DetailRow> allRows,
        ref int selectedRow,
        ref bool showingDetails)
    {
        int totalRows = allRows.Count;

        // Calculate available viewport rows based on terminal height
        // Layout: title (1) + table header (1) + data rows + footer (2) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 1;        // Title row with ICAO
        const int footerLines = 2;        // Two-line footer with navigation hints
        const int tableHeaderLines = 1;   // Column header row
        const int padding = 3;            // Border and spacing overhead
        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                // Move to previous non-header row
                int prevRow = selectedRow - 1;
                while (prevRow >= 0 && allRows[prevRow].IsSectionHeader)
                {
                    prevRow--;
                }
                // Only update if we found a valid non-header row
                if (prevRow >= 0)
                {
                    selectedRow = prevRow;
                }
                break;

            case ConsoleKey.DownArrow:
                // Move to next non-header row
                int nextRow = selectedRow + 1;
                while (nextRow < totalRows && allRows[nextRow].IsSectionHeader)
                {
                    nextRow++;
                }
                // Only update if we found a valid non-header row
                if (nextRow < totalRows)
                {
                    selectedRow = nextRow;
                }
                break;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.PageUp:
                // Jump up by viewport, then find nearest non-header
                int targetUp = Math.Max(0, selectedRow - availableRows);
                while (targetUp < selectedRow && allRows[targetUp].IsSectionHeader)
                {
                    targetUp++;
                }
                // Only update if we found a valid non-header row before current position
                if (targetUp < selectedRow && !allRows[targetUp].IsSectionHeader)
                {
                    selectedRow = targetUp;
                }
                break;

            case ConsoleKey.RightArrow:
            case ConsoleKey.PageDown:
                // Jump down by viewport, then find nearest non-header
                int targetDown = Math.Min(totalRows - 1, selectedRow + availableRows);
                while (targetDown > selectedRow && allRows[targetDown].IsSectionHeader)
                {
                    targetDown--;
                }
                // Only update if we found a valid non-header row after current position
                if (targetDown > selectedRow && !allRows[targetDown].IsSectionHeader)
                {
                    selectedRow = targetDown;
                }
                break;

            case ConsoleKey.Escape:
                showingDetails = false;
                break;

            case ConsoleKey.Q:
                return false;  // Quit
        }

        return true;  // Continue
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
        // Calculate available viewport rows based on terminal height
        // Layout: title (1) + table header (1) + data rows + footer (2) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 1;        // Title row "AIRCRAFT LIST - Aeromux"
        const int footerLines = 2;        // Two-line footer with navigation hints
        const int tableHeaderLines = 1;   // Column header row
        const int padding = 3;            // Border and spacing overhead

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);

        int viewportStart;
        int viewportEnd;

        // If all aircraft fit on screen, don't apply viewport scrolling
        if (sortedAircraft.Count <= availableRows)
        {
            viewportStart = 0;
            viewportEnd = sortedAircraft.Count;
        }
        else
        {
            // Apply viewport scrolling logic when aircraft exceed available height
            int halfViewport = availableRows / 2;
            viewportStart = Math.Max(0, selectedRow - halfViewport);
            viewportEnd = Math.Min(sortedAircraft.Count, viewportStart + availableRows);

            // Adjust if at end of list (show full viewport)
            if (viewportEnd - viewportStart < availableRows)
            {
                viewportStart = Math.Max(0, viewportEnd - availableRows);
            }
        }

        // Calculate scrollbar parameters
        int totalAircraft = sortedAircraft.Count;
        bool showScrollbar = totalAircraft > availableRows;
        int thumbSize = 1;
        int thumbStart = 0;

        if (showScrollbar)
        {
            // Thumb size: proportional to viewport/total ratio (minimum 1 row)
            thumbSize = Math.Max(1, (int)Math.Floor((double)availableRows * availableRows / totalAircraft));

            // Thumb position: maps viewport position to track position
            int scrollableRange = totalAircraft - availableRows;
            if (scrollableRange > 0)
            {
                double scrollProgress = (double)viewportStart / scrollableRange;
                thumbStart = (int)Math.Floor(scrollProgress * (availableRows - thumbSize));
            }
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
        table.AddColumn("[bold] [/]", col => col.Width(1).Centered().NoWrap());

        // Render aircraft in viewport
        for (int i = viewportStart; i < viewportEnd; i++)
        {
            Aircraft aircraft = sortedAircraft[i];
            TimeSpan age = DateTime.UtcNow - aircraft.Status.LastSeen;
            bool isSelected = i == selectedRow;

            // Calculate scrollbar character for this row
            string scrollbarChar;
            if (showScrollbar)
            {
                int rowInViewport = i - viewportStart;
                if (rowInViewport >= thumbStart && rowInViewport < thumbStart + thumbSize)
                {
                    scrollbarChar = "█"; // Thumb (full block)
                }
                else
                {
                    scrollbarChar = "░"; // Track (light shade)
                }
            }
            else
            {
                scrollbarChar = "░"; // Full track (all items fit - no scrolling needed)
            }

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

            // Use dBFS for table display as it provides intuitive signal quality indication
            string signal = aircraft.Status.SignalStrengthDecibel.HasValue
                ? $"{aircraft.Status.SignalStrengthDecibel.Value:F1}"
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
                    $"[black on white]{lastSeenNumber}[/]",
                    scrollbarChar); // Keep scrollbar with normal styling for visibility
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
                    lastSeenNumber,
                    scrollbarChar);
            }
        }

        // Fill remaining rows with empty content to always use full terminal height
        int rowsRendered = viewportEnd - viewportStart;
        int emptyRowsNeeded = availableRows - rowsRendered;
        for (int i = 0; i < emptyRowsNeeded; i++)
        {
            table.AddRow("", "", "", "", "", "", "", "", "", "░");
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
        // Table width is 104, but caption appears inside borders, so usable width is 100
        int usableWidth = 100;

        string footerRow1 = footerRow1Left + new string(' ', Math.Max(1, usableWidth - footerRow1Left.Length + 27 - footerRow1Right.Length + 27)) + footerRow1Right;
        string footerRow2 = footerRow2Left + new string(' ', Math.Max(1, usableWidth - footerRow2Left.Length + 18 - footerRow2Right.Length + 27)) + footerRow2Right;

        string footer = footerRow1 + "\n" + footerRow2;

        table.Caption(footer, new Style(foreground: Color.Grey));

        // Return table for Live display
        return table;
    }

    /// <summary>
    /// Represents a row in the aircraft detail view.
    /// </summary>
    private record DetailRow(string Field, string Value, bool IsSectionHeader = false);

    /// <summary>
    /// Builds detailed view for a single aircraft as a table (matching main table width).
    /// </summary>
    /// <param name="aircraft">Aircraft to display detailed information for.</param>
    /// <param name="distanceUnit">Unit to display distances (miles or kilometers).</param>
    /// <param name="altitudeUnit">Unit to display altitudes (feet or meters).</param>
    /// <param name="speedUnit">Unit to display speeds (knots, km/h, or mph).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <param name="selectedRow">Currently selected row index for highlighting (0-based, validated to skip headers).</param>
    /// <returns>Spectre.Console Table with detailed aircraft information and fixed 120-character width.</returns>
    private (Table, List<DetailRow>) BuildDetailView(
        Aircraft aircraft,
        DistanceUnit distanceUnit,
        AltitudeUnit altitudeUnit,
        SpeedUnit speedUnit,
        ReceiverConfig? receiverConfig,
        int selectedRow = 0)
    {
        // Calculate available viewport rows based on terminal height
        // Layout: title (1) + table header (1) + data rows + footer (2) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 1;        // Title row with ICAO
        const int footerLines = 2;        // Two-line footer with navigation hints
        const int tableHeaderLines = 1;   // Column header row (Field | Value | Scrollbar)
        const int padding = 3;            // Border and spacing overhead

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);

        // Collect all detail rows first (for viewport windowing)
        var allRows = new List<DetailRow>();

        // === IDENTIFICATION ===
        allRows.Add(new DetailRow("[bold]=== IDENTIFICATION =====================[/]", "", IsSectionHeader: true));
        allRows.Add(new DetailRow("ICAO Address", aircraft.Identification.ICAO));
        allRows.Add(new DetailRow("Callsign", aircraft.Identification.Callsign ?? "N/A"));
        allRows.Add(new DetailRow("Category", aircraft.Identification.Category?.ToString() ?? "N/A"));
        allRows.Add(new DetailRow("Squawk", aircraft.Identification.Squawk ?? "N/A"));
        allRows.Add(new DetailRow("Emergency", aircraft.Identification.EmergencyState.ToString()));
        allRows.Add(new DetailRow("Flight Status", aircraft.Identification.FlightStatus?.ToString() ?? "N/A"));
        allRows.Add(new DetailRow("ADS-B Version", aircraft.Identification.Version?.ToString() ?? "N/A"));
        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === STATUS ===
        allRows.Add(new DetailRow("[bold]=== STATUS =============================[/]", "", IsSectionHeader: true));
        allRows.Add(new DetailRow("First Seen", aircraft.Status.FirstSeen.ToString("HH:mm:ss")));
        allRows.Add(new DetailRow("Last Seen", $"{(DateTime.UtcNow - aircraft.Status.LastSeen).TotalSeconds:F1}s ago"));
        allRows.Add(new DetailRow("Total Messages", aircraft.Status.TotalMessages.ToString()));
        allRows.Add(new DetailRow("Position Msgs", aircraft.Status.PositionMessages.ToString()));
        allRows.Add(new DetailRow("Velocity Msgs", aircraft.Status.VelocityMessages.ToString()));
        allRows.Add(new DetailRow("ID Messages", aircraft.Status.IdentificationMessages.ToString()));

        string signalStrength = aircraft.Status is { SignalStrength: not null, SignalStrengthDecibel: not null }
            ? $"{aircraft.Status.SignalStrengthDecibel.Value:F1} dBFS (RSSI: {aircraft.Status.SignalStrength.Value:F1})"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Signal Strength", signalStrength));
        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === POSITION ===
        allRows.Add(new DetailRow("[bold]=== POSITION ===========================[/]", "", IsSectionHeader: true));

        string latitude = aircraft.Position.Coordinate != null
            ? $"{aircraft.Position.Coordinate.Latitude:F6}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Latitude", latitude));

        string longitude = aircraft.Position.Coordinate != null
            ? $"{aircraft.Position.Coordinate.Longitude:F6}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Longitude", longitude));

        // Distance
        string distance;
        if (aircraft.Position.Coordinate != null && receiverConfig?.Latitude.HasValue == true && receiverConfig?.Longitude.HasValue == true)
        {
            var receiverLocation = new GeographicCoordinate(
                receiverConfig.Latitude.Value,
                receiverConfig.Longitude.Value);

            double dist = distanceUnit == DistanceUnit.Miles
                ? receiverLocation.DistanceToMiles(aircraft.Position.Coordinate)
                : receiverLocation.DistanceToKilometers(aircraft.Position.Coordinate);

            string unitLabel = distanceUnit == DistanceUnit.Miles ? "mi" : "km";
            distance = $"{dist:F1} {unitLabel}";
        }
        else if (aircraft.Position.Coordinate != null)
        {
            distance = "N/A (no receiver location)";
        }
        else
        {
            distance = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Distance", distance));

        // Barometric altitude
        string baroAlt;
        if (aircraft.Position.BarometricAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                baroAlt = $"{aircraft.Position.BarometricAltitude.Feet:F0} ft";
            }
            else
            {
                double meters = aircraft.Position.BarometricAltitude.Feet * 0.3048;
                baroAlt = $"{meters:F0} m";
            }
        }
        else
        {
            baroAlt = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Barometric Altitude", baroAlt));

        // Geometric altitude
        string geoAlt;
        if (aircraft.Position.GeometricAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                geoAlt = $"{aircraft.Position.GeometricAltitude.Feet:F0} ft";
            }
            else
            {
                double meters = aircraft.Position.GeometricAltitude.Feet * 0.3048;
                geoAlt = $"{meters:F0} m";
            }
        }
        else
        {
            geoAlt = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Geo Altitude", geoAlt));

        allRows.Add(new DetailRow("On Ground", aircraft.Position.IsOnGround.ToString()));

        // Movement category (ground only)
        string movementCategory = aircraft.Position.MovementCategory?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Movement Category", movementCategory));

        // Antenna configuration
        string antenna = aircraft.Position.Antenna.HasValue
            ? (aircraft.Position.Antenna.Value == AntennaFlag.SingleAntenna ? "Single Antenna" : "Diversity Antenna")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Antenna", antenna));

        allRows.Add(new DetailRow("NACp", aircraft.Position.NACp?.ToString() ?? "N/A (no data yet)"));

        // NICbaro - barometric altitude integrity
        string nicBaro = aircraft.Position.NICbaro.HasValue
            ? (aircraft.Position.NICbaro.Value ? "Cross-checked" : "Not cross-checked")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NICbaro", nicBaro));

        allRows.Add(new DetailRow("SIL", aircraft.Position.SIL?.ToString() ?? "N/A (no data yet)"));

        // Last position update timestamp
        string posLastUpdate = aircraft.Position.LastUpdate.HasValue
            ? aircraft.Position.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", posLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === VELOCITY ===
        allRows.Add(new DetailRow("[bold]=== VELOCITY ===========================[/]", "", IsSectionHeader: true));

        Velocity? speedValue = aircraft.Velocity.Speed ?? aircraft.Velocity.GroundSpeed;
        string speed;
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

            speed = $"{displaySpeed:F0} {unitLabel} ({velocityTypeStr})";
        }
        else
        {
            speed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Speed", speed));

        string heading = aircraft.Velocity.Heading != null
            ? $"{aircraft.Velocity.Heading:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Heading", heading));

        string track = aircraft.Velocity.Track != null
            ? $"{aircraft.Velocity.Track:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track", track));

        string groundTrack = aircraft.Velocity.GroundTrack != null
            ? $"{aircraft.Velocity.GroundTrack:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Ground Track", groundTrack));

        string verticalRate = aircraft.Velocity.VerticalRate != null
            ? $"{aircraft.Velocity.VerticalRate:F0} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Vertical Rate", verticalRate));

        // Indicated Airspeed (IAS from Comm-B)
        string indicatedAirspeed;
        if (aircraft.Velocity.IndicatedAirspeed != null)
        {
            double displayIAS = speedUnit switch
            {
                SpeedUnit.Knots => aircraft.Velocity.IndicatedAirspeed.Knots,
                SpeedUnit.KilometersPerHour => aircraft.Velocity.IndicatedAirspeed.Knots * 1.852,
                SpeedUnit.MilesPerHour => aircraft.Velocity.IndicatedAirspeed.Knots * 1.15078,
                _ => aircraft.Velocity.IndicatedAirspeed.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            indicatedAirspeed = $"{displayIAS:F0} {unitLabel} (IAS)";
        }
        else
        {
            indicatedAirspeed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Indicated Airspeed", indicatedAirspeed));

        // True Airspeed (TAS from Comm-B)
        string trueAirspeed;
        if (aircraft.Velocity.TrueAirspeed != null)
        {
            double displayTAS = speedUnit switch
            {
                SpeedUnit.Knots => aircraft.Velocity.TrueAirspeed.Knots,
                SpeedUnit.KilometersPerHour => aircraft.Velocity.TrueAirspeed.Knots * 1.852,
                SpeedUnit.MilesPerHour => aircraft.Velocity.TrueAirspeed.Knots * 1.15078,
                _ => aircraft.Velocity.TrueAirspeed.Knots
            };

            string unitLabel = speedUnit switch
            {
                SpeedUnit.Knots => "kts",
                SpeedUnit.KilometersPerHour => "km/h",
                SpeedUnit.MilesPerHour => "mph",
                _ => "kts"
            };

            trueAirspeed = $"{displayTAS:F0} {unitLabel} (TAS)";
        }
        else
        {
            trueAirspeed = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("True Airspeed", trueAirspeed));

        // Navigation Accuracy Category for Velocity
        string nacv = aircraft.Velocity.NACv?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACv", nacv));

        // Last velocity update timestamp
        string velLastUpdate = aircraft.Velocity.LastUpdate.HasValue
            ? aircraft.Velocity.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", velLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === AUTOPILOT ===
        allRows.Add(new DetailRow("[bold]=== AUTOPILOT ==========================[/]", "", IsSectionHeader: true));

        // Selected Altitude
        string selectedAltitude;
        if (aircraft.Autopilot?.SelectedAltitude != null)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                selectedAltitude = $"{aircraft.Autopilot.SelectedAltitude.Feet:F0} ft";
            }
            else
            {
                double meters = aircraft.Autopilot.SelectedAltitude.Feet * 0.3048;
                selectedAltitude = $"{meters:F0} m";
            }
        }
        else
        {
            selectedAltitude = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Selected Altitude", selectedAltitude));

        // Altitude Source
        string altitudeSource = aircraft.Autopilot?.AltitudeSource?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Altitude Source", altitudeSource));

        // Selected Heading
        string selectedHeading = aircraft.Autopilot?.SelectedHeading.HasValue == true
            ? $"{aircraft.Autopilot.SelectedHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Selected Heading", selectedHeading));

        // Barometric Pressure Setting
        string barometricPressure = aircraft.Autopilot?.BarometricPressureSetting.HasValue == true
            ? $"{aircraft.Autopilot.BarometricPressureSetting.Value:F1} hPa"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Barometric Pressure", barometricPressure));

        // Vertical Mode
        string verticalMode = aircraft.Autopilot?.VerticalMode?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Vertical Mode", verticalMode));

        // Horizontal Mode
        string horizontalMode = aircraft.Autopilot?.HorizontalMode?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Horizontal Mode", horizontalMode));

        // Autopilot Engaged
        string autopilotEngaged = aircraft.Autopilot?.AutopilotEngaged.HasValue == true
            ? (aircraft.Autopilot.AutopilotEngaged.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Autopilot Engaged", autopilotEngaged));

        // VNAV Mode
        string vnavMode = aircraft.Autopilot?.VNAVMode.HasValue == true
            ? (aircraft.Autopilot.VNAVMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("VNAV Mode", vnavMode));

        // LNAV Mode
        string lnavMode = aircraft.Autopilot?.LNAVMode.HasValue == true
            ? (aircraft.Autopilot.LNAVMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("LNAV Mode", lnavMode));

        // Altitude Hold Mode
        string altitudeHoldMode = aircraft.Autopilot?.AltitudeHoldMode.HasValue == true
            ? (aircraft.Autopilot.AltitudeHoldMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Altitude Hold", altitudeHoldMode));

        // Approach Mode
        string approachMode = aircraft.Autopilot?.ApproachMode.HasValue == true
            ? (aircraft.Autopilot.ApproachMode.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Approach Mode", approachMode));

        // Last autopilot update timestamp
        string autopilotLastUpdate = aircraft.Autopilot?.LastUpdate.HasValue == true
            ? aircraft.Autopilot.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", autopilotLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === ACAS/TCAS ===
        allRows.Add(new DetailRow("[bold]=== ACAS/TCAS ==========================[/]", "", IsSectionHeader: true));

        // TCAS Operational
        string tcasOperational = aircraft.Acas?.TCASOperational.HasValue == true
            ? (aircraft.Acas.TCASOperational.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS Operational", tcasOperational));

        // Sensitivity Level
        string sensitivityLevel = aircraft.Acas?.SensitivityLevel.HasValue == true
            ? (aircraft.Acas.SensitivityLevel.Value == 0 ? "0 (Inoperative)" : aircraft.Acas.SensitivityLevel.Value.ToString())
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Sensitivity Level", sensitivityLevel));

        // Cross-Link Capability
        string crossLinkCapability = aircraft.Acas?.CrossLinkCapability.HasValue == true
            ? (aircraft.Acas.CrossLinkCapability.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Cross-Link Capability", crossLinkCapability));

        // Reply Information
        string replyInformation = aircraft.Acas?.ReplyInformation?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Reply Information", replyInformation));

        // TCAS RA Active
        string tcasRaActive = aircraft.Acas?.TCASRAActive.HasValue == true
            ? (aircraft.Acas.TCASRAActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS RA Active", tcasRaActive));

        // RA Terminated
        string raTerminated = aircraft.Acas?.ResolutionAdvisoryTerminated.HasValue == true
            ? (aircraft.Acas.ResolutionAdvisoryTerminated.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RA Terminated", raTerminated));

        // Multiple Threats
        string multipleThreats = aircraft.Acas?.MultipleThreatEncounter.HasValue == true
            ? (aircraft.Acas.MultipleThreatEncounter.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Multiple Threats", multipleThreats));

        // RAC: Not Below
        string racNotBelow = aircraft.Acas?.RACNotBelow.HasValue == true
            ? (aircraft.Acas.RACNotBelow.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Below", racNotBelow));

        // RAC: Not Above
        string racNotAbove = aircraft.Acas?.RACNotAbove.HasValue == true
            ? (aircraft.Acas.RACNotAbove.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Above", racNotAbove));

        // RAC: Not Left
        string racNotLeft = aircraft.Acas?.RACNotLeft.HasValue == true
            ? (aircraft.Acas.RACNotLeft.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Left", racNotLeft));

        // RAC: Not Right
        string racNotRight = aircraft.Acas?.RACNotRight.HasValue == true
            ? (aircraft.Acas.RACNotRight.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("RAC: Not Right", racNotRight));

        // Last ACAS update timestamp
        string acasLastUpdate = aircraft.Acas?.LastUpdate.HasValue == true
            ? aircraft.Acas.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", acasLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === FLIGHT DYNAMICS ===
        allRows.Add(new DetailRow("[bold]=== FLIGHT DYNAMICS ====================[/]", "", IsSectionHeader: true));

        // Roll Angle
        string rollAngle = aircraft.FlightDynamics?.RollAngle.HasValue == true
            ? $"{aircraft.FlightDynamics.RollAngle.Value:F2}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Roll Angle", rollAngle));

        // Magnetic Heading
        string magneticHeading = aircraft.FlightDynamics?.MagneticHeading.HasValue == true
            ? $"{aircraft.FlightDynamics.MagneticHeading.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Magnetic Heading", magneticHeading));

        // Barometric Vertical Rate
        string baroVerticalRate = aircraft.FlightDynamics?.BarometricVerticalRate.HasValue == true
            ? $"{aircraft.FlightDynamics.BarometricVerticalRate.Value:+0;-#} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Barometric Vert Rate", baroVerticalRate));

        // Inertial Vertical Rate
        string inertialVerticalRate = aircraft.FlightDynamics?.InertialVerticalRate.HasValue == true
            ? $"{aircraft.FlightDynamics.InertialVerticalRate.Value:+0;-#} ft/min"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Inertial Vert Rate", inertialVerticalRate));

        // Mach Number
        string machNumber = aircraft.FlightDynamics?.MachNumber.HasValue == true
            ? $"M {aircraft.FlightDynamics.MachNumber.Value:F3}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Mach Number", machNumber));

        // Track Rate
        string trackRate = aircraft.FlightDynamics?.TrackRate.HasValue == true
            ? $"{aircraft.FlightDynamics.TrackRate.Value:+0.00;-0.00} °/s"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Track Rate", trackRate));

        // Last flight dynamics update timestamp
        string flightDynamicsLastUpdate = aircraft.FlightDynamics?.LastUpdate.HasValue == true
            ? aircraft.FlightDynamics.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", flightDynamicsLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === METEOROLOGY ===
        allRows.Add(new DetailRow("[bold]=== METEOROLOGY ========================[/]", "", IsSectionHeader: true));

        // Wind Speed
        string windSpeed = aircraft.Meteo?.WindSpeed.HasValue == true
            ? $"{aircraft.Meteo.WindSpeed.Value} kts"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Wind Speed", windSpeed));

        // Wind Direction
        string windDirection = aircraft.Meteo?.WindDirection.HasValue == true
            ? $"{aircraft.Meteo.WindDirection.Value:F1}°"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Wind Direction", windDirection));

        // Static Air Temperature
        string staticAirTemp = aircraft.Meteo?.StaticAirTemperature.HasValue == true
            ? $"{aircraft.Meteo.StaticAirTemperature.Value:F1} °C"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Static Air Temp", staticAirTemp));

        // Pressure
        string pressure = aircraft.Meteo?.Pressure.HasValue == true
            ? $"{aircraft.Meteo.Pressure.Value:F1} hPa"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Pressure", pressure));

        // Turbulence
        string turbulence = aircraft.Meteo?.Turbulence?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Turbulence", turbulence));

        // Wind Shear
        string windShear = aircraft.Meteo?.WindShear?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Wind Shear", windShear));

        // Microburst
        string microburst = aircraft.Meteo?.Microburst?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Microburst", microburst));

        // Icing
        string icing = aircraft.Meteo?.Icing?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Icing", icing));

        // Wake Vortex
        string wakeVortex = aircraft.Meteo?.WakeVortex?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Wake Vortex", wakeVortex));

        // Radio Height
        string radioHeight;
        if (aircraft.Meteo?.RadioHeight.HasValue == true)
        {
            if (altitudeUnit == AltitudeUnit.Feet)
            {
                radioHeight = $"{aircraft.Meteo.RadioHeight.Value} ft";
            }
            else
            {
                double meters = aircraft.Meteo.RadioHeight.Value * 0.3048;
                radioHeight = $"{(int)meters} m";
            }
        }
        else
        {
            radioHeight = "N/A (no data yet)";
        }
        allRows.Add(new DetailRow("Radio Height", radioHeight));

        // Figure of Merit
        string figureOfMerit = aircraft.Meteo?.FigureOfMerit.HasValue == true
            ? aircraft.Meteo.FigureOfMerit.Value.ToString()
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Figure of Merit", figureOfMerit));

        // Humidity
        string humidity = aircraft.Meteo?.Humidity.HasValue == true
            ? $"{aircraft.Meteo.Humidity.Value:F1}%"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Humidity", humidity));

        // Last meteorological update timestamp
        string meteoLastUpdate = aircraft.Meteo?.LastUpdate.HasValue == true
            ? aircraft.Meteo.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", meteoLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === CAPABILITIES ===
        allRows.Add(new DetailRow("[bold]=== CAPABILITIES =======================[/]", "", IsSectionHeader: true));

        // Transponder Level
        string transponderLevel = aircraft.Capabilities?.TransponderLevel?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Transponder Level", transponderLevel));

        // TCAS Capability
        string tcasCapability = aircraft.Capabilities?.TCASCapability.HasValue == true
            ? (aircraft.Capabilities.TCASCapability.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS Capability", tcasCapability));

        // CDTI Available
        string cdtiAvailable = aircraft.Capabilities?.CockpitDisplayTraffic.HasValue == true
            ? (aircraft.Capabilities.CockpitDisplayTraffic.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("CDTI Available", cdtiAvailable));

        // ADS-B 1090ES
        string adsb1090es = aircraft.Capabilities?.ADSB1090ES.HasValue == true
            ? (aircraft.Capabilities.ADSB1090ES.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("ADS-B 1090ES", adsb1090es));

        // Air Referenced Velocity
        string airReferencedVelocity = aircraft.Capabilities?.AirReferencedVelocity.HasValue == true
            ? (aircraft.Capabilities.AirReferencedVelocity.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Air Referenced Velocity", airReferencedVelocity));

        // Target State Reporting
        string targetStateReporting = aircraft.Capabilities?.TargetStateReporting.HasValue == true
            ? (aircraft.Capabilities.TargetStateReporting.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Target State Reporting", targetStateReporting));

        // Trajectory Change Level
        string trajectoryChangeLevel = aircraft.Capabilities?.TrajectoryChangeLevel?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Trajectory Change Level", trajectoryChangeLevel));

        // UAT 978 Support
        string uat978Support = aircraft.Capabilities?.UAT978Support.HasValue == true
            ? (aircraft.Capabilities.UAT978Support.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("UAT 978 Support", uat978Support));

        // Position Offset Applied
        string positionOffsetApplied = aircraft.Capabilities?.PositionOffsetApplied.HasValue == true
            ? (aircraft.Capabilities.PositionOffsetApplied.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Position Offset Applied", positionOffsetApplied));

        // Low Power 1090ES
        string lowPower1090ES = aircraft.Capabilities?.LowPower1090ES.HasValue == true
            ? (aircraft.Capabilities.LowPower1090ES.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Low Power 1090ES", lowPower1090ES));

        // NACv (from Capabilities)
        string capabilitiesNacv = aircraft.Capabilities?.NACv?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACv", capabilitiesNacv));

        // NIC Supplement C
        string nicSupplementC = aircraft.Capabilities?.NICSupplementC.HasValue == true
            ? (aircraft.Capabilities.NICSupplementC.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NIC Supplement C", nicSupplementC));

        // Data Link Capability
        string dataLinkCapability = aircraft.Capabilities?.DataLinkCapabilityBits.HasValue == true
            ? $"0x{aircraft.Capabilities.DataLinkCapabilityBits.Value:X4}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Data Link Capability", dataLinkCapability));

        // Supported BDS Registers
        string supportedBdsRegisters = aircraft.Capabilities?.SupportedBDSRegisters.HasValue == true
            ? $"0x{aircraft.Capabilities.SupportedBDSRegisters.Value:X14}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Supported BDS Registers", supportedBdsRegisters));

        // Aircraft Dimensions
        string dimensions = aircraft.Capabilities?.Dimensions?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Aircraft Dimensions", dimensions));

        // Last capabilities update timestamp
        string capabilitiesLastUpdate = aircraft.Capabilities?.LastUpdate.HasValue == true
            ? aircraft.Capabilities.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", capabilitiesLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === DATA QUALITY ===
        allRows.Add(new DetailRow("[bold]=== DATA QUALITY =======================[/]", "", IsSectionHeader: true));

        // Geometric Vertical Accuracy
        string geometricVerticalAccuracy = aircraft.DataQuality?.GeometricVerticalAccuracy?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Geometric Vert Accuracy", geometricVerticalAccuracy));

        // NICbaro (TC 29)
        string nicBaroTc29 = aircraft.DataQuality?.NICbaro_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NICbaro (TC 29)", nicBaroTc29));

        // NIC Supplement A
        string nicSupplementA = aircraft.DataQuality?.NICSupplementA.HasValue == true
            ? (aircraft.DataQuality.NICSupplementA.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("NIC Supplement A", nicSupplementA));

        // SIL Supplement
        string silSupplement = aircraft.DataQuality?.SILSupplement?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("SIL Supplement", silSupplement));

        // SIL (TC 29)
        string silTc29 = aircraft.DataQuality?.SIL_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("SIL (TC 29)", silTc29));

        // NACp (TC 29)
        string nacpTc29 = aircraft.DataQuality?.NACp_TC29?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("NACp (TC 29)", nacpTc29));

        // Horizontal Reference
        string horizontalReference = aircraft.DataQuality?.HorizontalReference?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Horizontal Reference", horizontalReference));

        // Heading Type
        string headingType = aircraft.DataQuality?.HeadingType?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("Heading Type", headingType));

        // Last data quality update timestamp
        string dataQualityLastUpdate = aircraft.DataQuality?.LastUpdate.HasValue == true
            ? aircraft.DataQuality.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", dataQualityLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // === OPERATIONAL MODE ===
        allRows.Add(new DetailRow("[bold]=== OPERATIONAL MODE ===================[/]", "", IsSectionHeader: true));

        // TCAS RA Active
        string opModeTcasRaActive = aircraft.OperationalMode?.TCASRAActive.HasValue == true
            ? (aircraft.OperationalMode.TCASRAActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("TCAS RA Active", opModeTcasRaActive));

        // IDENT Switch Active
        string identSwitchActive = aircraft.OperationalMode?.IdentSwitchActive.HasValue == true
            ? (aircraft.OperationalMode.IdentSwitchActive.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("IDENT Switch Active", identSwitchActive));

        // Receiving ATC Services
        string receivingAtcServices = aircraft.OperationalMode?.ReceivingATCServices.HasValue == true
            ? (aircraft.OperationalMode.ReceivingATCServices.Value ? "Yes" : "No")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Receiving ATC Services", receivingAtcServices));

        // Antenna Configuration
        string antennaConfiguration = aircraft.OperationalMode?.SingleAntenna.HasValue == true
            ? (aircraft.OperationalMode.SingleAntenna.Value == AntennaFlag.SingleAntenna ? "Single" : "Diversity")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Antenna Configuration", antennaConfiguration));

        // System Design Assurance
        string systemDesignAssurance = aircraft.OperationalMode?.SystemDesignAssurance?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("System Design Assurance", systemDesignAssurance));

        // GPS Lateral Offset
        string gpsLateralOffset = aircraft.OperationalMode?.GPSLateralOffset?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("GPS Lateral Offset", gpsLateralOffset));

        // GPS Longitudinal Offset
        string gpsLongitudinalOffset = aircraft.OperationalMode?.GPSLongitudinalOffset?.ToString() ?? "N/A (no data yet)";
        allRows.Add(new DetailRow("GPS Longitudinal Offset", gpsLongitudinalOffset));

        // Downlink Request
        string downlinkRequest = aircraft.OperationalMode?.DownlinkRequest.HasValue == true
            ? aircraft.OperationalMode.DownlinkRequest.Value.ToString()
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Downlink Request", downlinkRequest));

        // Utility Message
        string utilityMessage = aircraft.OperationalMode?.UtilityMessage.HasValue == true
            ? $"0x{aircraft.OperationalMode.UtilityMessage.Value:X2}"
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Utility Message", utilityMessage));

        // Last operational mode update timestamp
        string operationalModeLastUpdate = aircraft.OperationalMode?.LastUpdate.HasValue == true
            ? aircraft.OperationalMode.LastUpdate.Value.ToString("HH:mm:ss")
            : "N/A (no data yet)";
        allRows.Add(new DetailRow("Last Update", operationalModeLastUpdate));

        allRows.Add(new DetailRow("", "", IsSectionHeader: true));  // Empty separator (non-selectable)

        // Calculate viewport (same logic as BuildTable)
        int totalRows = allRows.Count;

        // Ensure selectedRow is not on a section header
        if (selectedRow < totalRows && allRows[selectedRow].IsSectionHeader)
        {
            // Find next non-header row
            int nextRow = selectedRow + 1;
            while (nextRow < totalRows && allRows[nextRow].IsSectionHeader)
            {
                nextRow++;
            }
            selectedRow = nextRow < totalRows ? nextRow : selectedRow;
        }

        int viewportStart;
        int viewportEnd;

        // If all rows fit on screen, don't apply viewport scrolling
        if (totalRows <= availableRows)
        {
            viewportStart = 0;
            viewportEnd = totalRows;
        }
        else
        {
            // Apply viewport scrolling logic when rows exceed available height
            int halfViewport = availableRows / 2;
            viewportStart = Math.Max(0, selectedRow - halfViewport);
            viewportEnd = Math.Min(totalRows, viewportStart + availableRows);

            // Adjust if at end of list
            if (viewportEnd - viewportStart < availableRows)
            {
                viewportStart = Math.Max(0, viewportEnd - availableRows);
            }
        }

        // Calculate scrollbar parameters
        bool showScrollbar = totalRows > availableRows;
        int thumbSize = 1;
        int thumbStart = 0;

        if (showScrollbar)
        {
            thumbSize = Math.Max(1, (int)Math.Floor((double)availableRows * availableRows / totalRows));
            int scrollableRange = totalRows - availableRows;
            if (scrollableRange > 0)
            {
                double scrollProgress = (double)viewportStart / scrollableRange;
                thumbStart = (int)Math.Floor(scrollProgress * (availableRows - thumbSize));
            }
        }

        // Create table with scrollbar column
        Table table = new Table()
            .Border(TableBorder.Square)
            .Title($"AIRCRAFT DETAIL ({aircraft.Identification.ICAO}) - Aeromux", new Style(decoration:Decoration.Bold))
            .AddColumn(new TableColumn("[bold]Field[/]").Width(40).NoWrap().PadLeft(1).PadRight(1))
            .AddColumn(new TableColumn("[bold]Value[/]").Width(53).NoWrap().PadLeft(1).PadRight(1))
            .AddColumn(new TableColumn("[bold] [/]").Width(1).Centered().NoWrap());

        // Render rows in viewport
        for (int i = viewportStart; i < viewportEnd; i++)
        {
            DetailRow row = allRows[i];
            bool isSelected = i == selectedRow;

            // Calculate scrollbar character for this row
            string scrollbarChar;
            if (showScrollbar)
            {
                int rowInViewport = i - viewportStart;
                scrollbarChar = (rowInViewport >= thumbStart && rowInViewport < thumbStart + thumbSize)
                    ? "█" : "░";
            }
            else
            {
                scrollbarChar = "░";  // Always show track
            }

            // Apply highlighting to selected row (but not scrollbar or section headers)
            if (isSelected && !row.IsSectionHeader)
            {
                table.AddRow(
                    $"[black on white]{row.Field,-40}[/]",
                    $"[black on white]{row.Value,-53}[/]",
                    scrollbarChar);  // Scrollbar keeps normal styling
            }
            else
            {
                table.AddRow(row.Field, row.Value, scrollbarChar);
            }
        }

        // Fill remaining rows
        int rowsRendered = viewportEnd - viewportStart;
        int emptyRowsNeeded = availableRows - rowsRendered;
        for (int i = 0; i < emptyRowsNeeded; i++)
        {
            table.AddRow("", "", "░");
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
        string footerRow1Left = $"[bold]Row:[/] {selectedRow + 1}/{totalRows}";
        string footerRow1Right = $"[bold]Dist:[/] {distUnitLabel} | [bold]Alt:[/] {altUnitLabel} | [bold]Spd:[/] {speedUnitLabel}";

        // Footer row 2: left and right sections
        string footerRow2Left = "[bold]↑/↓[/]: Row, [bold]←/→[/]: Page";
        string footerRow2Right = "[bold]ESC[/]: Back, [bold]Q[/]: Quit";

        // Calculate spacing for right alignment (100 chars total width - border chars)
        // Table width is 104, but caption appears inside borders, so usable width is 100
        int usableWidth = 100;

        string footerRow1 = footerRow1Left + new string(' ', Math.Max(1, usableWidth - footerRow1Left.Length + 9 - footerRow1Right.Length + 27)) + footerRow1Right;
        string footerRow2 = footerRow2Left + new string(' ', Math.Max(1, usableWidth - footerRow2Left.Length + 18 - footerRow2Right.Length + 18)) + footerRow2Right;

        string footer = footerRow1 + "\n" + footerRow2;

        table.Caption(footer, new Style(foreground: Color.Grey));

        // Return table and allRows for navigation
        return (table, allRows);
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
