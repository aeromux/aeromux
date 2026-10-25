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
            "Version", "Timestamp", "Uptime", "AircraftCount", "Messages", "Sdr", "BeastSources",
            "Mlat", "Outputs", "Database");
        Names(doc.RootElement.GetProperty("Messages")).Should().Equal("Total", "PerSecond", "AveragePerSecond");
    }

    [Theory]
    [InlineData("Receiver")]
    [InlineData("HeatmapCollectionEnabled")]
    [InlineData("Devices")]
    [InlineData("Stream")]
    [InlineData("Frames")]
    public async Task Stats_NoLongerCarriesConfigurationOrTheOldStream(string field)
    {
        using JsonDocument doc = await GetStatsAsync();

        doc.RootElement.TryGetProperty(field, out _).Should().BeFalse();
    }

    [Fact]
    public async Task EverySection_HasExactlyItsFields()
    {
        _fixture.Sample(Receiver(
            messages: 1,
            sdr: new SdrStatistics([Device("roof", 0)]),
            beast: [Beast(messages: 0)],
            mlat: new MlatStatistics(0, 0)));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;

        Names(root.GetProperty("Sdr")).Should().Equal(
            "Extracted", "Corrected", "Duplicates", "Messages", "MessagesPerSecond", "Devices");
        Names(root.GetProperty("Sdr").GetProperty("Devices")[0]).Should().Equal(
            "Name", "DeviceIndex", "SamplesPerSecond", "Extracted", "Corrected", "Duplicates",
            "Messages", "MessagesPerSecond");
        Names(root.GetProperty("BeastSources")[0]).Should().Equal(
            "Host", "Port", "Connected", "Since", "Reconnects", "Messages", "MessagesPerSecond");
        Names(root.GetProperty("Mlat")).Should().Equal("Clients", "Messages", "MessagesPerSecond");
    }

    [Fact]
    public async Task BeforeAnyReading_CountsAreZeroAndSourcesEmpty()
    {
        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;

        root.GetProperty("Messages").GetProperty("Total").GetInt64().Should().Be(0);
        root.GetProperty("Messages").GetProperty("PerSecond").GetDouble().Should().Be(0);
        root.GetProperty("Sdr").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("BeastSources").GetArrayLength().Should().Be(0);
        root.GetProperty("Mlat").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // === SDR ===

    [Fact]
    public async Task Sdr_TotalsAreTheSumOfTheDevices()
    {
        _fixture.Sample(Receiver(
            messages: 15_372,
            sdr: new SdrStatistics(
            [
                Device("sdr0", 0, extracted: 10_508, corrected: 112, duplicates: 1_909, messages: 8_495),
                Device("sdr1", 1, extracted: 8_489, corrected: 97, duplicates: 1_555, messages: 6_877)
            ])));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement sdr = doc.RootElement.GetProperty("Sdr");

        sdr.GetProperty("Extracted").GetInt64().Should().Be(18_997);
        sdr.GetProperty("Corrected").GetInt64().Should().Be(209);
        sdr.GetProperty("Duplicates").GetInt64().Should().Be(3_464);
        sdr.GetProperty("Messages").GetInt64().Should().Be(15_372);
        sdr.GetProperty("Devices").EnumerateArray().Select(d => d.GetProperty("Name").GetString())
            .Should().Equal("sdr0", "sdr1");
    }

    [Fact]
    public async Task Sdr_DeviceCarriesItsCounters()
    {
        _fixture.Sample(Receiver(sdr: new SdrStatistics(
            [Device("roof", 2, extracted: 1000, corrected: 10, duplicates: 7, messages: 880)])));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement device = doc.RootElement.GetProperty("Sdr").GetProperty("Devices")[0];

        device.GetProperty("DeviceIndex").GetInt32().Should().Be(2);
        device.GetProperty("Extracted").GetInt64().Should().Be(1000);
        device.GetProperty("Corrected").GetInt64().Should().Be(10);
        device.GetProperty("Duplicates").GetInt64().Should().Be(7);
        device.GetProperty("Messages").GetInt64().Should().Be(880);
    }

    // === Rates ===

    [Fact]
    public async Task Rates_CoverTheTimeBetweenReadings_RoundedToOneDecimal()
    {
        _fixture.Sample(Receiver(
            messages: 0,
            sdr: new SdrStatistics([Device("roof", 0)]),
            beast: [Beast(messages: 0)],
            mlat: new MlatStatistics(1, 0)));
        _fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        _fixture.Sample(Receiver(
            messages: 1000,
            sdr: new SdrStatistics([Device("roof", 0, samples: 7_200_000, messages: 700)]),
            beast: [Beast(messages: 200)],
            mlat: new MlatStatistics(1, 100)));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;

        root.GetProperty("Messages").GetProperty("PerSecond").GetDouble().Should().Be(333.3);
        root.GetProperty("Sdr").GetProperty("MessagesPerSecond").GetDouble().Should().Be(233.3);
        JsonElement device = root.GetProperty("Sdr").GetProperty("Devices")[0];
        device.GetProperty("SamplesPerSecond").GetDouble().Should().Be(2_400_000);
        device.GetProperty("MessagesPerSecond").GetDouble().Should().Be(233.3);
        root.GetProperty("BeastSources")[0].GetProperty("MessagesPerSecond").GetDouble().Should().Be(66.7);
        root.GetProperty("Mlat").GetProperty("MessagesPerSecond").GetDouble().Should().Be(33.3);
    }

    [Fact]
    public async Task AveragePerSecond_IsTheTotalOverTheUptime()
    {
        var tracker = new Mock<IAircraftStateTracker>();
        await using var sampler = new DaemonStatisticsSampler(() => Receiver(messages: 1000), new ManualClock());
        sampler.Sample();

        StatsResponse stats = DaemonApiMapper.ToStats(sampler.GetSnapshot(), tracker.Object,
            DateTime.UtcNow.AddSeconds(-400), new OutputClientCounts(null, null, null), null);

        stats.Messages.AveragePerSecond.Should().BeApproximately(2.5, 0.05);
    }

    // === Beast and MLAT ===

    [Fact]
    public async Task BeastOnly_HasNoSdrSectionAndReportsTheSource()
    {
        DateTime since = new(2026, 10, 25, 13, 30, 4, DateTimeKind.Utc);
        _fixture.Sample(Receiver(
            messages: 11_100,
            beast: [new BeastSourceStatistics("192.168.1.20", 30005, true, since, 2, 11_100)]));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement root = doc.RootElement;
        JsonElement source = root.GetProperty("BeastSources")[0];

        root.GetProperty("Sdr").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("Messages").GetProperty("Total").GetInt64().Should().Be(11_100);
        source.GetProperty("Host").GetString().Should().Be("192.168.1.20");
        source.GetProperty("Connected").GetBoolean().Should().BeTrue();
        source.GetProperty("Since").GetDateTime().Should().Be(since);
        source.GetProperty("Reconnects").GetInt32().Should().Be(2);
        source.GetProperty("Messages").GetInt64().Should().Be(11_100);
    }

    [Fact]
    public async Task BeastSource_StillOnItsFirstAttempt_HasNullSince()
    {
        _fixture.Sample(Receiver(beast: [new BeastSourceStatistics("feeder", 30005, false, null, 0, 0)]));

        using JsonDocument doc = await GetStatsAsync();

        doc.RootElement.GetProperty("BeastSources")[0].GetProperty("Since").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Mlat_ReportsClientsAndMessages()
    {
        _fixture.Sample(Receiver(mlat: new MlatStatistics(Clients: 2, Messages: 2600)));

        using JsonDocument doc = await GetStatsAsync();
        JsonElement mlat = doc.RootElement.GetProperty("Mlat");

        mlat.GetProperty("Clients").GetInt32().Should().Be(2);
        mlat.GetProperty("Messages").GetInt64().Should().Be(2600);
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

    private static IEnumerable<string> Names(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name);

    private static ReceiverStatistics Receiver(
        long messages = 0,
        SdrStatistics? sdr = null,
        BeastSourceStatistics[]? beast = null,
        MlatStatistics? mlat = null) =>
        new(messages, sdr, beast ?? [], mlat);

    private static SdrDeviceStatistics Device(
        string name, int index, long samples = 0, long extracted = 0, long corrected = 0,
        long duplicates = 0, long messages = 0) =>
        new(name, index, samples, extracted, corrected, duplicates, messages);

    private static BeastSourceStatistics Beast(long messages) =>
        new("feeder", 30005, true, DateTime.UnixEpoch, 0, messages);
}
