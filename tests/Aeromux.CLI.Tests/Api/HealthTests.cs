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
using Aeromux.Core.ModeS;
using Aeromux.Infrastructure.Streaming;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for <c>GET /api/v1/health</c>: the status, its HTTP code and the problems, from
/// readings sampled on the fixture's manual clock. The rules themselves are covered by
/// the health evaluator's tests.
/// </summary>
public class HealthTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task Health_ReturnsExpectedFields()
    {
        using JsonDocument doc = await GetHealthAsync(HttpStatusCode.OK);

        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal(
            "Status", "Uptime", "AircraftCount", "Timestamp", "Problems");
    }

    [Fact]
    public async Task Health_ContentType_IsJson()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/health");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task BeforeAnyReading_IsOkWithNoProblems()
    {
        using JsonDocument doc = await GetHealthAsync(HttpStatusCode.OK);

        doc.RootElement.GetProperty("Status").GetString().Should().Be("OK");
        doc.RootElement.GetProperty("Problems").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task EverySourceDelivering_IsOk()
    {
        _fixture.Sample(Receiver(Device(samples: 0), Beast(connected: true)));
        _fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        _fixture.Sample(Receiver(Device(samples: 72_000_000), Beast(connected: true)));

        using JsonDocument doc = await GetHealthAsync(HttpStatusCode.OK);

        doc.RootElement.GetProperty("Status").GetString().Should().Be("OK");
        doc.RootElement.GetProperty("Problems").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SomeSourcesFailing_IsDegradedWith200()
    {
        // The device keeps delivering, the Beast source was lost 45 seconds ago
        DateTime lost = _fixture.Clock.UtcNow;
        _fixture.Sample(Receiver(Device(samples: 0), Beast(connected: false, since: lost)));
        _fixture.Clock.Advance(TimeSpan.FromSeconds(45));
        _fixture.Sample(Receiver(Device(samples: 108_000_000), Beast(connected: false, since: lost)));

        using JsonDocument doc = await GetHealthAsync(HttpStatusCode.OK);

        doc.RootElement.GetProperty("Status").GetString().Should().Be("Degraded");
        doc.RootElement.GetProperty("Problems").EnumerateArray().Select(p => p.GetString())
            .Should().Equal("Beast source 192.168.1.20:30005 has been disconnected for 45 s");
    }

    [Fact]
    public async Task EverySourceFailing_IsUnhealthyWith503()
    {
        _fixture.Sample(Receiver(Device(samples: 0), Beast(connected: false, since: _fixture.Clock.UtcNow)));
        _fixture.Clock.Advance(TimeSpan.FromSeconds(20));

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/health");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        doc.RootElement.GetProperty("Status").GetString().Should().Be("Unhealthy");
        doc.RootElement.GetProperty("Problems").EnumerateArray().Select(p => p.GetString()).Should().Equal(
            "SDR device 'default' (index 0) has delivered no samples for 20 s",
            "Beast source 192.168.1.20:30005 has been disconnected for 20 s");
    }

    private async Task<JsonDocument> GetHealthAsync(HttpStatusCode expected)
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(expected);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static ReceiverStatistics Receiver(SdrDeviceStatistics device, BeastSourceStatistics beast) =>
        new(0, new SdrStatistics([device]), [beast], null);

    private static SdrDeviceStatistics Device(long samples) =>
        new("default", 0, samples, 0, 0, 0, 0, MessageTypeCounts.Empty);

    private static BeastSourceStatistics Beast(bool connected, DateTime? since = null) =>
        new("192.168.1.20", 30005, connected, since ?? DateTime.UnixEpoch, 0, 0, MessageTypeCounts.Empty);
}
