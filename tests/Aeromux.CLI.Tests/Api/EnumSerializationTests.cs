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
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

public class EnumSerializationTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task Category_SerializesAsHeavy()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Identification")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Identification").GetProperty("Category").GetString().Should().Be("Heavy");
    }

    [Fact]
    public async Task EmergencyState_SerializesAsNoEmergency()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Identification")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Identification").GetProperty("EmergencyState").GetString().Should().Be("No Emergency");
    }

    [Fact]
    public async Task FlightStatus_SerializesAsAirborne()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Identification")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Identification").GetProperty("FlightStatus").GetString().Should().Be("Airborne");
    }

    [Fact]
    public async Task AdsbVersion_SerializesAsDO260B()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Identification")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Identification").GetProperty("AdsbVersion").GetString().Should().Be("DO-260B");
    }

    [Fact]
    public async Task FrameSource_SerializesAsSDR()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=Position")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Position").GetProperty("Source").GetString().Should().Be("SDR");
    }

    [Fact]
    public async Task AltitudeType_SerializesAsBarometric()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft() };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement alt = doc.RootElement.GetProperty("Aircraft")[0].GetProperty("BarometricAltitude");
        alt.GetProperty("Type").GetString().Should().Be("Barometric");
    }

    [Fact]
    public async Task VelocityType_SerializesAsGroundSpeed()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft() };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement speed = doc.RootElement.GetProperty("Aircraft")[0].GetProperty("Speed");
        speed.GetProperty("Type").GetString().Should().Be("GroundSpeed");
    }
}
