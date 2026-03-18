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

using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Network;
using Aeromux.Infrastructure.Network.Enums;
using Aeromux.Infrastructure.Streaming;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Manages creation, staggered startup, and ordered disposal of TCP broadcasters.
/// Each broadcaster gets its own channel reader for independent consumption from the receiver stream.
/// </summary>
/// <remarks>
/// IMPORTANT: Preserves 50ms delay between broadcaster startups to prevent a race condition
/// in .NET's Socket.ValidateBlockingMode() on macOS ARM64 where concurrent socket initialization
/// can corrupt internal state fields, causing AccessViolationException.
/// </remarks>
public sealed class DaemonBroadcasterCollection : IAsyncDisposable
{
    private TcpBroadcaster? _beastBroadcaster;
    private TcpBroadcaster? _jsonBroadcaster;
    private TcpBroadcaster? _sbsBroadcaster;

    /// <summary>
    /// Creates and starts TCP broadcasters based on enabled flags in the validated configuration.
    /// Broadcasters are started with staggered 50ms delays to prevent macOS ARM64 socket race conditions.
    /// </summary>
    /// <param name="config">Validated daemon configuration.</param>
    /// <param name="receiverStream">Receiver stream to subscribe to for frame data.</param>
    /// <param name="aircraftTracker">Aircraft state tracker for JSON/SBS format enrichment.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Number of broadcasters started.</returns>
    public async Task<int> StartBroadcastersAsync(
        DaemonValidatedConfig config,
        ReceiverStream receiverStream,
        AircraftStateTracker aircraftTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(receiverStream);
        ArgumentNullException.ThrowIfNull(aircraftTracker);

        // Receiver UUID passed to Beast broadcaster enables MLAT identification (sent as 0xe3 message)
        //
        // IMPORTANT: Staggered startup with 50ms delays between broadcasters
        // This prevents a race condition in .NET's Socket.ValidateBlockingMode() on macOS ARM64
        // where concurrent socket initialization can corrupt internal state fields, causing AccessViolationException
        if (config.BeastEnabled)
        {
            _beastBroadcaster = new TcpBroadcaster(
                config.BeastOutputPort,
                config.BindAddress,
                receiverStream,
                BroadcastFormat.Beast,
                config.ReceiverUuid);
            await _beastBroadcaster.StartAsync(cancellationToken);
            Log.Information("Beast broadcaster started on {BindAddress}:{Port}", config.BindAddress, config.BeastOutputPort);
            await Task.Delay(50, cancellationToken); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition (AccessViolationException)
        }

        if (config.JsonEnabled)
        {
            _jsonBroadcaster = new TcpBroadcaster(
                config.JsonOutputPort,
                config.BindAddress,
                receiverStream,
                BroadcastFormat.Json,
                receiverUuid: null, // JSON doesn't use receiver UUID (Beast only)
                aircraftTracker: aircraftTracker); // Required for JSON format
            await _jsonBroadcaster.StartAsync(cancellationToken);
            Log.Information("JSON broadcaster started on {BindAddress}:{Port} (aircraft mode, 1s rate limit)", config.BindAddress, config.JsonOutputPort);
            await Task.Delay(50, cancellationToken); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition (AccessViolationException)
        }

        if (config.SbsEnabled)
        {
            _sbsBroadcaster = new TcpBroadcaster(
                config.SbsOutputPort,
                config.BindAddress,
                receiverStream,
                BroadcastFormat.Sbs,
                receiverUuid: null, // SBS doesn't use receiver UUID (Beast only)
                aircraftTracker: aircraftTracker); // Required for SBS format
            await _sbsBroadcaster.StartAsync(cancellationToken);
            Log.Information("SBS broadcaster started on {BindAddress}:{Port}", config.BindAddress, config.SbsOutputPort);
            await Task.Delay(50, cancellationToken); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition (AccessViolationException)
        }

        int enabledCount = (config.BeastEnabled ? 1 : 0) + (config.JsonEnabled ? 1 : 0) + (config.SbsEnabled ? 1 : 0);
        if (enabledCount == 0)
        {
            Log.Warning("All TCP output formats disabled - no broadcasters started");
        }
        Log.Information("TCP broadcasters started: {Count} format(s) enabled", enabledCount);

        return enabledCount;
    }

    /// <summary>
    /// Disposes all broadcasters in order. Each DisposeAsync waits for background tasks
    /// then disposes clients, unsubscribing from the device stream.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_beastBroadcaster != null)
        {
            await _beastBroadcaster.DisposeAsync();
        }

        if (_jsonBroadcaster != null)
        {
            await _jsonBroadcaster.DisposeAsync();
        }

        if (_sbsBroadcaster != null)
        {
            await _sbsBroadcaster.DisposeAsync();
        }
    }
}
