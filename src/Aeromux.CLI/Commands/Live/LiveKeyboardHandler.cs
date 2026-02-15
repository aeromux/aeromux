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

using Aeromux.Core.Tracking;
using Serilog;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Handles keyboard input for table and detail views in the live TUI display.
/// Supports arrow key navigation, page up/down, unit toggling, and view switching.
/// </summary>
internal static class LiveKeyboardHandler
{
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
    public static bool HandleTableInput(
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
    public static bool HandleDetailInput(
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
}
