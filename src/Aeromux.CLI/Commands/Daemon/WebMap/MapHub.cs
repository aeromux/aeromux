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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace Aeromux.CLI.Commands.Daemon.WebMap;

/// <summary>
/// SignalR hub for real-time web map communication.
/// Manages per-client viewport tracking, aircraft selection, and push state.
/// </summary>
public sealed partial class MapHub : Hub
{
    /// <summary>
    /// Shared client state dictionary accessible by the push service.
    /// </summary>
    internal static readonly ConcurrentDictionary<string, MapHubClientState> ClientStates = new();

    [GeneratedRegex(@"^[0-9A-Fa-f]{6}$")]
    private static partial Regex IcaoPattern();

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        ClientStates.TryAdd(Context.ConnectionId, new MapHubClientState());
        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        ClientStates.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Updates the client's viewport bounds. The server only pushes aircraft within these bounds.
    /// </summary>
    public void UpdateViewport(double south, double west, double north, double east)
    {
        if (south >= north || south < -90 || north > 90 || west < -180 || east > 180)
        {
            return;
        }

        if (ClientStates.TryGetValue(Context.ConnectionId, out MapHubClientState? state))
        {
            state.ViewportBounds = (south, west, north, east);
        }
    }

    /// <summary>
    /// Sets the aircraft the client is viewing in detail.
    /// The server begins pushing AircraftDetailUpdated events for this aircraft.
    /// </summary>
    public void SelectAircraft(string icao)
    {
        ArgumentNullException.ThrowIfNull(icao);
        if (!IcaoPattern().IsMatch(icao))
        {
            return;
        }

        if (ClientStates.TryGetValue(Context.ConnectionId, out MapHubClientState? state))
        {
            state.SelectedIcao = icao.ToUpperInvariant();
            state.LastPushedDetailHash = 0;
        }
    }

    /// <summary>
    /// Clears the selected aircraft. The server stops pushing detail updates.
    /// </summary>
    public void DeselectAircraft()
    {
        if (ClientStates.TryGetValue(Context.ConnectionId, out MapHubClientState? state))
        {
            state.SelectedIcao = null;
            state.LastPushedDetailHash = 0;
        }
    }

    private static readonly int[] AllowedHeatmapCellSizesNm = [2, 5, 10, 20, 40];
    private static readonly int[] AllowedHeatmapWindowMinutes = [60, 360, 720, 1440];

    /// <summary>
    /// Sets the client's heatmap overlay parameters. Out-of-range values fall back to
    /// defaults (5 nm / 24 h). Toggling off stops server heatmap pushes.
    /// </summary>
    /// <param name="enabled">Whether this client renders the heatmap overlay.</param>
    /// <param name="cellSizeNm">Display cell size in nm (one of 2/5/10/20/40).</param>
    /// <param name="windowMinutes">Rolling window in minutes (one of 60/360/720/1440).</param>
    public void UpdateHeatmap(bool enabled, int cellSizeNm, int windowMinutes)
    {
        if (!AllowedHeatmapCellSizesNm.Contains(cellSizeNm))
        {
            cellSizeNm = 5;
        }
        if (!AllowedHeatmapWindowMinutes.Contains(windowMinutes))
        {
            windowMinutes = 1440;
        }

        if (ClientStates.TryGetValue(Context.ConnectionId, out MapHubClientState? state))
        {
            bool changed = state.HeatmapCellSizeNm != cellSizeNm
                           || state.HeatmapWindow != TimeSpan.FromMinutes(windowMinutes);
            state.HeatmapEnabled = enabled;
            state.HeatmapCellSizeNm = cellSizeNm;
            state.HeatmapWindow = TimeSpan.FromMinutes(windowMinutes);
            if (changed)
            {
                // Parameters changed — re-derive the colour scale cleanly and force a re-push.
                state.HeatmapLastScaleMax = 0;
                state.LastPushedHeatmapHash = 0;
            }
        }
    }
}
