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

public class InputValidationTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task InvalidIcao_NonHex_Returns400WithErrorMessage()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/ZZZZZZ");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Error").GetString().Should().Contain("Invalid ICAO");
    }

    [Fact]
    public async Task InvalidIcao_TooShort_Returns400()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidIcao_TooLong_Returns400()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19AA");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptySections_Returns200()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyType_Returns200()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Limit_Zero_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?limit=0");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Limit_Negative_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?limit=-1");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Limit_NonNumeric_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?limit=abc");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Limit_Valid_Returns200()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?limit=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
