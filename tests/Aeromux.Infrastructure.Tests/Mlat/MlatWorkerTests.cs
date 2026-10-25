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
using Aeromux.Infrastructure.Mlat;
using Aeromux.Infrastructure.Streaming;

namespace Aeromux.Infrastructure.Tests.Mlat;

/// <summary>
/// The MLAT input listens on the address it is given, and <see cref="ReceiverStream"/>
/// passes it the daemon's bind address instead of every interface. Port 0 lets the
/// operating system pick a free port.
/// </summary>
public class MlatWorkerTests
{
    [Fact]
    public void LocalEndpoint_BeforeStart_IsNull()
    {
        using var worker = new MlatWorker(0, IPAddress.Loopback, null, null);

        worker.LocalEndpoint.Should().BeNull();
    }

    [Fact]
    public void Start_OnLoopback_ListensOnLoopback()
    {
        using var cts = new CancellationTokenSource();
        using var worker = new MlatWorker(0, IPAddress.Loopback, null, null);

        worker.Start(cts.Token);

        worker.LocalEndpoint!.Address.Should().Be(IPAddress.Loopback);
        worker.LocalEndpoint.Port.Should().BePositive();
    }

    [Fact]
    public async Task ReceiverStream_MlatInputFollowsTheBindAddress()
    {
        await using ReceiverStream stream = CreateBeastOnlyStream(IPAddress.Loopback);

        await stream.StartAsync(CancellationToken.None);

        stream.MlatEndpoint!.Address.Should().Be(IPAddress.Loopback);
    }

    /// <summary>
    /// A Beast-only stream, so no SDR hardware is needed. The Beast source points at a port
    /// nothing listens on and keeps retrying in the background.
    /// </summary>
    private static ReceiverStream CreateBeastOnlyStream(IPAddress mlatBindAddress)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return new ReceiverStream(
            null,
            new TrackingConfig(),
            null,
            new MlatConfig { Enabled = true, InputPort = 0 },
            [new BeastSourceConfig { Host = "127.0.0.1", Port = closedPort }],
            mlatBindAddress);
    }
}
