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

using System.Text.RegularExpressions;
using Aeromux.Core.Tracking;
using Serilog;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Handles keyboard input for table and detail views in the live TUI display.
/// Supports arrow key navigation, page up/down, Home/End, unit toggling, column sorting,
/// search mode, F12 reset, and view switching.
/// </summary>
internal static class LiveKeyboardHandler
{
    /// <summary>
    /// Handles keyboard input for table view (normal and search modes).
    /// </summary>
    /// <param name="key">The console key information from keyboard input.</param>
    /// <param name="sortedAircraft">Current (filtered and sorted) aircraft list.</param>
    /// <param name="state">Mutable TUI state.</param>
    /// <returns>False if user requested quit, true to continue.</returns>
    public static bool HandleTableInput(
        ConsoleKeyInfo key,
        List<Aircraft> sortedAircraft,
        LiveTuiState state)
    {
        if (state.IsSearchActive)
        {
            return HandleSearchModeInput(key, sortedAircraft, state);
        }

        return HandleNormalTableInput(key, sortedAircraft, state);
    }

    /// <summary>
    /// Handles keyboard input for detail view (normal and search modes).
    /// </summary>
    /// <param name="key">The console key information.</param>
    /// <param name="allRows">All detail rows (needed to check for section headers).</param>
    /// <param name="state">Mutable TUI state.</param>
    /// <returns>False if user pressed Q (quit), true to continue.</returns>
    public static bool HandleDetailInput(
        ConsoleKeyInfo key,
        List<DetailRow> allRows,
        LiveTuiState state)
    {
        if (state.IsDetailSearchActive)
        {
            return HandleDetailSearchInput(key, allRows, state);
        }

        return HandleDetailNormalInput(key, allRows, state);
    }

