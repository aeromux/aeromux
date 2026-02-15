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
using System.Threading.Channels;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Infrastructure.Aggregation;
using Aeromux.Infrastructure.Mlat;
using Aeromux.Infrastructure.Sdr;
using Serilog;

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// Streams processed frames from local RTL-SDR device(s) and optional MLAT input.
/// Uses FrameAggregator to merge all sources (SDR devices + MLAT).
/// Used by 'aeromux live' standalone mode and 'aeromux daemon'.
///
/// PROCESSING PIPELINE (SDR):
/// RTL-SDR → IQ Samples → Demodulator → PreambleDetector → ValidatedFrameFactory → ValidatedFrame
/// → MessageParser → ProcessedFrame (frame + parsed message + timestamp + Source=Sdr)
///
/// PROCESSING PIPELINE (MLAT):
/// mlat-client → Beast (port 30104) → BeastParser → ValidatedFrame → MessageParser
/// → ProcessedFrame (frame + parsed message + timestamp + Source=Mlat)
///
/// BROADCAST ARCHITECTURE:
/// Supports multiple concurrent consumers (e.g., 3 TcpBroadcasters for Beast/JSON/SBS).
/// Each Subscribe() call creates an independent channel that receives ALL ProcessedFrames.
/// Internal broadcaster task fans out each frame to all registered subscribers.
///
/// THREAD SAFETY:
/// Uses Subscribe/Unsubscribe pattern to prevent async enumerator corruption.
/// Only ONE internal async enumerator exists (in BroadcastToSubscribersAsync).
/// Multiple concurrent GetDataAsync() calls would create multiple async enumerators,
/// corrupting the async state machine memory and causing AccessViolationException.
/// The Subscribe pattern ensures thread-safe fan-out via simple channel writes.
/// </summary>
public sealed class ReceiverStream : IFrameStream
{
    private readonly List<DeviceConfig> _deviceConfigs;
    private readonly TrackingConfig _trackingConfig;
    private readonly ReceiverConfig? _receiverConfig;
    private readonly MlatConfig? _mlatConfig;
    private FrameAggregator? _aggregator;  // Always created (for SDR devices + MLAT)
    private readonly List<DeviceWorker> _workers = [];
    private MlatWorker? _mlatWorker;  // Created if MLAT is enabled
    private IcaoConfidenceTracker? _confidenceTracker;  // Shared across all workers
    private volatile bool _started;  // Volatile ensures visibility across threads
    private readonly SemaphoreSlim _startLock = new(1, 1);  // Ensures single initialization

    // Broadcasting support: Fan out frames to multiple consumers
    // Dictionary maps ChannelReader (public handle) to Channel (internal control)
    // This allows Unsubscribe() to locate the correct channel from the reader reference
    private readonly Dictionary<ChannelReader<ProcessedFrame>, Channel<ProcessedFrame>> _subscribers = [];
    private readonly Lock _subscribersLock = new();
    private Task? _broadcastTask;

    // Lifecycle management: ReceiverStream has its own cancellation independent of consumers
    private CancellationTokenSource? _internalCts;

    public ReceiverStream(
        List<DeviceConfig> deviceConfigs,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig,
        MlatConfig? mlatConfig = null)
    {
        if (deviceConfigs == null || deviceConfigs.Count == 0)
        {
            throw new ArgumentException("At least one device configuration is required", nameof(deviceConfigs));
        }

        _deviceConfigs = deviceConfigs;
        _trackingConfig = trackingConfig;
        _receiverConfig = receiverConfig;
        _mlatConfig = mlatConfig;
    }

    // Convenience constructor for single device
    public ReceiverStream(
        DeviceConfig deviceConfig,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig,
        MlatConfig? mlatConfig = null)
        : this([deviceConfig], trackingConfig, receiverConfig, mlatConfig)
    {
    }

    /// <summary>
    /// Starts device workers and internal broadcasting.
    /// MUST be called once before any GetDataAsync() calls. Thread-safe, idempotent.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            // Create internal CTS for device lifecycle management
            _internalCts = new CancellationTokenSource();

            // Create shared confidence tracker (enables MLAT to mark ICAOs as confident for SDR workers)
            _confidenceTracker = new IcaoConfidenceTracker(
                _trackingConfig.ConfidenceLevel,
                _trackingConfig.IcaoTimeoutSeconds);

            // Always use FrameAggregator (simplifies logic, minimal overhead)
            _aggregator = new FrameAggregator();

            // Create DeviceWorkers with shared confidence tracker
            foreach (DeviceConfig deviceConfig in _deviceConfigs)
            {
                var worker = new DeviceWorker(
                    deviceConfig,
                    _trackingConfig,
                    _receiverConfig,
                    _confidenceTracker,  // Shared tracker - MLAT can mark ICAOs as confident
                    onDataParsed: (frame, message) =>
                        _aggregator.AddData(new ProcessedFrame(frame, message, DateTime.UtcNow)));

                worker.OpenDevice();
                worker.StartReceiving(_internalCts.Token);
                _workers.Add(worker);
            }

