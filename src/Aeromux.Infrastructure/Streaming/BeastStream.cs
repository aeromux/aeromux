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

using System.Net.Sockets;
using System.Threading.Channels;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Infrastructure.Network.Protocols;
using Serilog;

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// Streams processed frames from a Beast-compatible TCP source.
/// Connects to any Beast-compatible server (readsb, dump1090, dump1090-fa, aeromux daemon)
/// and provides ProcessedFrame data via the IFrameStream interface.
/// Used by both daemon and live commands via ReceiverStream for Beast TCP input sources.
///
/// PROCESSING PIPELINE:
/// TCP Beast Source → BeastParser → ValidatedFrame → IcaoConfidenceTracker →
/// MessageParser → ProcessedFrame → Broadcast to subscribers
///
/// CRITICAL: Includes IcaoConfidenceTracker to filter noise from real aircraft.
/// Beast protocol transmits ALL CRC-validated frames (no confidence filtering).
/// readsb/dump1090 broadcast frames before confidence filtering.
///
/// BROADCAST ARCHITECTURE:
/// Follows same Subscribe/Unsubscribe pattern as ReceiverStream.
/// Multiple consumers can subscribe to receive ALL ProcessedFrames concurrently.
/// Internal broadcaster task fans out each frame to all registered subscribers.
/// </summary>
public sealed class BeastStream : IFrameStream
{
    // TCP connection
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;

    // Processing components (MUST include IcaoConfidenceTracker!)
    private readonly BeastParser _beastParser = new();
    private readonly MessageParser _messageParser = new();
    private readonly IcaoConfidenceTracker _confidenceTracker;

    // Broadcasting support: Fan out frames to multiple consumers
    private readonly Dictionary<ChannelReader<ProcessedFrame>, Channel<ProcessedFrame>> _subscribers = [];
    private readonly Lock _subscribersLock = new();
    private volatile Channel<ProcessedFrame>[] _subscriberSnapshot = [];
    private Task? _broadcastTask;