    /// <summary>
    /// Handles keyboard input for detail view in normal (non-search) mode.
    /// Supports navigation, unit toggling (D/A/S), search activation (/), and view switching.
    /// </summary>
    /// <param name="key">The console key information from keyboard input.</param>
    /// <param name="allRows">All detail rows including section headers (for navigation bounds).</param>
    /// <param name="state">Mutable TUI state.</param>
    /// <returns>False if user requested quit, true to continue.</returns>
    private static bool HandleDetailNormalInput(
        ConsoleKeyInfo key,
        List<DetailRow> allRows,
        LiveTuiState state)
    {
        int totalRows = allRows.Count;
        int availableRows = CalculateDetailAvailableRows();

        // Check for '/' key to enter search mode (use KeyChar for cross-platform reliability)
        if (key.KeyChar == '/')
        {
            state.IsDetailSearchActive = true;
            state.DetailSearchInput = "";
            state.PreSearchDetailSelectedRow = state.DetailViewSelectedRow;
            Log.Debug("Detail search mode activated");
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                // Move to previous non-header row
                int prevRow = state.DetailViewSelectedRow - 1;
                while (prevRow >= 0 && allRows[prevRow].IsSectionHeader)
                {
                    prevRow--;
                }
                if (prevRow >= 0)
                {
                    state.DetailViewSelectedRow = prevRow;
                }
                break;

            case ConsoleKey.DownArrow:
                // Move to next non-header row
                int nextRow = state.DetailViewSelectedRow + 1;
                while (nextRow < totalRows && allRows[nextRow].IsSectionHeader)
                {
                    nextRow++;
                }
                if (nextRow < totalRows)
                {
                    state.DetailViewSelectedRow = nextRow;
                }
                break;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.PageUp:
                // Jump up by viewport, then find nearest non-header
                int targetUp = Math.Max(0, state.DetailViewSelectedRow - availableRows);
                while (targetUp < state.DetailViewSelectedRow && allRows[targetUp].IsSectionHeader)
                {
                    targetUp++;
                }
                if (targetUp < state.DetailViewSelectedRow && !allRows[targetUp].IsSectionHeader)
                {
                    state.DetailViewSelectedRow = targetUp;
                }
                break;

            case ConsoleKey.RightArrow:
            case ConsoleKey.PageDown:
                // Jump down by viewport, then find nearest non-header
                int targetDown = Math.Min(totalRows - 1, state.DetailViewSelectedRow + availableRows);
                while (targetDown > state.DetailViewSelectedRow && allRows[targetDown].IsSectionHeader)
                {
                    targetDown--;
                }
                if (targetDown > state.DetailViewSelectedRow && !allRows[targetDown].IsSectionHeader)
                {
                    state.DetailViewSelectedRow = targetDown;
                }
                break;

            case ConsoleKey.Home:
                // Jump to first non-header row
                int firstRow = 0;
                while (firstRow < totalRows && allRows[firstRow].IsSectionHeader)
                {
                    firstRow++;
                }
                if (firstRow < totalRows)
                {
                    state.DetailViewSelectedRow = firstRow;
                }
                break;

            case ConsoleKey.End:
                // Jump to last non-header row
                int lastRow = totalRows - 1;
                while (lastRow >= 0 && allRows[lastRow].IsSectionHeader)
                {
                    lastRow--;
                }
                if (lastRow >= 0)
                {
                    state.DetailViewSelectedRow = lastRow;
                }
                break;

            case ConsoleKey.D:
                state.DistanceUnit = state.DistanceUnit == DistanceUnit.Miles
                    ? DistanceUnit.Kilometers
                    : DistanceUnit.Miles;
                Log.Debug("Distance unit changed to: {Unit}", state.DistanceUnit);
                break;

            case ConsoleKey.A:
                state.AltitudeUnit = state.AltitudeUnit == AltitudeUnit.Feet
                    ? AltitudeUnit.Meters
                    : AltitudeUnit.Feet;
                Log.Debug("Altitude unit changed to: {Unit}", state.AltitudeUnit);
                break;

            case ConsoleKey.S:
                state.SpeedUnit = state.SpeedUnit switch
                {
                    SpeedUnit.Knots => SpeedUnit.KilometersPerHour,
                    SpeedUnit.KilometersPerHour => SpeedUnit.MilesPerHour,
                    SpeedUnit.MilesPerHour => SpeedUnit.Knots,
                    _ => SpeedUnit.Knots
                };
                Log.Debug("Speed unit changed to: {Unit}", state.SpeedUnit);
                break;

            case ConsoleKey.Escape:
                state.ShowingDetails = false;
                state.DetailAircraft = null;
                state.DetailAircraftExpired = false;
                state.IsDetailSearchActive = false;
                state.DetailSearchInput = "";
                break;

            case ConsoleKey.Q:
                return false;  // Quit
        }

        return true;  // Continue
    }