            // Start MLAT input worker (if enabled)
            if (_mlatConfig?.Enabled == true)
            {
                _mlatWorker = new MlatWorker(
                    _mlatConfig.InputPort,
                    IPAddress.Any,  // Accept from any interface
                    _receiverConfig,
                    _confidenceTracker,  // Shared tracker - MLAT marks ICAOs as confident
                    onDataParsed: (frame, message) =>
                        _aggregator.AddData(new ProcessedFrame(frame, message, DateTime.UtcNow, FrameSource.Mlat)));

                _mlatWorker.Start(_internalCts.Token);
                Log.Information("MLAT input worker started on port {Port}", _mlatConfig.InputPort);
            }
            else
            {
                Log.Information("MLAT input disabled");
            }

            // Start broadcaster task that fans out data to all subscribers
            _broadcastTask = Task.Run(() => BroadcastToSubscribersAsync(_internalCts.Token), cancellationToken);

            _started = true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public ChannelReader<ProcessedFrame> Subscribe()
    {
        if (!_started)
        {
            throw new InvalidOperationException("ReceiverStream not started. Call StartAsync() first.");
        }

        // Create dedicated channel for this subscriber
        var subscriberChannel = Channel.CreateUnbounded<ProcessedFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        // Register subscriber
        lock (_subscribersLock)
        {
            _subscribers.Add(subscriberChannel.Reader, subscriberChannel);
            Log.Debug("Registered new subscriber (total: {Count})", _subscribers.Count);
        }

        return subscriberChannel.Reader;
    }

    public void Unsubscribe(ChannelReader<ProcessedFrame> reader)
    {
        lock (_subscribersLock)
        {
            if (_subscribers.Remove(reader))
            {
                Log.Debug("Unregistered subscriber (remaining: {Count})", _subscribers.Count);
            }
        }
    }

    /// <summary>
    /// Background task that reads from FrameAggregator and fans out to all subscribers.
    /// Runs until cancellation or source completes.
    /// This is the ONLY place where an async enumerator is created on the FrameAggregator.
    /// All subscribers receive data through their own channels written to in this loop.
    /// This architecture prevents concurrent async enumerator creation which causes memory corruption.
    /// </summary>
    private async Task BroadcastToSubscribersAsync(CancellationToken ct)
    {
        try
        {
            if (_aggregator == null)
            {
                return;
            }

            // Fan-out loop: Broadcast each frame to all subscribers
            // This is the ONLY async enumerator created on the FrameAggregator
            await foreach (ProcessedFrame data in _aggregator.GetDataAsync(ct))
            {
                // Thread-safe snapshot: Copy subscriber channels while holding lock
                // Using List<Channel> instead of Dictionary reduces allocation overhead
                // Snapshot allows iteration without holding lock during channel writes
                List<Channel<ProcessedFrame>> snapshot;
                lock (_subscribersLock)
                {
                    snapshot = new List<Channel<ProcessedFrame>>(_subscribers.Values);
                }

                // Broadcast to all subscribers: Non-blocking writes to each channel
                foreach (Channel<ProcessedFrame> channel in snapshot)
                {
                    // TryWrite (non-blocking): Avoid blocking broadcast task on slow consumers
                    // Unbounded channels should never be full, but TryWrite is defensive
                    // Each subscriber reads from their own channel independently
                    channel.Writer.TryWrite(data);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Broadcaster task failed");
        }
        finally
        {
            // Complete all subscriber channels when source ends
            lock (_subscribersLock)
            {
                foreach (KeyValuePair<ChannelReader<ProcessedFrame>, Channel<ProcessedFrame>> kvp in _subscribers)
                {
                    kvp.Value.Writer.Complete();
                }
            }
        }
    }

    public StreamStatistics? GetStatistics()
    {
        if (_workers.Count == 0)
        {
            return null;
        }

        // Aggregate statistics from all workers (SDR devices + MLAT)
        return new StreamStatistics(
            _workers.Sum(w => w.PreambleDetector.FramesExtracted),
            _workers.Sum(w => w.ConfidenceTracker.ConfidentFrames),
            _mlatWorker?.FramesReceived ?? 0,
            _workers.Sum(w => w.ValidatedFrameFactory.FramesCorrected),
            _workers.Sum(w => w.MessageParser.MessagesParsed),
            DateTime.UtcNow - _workers.Min(w => w.StartTime));
    }

    public async ValueTask DisposeAsync()
    {
        // Step 1: Cancel internal operations (device workers, MLAT worker, and broadcast task)
        await (_internalCts?.CancelAsync() ?? Task.CompletedTask);

        // Step 2: Complete source and dispose aggregator
        _aggregator?.Dispose();

        // Step 3: Dispose MLAT worker if active
        _mlatWorker?.Dispose();

        // Step 4: Wait for broadcast task to complete (will complete subscriber channels)
        if (_broadcastTask != null)
        {
            try
            {
                await _broadcastTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }

        // Step 5: Dispose workers
        foreach (DeviceWorker worker in _workers)
        {
            worker.Dispose();
        }

        // Step 6: Dispose resources
        _internalCts?.Dispose();
        _startLock.Dispose();
    }
}
