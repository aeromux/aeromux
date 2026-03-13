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

public class ResponseFormatTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task AllEndpoints_ContentType_IsJson()
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft("407F19");
        _fixture.TrackerMock.Setup(t => t.GetAircraft("407F19")).Returns(aircraft);

        HttpResponseMessage listResponse = await _fixture.Client.GetAsync("/api/v1/aircraft");
        HttpResponseMessage detailResponse = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19");
        HttpResponseMessage historyResponse = await _fixture.Client.GetAsync("/api/v1/aircraft/407F19/history");
        HttpResponseMessage statsResponse = await _fixture.Client.GetAsync("/api/v1/stats");
        HttpResponseMessage healthResponse = await _fixture.Client.GetAsync("/api/v1/health");

        listResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        detailResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        historyResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        statsResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        healthResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task AircraftList_UsesPascalCaseFieldNames()
    {
        var aircraft = new List<Aircraft> { ApiTestFixture.CreateTestAircraft() };
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(aircraft);

        string json = await (await _fixture.Client.GetAsync("/api/v1/aircraft")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement item = doc.RootElement.GetProperty("Aircraft")[0];

        // Verify PascalCase (not camelCase)
        item.TryGetProperty("ICAO", out _).Should().BeTrue();
        item.TryGetProperty("Callsign", out _).Should().BeTrue();
        item.TryGetProperty("icao", out _).Should().BeFalse();
        item.TryGetProperty("callsign", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ErrorResponse_HasErrorField()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/ZZZZZZ");

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Error", out _).Should().BeTrue();
        doc.RootElement.GetProperty("Error").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Timestamps_AreIso8601Format()
    {
        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        string? timestamp = doc.RootElement.GetProperty("Timestamp").GetString();
        timestamp.Should().NotBeNull();
        timestamp.Should().Contain("T");
        timestamp.Should().EndWith("Z");
    }
}