    /// <summary>
    /// Handles keyboard input for detail view in search mode.
    /// All printable characters are treated as search input. D/A/S/Q have no special function.
    /// Up/Down cycles through matches with wrap-around. Escape cancels (restores position),
    /// Enter confirms (keeps position).
    /// </summary>
    /// <param name="key">The console key information from keyboard input.</param>
    /// <param name="allRows">All detail rows including section headers (for navigation bounds).</param>
    /// <param name="state">Mutable TUI state.</param>
    /// <returns>Always true — search mode never quits the application.</returns>
    private static bool HandleDetailSearchInput(
        ConsoleKeyInfo key,
        List<DetailRow> allRows,
        LiveTuiState state)
    {
        int totalRows = allRows.Count;
        int availableRows = CalculateDetailAvailableRows();

        switch (key.Key)
        {
            case ConsoleKey.Escape:
                // Cancel search, restore pre-search position
                state.IsDetailSearchActive = false;
                state.DetailSearchInput = "";
                state.DetailViewSelectedRow = state.PreSearchDetailSelectedRow;
                Log.Debug("Detail search cancelled, position restored");
                break;

            case ConsoleKey.Enter:
                // Confirm search, keep current position
                state.IsDetailSearchActive = false;
                state.DetailSearchInput = "";
                Log.Debug("Detail search confirmed");
                break;

            case ConsoleKey.Backspace:
                if (state.DetailSearchInput.Length > 0)
                {
                    state.DetailSearchInput = state.DetailSearchInput[..^1].TrimEnd(' ');
                    if (state.DetailSearchInput.Length > 0)
                    {
                        List<int> matches = FindDetailSearchMatches(allRows, state.DetailSearchInput);
                        if (matches.Count > 0)
                        {
                            state.DetailViewSelectedRow = matches[0];
                        }
                    }
                }
                break;

            case ConsoleKey.UpArrow:
                if (!string.IsNullOrEmpty(state.DetailSearchInput))
                {
                    // Cycle to previous match with wrap-around
                    List<int> matchesUp = FindDetailSearchMatches(allRows, state.DetailSearchInput);
                    if (matchesUp.Count > 0)
                    {
                        int prev = matchesUp.LastOrDefault(m => m < state.DetailViewSelectedRow, matchesUp[^1]);
                        state.DetailViewSelectedRow = prev;
                    }
                }
                break;

            case ConsoleKey.DownArrow:
            case ConsoleKey.Tab:
                if (!string.IsNullOrEmpty(state.DetailSearchInput))
                {
                    // Cycle to next match with wrap-around
                    List<int> matchesDown = FindDetailSearchMatches(allRows, state.DetailSearchInput);
                    if (matchesDown.Count > 0)
                    {
                        int next = matchesDown.FirstOrDefault(m => m > state.DetailViewSelectedRow, matchesDown[0]);
                        state.DetailViewSelectedRow = next;
                    }
                }
                break;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.PageUp:
                // Jump up by viewport, then find nearest non-header
                int targetUp = Math.Max(0, state.DetailViewSelectedRow - availableRows);
                while (targetUp < state.DetailViewSelectedRow && allRows[targetUp].IsSectionHeader)
                {
                    targetUp++;
                }
                if (targetUp < state.DetailViewSelectedRow && !allRows[targetUp].IsSectionHeader)
                {
                    state.DetailViewSelectedRow = targetUp;
                }
                break;

            case ConsoleKey.RightArrow:
            case ConsoleKey.PageDown:
                // Jump down by viewport, then find nearest non-header
                int targetDown = Math.Min(totalRows - 1, state.DetailViewSelectedRow + availableRows);
                while (targetDown > state.DetailViewSelectedRow && allRows[targetDown].IsSectionHeader)
                {
                    targetDown--;
                }
                if (targetDown > state.DetailViewSelectedRow && !allRows[targetDown].IsSectionHeader)
                {
                    state.DetailViewSelectedRow = targetDown;
                }
                break;

            case ConsoleKey.Home:
                // Jump to first non-header row
                int firstRow = 0;
                while (firstRow < totalRows && allRows[firstRow].IsSectionHeader)
                {
                    firstRow++;
                }
                if (firstRow < totalRows)
                {
                    state.DetailViewSelectedRow = firstRow;
                }
                break;

            case ConsoleKey.End:
                // Jump to last non-header row
                int lastRow = totalRows - 1;
                while (lastRow >= 0 && allRows[lastRow].IsSectionHeader)
                {
                    lastRow--;
                }
                if (lastRow >= 0)
                {
                    state.DetailViewSelectedRow = lastRow;
                }
                break;

            default:
                // Append allowed characters to search input (max 15 chars, uppercase display)
                if (IsDetailSearchChar(key.KeyChar) && state.DetailSearchInput.Length < 15)
                {
                    char ch = char.ToUpperInvariant(key.KeyChar);

                    // Reject leading space
                    if (ch == ' ' && state.DetailSearchInput.Length == 0)
                    {
                        break;
                    }

                    state.DetailSearchInput += ch;
                    List<int> matches = FindDetailSearchMatches(allRows, state.DetailSearchInput);
                    if (matches.Count > 0)
                    {
                        state.DetailViewSelectedRow = matches[0];
                    }
                }
                break;
        }

        return true;  // Search mode never quits
    }

