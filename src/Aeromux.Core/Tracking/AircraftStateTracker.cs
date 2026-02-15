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

using System.Collections.Concurrent;
using System.Threading.Channels;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.Tracking.Handlers;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Thread-safe aircraft state tracking implementation.
/// Maintains comprehensive state for all tracked aircraft with automatic expiration.
/// Uses immutable Aircraft records for thread-safe reads and ConcurrentDictionary for writes.
/// </summary>
public sealed class AircraftStateTracker : IAircraftStateTracker, IDisposable
{
    private readonly ConcurrentDictionary<string, Aircraft> _aircraft = new();
    private readonly Timer _cleanupTimer;
    private readonly TrackingConfig _trackingConfig;
    private readonly TrackingHandlerRegistry _handlerRegistry;
    private Task? _consumerTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new aircraft state tracker with the specified configuration.
    /// Starts background cleanup timer automatically.
    /// </summary>
    /// <param name="trackingConfig">Tracking configuration (history buffer sizes, enable flags)</param>
    public AircraftStateTracker(TrackingConfig trackingConfig)
    {
        _trackingConfig = trackingConfig ?? throw new ArgumentNullException(nameof(trackingConfig));
        _handlerRegistry = new TrackingHandlerRegistry();

        // Initialize timeout from config (convert seconds to TimeSpan)
        AircraftTimeout = TimeSpan.FromSeconds(trackingConfig.AircraftTimeoutSeconds);

        // Start background cleanup timer (runs every CleanupInterval to remove expired aircraft)
        _cleanupTimer = new Timer(
            _ => CleanupExpiredAircraft(),
            null,
            CleanupInterval,
            CleanupInterval);
    }

    /// <summary>
    /// Gets or sets the aircraft timeout duration.
    /// Aircraft not seen within this time are considered expired and removed during cleanup.
    /// Initialized from TrackingConfig.AircraftTimeoutSeconds in constructor.
    /// Default: 60 seconds
    /// </summary>
    public TimeSpan AircraftTimeout { get; set; }

    /// <summary>
    /// Gets or sets the cleanup interval for expired aircraft removal.
    /// Background timer runs at this frequency to scan and remove stale entries.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Fired when an aircraft's state is updated with new data.
    /// Provides both previous and updated state for comparison.
    /// Fired on EVERY frame update regardless of whether data actually changed.
    /// Subscribers MUST compare Previous vs Updated properties to detect actual state changes
    /// and avoid redundant processing. Use this pattern instead of multiple specialized events
    /// for better performance and simpler event model.
    /// </summary>
    public event EventHandler<AircraftUpdateEventArgs>? OnAircraftUpdated;

    /// <summary>
    /// Fired when a new aircraft is first detected and added to tracking.
    /// Occurs on first frame received for a previously unseen ICAO address.
    /// </summary>
    public event EventHandler<AircraftEventArgs>? OnAircraftAdded;

    /// <summary>
    /// Fired when an aircraft is removed due to timeout expiration.
    /// Occurs during background cleanup when aircraft not seen within AircraftTimeout period.
    /// </summary>
    public event EventHandler<AircraftEventArgs>? OnAircraftExpired;

    /// <summary>
    /// Starts consuming frames from a channel reader in a background task.
    /// Must be called once after construction, before any other operations.
    /// The background task will continuously read from the channel until it's completed or cancellation is requested.
    /// </summary>
    /// <param name="frameChannel">Channel reader providing ProcessedFrame instances to track</param>
    /// <param name="cancellationToken">Cancellation token to stop the consumption loop</param>
    /// <exception cref="InvalidOperationException">Thrown if called more than once</exception>
    public void StartConsuming(ChannelReader<ProcessedFrame> frameChannel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frameChannel);

        if (_consumerTask != null)
        {
            throw new InvalidOperationException("StartConsuming can only be called once");
        }

