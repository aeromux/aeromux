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

using Aeromux.CLI.Commands.Daemon;
using Aeromux.Core.ModeS;
using Aeromux.Infrastructure.Streaming;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands.Daemon;

/// <summary>
/// Tests for <see cref="DaemonStatisticsSampler"/>: rates over the sliding 60-second
/// window and the per-source history health evaluation relies on. Readings are
/// constructed and the clock is moved by hand, so no timer runs.
/// </summary>
public class DaemonStatisticsSamplerTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly ManualClock _clock = new();
    private ReceiverStatistics? _current;
    private readonly DaemonStatisticsSampler _sampler;

    public DaemonStatisticsSamplerTests()
    {
        _sampler = new DaemonStatisticsSampler(() => _current, _clock);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // xUnit v2 disposes through IAsyncLifetime; IAsyncDisposable states that the class owns
    // the sampler
    public Task DisposeAsync() => _sampler.DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _sampler.DisposeAsync();

    // === Before the first reading ===

    [Fact]
    public void BeforeFirstSample_SnapshotIsEmpty()
    {
        _sampler.GetSnapshot().Should().BeSameAs(StatisticsSnapshot.Empty);
    }

    [Fact]
    public void SourceNotRunning_TakesNoReading()
    {
        _current = null;

        _sampler.Sample();

        _sampler.GetSnapshot().Should().BeSameAs(StatisticsSnapshot.Empty);
    }

    // === Rates ===

    [Fact]
    public void OneReading_RatesAreZero()
    {
        SampleAt(Receiver(messages: 500));

        StatisticsSnapshot snapshot = _sampler.GetSnapshot();

        snapshot.Statistics!.Messages.Should().Be(500);
        snapshot.TakenAt.Should().Be(_clock.UtcNow);
        snapshot.MessagesPerSecond.Should().Be(0);
    }

    [Fact]
    public void PartialWindow_RateCoversTheTimeSinceTheFirstReading()
    {
        SampleAt(Receiver(messages: 0));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(messages: 100));

        _sampler.GetSnapshot().MessagesPerSecond.Should().Be(10);
    }

    [Fact]
    public void FullWindow_RateCoversTheLastSixtySeconds()
    {
        // 100 messages per second for two minutes, then 40 per second for one
        long messages = 0;
        SampleEverySecond(120, () => Receiver(messages += 100));
        SampleEverySecond(60, () => Receiver(messages += 40));

        _sampler.GetSnapshot().MessagesPerSecond.Should().Be(40);
    }

    [Fact]
    public void WindowSlides_ABurstOlderThanSixtySecondsNoLongerCounts()
    {
        SampleAt(Receiver(messages: 0));
        _clock.Advance(TimeSpan.FromSeconds(1));
        SampleAt(Receiver(messages: 6000));

        SampleEverySecond(59, () => Receiver(messages: 6000));
        _sampler.GetSnapshot().MessagesPerSecond.Should().Be(100, "the burst is still 60 seconds back");

        SampleEverySecond(1, () => Receiver(messages: 6000));
        _sampler.GetSnapshot().MessagesPerSecond.Should().Be(0);
    }

    [Fact]
    public void EverySectionGetsItsRates()
    {
        SampleAt(Receiver(
            messages: 0,
            sdr: Sdr(Device("default", samples: 0, messages: 0)),
            beast: [Beast("192.168.1.20", 30005, messages: 0)],
            mlat: new MlatStatistics(1, 0, MessageTypeCounts.Empty)));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(
            messages: 1000,
            sdr: Sdr(Device("default", samples: 24_000_000, messages: 3000)),
            beast: [Beast("192.168.1.20", 30005, messages: 150)],
            mlat: new MlatStatistics(1, 50, MessageTypeCounts.Empty)));

        StatisticsSnapshot snapshot = _sampler.GetSnapshot();

        snapshot.MessagesPerSecond.Should().Be(100);
        snapshot.SdrMessagesPerSecond.Should().Be(300, "the devices' messages together");
        snapshot.Devices.Should().ContainSingle();
        snapshot.Devices[0].SamplesPerSecond.Should().Be(2_400_000);
        snapshot.Devices[0].MessagesPerSecond.Should().Be(300);
        snapshot.BeastSources.Should().ContainSingle();
        snapshot.BeastSources[0].MessagesPerSecond.Should().Be(15);
        snapshot.Mlat!.MessagesPerSecond.Should().Be(5);
    }

    [Fact]
    public void DevicesKeepTheirOrderAndAreMatchedByName()
    {
        SampleAt(Receiver(sdr: Sdr(Device("roof", samples: 0), Device("attic", samples: 0))));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(sdr: Sdr(Device("roof", samples: 100), Device("attic", samples: 50))));

        IReadOnlyList<DeviceSnapshot> devices = _sampler.GetSnapshot().Devices;

        devices.Select(d => d.Device.Name).Should().Equal("roof", "attic");
        devices[0].SamplesPerSecond.Should().Be(10);
        devices[1].SamplesPerSecond.Should().Be(5);
    }

    [Fact]
    public void SameBeastSourceListedTwice_KeepsSeparateRates()
    {
        SampleAt(Receiver(beast: [Beast("feeder", 30005, messages: 0), Beast("feeder", 30005, messages: 0)]));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(beast: [Beast("feeder", 30005, messages: 100), Beast("feeder", 30005, messages: 20)]));

        IReadOnlyList<BeastSourceSnapshot> sources = _sampler.GetSnapshot().BeastSources;

        sources[0].MessagesPerSecond.Should().Be(10);
        sources[1].MessagesPerSecond.Should().Be(2);
    }

    // === Message type rates ===

    [Fact]
    public void MessageTypes_RateCoversTheWindow()
    {
        SampleAt(Receiver(sdr: Sdr(Device("default", 0) with { MessageTypes = Types(df17: 0) })));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(sdr: Sdr(Device("default", 0) with { MessageTypes = Types(df17: 500) })));

        _sampler.GetSnapshot().Devices[0].MessageTypes.DownlinkFormats[17].Should().Be(50);
    }

    [Fact]
    public void MessageTypes_ACodeFirstSeenInsideTheWindow_CountsFromZero()
    {
        // TC 0 appears after 10 seconds; its 30 messages over the next 10 are 30 / 20 s
        // measured from the window's start, not 0 from its first reading
        SampleAt(Receiver(beast: [Beast("feeder", 30005) with { MessageTypes = Types(df17: 0) }]));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(beast: [Beast("feeder", 30005) with { MessageTypes = Types(df17: 100, tc0: 10) }]));
        _clock.Advance(TimeSpan.FromSeconds(10));
        SampleAt(Receiver(beast: [Beast("feeder", 30005) with { MessageTypes = Types(df17: 200, tc0: 30) }]));

        MessageTypeRates rates = _sampler.GetSnapshot().BeastSources[0].MessageTypes;

        rates.TypeCodes[0].Should().Be(1.5);
        rates.DownlinkFormats[17].Should().Be(10);
    }

    [Fact]
    public void MessageTypes_WindowSlides()
    {
        SampleAt(Receiver(mlat: new MlatStatistics(0, 0, Types(df17: 0))));
        _clock.Advance(TimeSpan.FromSeconds(1));
        SampleAt(Receiver(mlat: new MlatStatistics(0, 0, Types(df17: 600))));
        SampleEverySecond(60, () => Receiver(mlat: new MlatStatistics(0, 0, Types(df17: 600))));

        _sampler.GetSnapshot().Mlat!.MessageTypes.DownlinkFormats[17].Should().Be(0, "the burst is now older than 60 seconds");
    }

    private static MessageTypeCounts Types(long df17, long tc0 = 0) => new(
        new Dictionary<int, long> { [17] = df17 },
        tc0 > 0 ? new Dictionary<int, long> { [0] = tc0 } : new Dictionary<int, long>(),
        new Dictionary<Aeromux.Core.ModeS.Enums.BdsCode, long>());

    // === Source history ===

    [Fact]
    public void DeviceSamples_LastIncreasedIsWhenTheCountLastWentUp()
    {
        SampleAt(Receiver(sdr: Sdr(Device("default", samples: 0))));
        DateTime firstSeen = _clock.UtcNow;
        _sampler.GetSnapshot().Devices[0].SamplesLastIncreased.Should().Be(firstSeen);

        _clock.Advance(TimeSpan.FromSeconds(1));
        SampleAt(Receiver(sdr: Sdr(Device("default", samples: 2_400_000))));
        DateTime increased = _clock.UtcNow;

        // Stalls: the count stays where it was
        SampleEverySecond(90, () => Receiver(sdr: Sdr(Device("default", samples: 2_400_000))));

        DeviceSnapshot device = _sampler.GetSnapshot().Devices[0];
        device.SamplesLastIncreased.Should().Be(increased, "the stall began after this, and outlasts the window");
        device.SamplesPerSecond.Should().Be(0);
    }

    [Fact]
    public void BeastSource_FirstSeenIsTheFirstReadingThatContainedIt()
    {
        SampleAt(Receiver(beast: [Beast("feeder", 30005)]));
        DateTime firstSeen = _clock.UtcNow;
        SampleEverySecond(5, () => Receiver(beast: [Beast("feeder", 30005)]));

        _sampler.GetSnapshot().BeastSources[0].FirstSeen.Should().Be(firstSeen);
    }

    // === Timer ===

    [Fact]
    public async Task Start_TakesAFirstReadingAtOnce_AndDisposeStopsIt()
    {
        var statistics = Receiver(messages: 7);
        await using var sampler = new DaemonStatisticsSampler(() => statistics);

        sampler.Start(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (sampler.GetSnapshot().Statistics == null)
        {
            await Task.Delay(10, cts.Token);
        }

        sampler.GetSnapshot().Statistics.Should().BeSameAs(statistics);
        await sampler.DisposeAsync();
    }

    // === Helpers ===

    private void SampleAt(ReceiverStatistics statistics)
    {
        _current = statistics;
        _sampler.Sample();
    }

    private void SampleEverySecond(int count, Func<ReceiverStatistics> next)
    {
        for (int i = 0; i < count; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            SampleAt(next());
        }
    }

    internal static ReceiverStatistics Receiver(
        long messages = 0,
        SdrStatistics? sdr = null,
        BeastSourceStatistics[]? beast = null,
        MlatStatistics? mlat = null) =>
        new(messages, sdr, beast ?? [], mlat);

    internal static SdrStatistics Sdr(params SdrDeviceStatistics[] devices) => new(devices);

    internal static SdrDeviceStatistics Device(string name, long samples, long messages = 0, int index = 0) =>
        new(name, index, samples, 0, 0, 0, messages, MessageTypeCounts.Empty);

    internal static BeastSourceStatistics Beast(
        string host, int port, long messages = 0, bool connected = true, DateTime? since = null) =>
        new(host, port, connected, since ?? DateTime.UnixEpoch, 0, messages, MessageTypeCounts.Empty);
}
