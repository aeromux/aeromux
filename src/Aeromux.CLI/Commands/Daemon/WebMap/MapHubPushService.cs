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

using System.Text.Json;
using System.Text.Json.Serialization;
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon.WebMap;

/// <summary>
/// Background service that pushes real-time aircraft updates to connected MapHub clients.
/// Runs a 1-second push loop, computes per-client diffs based on viewport and change detection.
/// </summary>
public sealed class MapHubPushService : BackgroundService
{
    private static readonly TimeSpan ClientPushTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan HeatmapPruneInterval = TimeSpan.FromSeconds(60);

    private readonly IAircraftStateTracker _tracker;
    private readonly IHubContext<MapHub> _hubContext;
    private readonly RangeOutlineTracker? _rangeOutlineTracker;
    private readonly HeatmapTracker? _heatmapTracker;
    private readonly JsonSerializerOptions _jsonOptions;
    private DateTime _lastHeatmapPrune = DateTime.MinValue;

    /// <summary>
    /// Initializes the push service with the aircraft tracker, hub context,
    /// and optional range-outline and heatmap trackers.
    /// </summary>
    public MapHubPushService(
        IAircraftStateTracker tracker,
        IHubContext<MapHub> hubContext,
        RangeOutlineTracker? rangeOutlineTracker = null,
        HeatmapTracker? heatmapTracker = null)
    {
        _tracker = tracker;
        _hubContext = hubContext;
        _rangeOutlineTracker = rangeOutlineTracker;
        _heatmapTracker = heatmapTracker;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PushUpdates(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MapHubPushService push iteration failed");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task PushUpdates(CancellationToken cancellationToken)
    {
        IReadOnlyList<Aircraft> allAircraft = _tracker.GetAllAircraft();
        int totalCount = allAircraft.Count;
        bool hasClients = !MapHub.ClientStates.IsEmpty;

        // Single pass over all aircraft:
        //   - Feed positions into the range-outline and heatmap trackers (always, regardless
        //     of clients, whenever either tracker is registered).
        //   - Project + change-hash each positioned aircraft once per tick, shared across
        //     all clients. Skipped entirely when no client is connected so an idle daemon
        //     does no per-aircraft mapping work.
        var snapshot = new List<(string Icao, AircraftListItem Item, int Hash)>(hasClients ? allAircraft.Count : 0);
        if (_rangeOutlineTracker is not null || _heatmapTracker is not null || hasClients)
        {
            foreach (Aircraft aircraft in allAircraft)
            {
                if (aircraft.Position.Coordinate is null)
                {
                    continue;
                }

                _rangeOutlineTracker?.RecordPosition(aircraft.Position.Coordinate);
                _heatmapTracker?.RecordPosition(aircraft.Identification.ICAO, aircraft.Position.Coordinate);

                if (hasClients)
                {
                    AircraftListItem item = DaemonApiMapper.ToListItem(aircraft);
                    snapshot.Add((aircraft.Identification.ICAO, item, ComputeListItemHash(item)));
                }
            }
        }

        // Periodically prune stale heatmap entries (once per minute, independent of clients).
        if (_heatmapTracker is not null && DateTime.UtcNow - _lastHeatmapPrune > HeatmapPruneInterval)
        {
            _heatmapTracker.Prune();
            _lastHeatmapPrune = DateTime.UtcNow;
        }

        // Compute the coverage outline once for all clients.
        List<RangeOutlineCoordinate>? outline = _rangeOutlineTracker?.GetOutline();
        int outlineHash = outline is not null ? ComputeHash(outline) : 0;

        foreach ((string connectionId, MapHubClientState state) in MapHub.ClientStates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var clientCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            clientCts.CancelAfter(ClientPushTimeout);

            try
            {
                await PushToClient(connectionId, state, snapshot, totalCount, outline, outlineHash, clientCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("MapHubPushService: client {ConnectionId} push timed out after {Timeout}s, skipping",
                    connectionId, ClientPushTimeout.TotalSeconds);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MapHubPushService failed to push to client {ConnectionId}", connectionId);
            }
        }
    }

    private async Task PushToClient(
        string connectionId,
        MapHubClientState state,
        IReadOnlyList<(string Icao, AircraftListItem Item, int Hash)> snapshot,
        int totalCount,
        List<RangeOutlineCoordinate>? outline,
        int outlineHash,
        CancellationToken cancellationToken)
    {
        IClientProxy client = _hubContext.Clients.Client(connectionId);

        // Filter the shared snapshot by this client's viewport and diff against
        // its last-pushed hashes. Each item/hash was computed once per tick in
        // PushUpdates and is reused across all clients here.
        HashSet<string> visibleIcaos = new();

        foreach ((string icao, AircraftListItem item, int hash) in snapshot)
        {
            GeographicCoordinate coordinate = item.Coordinate!; // non-null by snapshot construction
            if (!IsInViewport(coordinate.Latitude, coordinate.Longitude, state))
            {
                continue;
            }

            visibleIcaos.Add(icao);

            if (!state.LastPushedAircraft.TryGetValue(icao, out int lastHash) || lastHash != hash)
            {
                await client.SendAsync("AircraftUpdated", item, cancellationToken);
                state.LastPushedAircraft[icao] = hash;
            }
        }

        // Compute diffs: remove aircraft that left the viewport or expired.
        // The selected aircraft is excluded — its lifecycle is managed by the detail push block below.
        List<string> toRemove = new();
        foreach (string icao in state.LastPushedAircraft.Keys)
        {
            if (!visibleIcaos.Contains(icao) && icao != state.SelectedIcao)
            {
                await client.SendAsync("AircraftRemoved", icao, cancellationToken);
                toRemove.Add(icao);
            }
        }

        foreach (string icao in toRemove)
        {
            state.LastPushedAircraft.Remove(icao);
        }

        // Push detail for selected aircraft
        if (state.SelectedIcao is not null)
        {
            Aircraft? selectedAircraft = _tracker.GetAircraft(state.SelectedIcao);
            if (selectedAircraft is not null)
            {
                var detail = BuildDetailResponse(selectedAircraft);
                int detailHash = ComputeHash(detail);

                if (detailHash != state.LastPushedDetailHash)
                {
                    await client.SendAsync("AircraftDetailUpdated", detail, cancellationToken);
                    state.LastPushedDetailHash = detailHash;
                }
            }
            else
            {
                // Aircraft expired — notify client
                await client.SendAsync("AircraftRemoved", state.SelectedIcao, cancellationToken);
                state.SelectedIcao = null;
                state.LastPushedDetailHash = 0;
            }
        }

        // Push metadata
        await client.SendAsync("Metadata", new { TotalAircraftCount = totalCount }, cancellationToken);

        // Push range outline if changed
        if (outline is not null && outlineHash != state.LastPushedOutlineHash)
        {
            await client.SendAsync("RangeOutlineUpdated", outline, cancellationToken);
            state.LastPushedOutlineHash = outlineHash;
        }

        // Push heatmap for this client if enabled (and collection is running).
        if (_heatmapTracker is not null && state.HeatmapEnabled)
        {
            HeatmapResult result = _heatmapTracker.GetCells(
                state.HeatmapCellSizeNm, state.HeatmapWindow, state.ViewportBounds,
                state.HeatmapLastScaleMax);
            state.HeatmapLastScaleMax = result.ScaleMax; // smoothing continuity

            var heatmap = new
            {
                CellSizeNm = state.HeatmapCellSizeNm,
                WindowMinutes = (int)state.HeatmapWindow.TotalMinutes,
                ScaleMax = result.ScaleMax,
                MaxCount = result.MaxCount,
                Cells = result.Cells
            };
            int heatmapHash = ComputeHash(heatmap);
            if (heatmapHash != state.LastPushedHeatmapHash)
            {
                await client.SendAsync("HeatmapUpdated", heatmap, cancellationToken);
                state.LastPushedHeatmapHash = heatmapHash;
            }
        }
    }

    private static Dictionary<string, object?> BuildDetailResponse(Aircraft aircraft)
    {
        return new Dictionary<string, object?>
        {
            ["Timestamp"] = DateTime.UtcNow,
            ["Identification"] = DaemonApiMapper.ToIdentification(aircraft),
            ["DatabaseRecord"] = DaemonApiMapper.ToDatabaseRecord(aircraft),
            ["Status"] = DaemonApiMapper.ToStatus(aircraft),
            ["Position"] = DaemonApiMapper.ToPosition(aircraft),
            ["VelocityAndDynamics"] = DaemonApiMapper.ToVelocityAndDynamics(aircraft),
            ["Autopilot"] = DaemonApiMapper.ToAutopilot(aircraft),
            ["Meteorology"] = DaemonApiMapper.ToMeteorology(aircraft),
            ["Acas"] = DaemonApiMapper.ToAcas(aircraft),
            ["Capabilities"] = DaemonApiMapper.ToCapabilities(aircraft),
            ["DataQuality"] = DaemonApiMapper.ToDataQuality(aircraft)
        };
    }

    /// <summary>
    /// Computes a hash by JSON-serializing the object and hashing the resulting string.
    /// Trades a short-lived string allocation for simple, reliable deep-equality detection.
    /// Used for the cold detail and outline paths.
    /// </summary>
    /// <param name="obj">The object to hash; serialized with the shared push options.</param>
    /// <returns>An ordinal hash of the object's JSON representation.</returns>
    private int ComputeHash(object obj)
    {
        string json = JsonSerializer.Serialize(obj, _jsonOptions);
        return json.GetHashCode(StringComparison.Ordinal);
    }

    /// <summary>
    /// Computes an allocation-free change hash for a list item over its render-affecting
    /// fields. Deliberately excludes <see cref="AircraftListItem.SignalStrength"/>,
    /// <see cref="AircraftListItem.TotalMessages"/>, and <see cref="AircraftListItem.LastSeen"/>
    /// so per-message telemetry churn does not trigger a push for an otherwise-unchanged
    /// aircraft. Any change to a rendered field still sends the full item, so moving aircraft
    /// stay fully fresh; the detail panel keeps those fields live for the selected aircraft.
    /// </summary>
    /// <param name="i">The list item to hash.</param>
    /// <returns>An allocation-free hash over the item's render-affecting fields.</returns>
    private static int ComputeListItemHash(AircraftListItem i)
    {
        HashCode hash = new();
        hash.Add(i.ICAO);
        hash.Add(i.Callsign);
        hash.Add(i.Squawk);
        hash.Add(i.Category);
        hash.Add(i.Coordinate);
        hash.Add(i.BarometricAltitude);
        hash.Add(i.GeometricAltitude);
        hash.Add(i.IsOnGround);
        hash.Add(i.Speed);
        hash.Add(i.Track);
        hash.Add(i.SpeedOnGround);
        hash.Add(i.TrackOnGround);
        hash.Add(i.VerticalRate);
        hash.Add(i.DatabaseEnabled);
        hash.Add(i.Registration);
        hash.Add(i.TypeCode);
        hash.Add(i.TypeIcaoClass);
        hash.Add(i.TypeWtc);
        hash.Add(i.OperatorName);
        hash.Add(i.Military);
        hash.Add(i.Ladd);
        hash.Add(i.Pia);
        return hash.ToHashCode();
    }

    private static bool IsInViewport(double lat, double lon, MapHubClientState state)
    {
        if (state.ViewportBounds is null)
        {
            return false;
        }

        (double south, double west, double north, double east) = state.ViewportBounds.Value;
        return lat >= south && lat <= north && lon >= west && lon <= east;
    }
}