        // Start background task to consume frames from the channel
        _consumerTask = Task.Run(async () =>
        {
            await foreach (ProcessedFrame frame in frameChannel.ReadAllAsync(cancellationToken))
            {
                Update(frame);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Updates aircraft state with data from a processed frame.
    /// Creates new aircraft entry if ICAO address not seen before.
    /// Updates existing aircraft state if already tracked.
    /// Thread-safe: Uses ConcurrentDictionary.AddOrUpdate internally.
    /// </summary>
    /// <param name="frame">Processed frame containing raw Mode S data and parsed message</param>
    public void Update(ProcessedFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);

        string icao = frame.Frame.IcaoAddress;

        // AddOrUpdate is atomic - thread-safe for concurrent updates
        _aircraft.AddOrUpdate(
            icao,
            _ => CreateNew(frame),           // Factory for new aircraft
            (_, existing) => ApplyUpdate(existing, frame));  // Update function for existing
    }

    /// <summary>
    /// Gets all currently tracked aircraft (excludes expired).
    /// Returns a snapshot copy ordered by ICAO address (thread-safe for iteration).
    /// Expired aircraft (not seen within AircraftTimeout) are filtered out.
    /// </summary>
    /// <returns>Read-only list of all non-expired tracked aircraft, sorted by ICAO</returns>
    public IReadOnlyList<Aircraft> GetAllAircraft()
    {
        return _aircraft.Values
            .Where(a => !a.IsExpired(AircraftTimeout))
            .OrderBy(a => a.Identification.ICAO)
            .ToList();
    }

    /// <summary>
    /// Gets a specific aircraft by ICAO address.
    /// Returns null if aircraft not found or has expired.
    /// </summary>
    /// <param name="icao">24-bit ICAO address as 6-character hex string (e.g., "440CF8")</param>
    /// <returns>Aircraft state if found and not expired, otherwise null</returns>
    public Aircraft? GetAircraft(string icao)
    {
        if (_aircraft.TryGetValue(icao, out Aircraft? aircraft) && !aircraft.IsExpired(AircraftTimeout))
        {
            return aircraft;
        }
        return null;
    }

    /// <summary>
    /// Gets the count of currently tracked aircraft (excludes expired).
    /// Note: This is a computed property that filters expired aircraft on each access.
    /// </summary>
    public int Count => _aircraft.Values.Count(a => !a.IsExpired(AircraftTimeout));

    /// <summary>
    /// Creates a new aircraft record from the first frame received for an ICAO address.
    /// Initializes tracking state with minimal data, then applies first message via handler.
    /// Fires OnAircraftAdded event after creation.
    /// </summary>
    /// <param name="frame">First processed frame for this ICAO address</param>
    /// <returns>Newly created Aircraft record</returns>
    private Aircraft CreateNew(ProcessedFrame frame)
    {
        DateTime now = DateTime.UtcNow;
        string icao = frame.Frame.IcaoAddress;

        // Initialize aircraft with minimal state
        var aircraft = new Aircraft
        {
            Identification = new TrackedIdentification
            {
                ICAO = icao,
                Callsign = null,
                Squawk = null,
                Category = null,
                EmergencyState = EmergencyState.NoEmergency,
                FlightStatus = null
            },
            Position = new TrackedPosition(),
            Velocity = new TrackedVelocity(),
            Status = new TrackedStatus
            {
                SignalStrength = frame.Frame.SignalStrength,
                TotalMessages = 1,
                PositionMessages = 0,
                VelocityMessages = 0,
                IdentificationMessages = 0,
                FirstSeen = now,
                LastSeen = now,
                SeenSeconds = 0,
                SeenPosSeconds = null,
                NextJsonOutput = now
            },
            History = CreateHistory()
        };

        // Apply first message via handler (if handler exists for this message type)
        if (frame.ParsedMessage != null)
        {
            ITrackingHandler? handler = _handlerRegistry.GetHandler(frame.ParsedMessage.GetType());
            if (handler != null)
            {
                aircraft = handler.Apply(aircraft, frame.ParsedMessage, frame, now);
            }
        }

        // Update message counters based on message type
        aircraft = UpdateMessageCounters(aircraft, frame, isFirstMessage: true);

        // Update history buffers with first frame data
        aircraft = aircraft with { History = UpdateHistory(aircraft.History, frame, aircraft.Position, aircraft.Velocity, now) };

        // Notify listeners of new aircraft
        OnAircraftAdded?.Invoke(this, new AircraftEventArgs { Aircraft = aircraft });

        return aircraft;
    }

    /// <summary>
    /// Applies an update from a processed frame to existing aircraft state.
    /// Delegates message-specific logic to registered handlers.
    /// Fires OnAircraftUpdated event on every update (subscribers compare Previous vs Updated for filtering).
    /// </summary>
    /// <param name="existing">Current aircraft state</param>
    /// <param name="frame">New frame with updated data</param>
    /// <returns>New Aircraft record with updated state</returns>
    private Aircraft ApplyUpdate(Aircraft existing, ProcessedFrame frame)
    {
        DateTime now = DateTime.UtcNow;
        Aircraft updated = existing;

        // Apply message-specific updates via handler
        if (frame.ParsedMessage != null)
        {
            ITrackingHandler? handler = _handlerRegistry.GetHandler(frame.ParsedMessage.GetType());
            if (handler != null)
            {
                updated = handler.Apply(updated, frame.ParsedMessage, frame, now);
            }
        }

        // Update common status fields (signal strength, counters, timestamps)
        updated = UpdateCommonStatus(updated, frame, now);

        // Update history buffers
        updated = updated with { History = UpdateHistory(updated.History, frame, updated.Position, updated.Velocity, now) };

        // Always fire update event (subscribers compare Previous vs Updated for filtering)
        OnAircraftUpdated?.Invoke(this, new AircraftUpdateEventArgs
        {
            Previous = existing,
            Updated = updated
        });

        return updated;
    }

    /// <summary>
    /// Updates message counters based on message type.
    /// Called for both first message and subsequent updates.
    /// Counters track message frequency for data quality assessment, statistics reporting,
    /// and determining aircraft tracking confidence (e.g., aircraft with many position updates
    /// have higher quality tracks than those with only identification messages).
    /// </summary>
    private static Aircraft UpdateMessageCounters(Aircraft aircraft, ProcessedFrame frame, bool isFirstMessage)
    {
        int positionCount = frame.ParsedMessage is AirbornePosition or SurfacePosition ? 1 : 0;
        int velocityCount = frame.ParsedMessage is AirborneVelocity ? 1 : 0;
        int identCount = frame.ParsedMessage is AircraftIdentification ? 1 : 0;

        TrackedStatus status = aircraft.Status with
        {
            PositionMessages = isFirstMessage ? positionCount : aircraft.Status.PositionMessages + positionCount,
            VelocityMessages = isFirstMessage ? velocityCount : aircraft.Status.VelocityMessages + velocityCount,
            IdentificationMessages = isFirstMessage ? identCount : aircraft.Status.IdentificationMessages + identCount,
            SeenPosSeconds = positionCount > 0 ? 0 : aircraft.Status.SeenPosSeconds
        };

        return aircraft with { Status = status };
    }

    /// <summary>
    /// Updates common status fields that apply to all messages.
    /// Includes signal strength, total message count, timestamps, and seen calculations.
    /// </summary>
    private static Aircraft UpdateCommonStatus(Aircraft aircraft, ProcessedFrame frame, DateTime now)
    {
        // Update message counters first
        aircraft = UpdateMessageCounters(aircraft, frame, isFirstMessage: false);

        // Then update common status fields (preserving counter updates from above)
        TrackedStatus status = aircraft.Status with
        {
            SignalStrength = frame.Frame.SignalStrength,
            TotalMessages = aircraft.Status.TotalMessages + 1,
            LastSeen = now,
            SeenSeconds = (now - aircraft.Status.FirstSeen).TotalSeconds,
            SeenPosSeconds = aircraft.Position.LastUpdate.HasValue
                ? (now - aircraft.Position.LastUpdate.Value).TotalSeconds
                : aircraft.Status.SeenPosSeconds
        };

        return aircraft with { Status = status };
    }

    /// <summary>
    /// Creates empty history buffers based on tracking configuration.
    /// Each buffer is only created if enabled in config to save memory.
    /// </summary>
    /// <returns>TrackedHistory with configured buffers (or nulls if disabled)</returns>
    private TrackedHistory CreateHistory()
    {
        return new TrackedHistory
        {
            PositionHistory = _trackingConfig.EnablePositionHistory
                ? new CircularBuffer<PositionSnapshot>(_trackingConfig.MaxHistorySize)
                : null,
            AltitudeHistory = _trackingConfig.EnableAltitudeHistory
                ? new CircularBuffer<AltitudeSnapshot>(_trackingConfig.MaxHistorySize)
                : null,
            VelocityHistory = _trackingConfig.EnableVelocityHistory
                ? new CircularBuffer<VelocitySnapshot>(_trackingConfig.MaxHistorySize)
                : null
        };
    }

    /// <summary>
    /// Updates history buffers with new data from frame.
    /// Adds snapshots to circular buffers (if enabled and data available).
    /// Old entries are automatically overwritten when buffers reach capacity.
    /// </summary>
    /// <param name="history">Existing history record with buffers</param>
    /// <param name="frame">Processed frame (currently unused but passed for potential future extensions)</param>
    /// <param name="position">Extracted position data to add to history</param>
    /// <param name="velocity">Extracted velocity data to add to history</param>
    /// <param name="now">Current timestamp for the snapshot</param>
    /// <returns>Same history record (buffers are mutated in place)</returns>
    private TrackedHistory UpdateHistory(
        TrackedHistory history,
        ProcessedFrame frame,
        TrackedPosition position,
        TrackedVelocity velocity,
        DateTime now)
    {
        // === Add position snapshot (if enabled and coordinate available) ===
        if (history.PositionHistory != null && position.Coordinate != null)
        {
            history.PositionHistory.Add(new PositionSnapshot(
                now,
                position.Coordinate,
                position.NACp));
        }

        // === Add altitude snapshot (if enabled and altitude available) ===
        // Prefer barometric altitude, fall back to geometric if available
        if (history.AltitudeHistory != null && position.BarometricAltitude != null)
        {
            history.AltitudeHistory.Add(new AltitudeSnapshot(
                now,
                position.BarometricAltitude,
                AltitudeType.Barometric));
        }
        else if (history.AltitudeHistory != null && position.GeometricAltitude != null)
        {
            history.AltitudeHistory.Add(new AltitudeSnapshot(
                now,
                position.GeometricAltitude,
                AltitudeType.Geometric));
        }

        // === Add velocity snapshot (if enabled and velocity data available) ===
        // Captures both airborne velocity (Speed, Heading, Track from TC 19) and
        // surface velocity (GroundSpeed, GroundTrack from TC 5-8) in a single snapshot.
        // Snapshot added if either airborne or surface velocity data is present.
        // Used for speed graphs, acceleration analysis, and performance profiling.
        if (history.VelocityHistory != null &&
            (velocity.Speed != null || velocity.GroundSpeed != null))
        {
            history.VelocityHistory.Add(new VelocitySnapshot(
                now,
                velocity.Speed,              // Airborne velocity from TC 19 (maybe null if only surface data)
                velocity.Heading,            // True heading from TC 19 subtype 3-4 (maybe null)
                velocity.Track,              // Ground track from TC 19 subtype 1-2 (maybe null)
                velocity.GroundSpeed,        // Surface ground speed from TC 5-8 (maybe null if airborne)
                velocity.GroundTrack,        // Surface ground track from TC 5-8 (maybe null if airborne)
                velocity.VerticalRate,       // Vertical rate from TC 19 (maybe null)
                velocity.VelocitySubtype));  // Velocity subtype from TC 19 (null if only surface data)
        }

        return history;
    }

    /// <summary>
    /// Removes expired aircraft from tracking (runs periodically on background timer).
    /// Scans all aircraft, removes those not seen within AircraftTimeout period.
    /// Fires OnAircraftExpired event for each removed aircraft.
    /// </summary>
    private void CleanupExpiredAircraft()
    {
        var expired = _aircraft
            .Where(kvp => kvp.Value.IsExpired(AircraftTimeout))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string icao in expired)
        {
            if (_aircraft.TryRemove(icao, out Aircraft? aircraft))
            {
                OnAircraftExpired?.Invoke(this, new AircraftEventArgs { Aircraft = aircraft });
            }
        }
    }

    /// <summary>
    /// Disposes the tracker and releases resources.
    /// Waits for background consumer task to complete and stops cleanup timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Wait for consumer task to complete gracefully
        // (Channel should already be completed by upstream disposal)
        if (_consumerTask != null)
        {
            try
            {
                _consumerTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        _cleanupTimer.Dispose();
    }
}
