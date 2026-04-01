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

using System.Net;
using System.Text.Json;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

public class AircraftHistoryTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task History_NoTypeParam_ReturnsAllFourTypes()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Altitude", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Velocity", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("State", out _).Should().BeTrue();
    }

    [Fact]
    public async Task History_TypeParam_FiltersToRequestedType()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Altitude", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("Velocity", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("State", out _).Should().BeFalse();
    }

    [Fact]
    public async Task History_TypeParam_CaseInsensitive()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
    }

    [Fact]
    public async Task History_EmptyTypeParam_ReturnsAll()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Altitude", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Velocity", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("State", out _).Should().BeTrue();
    }

    [Fact]
    public async Task History_LimitParam_ReturnsLimitedEntries()
    {
        var posBuffer = new CircularBuffer<PositionSnapshot>(100);
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow.AddSeconds(-2), new GeographicCoordinate(47.0, 18.0), null));
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow.AddSeconds(-1), new GeographicCoordinate(47.1, 18.1), null));
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow, new GeographicCoordinate(47.2, 18.2), null));

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory
            {
                PositionHistory = posBuffer,
                AltitudeHistory = new CircularBuffer<AltitudeSnapshot>(100),
                VelocityHistory = new CircularBuffer<VelocitySnapshot>(100)
            }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position&limit=2")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Entries").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task History_InvalidLimit_Returns400(string limit)
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync($"/api/v1/aircraft/407F19/history?limit={limit}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_Disabled_ReturnsEnabledFalseNoCapacityOrEntries()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Enabled").GetBoolean().Should().BeFalse();
        pos.TryGetProperty("Capacity", out JsonElement capacity).Should().BeTrue();
        capacity.ValueKind.Should().Be(JsonValueKind.Null);
        pos.TryGetProperty("Entries", out JsonElement entries).Should().BeTrue();
        entries.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task History_Enabled_ReturnsEntriesWithMetadata()
    {
        var posBuffer = new CircularBuffer<PositionSnapshot>(100);
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow, new GeographicCoordinate(47.0, 18.0), null));

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory
            {
                PositionHistory = posBuffer,
                AltitudeHistory = new CircularBuffer<AltitudeSnapshot>(100),
                VelocityHistory = new CircularBuffer<VelocitySnapshot>(100)
            }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Enabled").GetBoolean().Should().BeTrue();
        pos.GetProperty("Capacity").GetInt32().Should().Be(100);
        pos.GetProperty("Count").GetInt32().Should().Be(1);
        pos.GetProperty("MinSequenceId").GetInt64().Should().Be(1);
        pos.GetProperty("MaxSequenceId").GetInt64().Should().Be(1);
        pos.GetProperty("Entries").GetArrayLength().Should().Be(1);

        JsonElement entry = pos.GetProperty("Entries")[0];
        entry.GetProperty("SequenceId").GetInt64().Should().Be(1);
    }

    // === after parameter tests ===

    [Fact]
    public async Task History_AfterParam_ReturnsOnlyNewEntries()
    {
        var posBuffer = new CircularBuffer<PositionSnapshot>(100);
        for (int i = 0; i < 5; i++)
        {
            posBuffer.Add(new PositionSnapshot(DateTime.UtcNow.AddSeconds(i), new GeographicCoordinate(47.0 + i * 0.1, 18.0), null));
        }

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory { PositionHistory = posBuffer }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position&after=3")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Count").GetInt32().Should().Be(5); // Total buffer count
        pos.GetProperty("Entries").GetArrayLength().Should().Be(2); // Only entries 4 and 5
        pos.GetProperty("Entries")[0].GetProperty("SequenceId").GetInt64().Should().Be(4);
        pos.GetProperty("Entries")[1].GetProperty("SequenceId").GetInt64().Should().Be(5);
    }

    [Fact]
    public async Task History_AfterParam_NoNewData_ReturnsEmpty()
    {
        var posBuffer = new CircularBuffer<PositionSnapshot>(100);
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow, new GeographicCoordinate(47.0, 18.0), null));

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory { PositionHistory = posBuffer }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position&after=1")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Entries").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task History_AfterParam_Zero_ReturnsAll()
    {
        var posBuffer = new CircularBuffer<PositionSnapshot>(100);
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow, new GeographicCoordinate(47.0, 18.0), null));
        posBuffer.Add(new PositionSnapshot(DateTime.UtcNow, new GeographicCoordinate(47.1, 18.1), null));

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory { PositionHistory = posBuffer }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position&after=0")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Position").GetProperty("Entries").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task History_AfterParam_Invalid_Returns400(string after)
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync($"/api/v1/aircraft/407F19/history?type=Position&after={after}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_AfterParam_WithoutType_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?after=42");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("requires a type");
    }

    [Fact]
    public async Task History_AfterParam_WithLimit_AfterTakesPrecedence()
    {
        var posBuffer = new CircularBuffer<PositionSnapshot>(100);
        for (int i = 0; i < 5; i++)
        {
            posBuffer.Add(new PositionSnapshot(DateTime.UtcNow.AddSeconds(i), new GeographicCoordinate(47.0 + i * 0.1, 18.0), null));
        }

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory { PositionHistory = posBuffer }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        // after=2 should return entries 3,4,5 (3 entries), ignoring limit=1
        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position&after=2&limit=1")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Position").GetProperty("Entries").GetArrayLength().Should().Be(3);
    }

    // === Single type enforcement tests ===

    [Fact]
    public async Task History_CommaSeparatedType_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position,Altitude");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_TypeParam_State_ReturnsStateOnly()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=State")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("State", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Position", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("Altitude", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("Velocity", out _).Should().BeFalse();
    }

    [Fact]
    public async Task History_TypeParam_StateCaseInsensitive()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=state")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("State", out _).Should().BeTrue();
    }

    // === Sequence ID field tests ===

    [Fact]
    public async Task History_EmptyEnabledBuffer_MinMaxSequenceIdNull()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory
            {
                PositionHistory = new CircularBuffer<PositionSnapshot>(100)
            }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Enabled").GetBoolean().Should().BeTrue();
        pos.GetProperty("Count").GetInt32().Should().Be(0);
        pos.GetProperty("MinSequenceId").ValueKind.Should().Be(JsonValueKind.Null);
        pos.GetProperty("MaxSequenceId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // === State type tests ===

    [Fact]
    public async Task History_StateType_ReturnsStateEntries()
    {
        var stateBuffer = new CircularBuffer<StateSnapshot>(100);
        stateBuffer.Add(new StateSnapshot(
            DateTime.UtcNow,
            new GeographicCoordinate(47.0, 18.0),
            NavigationAccuracyCategoryPosition.LessThan3m,
            38000,
            AltitudeType.Barometric,
            Velocity.FromKnots(480, VelocityType.GroundSpeed),
            null,
            294.59,
            null,
            null,
            0));

        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19") with
        {
            History = new TrackedHistory { StateHistory = stateBuffer }
        };
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=State")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement state = doc.RootElement.GetProperty("State");
        state.GetProperty("Enabled").GetBoolean().Should().BeTrue();
        state.GetProperty("Count").GetInt32().Should().Be(1);
        state.GetProperty("MinSequenceId").GetInt64().Should().Be(1);
        state.GetProperty("MaxSequenceId").GetInt64().Should().Be(1);

        JsonElement entry = state.GetProperty("Entries")[0];
        entry.GetProperty("SequenceId").GetInt64().Should().Be(1);
        entry.GetProperty("Position").GetProperty("Latitude").GetDouble().Should().BeApproximately(47.0, 0.001);
        entry.GetProperty("Track").GetDouble().Should().BeApproximately(294.59, 0.01);
        entry.GetProperty("VerticalRate").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task History_StateType_Disabled_ReturnsEnabledFalse()
    {
        // Default CreateTestAircraft has no StateHistory (null)
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=State")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement state = doc.RootElement.GetProperty("State");
        state.GetProperty("Enabled").GetBoolean().Should().BeFalse();
    }
}
