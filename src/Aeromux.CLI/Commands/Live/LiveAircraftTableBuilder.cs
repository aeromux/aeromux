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
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Spectre.Console;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Builds the aircraft list table with dynamic viewport, scrollbar, and footer.
/// Renders aircraft data with selection highlighting, search match highlighting, and unit-aware formatting.
/// </summary>
internal static class LiveAircraftTableBuilder
{
    /// <summary>
    /// Builds the aircraft list table with dynamic viewport, scrollbar, search highlighting, and three-row footer.
    /// </summary>
    /// <param name="sortedAircraft">Aircraft list (filtered and sorted).</param>
    /// <param name="stats">Stream statistics from ReceiverStream, or null in client mode.</param>
    /// <param name="selectedRow">Index of currently selected row for highlighting.</param>
    /// <param name="distanceUnit">Unit to display distances (miles or kilometers).</param>
    /// <param name="altitudeUnit">Unit to display altitudes (feet or meters).</param>
    /// <param name="speedUnit">Unit to display speeds (knots, km/h, or mph).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <param name="sortColumn">Current sort column for footer display.</param>
    /// <param name="sortDirection">Current sort direction for footer display.</param>
    /// <param name="isSearchActive">Whether search mode is active.</param>
    /// <param name="searchInput">Current search input text.</param>
    /// <returns>Spectre.Console Table with aircraft data, footer, and fixed width.</returns>
    public static Table Build(
        List<Aircraft> sortedAircraft,
        StreamStatistics? stats,
        int selectedRow,
        DistanceUnit distanceUnit,
        AltitudeUnit altitudeUnit,
        SpeedUnit speedUnit,
        ReceiverConfig? receiverConfig,
        SortColumn sortColumn,
        SortDirection sortDirection,
        bool isSearchActive,
        string searchInput)
    {
        // Calculate available viewport rows based on terminal height
        // Layout: title (1) + table header (1) + data rows + footer (3) + padding (3)
        // Minimum 5 rows ensures usable display even in very small terminals
        const int headerLines = 1;        // Title row "AIRCRAFT LIST - Aeromux"
        const int footerLines = 3;        // Three-line footer with navigation and sort/search hints
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

        bool searching = isSearchActive && searchInput.Length > 0;

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

            // Format ICAO and callsign with optional search highlighting
            string icaoDisplay = aircraft.Identification.ICAO;
            string callsign = aircraft.Identification.Callsign ?? "N/A";
            callsign = callsign.PadRight(8);

            if (searching)
            {
                icaoDisplay = HighlightMatch(icaoDisplay, searchInput, isSelected);
                callsign = HighlightMatch(callsign, searchInput, isSelected);
            }

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

                double distanceValue = distanceUnit switch
                {
                    DistanceUnit.NauticalMiles => receiverLocation.DistanceToNauticalMiles(aircraft.Position.Coordinate),
                    DistanceUnit.Miles => receiverLocation.DistanceToMiles(aircraft.Position.Coordinate),
                    DistanceUnit.Kilometers => receiverLocation.DistanceToKilometers(aircraft.Position.Coordinate),
                    _ => receiverLocation.DistanceToNauticalMiles(aircraft.Position.Coordinate)
                };

                string unitLabel = distanceUnit switch
                {
                    DistanceUnit.NauticalMiles => "nm",
                    DistanceUnit.Miles => "mi",
                    DistanceUnit.Kilometers => "km",
                    _ => "nm"
                };
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

            // Format messages: right-align to 8 chars
            string messages = $"{aircraft.Status.TotalMessages}".PadLeft(8);

            // Apply selection highlighting (inverted colors)
            // When searching, ICAO and callsign already contain markup from HighlightMatch
            if (isSelected)
            {
                string icaoCell = searching ? icaoDisplay : $"[black on white]{icaoDisplay}[/]";
                string callsignCell = searching ? callsign : $"[black on white]{callsign}[/]";

                table.AddRow(
                    icaoCell,
                    callsignCell,
                    $"[black on white]{altitude}[/]",
                    $"[black on white]{verticalRate}[/]",
                    $"[black on white]{distance}[/]",
                    $"[black on white]{speed}[/]",
                    $"[black on white]{messages}[/]",
                    $"[black on white]{signal}[/]",
                    $"[black on white]{lastSeenNumber}[/]",
                    scrollbarChar);
            }
            else
            {
                table.AddRow(
                    icaoDisplay,
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

        // Build footer (3 rows with left/right alignment)
        string distUnitLabel = distanceUnit switch
        {
            DistanceUnit.NauticalMiles => "nm",
            DistanceUnit.Miles => "mi",
            DistanceUnit.Kilometers => "km",
            _ => "nm"
        };
        string altUnitLabel = altitudeUnit == AltitudeUnit.Feet ? "ft" : "m";
        string speedUnitLabel = speedUnit switch
        {
            SpeedUnit.Knots => "kts",
            SpeedUnit.KilometersPerHour => "km/h",
            SpeedUnit.MilesPerHour => "mph",
            _ => "kts"
        };

        // Footer row 1: sort hints or search prompt (closest to table data)
        string footerRow1;
        if (isSearchActive)
        {
            string matchText = sortedAircraft.Count == 1 ? "1 match" : $"{sortedAircraft.Count} matches";
            string footerRow1Left = $"[white][bold]Search:[/] {Markup.Escape(searchInput)}_ ({matchText})[/]";
            string footerRow1Right = "[bold]ESC[/]: Cancel";
            footerRow1 = PadFooterRow(footerRow1Left, footerRow1Right);
        }
        else
        {
            footerRow1 = BuildSortHintRow(sortColumn, sortDirection);
        }

        // Footer row 2: status
        string footerRow2Left = $"[bold]Aircraft:[/] {sortedAircraft.Count} | [bold]Selected:[/] {selectedRow + 1}/{sortedAircraft.Count} | [bold]Viewport:[/] {viewportStart + 1}-{viewportEnd}";
        string footerRow2Right = $"[bold]Dist:[/] {distUnitLabel} | [bold]Alt:[/] {altUnitLabel} | [bold]Spd:[/] {speedUnitLabel}";

        // Footer row 3: navigation
        string footerRow3Left = "[bold]↑/↓[/]: Row, [bold]←/→[/]: Page, [bold]Home/End[/]";
        string footerRow3Right = "[bold]ENTER[/]: Details, [bold]D/A/S[/]: Units, [bold]/[/]: Search, [bold]Q[/]: Quit";

        // Table width is 104, but caption appears inside borders, so usable width is 100
        string footerRow2 = PadFooterRow(footerRow2Left, footerRow2Right);
        string footerRow3 = PadFooterRow(footerRow3Left, footerRow3Right);

        string footer = footerRow1 + "\n" + footerRow2 + "\n" + footerRow3;

        table.Caption(footer, new Style(foreground: Color.Grey));

        return table;
    }

    /// <summary>
    /// Pads a footer row so left and right sections fill exactly 100 visible characters.
    /// Accounts for Spectre.Console markup tags that don't consume visible width.
    /// </summary>
    private static string PadFooterRow(string left, string right)
    {
        const int usableWidth = 100;
        int leftVisible = VisibleLength(left);
        int rightVisible = VisibleLength(right);
        int padding = Math.Max(1, usableWidth - leftVisible - rightVisible);
        return left + new string(' ', padding) + right;
    }

    /// <summary>
    /// Calculates the visible length of a string by stripping Spectre.Console markup tags.
    /// Markup tags are enclosed in square brackets like [bold], [/], [black on white], etc.
    /// </summary>
    private static int VisibleLength(string markup)
    {
        // Strip all Spectre.Console markup tags: [bold], [/], [black on white], [bold yellow], etc.
        string stripped = Regex.Replace(markup, @"\[/?[^\]]*\]", "");
        return stripped.Length;
    }

    /// <summary>
    /// Builds footer row 1 for normal mode with F-key sort hints and active sort indicator.
    /// The active sort column's F-key label is highlighted in bold white; others are gray.
    /// </summary>
    private static string BuildSortHintRow(SortColumn sortColumn, SortDirection sortDirection)
    {
        var labels = new (SortColumn Column, string Key, string Label)[]
        {
            (SortColumn.ICAO, "F1", "ICAO"),
            (SortColumn.Callsign, "F2", "Callsign"),
            (SortColumn.Altitude, "F3", "Altitude"),
            (SortColumn.Vertical, "F4", "Vertical"),
            (SortColumn.Distance, "F5", "Distance"),
            (SortColumn.Speed, "F6", "Speed"),
        };

        string arrow = sortDirection == SortDirection.Ascending ? "▲" : "▼";

        var parts = new List<string>();
        foreach (var (column, fKey, label) in labels)
        {
            parts.Add(column == sortColumn
                ? $"[bold white]{fKey}: {label} {arrow}[/]"
                : $"[bold]{fKey}:[/] [grey]{label}[/]");
        }

        string left = string.Join("  ", parts);
        string right = "[bold]F12:[/] [grey]Reset[/]";

        return PadFooterRow(left, right);
    }

    /// <summary>
    /// Highlights the first occurrence of searchTerm within text using Spectre.Console markup.
    /// Produces correct styling for both normal and selected (inverted) rows.
    /// </summary>
    private static string HighlightMatch(string text, string searchTerm, bool isSelected)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return isSelected ? $"[black on white]{Markup.Escape(text)}[/]" : text;
        }

        int matchIndex = text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return isSelected ? $"[black on white]{Markup.Escape(text)}[/]" : text;
        }

        string pre = Markup.Escape(text[..matchIndex]);
        string match = Markup.Escape(text[matchIndex..(matchIndex + searchTerm.Length)]);
        string post = Markup.Escape(text[(matchIndex + searchTerm.Length)..]);

        if (isSelected)
        {
            // Selected row: red highlight on white background, rest inverted
            return $"[black on white]{pre}[/][bold red on white]{match}[/][black on white]{post}[/]";
        }

        // Normal row: red highlight, rest unstyled
        return $"{pre}[bold red]{match}[/]{post}";
    }
}
