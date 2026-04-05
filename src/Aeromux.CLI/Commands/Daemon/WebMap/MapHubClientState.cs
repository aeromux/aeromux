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

namespace Aeromux.CLI.Commands.Daemon.WebMap;

/// <summary>
/// Per-connection state for a MapHub client.
/// Tracks the client's viewport bounds, selected aircraft, and last push snapshot
/// for change detection.
/// </summary>
public sealed class MapHubClientState
{
    /// <summary>
    /// The client's current viewport bounds (south, west, north, east).
    /// Null until the client sends its first UpdateViewport call.
    /// </summary>
    public (double South, double West, double North, double East)? ViewportBounds { get; set; }

    /// <summary>
    /// The ICAO address of the aircraft the client has selected for detail view.
    /// Null when no aircraft is selected.
    /// </summary>
    public string? SelectedIcao { get; set; }

    /// <summary>
    /// Maps ICAO address to a hash of the last pushed state for that aircraft.
    /// Used to detect changes and only push diffs.
    /// </summary>
    public Dictionary<string, int> LastPushedAircraft { get; } = new();

    /// <summary>
    /// Hash of the last pushed detail object for the selected aircraft.
    /// Used to avoid pushing unchanged detail data.
    /// </summary>
    public int LastPushedDetailHash { get; set; }
}
