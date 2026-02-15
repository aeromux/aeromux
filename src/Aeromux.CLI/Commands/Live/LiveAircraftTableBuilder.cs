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
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Spectre.Console;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Builds the aircraft list table with dynamic viewport, scrollbar, and footer.
/// Renders aircraft data with selection highlighting and unit-aware formatting.
/// </summary>
internal static class LiveAircraftTableBuilder
{
    /// <summary>
    /// Builds aircraft table with dynamic viewport.
    /// </summary>
    /// <param name="sortedAircraft">Aircraft list sorted by ICAO for stable display order.</param>
    /// <param name="stats">Stream statistics from ReceiverStream, or null in client mode.</param>
    /// <param name="selectedRow">Index of currently selected row for highlighting.</param>
    /// <param name="distanceUnit">Unit to display distances (miles or kilometers).</param>
    /// <param name="altitudeUnit">Unit to display altitudes (feet or meters).</param>
    /// <param name="speedUnit">Unit to display speeds (knots, km/h, or mph).</param>
    /// <param name="receiverConfig">Receiver location for distance calculation, or null if not configured.</param>
    /// <returns>Spectre.Console Table with aircraft data, footer, and fixed 120-character width.</returns>
    public static Table Build(
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

            // Format messages: right-align to 8 chars
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

        return table;
    }
}
