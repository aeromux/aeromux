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

using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Database;
using Aeromux.Infrastructure.Photos;
using Aeromux.Infrastructure.Streaming;
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Orchestrates the daemon service lifecycle: creates and starts ReceiverStream,
/// AircraftStateTracker, and BroadcasterCollection, and ensures ordered shutdown.
/// </summary>
/// <remarks>
/// CRITICAL SHUTDOWN ORDER enforced by DisposeAsync:
/// 1. Stop TCP broadcasters (unsubscribe from device stream)
/// 2. Stop REST API server
/// 3. Stop receiver stream (close RTL-SDR devices, complete broadcast channel)
/// 4. Dispose aircraft tracker (wait for consumer task, dispose cleanup timer)
/// 5. Close database connection
/// </remarks>
public sealed class DaemonOrchestrator : IAsyncDisposable
{
    private readonly DaemonValidatedConfig _config;
    private AircraftDatabaseLookupService? _databaseLookup;
    private ReceiverStream? _receiverStream;
    private AircraftStateTracker? _aircraftTracker;
    private AircraftPhotoCache? _photoCache;
    private AircraftPhotoService? _photoService;
    private DaemonBroadcasterCollection? _broadcasters;
    private WebApplication? _webApp;
    private bool _disposed;

    /// <summary>
    /// Creates a new daemon orchestrator with the specified validated configuration.
    /// </summary>
    /// <param name="config">Validated daemon configuration.</param>
    public DaemonOrchestrator(DaemonValidatedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    /// Number of enabled RTL-SDR devices.
    /// </summary>
    public int DeviceCount => _config.EnabledSdrSources.Count;

    /// <summary>
    /// Creates and starts all daemon services in the correct order.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Number of broadcasters started.</returns>
    public async Task<int> StartAsync(CancellationToken cancellationToken)
    {
        // Create ReceiverStream with SDR and/or Beast sources (uninitialized - not started yet)
        _receiverStream = new ReceiverStream(
            _config.UseSdr ? _config.EnabledSdrSources : null,
            _config.Config.Tracking!,
            _config.Config.Receiver,
            _config.MlatConfig,
            _config.UseBeast ? _config.BeastSources : null);

        Log.Information("Receiver stream created. SDR={SdrCount}, Beast={BeastCount}",
            _config.EnabledSdrSources.Count, _config.BeastSources.Count);

        // CRITICAL STARTUP ORDER:
        // Start ReceiverStream FIRST (opens RTL-SDR devices and begins internal broadcasting)
        // This MUST complete before TcpBroadcasters call Subscribe()
        // ReceiverStream.StartAsync() initializes the internal broadcaster task and makes Subscribe() available
        await _receiverStream.StartAsync(cancellationToken);
        Log.Information("Device stream started");

        // Create database lookup service (null if database not configured or unavailable)
        _databaseLookup = DatabaseLookupFactory.TryCreate(_config.Config.Database);

        // Create centralized aircraft state tracker for all devices
        // Tracks aircraft across multiple RTL-SDR devices (automatic deduplication by ICAO)
        _aircraftTracker = new AircraftStateTracker(_config.Config.Tracking!, _databaseLookup);

        // Subscribe to aircraft lifecycle events for operational visibility
        // Logs new aircraft to track what's being received and help diagnose coverage issues
        _aircraftTracker.OnAircraftAdded += (sender, e) =>
        {
            Aircraft aircraft = e.Aircraft;
            Log.Information("New aircraft: ICAO={Icao}, Callsign={Callsign}",
                aircraft.Identification.ICAO,
                aircraft.Identification.Callsign ?? "Unknown");
        };

        // Log significant updates (typically for debug, currently does nothing)
        _aircraftTracker.OnAircraftUpdated += (previous, updated) =>
        {
            // Example
            //
            // Aircraft prev = e.Previous;
            // Aircraft curr = e.Updated;
            //
            // bool positionChanged = prev.Position.Coordinate != curr.Position.Coordinate ||
            //                       prev.Position.BarometricAltitude != curr.Position.BarometricAltitude;
            // bool velocityChanged = prev.Velocity.GroundSpeed != curr.Velocity.GroundSpeed ||
            //                       prev.Velocity.Speed != curr.Velocity.Speed;
            //
            // if (positionChanged || velocityChanged)
            // {
            //     Log.Debug("Aircraft update: ICAO={Icao}, Position={Position}, Alt={Altitude}, Speed={Velocity}",
            //         curr.Identification.ICAO,
            //         curr.Position.Coordinate,
            //         curr.Position.BarometricAltitude,
            //         curr.Velocity.GroundSpeed ?? curr.Velocity.Speed);
            // }
        };

        // IMPORTANT: Tracker runs in background, consuming frames automatically
        _aircraftTracker.StartConsuming(_receiverStream.Subscribe(), cancellationToken);
        Log.Information("Aircraft state tracker started");

        // Photo metadata cache + Planespotters lookup service. The service
        // subscribes to AircraftStateTracker.OnAircraftExpired in its
        // constructor, so it must be created after the tracker and disposed
        // before it (see DisposeAsync ordering).
        _photoCache = new AircraftPhotoCache();
        IPlanespottersApiClient planespottersClient = new PlanespottersApiClient();
        _photoService = new AircraftPhotoService(planespottersClient, _photoCache, _aircraftTracker);
        Log.Information("Aircraft photo service started (Planespotters metadata cache, cap {Cap})",
            _photoCache.Capacity);

        // Start REST API server (if enabled)
        if (_config.ApiEnabled)
        {
            DateTime startTime = DateTime.UtcNow;
            _webApp = DaemonApiServer.Build(_config, _aircraftTracker, _photoService,
                () => _receiverStream?.GetStatistics(), startTime);
            await _webApp.StartAsync(cancellationToken);
            Log.Information("REST API listening on http://{Bind}:{Port}/api/v1",
                _config.BindAddress, _config.ApiPort);
        }
        else
        {
            Log.Information("REST API disabled");
        }

        // Create and start TCP broadcasters
        _broadcasters = new DaemonBroadcasterCollection();
        return await _broadcasters.StartBroadcastersAsync(_config, _receiverStream, _aircraftTracker, cancellationToken);
    }

    /// <summary>
    /// Gets aggregated stream statistics from all devices.
    /// </summary>
    /// <returns>Stream statistics, or null if the stream hasn't started.</returns>
    public StreamStatistics? GetStatistics() => _receiverStream?.GetStatistics();

    /// <summary>
    /// Performs ordered shutdown of all daemon services.
    /// CRITICAL: Shutdown order must be broadcasters -> stream -> tracker to ensure
    /// clean resource cleanup and prevent data loss.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Console.WriteLine();
        Console.WriteLine("Shutting down TCP broadcasters, tracker, and device stream...");
        Log.Information("Shutting down TCP broadcasters, tracker, and device stream...");

        // Step 1: Stop TCP broadcasters first (null-safe disposal)
        // Each DisposeAsync waits for background tasks then disposes clients
        // This unsubscribes from device stream and stops consuming data
        if (_broadcasters != null)
        {
            await _broadcasters.DisposeAsync();
        }

        // Step 2: Stop REST API server
        if (_webApp != null)
        {
            await _webApp.DisposeAsync();
            Log.Information("REST API stopped");
        }

        // Step 3: Stop device stream
        // Closes RTL-SDR devices and completes internal broadcast channel
        // This will complete the trackerChannel, causing the tracker's consumer task to finish
        if (_receiverStream != null)
        {
            await _receiverStream.DisposeAsync();
        }

        // Step 4: Dispose photo service first (it has a subscription to the tracker;
        // unsubscribe before the tracker goes away).
        _photoService?.Dispose();

        // Step 5: Dispose aircraft tracker
        // Tracker.Dispose() waits for consumer task to complete, then disposes cleanup timer
        if (_aircraftTracker != null)
        {
            _aircraftTracker.Dispose();
            Log.Information("Aircraft state tracker stopped");
        }

        // Step 6: Close database connection
        _databaseLookup?.Dispose();

        Console.WriteLine("All device workers and TCP broadcasters stopped.");
        Log.Information("All device workers and TCP broadcasters stopped");
    }
}