    // Lifecycle management
    private CancellationTokenSource? _internalCts;
    private volatile bool _started;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    // Retry configuration: startup → backoff → persistent
    // Phase 1: Fast retries during startup (5 × 5s = 25s)
    private static readonly TimeSpan[] StartupDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5)
    ];

    // Phase 2: Escalating backoff (5s → 10s → 20s → 30s → 60s)
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

    // Phase 3: Persistent retry forever
    private static readonly TimeSpan PersistentDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Creates a new BeastStream that connects to a Beast-compatible TCP source.
    /// </summary>
    /// <param name="host">Beast server hostname or IP address.</param>
    /// <param name="port">Beast server port (default: 30005).</param>
    /// <param name="trackingConfig">Tracking configuration for IcaoConfidenceTracker.</param>
    /// <exception cref="ArgumentException">Thrown when host is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when trackingConfig is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port is not between 1 and 65535.</exception>
    public BeastStream(string host, int port, TrackingConfig trackingConfig)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(trackingConfig);

        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535");
        }

        _host = host;
        _port = port;

        // Create confidence tracker to filter noise (same as DeviceWorker does)
        // readsb/dump1090 transmit ALL validated frames via Beast, not just confident ones
        _confidenceTracker = new IcaoConfidenceTracker(
            trackingConfig.ConfidenceLevel,
            trackingConfig.IcaoTimeoutSeconds);
    }

    /// <summary>
    /// Starts the background connection and broadcasting task. Returns immediately (non-blocking).
    /// MUST be called once before any Subscribe() calls. Thread-safe, idempotent.
    /// Connection to the Beast source happens in the background with persistent retries:
    /// 5×5s (startup) → 5s,10s,20s,30s,60s (backoff) → 60s (forever).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the connection.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return; // Idempotent: Already started
        }

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return; // Double-check after acquiring lock
            }

            _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start background task that connects and broadcasts (non-blocking)
            // Connection retries happen inside the background task
            _broadcastTask = Task.Run(() => ConnectAndBroadcastAsync(_internalCts.Token), _internalCts.Token);

            _started = true;

            Log.Information("BeastStream started: connecting to {Host}:{Port} in background", _host, _port);
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>
    /// Connects to the Beast TCP source with a 5-second timeout.
    /// </summary>
    private async Task<TcpClient> ConnectAsync(CancellationToken ct)
    {
        var client = new TcpClient();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await client.ConnectAsync(_host, _port, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            client.Dispose();
            Log.Error("BeastStream: Connection timeout after 5 seconds: {Host}:{Port}", _host, _port);
            throw new SocketException((int)SocketError.TimedOut);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return client;
    }

    /// <summary>
    /// Subscribes to the data stream and returns a dedicated channel for this subscriber.
    /// Multiple subscribers can call this to receive the same data stream concurrently.
    /// StartAsync() must be called first, otherwise throws InvalidOperationException.
    /// </summary>
    /// <returns>ChannelReader to read ProcessedFrame data from Beast source.</returns>
    /// <exception cref="InvalidOperationException">Thrown when StartAsync() has not been called.</exception>
    public ChannelReader<ProcessedFrame> Subscribe()
    {
        if (!_started)
        {
            throw new InvalidOperationException("StartAsync() must be called before Subscribe()");
        }

        // Create unbounded channel for this subscriber
        var channel = Channel.CreateUnbounded<ProcessedFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false, // Multiple broadcasters could write
            AllowSynchronousContinuations = false
        });

        lock (_subscribersLock)
        {
            _subscribers[channel.Reader] = channel;
            _subscriberSnapshot = [.. _subscribers.Values];
            Log.Debug("BeastStream: New subscriber added (total: {Count})", _subscribers.Count);
        }

        return channel.Reader;
    }

    /// <summary>
    /// Unsubscribes a channel from the data stream.
    /// Called by consumers when they no longer need data (typically in dispose).
    /// Safe to call multiple times with same reader (idempotent).
    /// </summary>
    /// <param name="reader">The channel reader to unsubscribe (obtained from Subscribe()).</param>
    public void Unsubscribe(ChannelReader<ProcessedFrame> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        lock (_subscribersLock)
        {
            if (!_subscribers.Remove(reader, out Channel<ProcessedFrame>? channel))
            {
                return;
            }

            _subscriberSnapshot = [.. _subscribers.Values];
            channel.Writer.TryComplete();
            Log.Debug("BeastStream: Subscriber removed (remaining: {Count})", _subscribers.Count);
        }
    }

    /// <summary>
    /// Gets current statistics snapshot.
    /// Returns null because Beast source doesn't expose statistics (only raw frames).
    /// Statistics are only available in standalone mode (from ReceiverStream).
    /// Remote Beast source doesn't provide frame counts or MLAT info.
    /// </summary>
    public StreamStatistics? GetStatistics()
    {
        // Client mode: Beast source doesn't expose statistics
        // Remote Beast source doesn't provide frame counts or MLAT info
        return null;
    }

    /// <summary>
    /// Background task that handles the full lifecycle: connect → read/broadcast → reconnect.
    /// Retries persistently: 5×5s (startup) → 5s,10s,20s,30s,60s (backoff) → 60s (forever).
    /// On successful connection, the delay index resets so the next disconnect starts fresh.
    /// </summary>
    private async Task ConnectAndBroadcastAsync(CancellationToken ct)
    {
        int delayIndex = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Try to connect
                try
                {
                    _client = await ConnectAsync(ct);
                    Log.Information("BeastStream: Connected to {Host}:{Port}", _host, _port);
                    delayIndex = 0; // Reset retry sequence on successful connection
                }
                catch (OperationCanceledException)
                {
                    return; // Normal shutdown
                }
                catch (Exception ex) when (ex is SocketException or IOException)
                {
                    Log.Warning("BeastStream: Connection to {Host}:{Port} failed: {Error}",
                        _host, _port, ex.Message);
                    TimeSpan delay = GetRetryDelay(delayIndex++);
                    Log.Information("BeastStream: Retrying {Host}:{Port} in {Delay}s",
                        _host, _port, (int)delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                    continue; // Back to top — try connecting again
                }

                // Connected — read and broadcast frames until disconnect
                try
                {
                    await ReadAndBroadcastAsync(ct);
                    // Clean exit — server closed connection gracefully
                    Log.Information("BeastStream: Server {Host}:{Port} closed connection", _host, _port);
                }
                catch (OperationCanceledException)
                {
                    return; // Normal shutdown
                }
                catch (Exception ex) when (ex is SocketException or IOException)
                {
                    Log.Warning("BeastStream: Connection lost to {Host}:{Port}: {Error}",
                        _host, _port, ex.Message);
                }

                // Disconnected — clean up client, wait, then retry at top of loop
                _client?.Close();
                _client?.Dispose();
                _client = null;

                TimeSpan reconnectDelay = GetRetryDelay(delayIndex++);
                Log.Information("BeastStream: Reconnecting to {Host}:{Port} in {Delay}s",
                    _host, _port, (int)reconnectDelay.TotalSeconds);
                await Task.Delay(reconnectDelay, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Log.Error(ex, "BeastStream: Unexpected error in background task");
        }
    }

    /// <summary>
    /// Reads frames from the current TCP connection and broadcasts to subscribers.
    /// Returns when the stream ends or throws on connection errors.
    /// </summary>
    private async Task ReadAndBroadcastAsync(CancellationToken ct)
    {
        NetworkStream stream = _client!.GetStream();

        await foreach (ValidatedFrame validatedFrame in _beastParser.ParseStreamAsync(stream, ct))
        {
            // CRITICAL: Apply confidence filtering (same as DeviceWorker)
            // Beast protocol transmits ALL CRC-validated frames (noise + real aircraft)
            bool isConfident = _confidenceTracker.TrackAndValidate(validatedFrame, out bool _);

            if (!isConfident)
            {
                continue; // Skip non-confident frames (noise)
            }

            // Parse message for confident frames only
            ModeSMessage? message = _messageParser.ParseMessage(validatedFrame);

            // Construct ProcessedFrame with Beast source marking
            var processedFrame = new ProcessedFrame(validatedFrame, message, DateTime.UtcNow, FrameSource.Beast);

            // Copy-on-write snapshot: volatile read is lock-free and allocation-free
            foreach (Channel<ProcessedFrame> channel in _subscriberSnapshot)
            {
                channel.Writer.TryWrite(processedFrame);
            }
        }
    }

    /// <summary>
    /// Returns the retry delay for the given attempt index.
    /// Phases: StartupDelays (5×5s) → BackoffDelays (5,10,20,30,60) → PersistentDelay (60s forever).
    /// </summary>
    private static TimeSpan GetRetryDelay(int index)
    {
        if (index < StartupDelays.Length)
        {
            return StartupDelays[index];
        }

        int backoffIndex = index - StartupDelays.Length;
        if (backoffIndex < BackoffDelays.Length)
        {
            return BackoffDelays[backoffIndex];
        }

        return PersistentDelay;
    }

    /// <summary>
    /// Disposes resources and stops the broadcast task.
    /// Cancels internal token, waits for broadcast task to complete, closes TCP connection,
    /// and completes all subscriber channels. Thread-safe and idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_internalCts != null)
        {
            await _internalCts.CancelAsync();

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

            _internalCts.Dispose();
        }

        if (_client != null)
        {
            _client.Close();
            _client.Dispose();
        }

        // Complete all subscriber channels
        lock (_subscribersLock)
        {
            foreach (Channel<ProcessedFrame> channel in _subscribers.Values)
            {
                channel.Writer.TryComplete();
            }
            _subscribers.Clear();
        }

        _startLock.Dispose();
        _confidenceTracker.Dispose();

        Log.Information("BeastStream disposed");
    }
}
