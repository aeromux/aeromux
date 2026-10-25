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
using System.Net.Sockets;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Streaming;

namespace Aeromux.Infrastructure.Tests.Streaming;

/// <summary>
/// Tests for <see cref="ReceiverStream.GetReceiverStatistics"/> on a Beast-only receiver,
/// which needs no SDR hardware. The SDR section is covered by the daemon's statistics
/// tests with constructed readings.
/// </summary>
public class ReceiverStreamStatisticsTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task BeforeStart_IsNull()
    {
        using TcpListener listener = CreateListener(out int port);
        await using ReceiverStream stream = CreateStream(port, mlatEnabled: false);

        stream.GetReceiverStatistics().Should().BeNull();
    }

    [Fact]
    public async Task BeastOnly_ReportsBeastSourceAndMlatWithoutSdr()
    {
        using TcpListener listener = CreateListener(out int port);
        await using ReceiverStream stream = CreateStream(port, mlatEnabled: true);

        await stream.StartAsync(CancellationToken.None);
        ReceiverStatistics statistics = stream.GetReceiverStatistics()!;

        statistics.Sdr.Should().BeNull();
        statistics.BeastSources.Should().ContainSingle()
            .Which.Should().Match<BeastSourceStatistics>(s => s.Host == "127.0.0.1" && s.Port == port);
        statistics.Mlat.Should().Be(new MlatStatistics(0, 0));
        statistics.FramesDelivered.Should().Be(0);
    }

    [Fact]
    public async Task MlatDisabled_HasNoMlatSection()
    {
        using TcpListener listener = CreateListener(out int port);
        await using ReceiverStream stream = CreateStream(port, mlatEnabled: false);

        await stream.StartAsync(CancellationToken.None);

        stream.GetReceiverStatistics()!.Mlat.Should().BeNull();
    }

    [Fact]
    public async Task BeastFrames_AcceptedOnesAreDelivered()
    {
        // Low confidence: frames 1 to 4 of the address are unconfirmed, 5 to 8 accepted
        using TcpListener listener = CreateListener(out int port);
        await using ReceiverStream stream = CreateStream(port, mlatEnabled: false);
        await stream.StartAsync(CancellationToken.None);

        using var acceptCts = new CancellationTokenSource(WaitLimit);
        using TcpClient client = await listener.AcceptTcpClientAsync(acceptCts.Token);
        byte[] frame = BeastTestData.CreateCompleteFrameStream(
            true, DateTime.UtcNow, 100, BeastTestData.HexToBytes(RealFrames.AirbornePos_80073B_Even)).ToArray();
        for (int i = 0; i < 8; i++)
        {
            await client.GetStream().WriteAsync(frame);
        }

        using var waitCts = new CancellationTokenSource(WaitLimit);
        ReceiverStatistics statistics;
        while ((statistics = stream.GetReceiverStatistics()!).FramesDelivered < 4)
        {
            await Task.Delay(10, waitCts.Token);
        }

        BeastSourceStatistics source = statistics.BeastSources[0];
        source.Connected.Should().BeTrue();
        source.Received.Should().Be(8);
        source.Accepted.Should().Be(4);
        statistics.FramesDelivered.Should().Be(source.Accepted);
    }

    private static ReceiverStream CreateStream(int beastPort, bool mlatEnabled) =>
        new(
            null,
            new TrackingConfig { ConfidenceLevel = ConfidenceLevel.Low },
            null,
            new MlatConfig { Enabled = mlatEnabled, InputPort = 0 },
            [new BeastSourceConfig { Host = "127.0.0.1", Port = beastPort }],
            IPAddress.Loopback);

    private static TcpListener CreateListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }
}
