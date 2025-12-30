// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
public class ChannelConsumptionTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public async Task StartConsuming_ReadsFromChannel_TracksAllAircraft()
    {
        // Arrange
        _tracker = CreateTracker();
        var channel = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        _disposables.Add(cts);

        // Write 3 frames to channel
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"), cts.Token);
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"), cts.Token);
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AllCall_80073B, "80073B"), cts.Token);
        channel.Writer.Complete();

        // Act
        _tracker.StartConsuming(channel.Reader, cts.Token);

        // Wait for consumption to complete
        await Task.Delay(500, cts.Token);

        // Assert
        _tracker.Count.Should().Be(3);
        _tracker.GetAircraft("471DBC").Should().NotBeNull();
        _tracker.GetAircraft("4D2407").Should().NotBeNull();
        _tracker.GetAircraft("80073B").Should().NotBeNull();
    }

    [Fact]
    public void StartConsuming_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        _tracker = CreateTracker();
        var channel1 = Channel.CreateUnbounded<ProcessedFrame>();
        var channel2 = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        _disposables.Add(cts);

        // Act
        _tracker.StartConsuming(channel1.Reader, cts.Token);

        // Assert
        Action secondCall = () => _tracker.StartConsuming(channel2.Reader, cts.Token);
        secondCall.Should().Throw<InvalidOperationException>()
            .WithMessage("StartConsuming can only be called once");

        channel1.Writer.Complete();
        channel2.Writer.Complete();
    }

    [Fact]
    public async Task StartConsuming_WithCancellation_StopsConsumption()
    {
        // Arrange
        _tracker = CreateTracker();
        var channel = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        _disposables.Add(cts);

        // Write initial frames
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"), cts.Token);

        // Act
        _tracker.StartConsuming(channel.Reader, cts.Token);
        await Task.Delay(100, cts.Token);

        // Cancel
        await cts.CancelAsync();
        await Task.Delay(100, CancellationToken.None);

        // Write more frames after cancellation (use CancellationToken.None since we expect this to work after cancellation)
        await channel.Writer.WriteAsync(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"), CancellationToken.None);
        await Task.Delay(100, CancellationToken.None);

        // Assert - Only first frame should be processed
        _tracker.Count.Should().Be(1);
        _tracker.GetAircraft("471DBC").Should().NotBeNull();
        _tracker.GetAircraft("4D2407").Should().BeNull("frame written after cancellation");

        channel.Writer.Complete();
    }

    [Fact]
    public async Task StartConsuming_ManyFrames_ProcessesAllInOrder()
    {
        // Arrange
        _tracker = CreateTracker();
        var channel = Channel.CreateUnbounded<ProcessedFrame>();
        var cts = new CancellationTokenSource();
        _disposables.Add(cts);

        // Act - Start consuming
        _tracker.StartConsuming(channel.Reader, cts.Token);

        // Write frames gradually
        for (int i = 0; i < 100; i++)
        {
            await channel.Writer.WriteAsync(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"), cts.Token);
            await Task.Delay(1, cts.Token);
        }

        channel.Writer.Complete();
        await Task.Delay(500, cts.Token); // Wait for all frames to be consumed

        // Assert
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();
        aircraft!.Status.TotalMessages.Should().Be(100);
    }
}
