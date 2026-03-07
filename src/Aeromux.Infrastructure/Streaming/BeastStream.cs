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
/// Used by 'aeromux live --connect' client mode.
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
    /// Starts the Beast TCP connection and begins internal broadcasting.
    /// MUST be called once before any Subscribe() calls. Thread-safe, idempotent.
    /// Throws SocketException if connection fails (synchronously before starting background task).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the connection.</param>
    /// <exception cref="SocketException">Thrown when TCP connection to Beast source fails.</exception>
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

            // CRITICAL: Connect to Beast source BEFORE starting background task
            // This ensures SocketException is thrown synchronously if connection fails
            // Use 5-second timeout for connection attempt
            _client = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_internalCts.Token, timeoutCts.Token);

            try
            {
                await _client.ConnectAsync(_host, _port, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Connection timeout
                Log.Error("BeastStream: Connection timeout after 5 seconds: {Host}:{Port}", _host, _port);
                throw new SocketException((int)SocketError.TimedOut);
            }

            Log.Information("BeastStream: Connected to {Host}:{Port}", _host, _port);

            // Start background broadcast task (connection already established)
            _broadcastTask = Task.Run(() => BroadcastToSubscribersAsync(_internalCts.Token), _internalCts.Token);

            _started = true;

            Log.Information("BeastStream started: broadcasting from {Host}:{Port}", _host, _port);
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
    /// Background task that parses frames from already-connected Beast source,
    /// applies confidence filtering, and fans out to all subscribers.
    /// Connection is established in StartAsync before this task starts.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the broadcast task.</param>
    private async Task BroadcastToSubscribersAsync(CancellationToken ct)
    {
        try
        {
            // Get stream from already-connected client (connection established in StartAsync)
            if (_client == null)
            {
                throw new InvalidOperationException("BeastStream client not connected (StartAsync must be called first)");
            }

            NetworkStream stream = _client.GetStream();

            // Parse Beast stream (returns ValidatedFrame)
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
                // Snapshot array is rebuilt only when subscribers change (Subscribe/Unsubscribe)
                foreach (Channel<ProcessedFrame> channel in _subscriberSnapshot)
                {
                    channel.Writer.TryWrite(processedFrame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            Log.Information("BeastStream: Broadcast task cancelled");
        }
        catch (SocketException ex)
        {
            Log.Error(ex, "BeastStream: TCP connection error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "BeastStream: Unexpected error in broadcast task");
            throw;
        }
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

        Log.Information("BeastStream disposed");
    }
}
