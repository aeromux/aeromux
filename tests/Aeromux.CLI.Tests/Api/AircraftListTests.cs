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
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

public class AircraftListTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task AircraftList_EmptyState_ReturnsZeroCountAndEmptyArray()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Count").GetInt32().Should().Be(0);
        doc.RootElement.GetProperty("Timestamp").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("Aircraft").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AircraftList_WithAircraft_ReturnsCorrectCount()
    {
        var aircraft = new List<Aircraft>
        {
            ApiTestFixture.CreateTestAircraft("407F19", "VIR359"),
            ApiTestFixture.CreateTestAircraft("3C6545", "DLH1234")
        };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Count").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("Aircraft").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task AircraftList_ItemContainsExpectedFields()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft() };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement item = doc.RootElement.GetProperty("Aircraft")[0];

        item.GetProperty("ICAO").GetString().Should().Be("407F19");
        item.GetProperty("Callsign").GetString().Should().Be("VIR359");
        item.GetProperty("Squawk").GetString().Should().Be("2646");
        item.GetProperty("Category").GetString().Should().Be("Heavy");
        item.GetProperty("IsOnGround").GetBoolean().Should().BeFalse();
        item.GetProperty("TotalMessages").GetInt32().Should().Be(437);
        item.GetProperty("DatabaseEnabled").GetBoolean().Should().BeTrue();
        item.GetProperty("Registration").GetString().Should().Be("G-VWHO");
        item.GetProperty("TypeCode").GetString().Should().Be("A346");
        item.GetProperty("OperatorName").GetString().Should().Be("Virgin Atlantic");
    }

    [Fact]
    public async Task AircraftList_RichAltitudeObject_HasAllUnits()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft() };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement alt = doc.RootElement.GetProperty("Aircraft")[0].GetProperty("BarometricAltitude");
        alt.GetProperty("Type").GetString().Should().Be("Barometric");
        alt.GetProperty("Feet").GetInt32().Should().Be(38000);
        alt.GetProperty("Meters").GetInt32().Should().BeGreaterThan(0);
        alt.GetProperty("FlightLevel").GetInt32().Should().Be(380);
    }

    [Fact]
    public async Task AircraftList_RichSpeedObject_HasAllUnits()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft() };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement speed = doc.RootElement.GetProperty("Aircraft")[0].GetProperty("Speed");
        speed.GetProperty("Type").GetString().Should().Be("GroundSpeed");
        speed.GetProperty("Knots").GetInt32().Should().Be(480);
        speed.GetProperty("KilometersPerHour").GetInt32().Should().BeGreaterThan(0);
        speed.GetProperty("MilesPerHour").GetInt32().Should().BeGreaterThan(0);
        speed.GetProperty("MetersPerSecond").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AircraftList_DatabaseDisabled_NullDatabaseFields()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft(databaseEnabled: false) };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement item = doc.RootElement.GetProperty("Aircraft")[0];
        item.GetProperty("DatabaseEnabled").GetBoolean().Should().BeFalse();
        item.GetProperty("Registration").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("TypeCode").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("OperatorName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AircraftList_ContentType_IsJson()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
