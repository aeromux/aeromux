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
using Aeromux.Infrastructure.Streaming;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Api;

public class StatsTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task Stats_ReturnsAllExpectedFields()
    {
        _fixture.Statistics = new StreamStatistics(125000, 98000, 0, 0, 0, TimeSpan.FromSeconds(3600));

        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("Timestamp", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Uptime", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("AircraftCount", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Devices", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Stream", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Receiver", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Stats_StreamHasExpectedFields()
    {
        _fixture.Statistics = new StreamStatistics(125000, 98000, 0, 0, 0, TimeSpan.FromSeconds(3600));

        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement stream = doc.RootElement.GetProperty("Stream");
        stream.GetProperty("TotalFrames").GetInt64().Should().Be(125000);
        stream.GetProperty("ValidFrames").GetInt64().Should().Be(98000);
        stream.GetProperty("FramesPerSecond").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Stats_CrcErrors_EqualsTotalMinusValid()
    {
        _fixture.Statistics = new StreamStatistics(125000, 98000, 0, 0, 0, TimeSpan.FromSeconds(3600));

        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement stream = doc.RootElement.GetProperty("Stream");
        stream.GetProperty("CrcErrors").GetInt64().Should().Be(125000 - 98000);
    }

    [Fact]
    public async Task Stats_ReceiverHasExpectedFields()
    {
        _fixture.Statistics = new StreamStatistics(125000, 98000, 0, 0, 0, TimeSpan.FromSeconds(3600));

        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement receiver = doc.RootElement.GetProperty("Receiver");
        receiver.GetProperty("Latitude").GetDouble().Should().BeApproximately(46.907982, 0.001);
        receiver.GetProperty("Longitude").GetDouble().Should().BeApproximately(19.693172, 0.001);
        receiver.GetProperty("AltitudeMeters").GetInt32().Should().Be(120);
        receiver.GetProperty("Name").GetString().Should().Be("Test");
    }

    [Fact]
    public async Task Stats_NullStatistics_ReturnsZeroStreamValues()
    {
        _fixture.Statistics = null;

        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement stream = doc.RootElement.GetProperty("Stream");
        stream.GetProperty("TotalFrames").GetInt64().Should().Be(0);
        stream.GetProperty("ValidFrames").GetInt64().Should().Be(0);
        stream.GetProperty("CrcErrors").GetInt64().Should().Be(0);
        stream.GetProperty("FramesPerSecond").GetDouble().Should().Be(0);
    }
}
