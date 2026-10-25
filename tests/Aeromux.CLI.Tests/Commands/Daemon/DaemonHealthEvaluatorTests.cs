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
using Aeromux.Infrastructure.Streaming;
using FluentAssertions;
using static Aeromux.CLI.Tests.Commands.Daemon.DaemonStatisticsSamplerTests;

namespace Aeromux.CLI.Tests.Commands.Daemon;

/// <summary>
/// Tests for <see cref="DaemonHealthEvaluator"/>, fed by a sampler on a manual clock so
/// the durations come from the same history the REST API will use.
/// </summary>
public class DaemonHealthEvaluatorTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly ManualClock _clock = new();
    private ReceiverStatistics? _current;
    private readonly DaemonStatisticsSampler _sampler;

    public DaemonHealthEvaluatorTests()
    {
        _sampler = new DaemonStatisticsSampler(() => _current, _clock);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // xUnit v2 disposes through IAsyncLifetime; IAsyncDisposable states that the class owns
    // the sampler
    public Task DisposeAsync() => _sampler.DisposeAsync().AsTask();

    ValueTask IAsyncDisposable.DisposeAsync() => _sampler.DisposeAsync();

    [Fact]
    public void BeforeFirstSample_IsOk()
    {
        HealthVerdict verdict = Evaluate();

        verdict.Status.Should().Be("OK");
        verdict.Problems.Should().BeEmpty();
    }

    // === SDR devices ===

    [Fact]
    public void DeviceDeliveringSamples_IsNotFailing()
    {
        long samples = 0;
        for (int i = 0; i < 30; i++)
        {
            SampleAndAdvance(Receiver(sdr: Sdr(Device("default", samples += 2_400_000))), TimeSpan.FromSeconds(1));
        }

        Evaluate().Status.Should().Be("OK");
    }

    [Fact]
    public void DeviceExtractingNothing_ButDeliveringSamples_IsNotFailing()
    {
        // A quiet sky: samples arrive, no frames
        SampleAndAdvance(Receiver(sdr: Sdr(Device("default", 0))), TimeSpan.FromSeconds(30));
        SampleAndAdvance(Receiver(sdr: Sdr(Device("default", 72_000_000))), TimeSpan.Zero);

        Evaluate().Status.Should().Be("OK");
    }

    [Fact]
    public void DeviceStopsDeliveringSamples_FailsAfterTenSeconds()
    {
        SampleAndAdvance(Receiver(sdr: Sdr(Device("default", 2_400_000))), TimeSpan.FromSeconds(9));
        SampleAndAdvance(Receiver(sdr: Sdr(Device("default", 2_400_000))), TimeSpan.Zero);
        Evaluate().Status.Should().Be("OK", "9 seconds is within the threshold");

        _clock.Advance(TimeSpan.FromSeconds(1));
        HealthVerdict verdict = Evaluate();

        verdict.Status.Should().Be("Unhealthy");
        verdict.Problems.Should().Equal("SDR device 'default' (index 0) has delivered no samples for 10 s");
    }

    [Fact]
    public void DeviceNeverDeliveringSamples_FailsTenSecondsAfterItFirstAppeared()
    {
        SampleAndAdvance(Receiver(sdr: Sdr(Device("roof", 0, index: 1))), TimeSpan.FromSeconds(12.7));

        Evaluate().Problems.Should().Equal("SDR device 'roof' (index 1) has delivered no samples for 12 s");
    }

    // === Beast sources ===

    [Fact]
    public void BeastConnectedButSilent_IsNotFailing()
    {
        SampleAndAdvance(Receiver(beast: [Beast("feeder", 30005, connected: true, since: _clock.UtcNow)]),
            TimeSpan.FromMinutes(5));

        Evaluate().Status.Should().Be("OK");
    }

    [Fact]
    public void BeastDisconnected_FailsTenSecondsAfterItWasLost()
    {
        DateTime lost = _clock.UtcNow;
        SampleAndAdvance(Receiver(beast: [Beast("192.168.1.20", 30005, connected: false, since: lost)]),
            TimeSpan.FromSeconds(9));
        Evaluate().Status.Should().Be("OK");

        _clock.Advance(TimeSpan.FromSeconds(36));
        HealthVerdict verdict = Evaluate();

        verdict.Status.Should().Be("Unhealthy");
        verdict.Problems.Should().Equal("Beast source 192.168.1.20:30005 has been disconnected for 45 s");
    }

    [Fact]
    public void BeastStillOnItsFirstAttempt_CountsFromWhenItFirstAppeared()
    {
        // Since is null until the first attempt completes
        _current = Receiver(beast: [new BeastSourceStatistics("feeder", 30005, false, null, 0, 0)]);
        _sampler.Sample();

        _clock.Advance(TimeSpan.FromSeconds(9));
        Evaluate().Status.Should().Be("OK");

        _clock.Advance(TimeSpan.FromSeconds(1));
        Evaluate().Problems.Should().Equal("Beast source feeder:30005 has been disconnected for 10 s");
    }

    // === Overall status ===

    [Fact]
    public void OneOfTwoSourcesFailing_IsDegraded()
    {
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 2_400_000)),
                beast: [Beast("feeder", 30005, connected: false, since: _clock.UtcNow)]),
            TimeSpan.FromSeconds(1));
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 4_800_000)),
                beast: [Beast("feeder", 30005, connected: false, since: _clock.UtcNow.AddSeconds(-1))]),
            TimeSpan.FromSeconds(14));
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 38_400_000)),
                beast: [Beast("feeder", 30005, connected: false, since: _clock.UtcNow.AddSeconds(-15))]),
            TimeSpan.Zero);

        HealthVerdict verdict = Evaluate();

        verdict.Status.Should().Be("Degraded");
        verdict.Problems.Should().Equal("Beast source feeder:30005 has been disconnected for 15 s");
    }

    [Fact]
    public void EverySourceFailing_IsUnhealthy()
    {
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 0)),
                beast: [Beast("feeder", 30005, connected: false, since: _clock.UtcNow)]),
            TimeSpan.FromSeconds(20));

        HealthVerdict verdict = Evaluate();

        verdict.Status.Should().Be("Unhealthy");
        verdict.Problems.Should().HaveCount(2);
    }

    [Fact]
    public void Mlat_IsNeverJudged()
    {
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 0)),
                mlat: new MlatStatistics(Clients: 0, Messages: 0)),
            TimeSpan.Zero);
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 2_400_000)),
                mlat: new MlatStatistics(Clients: 0, Messages: 0)),
            TimeSpan.FromMinutes(1));
        SampleAndAdvance(Receiver(
                sdr: Sdr(Device("default", 146_400_000)),
                mlat: new MlatStatistics(Clients: 0, Messages: 0)),
            TimeSpan.Zero);

        Evaluate().Status.Should().Be("OK", "no mlat-client connected and no MLAT frames is not a problem");
    }

    private void SampleAndAdvance(ReceiverStatistics statistics, TimeSpan advance)
    {
        _current = statistics;
        _sampler.Sample();
        _clock.Advance(advance);
    }

    private HealthVerdict Evaluate() => DaemonHealthEvaluator.Evaluate(_sampler.GetSnapshot(), _sampler.UtcNow);
}
