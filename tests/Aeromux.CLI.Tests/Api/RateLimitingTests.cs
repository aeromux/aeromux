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

public class RateLimitingTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task AircraftList_RapidRequests_SecondReturns429()
    {
        HttpResponseMessage first = await _fixture.Client.GetAsync("/api/v1/aircraft");
        HttpResponseMessage second = await _fixture.Client.GetAsync("/api/v1/aircraft");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task AircraftDetail_RapidRequests_SecondReturns429()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage first = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19");
        HttpResponseMessage second = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task AircraftHistory_RapidRequests_SecondReturns429()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage first = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history");
        HttpResponseMessage second = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task Stats_NotRateLimited_BothReturn200()
    {
        HttpResponseMessage first = await _fixture.Client.GetAsync("/api/v1/stats");
        HttpResponseMessage second = await _fixture.Client.GetAsync("/api/v1/stats");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_NotRateLimited_BothReturn200()
    {
        HttpResponseMessage first = await _fixture.Client.GetAsync("/api/v1/health");
        HttpResponseMessage second = await _fixture.Client.GetAsync("/api/v1/health");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
