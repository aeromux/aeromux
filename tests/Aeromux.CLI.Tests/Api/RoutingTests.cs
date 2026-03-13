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
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

public class RoutingTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task GetAircraftList_Returns200()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAircraftDetail_ValidIcao_Returns200()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAircraftDetail_UnknownIcao_Returns404()
    {
        _fixture.TrackerMock.Setup(t => t.GetAircraft("AAAAAA")).Returns((Aircraft?)null);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/AAAAAA");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAircraftDetail_InvalidIcaoFormat_Returns400()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/ZZZZZZ");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAircraftDetail_ShortIcao_Returns400()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAircraftDetail_InvalidSection_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=InvalidName");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAircraftDetail_HistorySection_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19?sections=History");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_Returns200()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHistory_InvalidType_Returns400()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history?type=InvalidType");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStats_Returns200()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/stats");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/unknown");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostAircraftList_Returns405()
    {
        HttpResponseMessage response = await _fixture.Client.PostAsync("/api/v1/aircraft", null);
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetAircraftDetail_CaseInsensitiveIcao_ReturnsSameAircraft()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage lowerResponse = await _fixture.Client.GetAsync("/api/v1/aircraft/407f19");
        HttpResponseMessage upperResponse = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19");

        lowerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        upperResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
