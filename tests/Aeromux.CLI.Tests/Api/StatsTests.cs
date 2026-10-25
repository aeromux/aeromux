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
using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for <c>GET /api/v1/stats</c>: measurements per input and output, with rates from
/// the statistics sampler. Readings are constructed and sampled on a manual clock.
/// </summary>
public class StatsTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    // === Shape ===

    [Fact]
    public async Task Stats_HasTheTopLevelFieldsInOrder()
    {
        using JsonDocument doc = await GetStatsAsync();

        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal(
            "Version", "Timestamp", "Uptime", "AircraftCount", "Frames", "Sdr", "BeastSources",
            "Mlat", "Outputs", "Database");
    }

    [Theory]
    [InlineData("Receiver")]
    [InlineData("HeatmapCollectionEnabled")]
    [InlineData("Devices")]
    [InlineData("Stream")]
    public async Task Stats_NoLongerCarriesConfigurationOrTheOldStream(string field)
    {
        using JsonDocument doc = await GetStatsAsync();

        doc.RootElement.TryGetProperty(field, out _).Should().BeFalse();
    }

    [Fact]
    public async Task BeforeAnyReading_CountsAreZeroAndSourcesEmpty()
    {
        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;

        root.GetProperty("Frames").GetProperty("Total").GetInt64().Should().Be(0);
        root.GetProperty("Frames").GetProperty("PerSecond").GetDouble().Should().Be(0);
        root.GetProperty("Sdr").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("BeastSources").GetArrayLength().Should().Be(0);
        root.GetProperty("Mlat").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // === SDR ===

    [Fact]
    public async Task Sdr_TotalsAreTheSumOfTheDevices()
    {
        _fixture.Sample(Receiver(
            frames: 900,
            sdr: new SdrStatistics(Unconfirmed: 70, Accepted: 800,
            [
                Device("roof", 0, extracted: 1000, crcErrors: 100, corrected: 10),
                Device("attic", 1, extracted: 500, crcErrors: 30, corrected: 5)
            ])));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement sdr = doc.RootElement.GetProperty("Sdr");

        sdr.GetProperty("Extracted").GetInt64().Should().Be(1500);
        sdr.GetProperty("CrcErrors").GetInt64().Should().Be(130);
        sdr.GetProperty("Corrected").GetInt64().Should().Be(15);
        sdr.GetProperty("Unconfirmed").GetInt64().Should().Be(70, "confidence filtering is counted receiver-wide");
        sdr.GetProperty("Accepted").GetInt64().Should().Be(800);
        sdr.GetProperty("Devices").EnumerateArray().Select(d => d.GetProperty("Name").GetString())
            .Should().Equal("roof", "attic");
    }

    [Fact]
    public async Task Sdr_DeviceHasEveryField()
    {
        _fixture.Sample(Receiver(sdr: new SdrStatistics(0, 0,
            [new SdrDeviceStatistics("roof", 2, DateTime.UnixEpoch, 24_000_000, 1000, 100, 10, 7, 880)])));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement device = doc.RootElement.GetProperty("Sdr").GetProperty("Devices")[0];

        device.EnumerateObject().Select(p => p.Name).Should().Equal(
            "Name", "DeviceIndex", "SamplesPerSecond", "Extracted", "CrcErrors", "Corrected",
            "Duplicates", "Messages", "ExtractedPerSecond");
        device.GetProperty("DeviceIndex").GetInt32().Should().Be(2);
        device.GetProperty("Duplicates").GetInt64().Should().Be(7);
        device.GetProperty("Messages").GetInt64().Should().Be(880);
    }

    // === Rates ===

    [Fact]
    public async Task Rates_CoverTheTimeBetweenReadings_RoundedToOneDecimal()
    {
        _fixture.Sample(Receiver(
            frames: 0,
            sdr: new SdrStatistics(0, 0, [Device("roof", 0, samples: 0, extracted: 0)]),
            beast: [Beast(accepted: 0)],
            mlat: new MlatStatistics(1, 0)));
        _fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        _fixture.Sample(Receiver(
            frames: 1000,
            sdr: new SdrStatistics(0, 700, [Device("roof", 0, samples: 7_200_000, extracted: 1000)]),
            beast: [Beast(accepted: 200)],
            mlat: new MlatStatistics(1, 100)));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;

        root.GetProperty("Frames").GetProperty("PerSecond").GetDouble().Should().Be(333.3);
        root.GetProperty("Sdr").GetProperty("AcceptedPerSecond").GetDouble().Should().Be(233.3);
        JsonElement device = root.GetProperty("Sdr").GetProperty("Devices")[0];
        device.GetProperty("SamplesPerSecond").GetDouble().Should().Be(2_400_000);
        device.GetProperty("ExtractedPerSecond").GetDouble().Should().Be(333.3);
        root.GetProperty("BeastSources")[0].GetProperty("AcceptedPerSecond").GetDouble().Should().Be(66.7);
        root.GetProperty("Mlat").GetProperty("ReceivedPerSecond").GetDouble().Should().Be(33.3);
    }

    [Fact]
    public async Task AveragePerSecond_IsTheTotalOverTheUptime()
    {
        var tracker = new Mock<IAircraftStateTracker>();
        await using var sampler = new DaemonStatisticsSampler(() => Receiver(frames: 1000), new ManualClock());
        sampler.Sample();

        StatsResponse stats = DaemonApiMapper.ToStats(sampler.GetSnapshot(), tracker.Object,
            DateTime.UtcNow.AddSeconds(-400), new OutputClientCounts(null, null, null), null);

        stats.Frames.AveragePerSecond.Should().BeApproximately(2.5, 0.05);
    }

    // === Beast and MLAT ===

    [Fact]
    public async Task BeastOnly_HasNoSdrSectionAndReportsTheSource()
    {
        DateTime since = new(2026, 10, 25, 13, 30, 4, DateTimeKind.Utc);
        _fixture.Sample(Receiver(
            frames: 11_100,
            beast: [new BeastSourceStatistics("192.168.1.20", 30005, true, since, 2, 12_000, 900, 11_100)]));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;
        JsonElement source = root.GetProperty("BeastSources")[0];

        root.GetProperty("Sdr").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("Frames").GetProperty("Total").GetInt64().Should().Be(11_100);
        source.EnumerateObject().Select(p => p.Name).Should().Equal(
            "Host", "Port", "Connected", "Since", "Reconnects", "Received", "Unconfirmed", "Accepted",
            "AcceptedPerSecond");
        source.GetProperty("Host").GetString().Should().Be("192.168.1.20");
        source.GetProperty("Connected").GetBoolean().Should().BeTrue();
        source.GetProperty("Since").GetDateTime().Should().Be(since);
        source.GetProperty("Reconnects").GetInt32().Should().Be(2);
        source.GetProperty("Received").GetInt64().Should().Be(12_000);
        source.GetProperty("Unconfirmed").GetInt64().Should().Be(900);
    }

    [Fact]
    public async Task BeastSource_StillOnItsFirstAttempt_HasNullSince()
    {
        _fixture.Sample(Receiver(beast: [new BeastSourceStatistics("feeder", 30005, false, null, 0, 0, 0, 0)]));

        using JsonDocument doc = await GetStatsAsync();

        doc.RootElement.GetProperty("BeastSources")[0].GetProperty("Since").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mlat_ReportsClientsAndFrames()
    {
        _fixture.Sample(Receiver(mlat: new MlatStatistics(Clients: 2, Received: 2600)));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement mlat = doc.RootElement.GetProperty("Mlat");

        mlat.GetProperty("Clients").GetInt32().Should().Be(2);
        mlat.GetProperty("Received").GetInt64().Should().Be(2600);
    }

    // === Outputs and database ===

    [Fact]
    public async Task Outputs_DefaultFixture_BeastEnabledWithNoClients()
    {
        using JsonDocument doc = await GetStatsAsync();
        JsonElement outputs = doc.RootElement.GetProperty("Outputs");

        outputs.GetProperty("Beast").GetProperty("Clients").GetInt32().Should().Be(0);
        outputs.GetProperty("Json").ValueKind.Should().Be(JsonValueKind.Null);
        outputs.GetProperty("Sbs").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Outputs_ReportConnectedClients()
    {
        _fixture.OutputClients = new OutputClientCounts(Beast: 3, Json: 1, Sbs: null);

        using JsonDocument doc = await GetStatsAsync();
        JsonElement outputs = doc.RootElement.GetProperty("Outputs");

        outputs.GetProperty("Beast").GetProperty("Clients").GetInt32().Should().Be(3);
        outputs.GetProperty("Json").GetProperty("Clients").GetInt32().Should().Be(1);
        outputs.GetProperty("Sbs").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Database_IsNullWhenEnrichmentIsOff()
    {
        using JsonDocument doc = await GetStatsAsync();

        doc.RootElement.GetProperty("Database").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("2026.10.20")]
    [InlineData(null)]
    public async Task Database_ReportsTheLoadedVersion(string? version)
    {
        using var lookup = new SwappableAircraftDatabaseLookup(null, version);
        await using ApiTestFixture fixture = await ApiTestFixture.CreateAsync(databaseLookup: lookup);

        string json = await (await fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Database").GetProperty("Version").GetString().Should().Be(version);
    }

    // === Helpers ===

    private async Task<JsonDocument> GetStatsAsync()
    {
        string json = await (await _fixture.Client.GetAsync("/api/v1/stats")).Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private static ReceiverStatistics Receiver(
        long frames = 0,
        SdrStatistics? sdr = null,
        BeastSourceStatistics[]? beast = null,
        MlatStatistics? mlat = null) =>
        new(frames, sdr, beast ?? [], mlat);

    private static SdrDeviceStatistics Device(
        string name, int index, long samples = 0, long extracted = 0, long crcErrors = 0, long corrected = 0) =>
        new(name, index, DateTime.UnixEpoch, samples, extracted, crcErrors, corrected, 0, 0);

    private static BeastSourceStatistics Beast(long accepted) =>
        new("feeder", 30005, true, DateTime.UnixEpoch, 0, accepted, 0, accepted);
}
