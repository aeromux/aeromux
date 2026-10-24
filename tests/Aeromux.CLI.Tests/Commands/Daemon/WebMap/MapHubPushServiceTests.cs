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
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.CLI.Commands.Daemon.WebMap;
using Aeromux.CLI.Tests.Api;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Aeromux.CLI.Tests.Commands.Daemon.WebMap;

/// <summary>
/// Tests for what the push service sends each Web Map client on a tick. Each test owns its
/// client states, so the push services started by the API test fixtures cannot touch them.
/// </summary>
public class MapHubPushServiceTests
{
    private static readonly GeographicBounds Hungary = new(45.5, 16.0, 48.6, 23.0);

    private readonly ConcurrentDictionary<string, MapHubClientState> _clientStates = new();
    private readonly Dictionary<string, RecordingClient> _proxies = new();
    private readonly Mock<IAircraftStateTracker> _tracker = new();
    private readonly Mock<IHubContext<MapHub>> _hub = new();
    private List<Aircraft> _aircraft = [];

    public MapHubPushServiceTests()
    {
        _tracker.Setup(t => t.GetAllAircraft()).Returns(() => _aircraft);
        _tracker.Setup(t => t.GetAircraft(It.IsAny<string>()))
            .Returns((string icao) => _aircraft.FirstOrDefault(a => a.Identification.ICAO == icao));

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Client(It.IsAny<string>())).Returns((string id) => _proxies[id]);
        _hub.Setup(h => h.Clients).Returns(clients.Object);
    }

    // --- Viewport and diffs ---

    [Fact]
    public async Task NoViewport_SendsOnlyMetadata()
    {
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (_, RecordingClient client) = Connect("c1", viewport: null);

        using MapHubPushService service = CreateService();
        await TickAsync(service);

        client.Methods.Should().Equal("Metadata");
    }

    [Fact]
    public async Task AircraftInView_IsPushedOnce_UntilARenderedFieldChanges()
    {
        _aircraft = [At("AAA001", 47.0, 19.0, feet: 36000)];
        (_, RecordingClient client) = Connect("c1", Hungary);
        using MapHubPushService service = CreateService();

        await TickAsync(service);
        client.Sent("AircraftUpdated").Should().ContainSingle();

        client.Clear();
        await TickAsync(service);
        client.Sent("AircraftUpdated").Should().BeEmpty();

        client.Clear();
        _aircraft = [At("AAA001", 47.0, 19.0, feet: 35000)];
        await TickAsync(service);
        client.Sent("AircraftUpdated").Should().ContainSingle()
            .Which[0].As<AircraftListItem>().BarometricAltitude!.Feet.Should().Be(35000);
    }

    [Fact]
    public async Task TelemetryOnlyChange_IsNotPushed()
    {
        Aircraft aircraft = At("AAA001", 47.0, 19.0);
        _aircraft = [aircraft];
        (_, RecordingClient client) = Connect("c1", Hungary);
        using MapHubPushService service = CreateService();
        await TickAsync(service);
        client.Clear();

        // Signal strength, message count and last-seen time change with every message, so they
        // are deliberately left out of the change hash.
        _aircraft = [aircraft with
        {
            Status = aircraft.Status with
            {
                SignalStrength = 5.0,
                TotalMessages = aircraft.Status.TotalMessages + 50,
                LastSeen = aircraft.Status.LastSeen.AddSeconds(3)
            }
        }];
        await TickAsync(service);

        client.Sent("AircraftUpdated").Should().BeEmpty();
    }

    [Fact]
    public async Task AircraftLeavingTheView_IsRemoved()
    {
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (_, RecordingClient client) = Connect("c1", Hungary);
        using MapHubPushService service = CreateService();
        await TickAsync(service);
        client.Clear();

        _aircraft = [At("AAA001", 52.0, 19.0)];
        await TickAsync(service);

        client.Sent("AircraftRemoved").Should().ContainSingle().Which[0].Should().Be("AAA001");
    }

    [Fact]
    public async Task ExpiredAircraft_IsRemoved()
    {
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (MapHubClientState state, RecordingClient client) = Connect("c1", Hungary);
        using MapHubPushService service = CreateService();
        await TickAsync(service);
        client.Clear();

        _aircraft = [];
        await TickAsync(service);

        client.Sent("AircraftRemoved").Should().ContainSingle().Which[0].Should().Be("AAA001");
        state.LastPushedAircraft.Should().BeEmpty();
    }

    [Fact]
    public async Task AircraftWithoutPosition_IsNeverPushed()
    {
        Aircraft noPosition = ApiTestFixture.CreateTestAircraft("AAA001") with { Position = new TrackedPosition() };
        _aircraft = [noPosition];
        (_, RecordingClient client) = Connect("c1", new GeographicBounds(-90, -180, 90, 180));

        using MapHubPushService service = CreateService();
        await TickAsync(service);

        client.Sent("AircraftUpdated").Should().BeEmpty();
    }

    [Fact]
    public async Task ViewportAcrossTheMeridian_ReceivesAircraftOnBothSides()
    {
        _aircraft = [At("EAST01", -40.0, 178.0), At("WEST01", -40.0, -178.0), At("AWAY01", -40.0, 0.0)];
        (_, RecordingClient client) = Connect("c1", new GeographicBounds(-50, 170, -30, -170));

        using MapHubPushService service = CreateService();
        await TickAsync(service);

        client.Sent("AircraftUpdated").Select(a => a[0].As<AircraftListItem>().ICAO)
            .Should().BeEquivalentTo("EAST01", "WEST01");
    }

    // --- Selection ---

    [Fact]
    public async Task SelectedAircraft_DetailIsPushedOnEveryTick()
    {
        // The detail carries its own Timestamp, so it is re-sent each tick even when nothing
        // else changed. The Web Map adds a flight-profile point per detail push and relies on it.
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (MapHubClientState state, RecordingClient client) = Connect("c1", Hungary);
        state.SelectedIcao = "AAA001";
        using MapHubPushService service = CreateService();

        await TickAsync(service);
        await TickAsync(service);

        client.Sent("AircraftDetailUpdated").Should().HaveCount(2);
        client.Sent("AircraftDetailUpdated")[0][0].As<Dictionary<string, object?>>()
            .Should().ContainKeys("Timestamp", "Identification", "Position", "DataQuality");
    }

    [Fact]
    public async Task SelectedAircraft_LeavingTheView_IsNotRemoved()
    {
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (MapHubClientState state, RecordingClient client) = Connect("c1", Hungary);
        state.SelectedIcao = "AAA001";
        using MapHubPushService service = CreateService();
        await TickAsync(service);
        client.Clear();

        _aircraft = [At("AAA001", 52.0, 19.0)];
        await TickAsync(service);

        client.Sent("AircraftRemoved").Should().BeEmpty();
        client.Sent("AircraftDetailUpdated").Should().ContainSingle();
    }

    [Fact]
    public async Task SelectedAircraft_Expiring_IsRemovedAndDeselected()
    {
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (MapHubClientState state, RecordingClient client) = Connect("c1", Hungary);
        state.SelectedIcao = "AAA001";
        using MapHubPushService service = CreateService();
        await TickAsync(service);
        client.Clear();

        _aircraft = [];
        await TickAsync(service);

        client.Sent("AircraftRemoved").Should().ContainSingle().Which[0].Should().Be("AAA001");
        state.SelectedIcao.Should().BeNull();
        state.LastPushedDetailHash.Should().Be(0);
        state.LastPushedAircraft.Should().BeEmpty();

        // Once deselected, the next tick must not remove it a second time.
        client.Clear();
        await TickAsync(service);
        client.Sent("AircraftRemoved").Should().BeEmpty();
    }

    // --- Overlays ---

    [Fact]
    public async Task RangeOutline_IsPushedFirst_ThenOnlyWhenItChanges()
    {
        // The tracker reports an outline only once three bearing sectors are populated,
        // so the aircraft sit north, east and south of the receiver.
        var outline = new RangeOutlineTracker(47.0, 19.0);
        _aircraft = [At("NORTH1", 47.5, 19.0), At("EAST01", 47.0, 20.0), At("SOUTH1", 46.5, 19.0)];
        (_, RecordingClient client) = Connect("c1", Hungary);
        using MapHubPushService service = CreateService(outline: outline);

        await TickAsync(service);
        client.Sent("RangeOutlineUpdated").Should().ContainSingle()
            .Which[0].As<List<RangeOutlineCoordinate>>().Should().HaveCount(3);

        client.Clear();
        await TickAsync(service);
        client.Sent("RangeOutlineUpdated").Should().BeEmpty();

        client.Clear();
        _aircraft = [At("NORTH1", 48.5, 19.0), At("EAST01", 47.0, 20.0), At("SOUTH1", 46.5, 19.0)]; // north reaches farther
        await TickAsync(service);
        client.Sent("RangeOutlineUpdated").Should().ContainSingle();
    }

    [Fact]
    public async Task Heatmap_IsPushedWhenEnabled_NotRepeated_AndReprojectedAfterAPan()
    {
        var heatmap = new HeatmapTracker(47.0);
        _aircraft = [At("AAA001", 47.0, 19.0)];
        (MapHubClientState state, RecordingClient client) = Connect("c1", Hungary);
        state.HeatmapEnabled = true;
        using MapHubPushService service = CreateService(heatmap: heatmap);

        await TickAsync(service);
        client.Sent("HeatmapUpdated").Should().ContainSingle();

        // Same view, snapshot still fresh: nothing to send.
        client.Clear();
        await TickAsync(service);
        client.Sent("HeatmapUpdated").Should().BeEmpty();

        // A pan to where there is no traffic re-projects the overlay to an empty one.
        client.Clear();
        state.ViewportBounds = new GeographicBounds(-10, -10, 10, 10);
        await TickAsync(service);
        client.Sent("HeatmapUpdated").Should().ContainSingle();
    }

    [Fact]
    public async Task NoClients_PositionsStillFeedTheHeatmap()
    {
        var heatmap = new HeatmapTracker(47.0);
        _aircraft = [At("AAA001", 47.0, 19.0)];

        using MapHubPushService service = CreateService(heatmap: heatmap);
        await TickAsync(service);

        heatmap.PopulatedBaseCellCount.Should().Be(1);
    }

    // --- Resilience ---

    [Fact]
    public async Task FailingClient_DoesNotStopPushesToOthers()
    {
        _aircraft = [At("AAA001", 47.0, 19.0)];
        Connect("broken", Hungary, throws: true);
        (_, RecordingClient healthy) = Connect("healthy", Hungary);

        using MapHubPushService service = CreateService();
        await TickAsync(service);

        healthy.Sent("AircraftUpdated").Should().ContainSingle();
        healthy.Sent("Metadata").Should().ContainSingle();
    }

    // --- Helpers ---

    private MapHubPushService CreateService(RangeOutlineTracker? outline = null, HeatmapTracker? heatmap = null) =>
        new(_tracker.Object, _hub.Object, _clientStates, outline, heatmap);

    private static Task TickAsync(MapHubPushService service) => service.PushUpdates(CancellationToken.None);

    private (MapHubClientState State, RecordingClient Client) Connect(
        string connectionId, GeographicBounds? viewport, bool throws = false)
    {
        var state = new MapHubClientState { ViewportBounds = viewport };
        var client = new RecordingClient(throws);
        _clientStates[connectionId] = state;
        _proxies[connectionId] = client;
        return (state, client);
    }

    private static Aircraft At(string icao, double lat, double lon, int feet = 36000)
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(icao);
        return aircraft with
        {
            Position = aircraft.Position with
            {
                Coordinate = new GeographicCoordinate(lat, lon),
                BarometricAltitude = Altitude.FromFeet(feet, AltitudeType.Barometric)
            }
        };
    }

    /// <summary>
    /// Records every message sent to one client, or fails every send when asked to.
    /// </summary>
    private sealed class RecordingClient(bool throws) : ISingleClientProxy
    {
        private readonly List<(string Method, object?[] Args)> _calls = [];

        public IEnumerable<string> Methods => _calls.Select(c => c.Method);

        public List<object?[]> Sent(string method) =>
            _calls.Where(c => c.Method == method).Select(c => c.Args).ToList();

        public void Clear() => _calls.Clear();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (throws)
            {
                throw new InvalidOperationException("Connection lost");
            }

            _calls.Add((method, args));
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