    /// <summary>
    /// Calculates available viewport rows for the detail view based on terminal height.
    /// </summary>
    private static int CalculateDetailAvailableRows()
    {
        const int headerLines = 1;        // Title row with ICAO
        const int footerLines = 2;        // Two-line footer with navigation hints
        const int tableHeaderLines = 1;   // Column header row
        const int padding = 3;            // Border and spacing overhead
        return Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);
    }

    /// <summary>
    /// Returns true if the character is allowed in detail search input:
    /// English letters, digits, space, parentheses, hyphen, comma.
    /// </summary>
    private static bool IsDetailSearchChar(char c)
    {
        return char.IsAsciiLetter(c)
               || char.IsAsciiDigit(c)
               || c is ' ' or '(' or ')' or '-' or ',';
    }

    /// <summary>
    /// Finds all detail row indices that match the search input (case-insensitive substring match
    /// on field names, excluding section headers). Markup tags are stripped before matching.
    /// </summary>
    /// <param name="allRows">All detail rows including section headers.</param>
    /// <param name="searchInput">Search text to match against field names.</param>
    /// <returns>List of row indices (into allRows) for matching data fields.</returns>
    private static List<int> FindDetailSearchMatches(List<DetailRow> allRows, string searchInput)
    {
        var matches = new List<int>();
        for (int i = 0; i < allRows.Count; i++)
        {
            if (allRows[i].IsSectionHeader)
            {
                continue;
            }

            string plainField = Regex.Replace(allRows[i].Field, @"\[/?[^\]]*\]", "");
            if (plainField.Contains(searchInput, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(i);
            }
        }

        return matches;
    }

    /// <summary>
    /// Handles keyboard input when search mode is active.
    /// Only allows search-related keys; all other keys are ignored.
    /// </summary>
    private static bool HandleSearchModeInput(
        ConsoleKeyInfo key,
        List<Aircraft> sortedAircraft,
        LiveTuiState state)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
                // Cancel search, restore previous selection
                state.IsSearchActive = false;
                state.SearchInput = "";
                state.SelectedIcao = state.PreSearchSelectedIcao;
                state.PreSearchSelectedIcao = null;
                Log.Debug("Search cancelled");
                break;

            case ConsoleKey.Enter:
                // Confirm search and open detail view of selected aircraft
                state.IsSearchActive = false;
                state.SearchInput = "";
                state.PreSearchSelectedIcao = null;
                if (sortedAircraft.Count > 0)
                {
                    state.ShowingDetails = true;
                    state.DetailViewSelectedRow = 2;
                }
                Log.Debug("Search confirmed, opening details");
                break;

            case ConsoleKey.Backspace:
                if (state.SearchInput.Length > 0)
                {
                    state.SearchInput = state.SearchInput[..^1];
                }
                break;

            case ConsoleKey.UpArrow:
                state.SelectedRow = Math.Max(0, state.SelectedRow - 1);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;

            case ConsoleKey.DownArrow:
                state.SelectedRow = Math.Min(Math.Max(0, sortedAircraft.Count - 1), state.SelectedRow + 1);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;

            case ConsoleKey.Home:
                state.SelectedRow = 0;
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[0].Identification.ICAO : null;
                break;

            case ConsoleKey.End:
                state.SelectedRow = Math.Max(0, sortedAircraft.Count - 1);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;

            case ConsoleKey.F12:
                state.ResetToDefaults();
                Log.Debug("All settings reset to defaults (from search mode)");
                break;

            default:
                // Append alphanumeric characters to search input
                if (char.IsLetterOrDigit(key.KeyChar) && state.SearchInput.Length < 8)
                {
                    state.SearchInput += char.ToUpperInvariant(key.KeyChar);
                }
                break;
        }

        return true;  // Search mode never quits (Escape cancels search, Q is ignored)
    }

    /// <summary>
    /// Handles keyboard input for normal (non-search) table view.
    /// </summary>
    private static bool HandleNormalTableInput(
        ConsoleKeyInfo key,
        List<Aircraft> sortedAircraft,
        LiveTuiState state)
    {
        // Calculate available viewport rows based on terminal height
        // Layout: title (1) + table header (1) + data rows + footer (3) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 1;        // Title row "AIRCRAFT LIST - Aeromux"
        const int footerLines = 3;        // Three-line footer with navigation and sort/search hints
        const int tableHeaderLines = 1;   // Column header row
        const int padding = 3;            // Border and spacing overhead

        int availableRows = Math.Max(5, Console.WindowHeight - headerLines - footerLines - tableHeaderLines - padding);

        // Check for '/' key to enter search mode (use KeyChar for cross-platform reliability)
        if (key.KeyChar == '/')
        {
            state.IsSearchActive = true;
            state.SearchInput = "";
            state.PreSearchSelectedIcao = state.SelectedIcao;
            Log.Debug("Search mode activated");
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                state.SelectedRow = Math.Max(0, state.SelectedRow - 1);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.DownArrow:
                state.SelectedRow = Math.Min(Math.Max(0, sortedAircraft.Count - 1), state.SelectedRow + 1);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.LeftArrow:
            case ConsoleKey.PageUp:
                state.SelectedRow = Math.Max(0, state.SelectedRow - availableRows);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.RightArrow:
            case ConsoleKey.PageDown:
                state.SelectedRow = Math.Min(Math.Max(0, sortedAircraft.Count - 1), state.SelectedRow + availableRows);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.Home:
                state.SelectedRow = 0;
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[0].Identification.ICAO : null;
                break;
            case ConsoleKey.End:
                state.SelectedRow = Math.Max(0, sortedAircraft.Count - 1);
                state.SelectedIcao = sortedAircraft.Count > 0 ? sortedAircraft[state.SelectedRow].Identification.ICAO : null;
                break;
            case ConsoleKey.Enter:
                if (sortedAircraft.Count > 0)
                {
                    state.ShowingDetails = true;
                    state.DetailViewSelectedRow = 2;
                    state.DetailAircraft = sortedAircraft[state.SelectedRow];
                    state.DetailAircraftExpired = false;
                }
                break;
            case ConsoleKey.D:
                state.DistanceUnit = state.DistanceUnit == DistanceUnit.Miles
                    ? DistanceUnit.Kilometers
                    : DistanceUnit.Miles;
                Log.Debug("Distance unit changed to: {Unit}", state.DistanceUnit);
                break;
            case ConsoleKey.A:
                state.AltitudeUnit = state.AltitudeUnit == AltitudeUnit.Feet
                    ? AltitudeUnit.Meters
                    : AltitudeUnit.Feet;
                Log.Debug("Altitude unit changed to: {Unit}", state.AltitudeUnit);
                break;
            case ConsoleKey.S:
                state.SpeedUnit = state.SpeedUnit switch
                {
                    SpeedUnit.Knots => SpeedUnit.KilometersPerHour,
                    SpeedUnit.KilometersPerHour => SpeedUnit.MilesPerHour,
                    SpeedUnit.MilesPerHour => SpeedUnit.Knots,
                    _ => SpeedUnit.Knots
                };
                Log.Debug("Speed unit changed to: {Unit}", state.SpeedUnit);
                break;
            case ConsoleKey.F1:
                HandleSortKey(state, SortColumn.ICAO);
                break;
            case ConsoleKey.F2:
                HandleSortKey(state, SortColumn.Callsign);
                break;
            case ConsoleKey.F3:
                HandleSortKey(state, SortColumn.Altitude);
                break;
            case ConsoleKey.F4:
                HandleSortKey(state, SortColumn.Vertical);
                break;
            case ConsoleKey.F5:
                HandleSortKey(state, SortColumn.Distance);
                break;
            case ConsoleKey.F6:
                HandleSortKey(state, SortColumn.Speed);
                break;
            case ConsoleKey.F12:
                state.ResetToDefaults();
                Log.Debug("All settings reset to defaults");
                break;
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                Log.Information("User quit requested (Q/ESC)");
                return false;
        }

        return true;
    }

    /// <summary>
    /// Handles sort key cycling: same column cycles asc -> desc -> default, different column starts at ascending.
    /// </summary>
    private static void HandleSortKey(LiveTuiState state, SortColumn column)
    {
        if (state.SortColumn == column)
        {
            // Same column: cycle ascending -> descending -> default (ICAO ascending)
            if (state.SortDirection == SortDirection.Ascending)
            {
                state.SortDirection = SortDirection.Descending;
            }
            else
            {
                // Back to default
                state.SortColumn = SortColumn.ICAO;
                state.SortDirection = SortDirection.Ascending;
            }
        }
        else
        {
            // Different column: start at ascending
            state.SortColumn = column;
            state.SortDirection = SortDirection.Ascending;
        }

        Log.Debug("Sort changed to: {Column} {Direction}", state.SortColumn, state.SortDirection);
    }
}
