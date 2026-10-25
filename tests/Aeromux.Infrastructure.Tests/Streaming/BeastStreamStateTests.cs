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
/// Tests for <see cref="BeastStream.GetSourceStatistics"/>: connection state, reconnect
/// count and frame counters, against a local TCP listener. The retry delay is shortened
/// so a reconnect takes milliseconds instead of the production 5 seconds.
/// </summary>
public class BeastStreamStateTests
{
    private static readonly TimeSpan FastRetry = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    // Made up from AirbornePos_80073B_Even: DF 18 with CF 1 (non-ICAO address), parity recomputed
    private const string Df18Cf1Position = "9180073B58CD7331C274978DD890";

    [Fact]
    public async Task BeforeStart_NotConnectedAndNoSince()
    {
        await using var stream = new BeastStream("127.0.0.1", 30005, new TrackingConfig());

        BeastSourceStatistics statistics = stream.GetSourceStatistics();

        statistics.Host.Should().Be("127.0.0.1");
        statistics.Port.Should().Be(30005);
        statistics.Connected.Should().BeFalse();
        statistics.Since.Should().BeNull();
        statistics.Reconnects.Should().Be(0);
        statistics.Messages.Should().Be(0);
    }

    [Fact]
    public async Task FirstAttemptFails_SinceIsTheFirstFailure()
    {
        int port = GetClosedPort();
        int attempts = 0;
        DateTime before = DateTime.UtcNow;
        await using var stream = new BeastStream("127.0.0.1", port, new TrackingConfig(), _ =>
        {
            Interlocked.Increment(ref attempts);
            return FastRetry;
        });

        await stream.StartAsync();
        BeastSourceStatistics first = await WaitForAsync(stream, s => s.Since != null);
        await WaitUntilAsync(() => Volatile.Read(ref attempts) >= 3);
        BeastSourceStatistics later = stream.GetSourceStatistics();

        first.Connected.Should().BeFalse();
        first.Since.Should().BeOnOrAfter(before);
        later.Since.Should().Be(first.Since, "further failed attempts do not move the time the source was lost");
        later.Reconnects.Should().Be(0);
    }

    [Fact]
    public async Task ConnectDisconnectReconnect_TracksStateAndReconnects()
    {
        using TcpListener listener = CreateListener(out int port);
        await using var stream = new BeastStream("127.0.0.1", port, new TrackingConfig(), _ => FastRetry);
        await stream.StartAsync();

        // First connection
        using (TcpClient first = await AcceptAsync(listener))
        {
            BeastSourceStatistics connected = await WaitForAsync(stream, s => s.Connected);
            connected.Since.Should().NotBeNull();
            connected.Reconnects.Should().Be(0);

            first.Close();
            BeastSourceStatistics lost = await WaitForAsync(stream, s => !s.Connected);
            lost.Since.Should().BeOnOrAfter(connected.Since!.Value);
            lost.Reconnects.Should().Be(0);

            // Second connection
            using TcpClient second = await AcceptAsync(listener);
            BeastSourceStatistics reconnected = await WaitForAsync(stream, s => s.Connected);
            reconnected.Since.Should().BeOnOrAfter(lost.Since!.Value);
            reconnected.Reconnects.Should().Be(1);
        }
    }

    [Fact]
    public async Task Frames_ConfirmedOnesBecomeMessages()
    {
        // Low confidence confirms an address on its fifth frame: frames 1 to 4 are held
        // back, 5 to 8 become messages. The DF 18 frame carries no ICAO aircraft address,
        // so confidence filtering passes it on without judging it.
        var tracking = new TrackingConfig { ConfidenceLevel = ConfidenceLevel.Low };
        using TcpListener listener = CreateListener(out int port);
        await using var stream = new BeastStream("127.0.0.1", port, tracking, _ => FastRetry);
        await stream.StartAsync();

        using TcpClient client = await AcceptAsync(listener);
        NetworkStream network = client.GetStream();
        for (int i = 0; i < 8; i++)
        {
            await network.WriteAsync(EncodeLong(RealFrames.AirbornePos_80073B_Even));
        }
        await network.WriteAsync(EncodeLong(Df18Cf1Position));

        BeastSourceStatistics statistics = await WaitForAsync(stream, s => s.Messages == 5);

        statistics.Messages.Should().Be(5);
    }

    // ─── Helpers ───

    private static byte[] EncodeLong(string hex) =>
        BeastTestData.CreateCompleteFrameStream(true, DateTime.UtcNow, 100, BeastTestData.HexToBytes(hex)).ToArray();

    private static async Task<TcpClient> AcceptAsync(TcpListener listener)
    {
        using var cts = new CancellationTokenSource(WaitLimit);
        return await listener.AcceptTcpClientAsync(cts.Token);
    }

    private static async Task<BeastSourceStatistics> WaitForAsync(
        BeastStream stream, Func<BeastSourceStatistics, bool> condition)
    {
        BeastSourceStatistics statistics = stream.GetSourceStatistics();
        await WaitUntilAsync(() => condition(statistics = stream.GetSourceStatistics()));
        return statistics;
    }

    /// <summary>
    /// Polls a condition that a background task will make true, failing after
    /// <see cref="WaitLimit"/> instead of hanging.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(WaitLimit);
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    private static TcpListener CreateListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    /// <summary>
    /// A port nothing listens on: bound once to get a free number, then released.
    /// </summary>
    private static int GetClosedPort()
    {
        using TcpListener listener = CreateListener(out int port);
        listener.Stop();
        return port;
    }
}
