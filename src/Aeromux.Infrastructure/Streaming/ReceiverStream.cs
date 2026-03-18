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
/// Used by both 'aeromux daemon' and 'aeromux live' commands.
///
/// PROCESSING PIPELINE (SDR):
/// RTL-SDR → IQ Samples → Demodulator → PreambleDetector → ValidatedFrameFactory → ValidatedFrame
/// → MessageParser → ProcessedFrame (frame + parsed message + timestamp + Source=Sdr)
///
/// PROCESSING PIPELINE (Beast Input):
/// Beast TCP Source → BeastParser → ValidatedFrame → IcaoConfidenceTracker →
/// MessageParser → ProcessedFrame (frame + parsed message + timestamp + Source=Beast)
/// Each BeastStream has its own IcaoConfidenceTracker (filters noise from real aircraft).
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
    private readonly List<SdrSourceConfig>? _sourceConfigs;
    private readonly List<BeastSourceConfig>? _beastSourceConfigs;
    private readonly TrackingConfig _trackingConfig;
    private readonly ReceiverConfig? _receiverConfig;
    private readonly MlatConfig? _mlatConfig;
    private FrameAggregator? _aggregator;  // Always created (for SDR devices + Beast + MLAT)
    private readonly List<DeviceWorker> _workers = [];
    private readonly List<BeastStream> _beastStreams = [];
    private readonly List<Task> _forwardTasks = [];  // Beast frame forwarding tasks (must be awaited during disposal)
    private MlatWorker? _mlatWorker;  // Created if MLAT is enabled
    private IcaoConfidenceTracker? _confidenceTracker;  // Shared across all workers
    private volatile bool _started;  // Volatile ensures visibility across threads
    private readonly SemaphoreSlim _startLock = new(1, 1);  // Ensures single initialization

    // Broadcasting support: Fan out frames to multiple consumers
    // Dictionary maps ChannelReader (public handle) to Channel (internal control)
    // This allows Unsubscribe() to locate the correct channel from the reader reference
    private readonly Dictionary<ChannelReader<ProcessedFrame>, Channel<ProcessedFrame>> _subscribers = [];
    private readonly Lock _subscribersLock = new();
    private volatile Channel<ProcessedFrame>[] _subscriberSnapshot = [];
    private Task? _broadcastTask;

    // Lifecycle management: ReceiverStream has its own cancellation independent of consumers
    private CancellationTokenSource? _internalCts;

    /// <summary>
    /// Creates a new ReceiverStream with the specified input sources.
    /// At least one input source (SDR or Beast) must be provided.
    /// </summary>
    /// <param name="sourceConfigs">SDR device configurations, or null for Beast-only mode.</param>
    /// <param name="trackingConfig">Tracking configuration for confidence filtering and timeouts.</param>
    /// <param name="receiverConfig">Receiver location for surface position decoding, or null if not configured.</param>
    /// <param name="mlatConfig">MLAT input configuration, or null to disable MLAT.</param>
    /// <param name="beastSourceConfigs">Beast TCP input sources, or null for SDR-only mode.</param>
    /// <exception cref="ArgumentException">Thrown when no input sources (SDR or Beast) are provided.</exception>
    public ReceiverStream(
        List<SdrSourceConfig>? sourceConfigs,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig,
        MlatConfig? mlatConfig = null,
        List<BeastSourceConfig>? beastSourceConfigs = null)
    {
        bool hasSdr = sourceConfigs is { Count: > 0 };
        bool hasBeast = beastSourceConfigs is { Count: > 0 };

        if (!hasSdr && !hasBeast)
        {
            throw new ArgumentException(
                "At least one input source (SDR or Beast) is required");
        }

        _sourceConfigs = hasSdr ? sourceConfigs : null;
        _beastSourceConfigs = hasBeast ? beastSourceConfigs : null;
        _trackingConfig = trackingConfig;
        _receiverConfig = receiverConfig;
        _mlatConfig = mlatConfig;
    }

    /// <summary>
    /// Creates a new ReceiverStream for a single SDR device (convenience constructor).
    /// </summary>
    /// <param name="sourceConfig">Single SDR device configuration.</param>
    /// <param name="trackingConfig">Tracking configuration for confidence filtering and timeouts.</param>
    /// <param name="receiverConfig">Receiver location for surface position decoding, or null if not configured.</param>
    /// <param name="mlatConfig">MLAT input configuration, or null to disable MLAT.</param>
    public ReceiverStream(
        SdrSourceConfig sourceConfig,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig,
        MlatConfig? mlatConfig = null)
        : this([sourceConfig], trackingConfig, receiverConfig, mlatConfig)
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

            // Create DeviceWorkers with shared confidence tracker (if SDR sources configured)
            if (_sourceConfigs != null)
            {
                foreach (SdrSourceConfig sourceConfig in _sourceConfigs)
                {
                    var worker = new DeviceWorker(
                        sourceConfig,
                        _trackingConfig,
                        _receiverConfig,
                        _confidenceTracker,  // Shared tracker - MLAT can mark ICAOs as confident
                        onDataParsed: (frame, message) =>
                            _aggregator.AddData(new ProcessedFrame(frame, message, DateTime.UtcNow)));

                    worker.OpenDevice();
                    worker.StartReceiving(_internalCts.Token);
                    _workers.Add(worker);
                }
            }

            // Create BeastStream instances (if Beast sources configured)
            // Each BeastStream has its own IcaoConfidenceTracker (filters noise from real aircraft)
            // Frames are forwarded into the shared FrameAggregator
            if (_beastSourceConfigs != null)
            {
                foreach (BeastSourceConfig beastConfig in _beastSourceConfigs)
                {
                    await StartBeastSourceAsync(beastConfig, _internalCts.Token);
                    await Task.Delay(50, _internalCts.Token); // Prevent macOS ARM64 Socket.ValidateBlockingMode race condition
                }
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

    /// <summary>
    /// Subscribes to the data stream and returns a dedicated channel for this subscriber.
    /// Multiple subscribers can call this to receive the same data stream concurrently.
    /// StartAsync() must be called first, otherwise throws InvalidOperationException.
    /// </summary>
    /// <returns>ChannelReader to read ProcessedFrame data from all configured sources.</returns>
    /// <exception cref="InvalidOperationException">Thrown when StartAsync() has not been called.</exception>
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
            _subscriberSnapshot = [.. _subscribers.Values];
            Log.Debug("Registered new subscriber (total: {Count})", _subscribers.Count);
        }

        return subscriberChannel.Reader;
    }

    /// <summary>
    /// Unsubscribes a channel from the data stream.
    /// Called by consumers when they no longer need data (typically in dispose).
    /// Safe to call multiple times with same reader (idempotent).
    /// </summary>
    /// <param name="reader">The channel reader to unsubscribe (obtained from Subscribe()).</param>
    public void Unsubscribe(ChannelReader<ProcessedFrame> reader)
    {
        lock (_subscribersLock)
        {
            if (_subscribers.Remove(reader))
            {
                _subscriberSnapshot = [.. _subscribers.Values];
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
                // Copy-on-write snapshot: volatile read is lock-free and allocation-free
                // Snapshot array is rebuilt only when subscribers change (Subscribe/Unsubscribe)
                foreach (Channel<ProcessedFrame> channel in _subscriberSnapshot)
                {
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

    /// <summary>
    /// Creates, starts, and registers a single Beast input source.
    /// StartAsync() is non-blocking — connection happens in the background with persistent retries.
    /// </summary>
    private async Task StartBeastSourceAsync(BeastSourceConfig beastConfig, CancellationToken ct)
    {
        var beastStream = new BeastStream(beastConfig.Host, beastConfig.Port, _trackingConfig);
        await beastStream.StartAsync(ct);

        ChannelReader<ProcessedFrame> beastReader = beastStream.Subscribe();
        _forwardTasks.Add(ForwardBeastFramesAsync(beastReader, ct));

        _beastStreams.Add(beastStream);
        Log.Information("Beast input source started: {Host}:{Port} (connecting in background)",
            beastConfig.Host, beastConfig.Port);
    }

    /// <summary>
    /// Forwards frames from a BeastStream subscriber channel into the shared FrameAggregator.
    /// Runs as a fire-and-forget background task for each Beast source.
    /// </summary>
    private async Task ForwardBeastFramesAsync(ChannelReader<ProcessedFrame> reader, CancellationToken ct)
    {
        try
        {
            await foreach (ProcessedFrame frame in reader.ReadAllAsync(ct))
            {
                _aggregator?.AddData(frame);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Beast frame forwarding task failed");
        }
    }

    /// <summary>
    /// Gets current statistics snapshot aggregated from all SDR device workers.
    /// Returns null when no SDR devices are active (Beast-only mode).
    /// </summary>
    /// <returns>Aggregated stream statistics, or null if no SDR devices are active.</returns>
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

    /// <summary>
    /// Disposes all resources in the correct shutdown order.
    /// All frame producers must be fully stopped before the aggregator is disposed
    /// to prevent segfaults from concurrent access during shutdown.
    /// Thread-safe and idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Step 1: Cancel internal operations (signals all background tasks to stop)
        await (_internalCts?.CancelAsync() ?? Task.CompletedTask);

        // Step 2: Stop all frame producers BEFORE disposing the aggregator.
        // ForwardBeastFramesAsync tasks write to the aggregator — they must complete first.

        // Step 2a: Dispose Beast streams (stops background tasks, completes subscriber channels)
        foreach (BeastStream beastStream in _beastStreams)
        {
            await beastStream.DisposeAsync();
        }

        // Step 2b: Wait for Beast forwarding tasks to finish (they read from Beast subscriber channels)
        foreach (Task forwardTask in _forwardTasks)
        {
            try
            {
                await forwardTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }

        // Step 2c: Dispose SDR device workers (stops OnSamplesAvailable callbacks)
        foreach (DeviceWorker worker in _workers)
        {
            worker.Dispose();
        }

        // Step 2d: Dispose MLAT worker if active
        _mlatWorker?.Dispose();

        // Step 3: Now safe to dispose aggregator (all producers have stopped)
        _aggregator?.Dispose();

        // Step 4: Wait for broadcast task to complete (reads from aggregator, completes subscriber channels)
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

        // Step 5: Dispose resources
        _internalCts?.Dispose();
        _startLock.Dispose();
    }
}
