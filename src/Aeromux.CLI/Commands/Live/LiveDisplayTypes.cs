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

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Unit for distance display (miles or kilometers).
/// </summary>
internal enum DistanceUnit { Miles, Kilometers }

/// <summary>
/// Unit for altitude display (feet or meters).
/// </summary>
internal enum AltitudeUnit { Feet, Meters }

/// <summary>
/// Unit for speed display (knots, km/h, or mph).
/// </summary>
internal enum SpeedUnit { Knots, KilometersPerHour, MilesPerHour }

/// <summary>
/// Represents a row in the aircraft detail view.
/// </summary>
/// <param name="Field">The field name displayed in the left column.</param>
/// <param name="Value">The field value displayed in the right column.</param>
/// <param name="IsSectionHeader">True if this row is a section header or separator (non-selectable).</param>
internal record DetailRow(string Field, string Value, bool IsSectionHeader = false);

/// <summary>
/// Column used for sorting the aircraft table.
/// </summary>
internal enum SortColumn { ICAO, Callsign, Altitude, Vertical, Distance, Speed }

/// <summary>
/// Sort direction for the aircraft table.
/// </summary>
internal enum SortDirection { Ascending, Descending }

/// <summary>
/// Mutable state for the live TUI display. Consolidates all state variables
/// previously scattered as locals in LiveTuiDisplay.RunAsync().
/// Passed by reference to keyboard handlers and builders.
/// </summary>
internal sealed class LiveTuiState
{
    // Selection
    public string? SelectedIcao { get; set; }
    public int SelectedRow { get; set; }

    // View mode
    public bool ShowingDetails { get; set; }
    public int DetailViewSelectedRow { get; set; }
    public List<DetailRow>? CurrentDetailRows { get; set; }
    public Aircraft? DetailAircraft { get; set; }
    public bool DetailAircraftExpired { get; set; }

    // Display units
    public DistanceUnit DistanceUnit { get; set; } = DistanceUnit.Miles;
    public AltitudeUnit AltitudeUnit { get; set; } = AltitudeUnit.Feet;
    public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.Knots;

    // Sorting
    public SortColumn SortColumn { get; set; } = SortColumn.ICAO;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

    // Search (table view)
    public bool IsSearchActive { get; set; }
    public string SearchInput { get; set; } = "";
    public string? PreSearchSelectedIcao { get; set; }

    // Search (detail view)
    public bool IsDetailSearchActive { get; set; }
    public string DetailSearchInput { get; set; } = "";
    public int PreSearchDetailSelectedRow { get; set; }

    /// <summary>
    /// Resets sort, units, and search to defaults. Preserves selection (FR-RESET-02).
    /// </summary>
    public void ResetToDefaults()
    {
        SortColumn = SortColumn.ICAO;
        SortDirection = SortDirection.Ascending;
        DistanceUnit = DistanceUnit.Miles;
        AltitudeUnit = AltitudeUnit.Feet;
        SpeedUnit = SpeedUnit.Knots;
        IsSearchActive = false;
        SearchInput = "";
        PreSearchSelectedIcao = null;
        IsDetailSearchActive = false;
        DetailSearchInput = "";
        PreSearchDetailSelectedRow = 0;
        DetailAircraft = null;
        DetailAircraftExpired = false;
    }
}
