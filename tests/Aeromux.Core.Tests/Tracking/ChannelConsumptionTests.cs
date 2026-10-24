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

using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;
using System.Threading.Channels;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for StartConsuming with ChannelReader.
/// </summary>
/// <remarks>
/// The consumer runs in the background, so these tests wait for what they need to have happened
/// (a frame processed, the consumer stopped) rather than for a length of time. The bound on each
/// wait only turns a hang into a failure; it is never what makes a test pass.
/// </remarks>
public class ChannelConsumptionTests : AircraftStateTrackerTestsBase
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task StartConsuming_ReadsFromChannel_TracksAllAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        var channel = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Write 3 frames to channel
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"), cts.Token);
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"), cts.Token);
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AllCall_80073B, "80073B"), cts.Token);
        channel.Writer.Complete();

        // Act - the writer is complete, so the consumer stops once it has drained the channel
        Tracker.StartConsuming(channel.Reader, cts.Token);
        await Tracker.ConsumerCompletion.WaitAsync(Bound);

        // Assert
        Tracker.Count.Should().Be(3);
        Tracker.GetAircraft("471DBC").Should().NotBeNull();
        Tracker.GetAircraft("4D2407").Should().NotBeNull();
        Tracker.GetAircraft("80073B").Should().NotBeNull();
    }

    [Fact]
    public void StartConsuming_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        Tracker = CreateTracker();
        var channel1 = Channel.CreateUnbounded<ProcessedFrame>();
        var channel2 = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Act
        Tracker.StartConsuming(channel1.Reader, cts.Token);

        // Assert
        Action secondCall = () => Tracker.StartConsuming(channel2.Reader, cts.Token);
        secondCall.Should().Throw<InvalidOperationException>()
            .WithMessage("StartConsuming can only be called once");

        channel1.Writer.Complete();
        channel2.Writer.Complete();
    }

    [Fact]
    public async Task StartConsuming_WithCancellation_StopsConsumption()
    {
        // Arrange
        Tracker = CreateTracker();
        using var processed = new SemaphoreSlim(0);
        Tracker.OnAircraftAdded += (_, _) => processed.Release();
        var channel = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"), cts.Token);
        Tracker.StartConsuming(channel.Reader, cts.Token);
        (await processed.WaitAsync(Bound)).Should().BeTrue("the first frame should be consumed");

        // Act - cancel and wait until the consumer has actually stopped
        await cts.CancelAsync();
        await Tracker.ConsumerCompletion.WaitAsync(Bound).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        Tracker.ConsumerCompletion.IsCanceled.Should().BeTrue("cancellation should end the consumer");

        // A frame written now has no consumer left to read it
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"), CancellationToken.None);
        channel.Writer.Complete();

        // Assert - Only first frame should be processed
        Tracker.Count.Should().Be(1);
        Tracker.GetAircraft("471DBC").Should().NotBeNull();
        Tracker.GetAircraft("4D2407").Should().BeNull("frame written after cancellation");
    }

    [Fact]
    public async Task StartConsuming_ManyFrames_ProcessesAllInOrder()
    {
        // Arrange
        Tracker = CreateTracker();
        using var processed = new SemaphoreSlim(0);
        Tracker.OnAircraftAdded += (_, _) => processed.Release();
        Tracker.OnAircraftUpdated += (_, _) => processed.Release();
        var channel = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Act - write frames one at a time, each only after the previous one was processed, so
        // the consumer goes back to waiting for data between every frame
        Tracker.StartConsuming(channel.Reader, cts.Token);
        for (int i = 0; i < 100; i++)
        {
            await channel.Writer.WriteAsync(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"), cts.Token);
            (await processed.WaitAsync(Bound)).Should().BeTrue($"frame {i + 1} should be consumed");
        }

        channel.Writer.Complete();
        await Tracker.ConsumerCompletion.WaitAsync(Bound);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();
        aircraft!.Status.TotalMessages.Should().Be(100);
    }
}
