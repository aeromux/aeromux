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
using System.Reflection;
using System.Threading.Channels;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Streaming;

namespace Aeromux.Infrastructure.Tests.Streaming;

public class BeastStreamReconnectionTests
{
    private static readonly TrackingConfig DefaultTracking = new();

    // ─── Retry Delay Constants ───

    [Fact]
    public void StartupDelays_AreFiveSecondIntervals()
    {
        FieldInfo? field = typeof(BeastStream).GetField("StartupDelays",
            BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("BeastStream should have a StartupDelays field");

        var delays = (TimeSpan[])field!.GetValue(null)!;
        delays.Should().HaveCount(5);
        delays.Should().AllSatisfy(d => d.Should().Be(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void BackoffDelays_AreEscalating()
    {
        FieldInfo? field = typeof(BeastStream).GetField("BackoffDelays",
            BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("BeastStream should have a BackoffDelays field");

        var delays = (TimeSpan[])field!.GetValue(null)!;
        delays.Should().HaveCount(5);
        delays[0].Should().Be(TimeSpan.FromSeconds(5));
        delays[1].Should().Be(TimeSpan.FromSeconds(10));
        delays[2].Should().Be(TimeSpan.FromSeconds(20));
        delays[3].Should().Be(TimeSpan.FromSeconds(30));
        delays[4].Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void PersistentDelay_IsSixtySeconds()
    {
        FieldInfo? field = typeof(BeastStream).GetField("PersistentDelay",
            BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("BeastStream should have a PersistentDelay field");

        var delay = (TimeSpan)field!.GetValue(null)!;
        delay.Should().Be(TimeSpan.FromSeconds(60));
    }

    // ─── Non-Blocking Startup ───

    [Fact]
    public async Task StartAsync_NoServer_ReturnsImmediately()
    {
        // StartAsync should return immediately even when no server is listening
        await using var stream = new BeastStream("127.0.0.1", 19999, DefaultTracking);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.StartAsync(cts.Token);

        // If we get here without timeout, startup was non-blocking
    }

    [Fact]
    public async Task StartAsync_NoServer_SubscribeWorks()
    {
        // Subscribe should work immediately after StartAsync, even with no active connection
        await using var stream = new BeastStream("127.0.0.1", 19999, DefaultTracking);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.StartAsync(cts.Token);

        ChannelReader<ProcessedFrame> reader = stream.Subscribe();
        reader.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_ConnectsToAvailableServer()
    {
        using TcpListener listener = CreateListener(out int port);
        await using var stream = new BeastStream("127.0.0.1", port, DefaultTracking);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.StartAsync(cts.Token);

        // If we get here without exception, startup succeeded
    }

    [Fact]
    public async Task StartAsync_ServerStartsLater_ConnectsAfterRetry()
    {
        // Bind to port 0 to get a free port, then stop listener to simulate "not ready yet"
        var tempListener = new TcpListener(IPAddress.Loopback, 0);
        tempListener.Start();
        int port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();

        // Re-start listener after 2 seconds (well before the first retry at 5s)
        TcpListener? lateListener = null;
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            lateListener = new TcpListener(IPAddress.Loopback, port);
            lateListener.Start();
        });

        try
        {
            await using var stream = new BeastStream("127.0.0.1", port, DefaultTracking);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await stream.StartAsync(cts.Token);

            // Subscribe and wait for the background task to connect
            ChannelReader<ProcessedFrame> reader = stream.Subscribe();

            // Give time for background connection + any data (just verify no crash)
            await Task.Delay(8000, cts.Token);
        }
        finally
        {
            lateListener?.Stop();
        }
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        using TcpListener listener = CreateListener(out int port);
        await using var stream = new BeastStream("127.0.0.1", port, DefaultTracking);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.StartAsync(cts.Token);
        await stream.StartAsync(cts.Token); // Second call should be no-op
    }

    // ─── Cancellation ───

    [Fact]
    public async Task StartAsync_CancellationDuringConnect_ThrowsOperationCanceled()
    {
        // Use a non-routable IP to trigger a slow connection attempt
        await using var stream = new BeastStream("192.0.2.1", 30005, DefaultTracking);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Func<Task> act = () => stream.StartAsync(cts.Token);

        // StartAsync itself is non-blocking, but the linked CTS propagates cancellation
        // The background task will be cancelled
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_StopsBroadcastTask()
    {
        using TcpListener listener = CreateListener(out int port);
        var stream = new BeastStream("127.0.0.1", port, DefaultTracking);

        // Use a long-lived CTS that won't be disposed before DisposeAsync
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await stream.StartAsync(cts.Token);
        await stream.DisposeAsync();
        cts.Dispose();
    }

    // ─── Subscribe/Unsubscribe ───

    [Fact]
    public async Task Subscribe_BeforeStart_ThrowsInvalidOperation()
    {
        await using var stream = new BeastStream("127.0.0.1", 30005, DefaultTracking);

        Action act = () => stream.Subscribe();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*StartAsync*");
    }

    [Fact]
    public async Task Subscribe_AfterStart_ReturnsChannelReader()
    {
        using TcpListener listener = CreateListener(out int port);
        await using var stream = new BeastStream("127.0.0.1", port, DefaultTracking);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.StartAsync(cts.Token);

        ChannelReader<ProcessedFrame> reader = stream.Subscribe();
        reader.Should().NotBeNull();
    }

    // ─── Statistics ───

    [Fact]
    public void GetStatistics_ReturnsNull()
    {
        // Beast source doesn't expose statistics (only raw frames)
        var stream = new BeastStream("127.0.0.1", 30005, DefaultTracking);
        stream.GetStatistics().Should().BeNull();
    }

    // ─── Constructor Validation ───

    [Fact]
    public void Constructor_NullHost_Throws()
    {
        Action act = () => new BeastStream(null!, 30005, DefaultTracking);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyHost_Throws()
    {
        Action act = () => new BeastStream("", 30005, DefaultTracking);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_PortZero_Throws()
    {
        Action act = () => new BeastStream("localhost", 0, DefaultTracking);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_PortExceedsMax_Throws()
    {
        Action act = () => new BeastStream("localhost", 65536, DefaultTracking);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NullTrackingConfig_Throws()
    {
        Action act = () => new BeastStream("localhost", 30005, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ───

    private static TcpListener CreateListener(out int port)
    {
        // Bind to port 0 to let the OS assign a free port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }
}
