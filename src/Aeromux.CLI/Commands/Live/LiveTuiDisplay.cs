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
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Database;
using Aeromux.Infrastructure.Streaming;
using Serilog;
using Spectre.Console;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Manages the TUI (Terminal User Interface) display loop for the live command.
/// Owns AircraftStateTracker lifecycle, Spectre.Console Live display, keyboard input polling,
/// and terminal resize workaround. Delegates rendering to AircraftTableBuilder and AircraftDetailBuilder.
/// </summary>
internal sealed class LiveTuiDisplay
{
    /// <summary>
    /// Runs the TUI display loop with tracker lifecycle management.
    /// </summary>
    /// <param name="stream">ReceiverStream providing ProcessedFrame data from all configured sources.</param>
    /// <param name="settings">Command settings for mode detection in session summary.</param>
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
    public async Task<int> RunAsync(
        IFrameStream stream,
        LiveSettings settings,
        ReceiverConfig? receiverConfig,
        DateTime sessionStart,
        CancellationToken ct)
    {
        // Create local AircraftStateTracker (follows DaemonCommand pattern)
        AeromuxConfig config = ConfigurationProvider.Current;
        AircraftDatabaseLookupService? databaseLookup = DatabaseLookupFactory.TryCreate(config.Database);
        var tracker = new AircraftStateTracker(config.Tracking!, databaseLookup);

        // Create linked cancellation token for tracker consumer task
        // This allows us to cancel the tracker independently when user quits
        using var trackerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Subscribe to aircraft lifecycle events for operational visibility
        tracker.OnAircraftAdded += (_, e) =>
        {
            Aircraft aircraft = e.Aircraft;
            Log.Information("New aircraft: ICAO={Icao}, Callsign={Callsign}",
                aircraft.Identification.ICAO,
                aircraft.Identification.Callsign ?? "Unknown");
        };

        // Log significant updates (typically for debug, currently does nothing)
        tracker.OnAircraftUpdated += (_, e) =>
        {
            // Example
            //
            // Aircraft prev = e.Previous;
            // Aircraft curr = e.Updated;
            //
            // bool positionChanged = prev.Position.Coordinate != curr.Position.Coordinate ||
            //                       prev.Position.BarometricAltitude != curr.Position.BarometricAltitude;
            // bool velocityChanged = prev.Velocity.GroundSpeed != curr.Velocity.GroundSpeed ||
            //                       prev.Velocity.Speed != curr.Velocity.Speed;
            //
            // if (positionChanged || velocityChanged)
            // {
            //     Log.Debug("Aircraft update: ICAO={Icao}, Position={Position}, Alt={Altitude}, Speed={Velocity}",
            //         curr.Identification.ICAO,
            //         curr.Position.Coordinate,
            //         curr.Position.BarometricAltitude,
            //         curr.Velocity.GroundSpeed ?? curr.Velocity.Speed);
            // }
        };

        // IMPORTANT: Tracker runs in background, consuming frames automatically
        // Uses trackerCts.Token so we can cancel it independently before disposal
        tracker.StartConsuming(stream.Subscribe(), trackerCts.Token);
        Log.Information("Aircraft state tracker started");

        AnsiConsole.Clear();

        // Suppress console logging during TUI to prevent stdout corruption
        LoggingConfig loggingConfig = config.Logging!;
        bool wasConsoleEnabled = loggingConfig.Console.Enabled;
        if (wasConsoleEnabled)
        {
            loggingConfig.Console.Enabled = false;
            Program.ConfigureLogging(loggingConfig);
        }

        // TUI state (consolidates all display state into a single mutable object)
        var state = new LiveTuiState();

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
                            // Poll tracker for current state, apply search filter and sort
                            List<Aircraft> sortedAircraft = LiveAircraftSorter.SortAndFilter(
                                tracker.GetAllAircraft().ToList(),
                                state.SortColumn,
                                state.SortDirection,
                                state.IsSearchActive ? state.SearchInput : null,
                                receiverConfig);

                            // Track selection by ICAO (not row index) to maintain selection stability
                            // Aircraft list changes every second (timeouts, new aircraft), so row indices shift
                            // ICAO remains constant for each aircraft, ensuring selection persists across refreshes
                            if (state.SelectedIcao != null)
                            {
                                int foundIndex = sortedAircraft.FindIndex(a => a.Identification.ICAO == state.SelectedIcao);
                                if (foundIndex >= 0)
                                {
                                    state.SelectedRow = foundIndex;
                                    if (state.ShowingDetails)
                                    {
                                        state.DetailAircraft = sortedAircraft[foundIndex];
                                        state.DetailAircraftExpired = false;
                                    }
                                }
                                else if (state is { ShowingDetails: true, DetailAircraft: not null })
                                {
                                    // Aircraft expired while in detail view — freeze snapshot, show [EXPIRED]
                                    state.DetailAircraftExpired = true;
                                }
                                else
                                {
                                    // Selected aircraft expired (timeout) or filtered out, select first available
                                    state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[0].Identification.ICAO : null;
                                    state.SelectedRow = 0;
                                }
                            }
                            else
                            {
                                // Initial selection
                                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[0].Identification.ICAO : null;
                                state.SelectedRow = 0;
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
                            if (state is { ShowingDetails: true, DetailAircraft: not null })
                            {
                                (Table detailTable, List<DetailRow> detailRows) = LiveAircraftDetailBuilder.Build(
                                    state.DetailAircraft,
                                    state.DistanceUnit,
                                    state.AltitudeUnit,
                                    state.SpeedUnit,
                                    receiverConfig,
                                    state.DetailViewSelectedRow,
                                    state.DetailAircraftExpired,
                                    state.IsDetailSearchActive,
                                    state.DetailSearchInput);

                                state.CurrentDetailRows = detailRows;
                                ctx.UpdateTarget(detailTable);
                            }
                            else
                            {
                                ctx.UpdateTarget(LiveAircraftTableBuilder.Build(
                                    sortedAircraft,
                                    stream.GetStatistics(),
                                    state.SelectedRow,
                                    state.DistanceUnit,
                                    state.AltitudeUnit,
                                    state.SpeedUnit,
                                    receiverConfig,
                                    state.SortColumn,
                                    state.SortDirection,
                                    state.IsSearchActive,
                                    state.SearchInput));
                            }
                            ctx.Refresh();

                            // Check for keyboard input with timeout (non-blocking poll every 50ms)
                            DateTime startTime = DateTime.UtcNow;
                            while ((DateTime.UtcNow - startTime).TotalMilliseconds < 1000 && !ct.IsCancellationRequested)
                            {
                                if (Console.KeyAvailable)
                                {
                                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                                    if (state.ShowingDetails)
                                    {
                                        // Detail view keyboard handling
                                        if (state.CurrentDetailRows != null)
                                        {
                                            if (!LiveKeyboardHandler.HandleDetailInput(key, state.CurrentDetailRows, state))
                                            {
                                                shouldQuit = true;
                                                return;  // Quit
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Table view keyboard handling (normal and search modes)
                                        if (!LiveKeyboardHandler.HandleTableInput(key, sortedAircraft, state))
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

            // Restore console logging so session summary and post-TUI output appears
            if (wasConsoleEnabled)
            {
                loggingConfig.Console.Enabled = true;
                Program.ConfigureLogging(loggingConfig);
            }

            // CRITICAL: Cancel tracker consumer task BEFORE disposing
            // This prevents ObjectDisposedException during shutdown
            await trackerCts.CancelAsync();

            // Now safe to dispose tracker (consumer task will complete gracefully)
            tracker.Dispose();
            Log.Information("Aircraft state tracker stopped");

            // Close database connection
            databaseLookup?.Dispose();

            // Display session summary with statistics
            LiveSessionReporter.LogSessionSummary(
                sessionStart,
                stream.GetStatistics(),
                tracker.GetAllAircraft().Count);
        }

        return 0;
    }
}
