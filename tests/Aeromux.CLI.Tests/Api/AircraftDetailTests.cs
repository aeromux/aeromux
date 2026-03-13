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
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

public class AircraftDetailTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task DetailFull_ContainsAllSections()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(
            capabilities: new TrackedCapabilities { TransponderLevel = TransponderCapability.Level2PlusAirborne });
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Timestamp", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Identification", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("DatabaseRecord", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("VelocityAndDynamics", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Autopilot", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Meteorology", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Acas", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Capabilities", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("DataQuality", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DetailFiltered_ReturnsOnlyRequestedSections()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft();
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Timestamp", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Identification", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("Status", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DetailFiltered_MultipleSections()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(
            autopilot: new TrackedAutopilot { AutopilotEngaged = true });
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Position,Autopilot")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Autopilot", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Identification", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DetailFiltered_CaseInsensitiveSections()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft();
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DetailFiltered_EmptySections_ReturnsAll()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft();
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        // Empty sections parameter treated as omitted — returns all sections
        doc.RootElement.TryGetProperty("Identification", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Position", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DetailIdentification_HasCorrectFields()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft();
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Identification")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement id = doc.RootElement.GetProperty("Identification");
        id.GetProperty("ICAO").GetString().Should().Be("407F19");
        id.GetProperty("Callsign").GetString().Should().Be("VIR359");
        id.GetProperty("EmergencyState").GetString().Should().Be("No Emergency");
        id.GetProperty("FlightStatus").GetString().Should().Be("Airborne");
        id.GetProperty("AdsbVersion").GetString().Should().Be("DO-260B");
        id.GetProperty("Category").GetString().Should().Be("Heavy");
    }

    [Fact]
    public async Task DetailDatabaseRecord_DisabledReturnsNull()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(databaseEnabled: false);
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=DatabaseRecord")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("DatabaseRecord").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DetailDatabaseRecord_EnabledWithMatch_HasFields()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(databaseEnabled: true);
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=DatabaseRecord")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement db = doc.RootElement.GetProperty("DatabaseRecord");
        db.ValueKind.Should().Be(JsonValueKind.Object);
        db.GetProperty("Registration").GetString().Should().Be("G-VWHO");
        db.GetProperty("OperatorName").GetString().Should().Be("Virgin Atlantic");
    }

    [Fact]
    public async Task DetailNullableSection_NullWhenNoData()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(meteo: null, autopilot: null);
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Meteorology,Autopilot")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Meteorology").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("Autopilot").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DetailPosition_HasExpectedFields()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft();
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement pos = doc.RootElement.GetProperty("Position");
        pos.GetProperty("Coordinate").GetProperty("Latitude").GetDouble().Should().BeApproximately(47.3975, 0.01);
        pos.GetProperty("IsOnGround").GetBoolean().Should().BeFalse();
        pos.GetProperty("Source").GetString().Should().Be("SDR");
        pos.GetProperty("HadMlatPosition").GetBoolean().Should().BeFalse();
    }
}
