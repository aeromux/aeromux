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
using Aeromux.Core.Tracking;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace Aeromux.CLI.Commands.Daemon.WebMap;

/// <summary>
/// Background service that pushes real-time aircraft updates to connected MapHub clients.
/// Runs a 1-second push loop, computes per-client diffs based on viewport and change detection.
/// </summary>
public sealed class MapHubPushService : BackgroundService
{
    private readonly IAircraftStateTracker _tracker;
    private readonly IHubContext<MapHub> _hubContext;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes the push service with the aircraft tracker and hub context.
    /// </summary>
    public MapHubPushService(IAircraftStateTracker tracker, IHubContext<MapHub> hubContext)
    {
        _tracker = tracker;
        _hubContext = hubContext;
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
            catch (Exception)
            {
                // Swallow exceptions to keep the loop running
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task PushUpdates(CancellationToken cancellationToken)
    {
        IReadOnlyList<Aircraft> allAircraft = _tracker.GetAllAircraft();
        int totalCount = allAircraft.Count;

        foreach ((string connectionId, MapHubClientState state) in MapHub.ClientStates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await PushToClient(connectionId, state, allAircraft, totalCount, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Client may have disconnected — skip silently
            }
        }
    }

    private async Task PushToClient(
        string connectionId,
        MapHubClientState state,
        IReadOnlyList<Aircraft> allAircraft,
        int totalCount,
        CancellationToken cancellationToken)
    {
        IClientProxy client = _hubContext.Clients.Client(connectionId);

        // Filter aircraft by viewport
        Dictionary<string, AircraftListItem> visibleAircraft = new();

        if (state.ViewportBounds is not null)
        {
            foreach (Aircraft aircraft in allAircraft)
            {
                if (aircraft.Position.Coordinate is null)
                {
                    continue;
                }

                double lat = aircraft.Position.Coordinate.Latitude;
                double lon = aircraft.Position.Coordinate.Longitude;

                if (IsInViewport(lat, lon, state))
                {
                    AircraftListItem item = DaemonApiMapper.ToListItem(aircraft);
                    visibleAircraft[aircraft.Identification.ICAO] = item;
                }
            }
        }

        // Compute diffs: new/changed aircraft
        foreach ((string icao, AircraftListItem item) in visibleAircraft)
        {
            int hash = ComputeHash(item);

            if (!state.LastPushedAircraft.TryGetValue(icao, out int lastHash) || lastHash != hash)
            {
                await client.SendAsync("AircraftUpdated", item, cancellationToken);
                state.LastPushedAircraft[icao] = hash;
            }
        }

        // Compute diffs: removed aircraft (left viewport or expired)
        List<string> toRemove = new();
        foreach (string icao in state.LastPushedAircraft.Keys)
        {
            if (!visibleAircraft.ContainsKey(icao))
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

    private int ComputeHash(object obj)
    {
        string json = JsonSerializer.Serialize(obj, _jsonOptions);
        return json.GetHashCode(StringComparison.Ordinal);
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
