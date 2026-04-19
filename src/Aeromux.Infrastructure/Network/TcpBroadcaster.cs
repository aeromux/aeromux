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
using System.Threading.Channels;
using Aeromux.Core.ModeS;
using Aeromux.Core.Tracking;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Infrastructure.Network.Enums;
using Aeromux.Infrastructure.Streaming;
using Aeromux.Infrastructure.Network.Protocols;
using Serilog;

namespace Aeromux.Infrastructure.Network;

/// <summary>
/// TCP broadcaster that sends processed frames to multiple clients.
/// Supports Beast, JSON, and SBS formats (dump1090/readsb/tar1090 compatible).
/// Broadcasts ProcessedFrame data from IFrameStream to all connected clients.
/// Beast encoder uses raw ValidatedFrame, JSON/SBS encoders use Aircraft state from tracker.
/// </summary>
/// <remarks>
/// ARCHITECTURE:
/// - One TcpBroadcaster instance per format/port (e.g., Beast on 30005, JSON on 30006, SBS on 30003)
/// - Each instance accepts multiple concurrent clients
/// - All clients receive the same data stream from IFrameStream via Subscribe()
/// - Client disconnections are detected and cleaned up automatically
///
/// FORMAT SELECTION:
/// The BroadcastFormat enum determines which encoder is used:
/// - Beast: BeastEncoder.Encode(data.Frame) - binary protocol for raw frames
/// - JSON: JsonEncoder.Encode(data) - full Aircraft state JSON with rate limiting
/// - SBS: SbsEncoder.Encode(data) - BaseStation CSV with Aircraft state for VRS compatibility
///
/// THREADING MODEL:
/// - AcceptClientsAsync: Background task accepting new connections (synchronous accept with polling)
/// - BroadcastFramesAsync: Background task reading from IFrameStream and writing to all clients
/// - Both tasks run concurrently and coordinate via thread-safe client list
///
/// DISPOSAL ORDER:
/// 1. Cancel background tasks (CancelAsync)
/// 2. Wait for AcceptClientsAsync to complete (before disposing listener)
/// 3. Wait for BroadcastFramesAsync to complete (before disposing clients)
/// 4. Dispose listener, clients, and unsubscribe from frame stream
/// </remarks>
public sealed class TcpBroadcaster : IAsyncDisposable
{
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);  // Disconnect clients that block writes beyond this

    private readonly int _port;
    private readonly IPAddress _bindAddress;
    private readonly IFrameStream _frameStream;
    private readonly BroadcastFormat _format;
    private readonly Guid? _receiverUuid;
    private readonly BeastEncoder? _beastEncoder;
    private readonly SbsEncoder? _sbsEncoder;
    private readonly JsonEncoder? _jsonEncoder;
    private ChannelReader<ProcessedFrame>? _dataReader;
    private bool _receiverIdSent;  // Prevents duplicate receiver ID broadcasts when multiple frames are sent

    // Client management (thread-safe via Lock)
    private readonly List<TcpClient> _clients = [];
    private readonly Lock _clientsLock = new();
    private volatile TcpClient[] _clientsSnapshot = [];

    // Background tasks and cancellation
    private TcpListener? _listener;
    private Task? _acceptTask;
    private Task? _broadcastTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a new TCP broadcaster for the specified format and port.
    /// </summary>
    /// <param name="port">TCP port to listen on (e.g., 30005 for Beast, 30006 for JSON, 30003 for SBS)</param>
    /// <param name="bindAddress">IP address to bind to (IPAddress.Any, Loopback, or specific interface)</param>
    /// <param name="frameStream">Frame stream providing processed frames to broadcast</param>
    /// <param name="format">Broadcast format (determines which encoder is used)</param>
    /// <param name="receiverUuid">Optional receiver UUID for MLAT triangulation (Beast format only)</param>
    /// <param name="aircraftTracker">Aircraft state tracker (required for SBS and JSON formats)</param>
    public TcpBroadcaster(
        int port,
        IPAddress bindAddress,
        IFrameStream frameStream,
        BroadcastFormat format,
        Guid? receiverUuid = null,
        IAircraftStateTracker? aircraftTracker = null)
    {
        _port = port;
        _bindAddress = bindAddress;
        _frameStream = frameStream;
        _format = format;
        _receiverUuid = receiverUuid;

        switch (format)
        {
            // Create encoder instances based on format
            // Beast encoder is stateful (reference time)
            // SBS and JSON encoders are stateful (aircraft state tracking, rate limiting)
            case BroadcastFormat.Beast:
                _beastEncoder = new BeastEncoder();
                break;
            case BroadcastFormat.Sbs:
            {
                if (aircraftTracker == null)
                {
                    throw new ArgumentNullException(nameof(aircraftTracker),
                        "Aircraft state tracker is required for SBS format");
                }
                _sbsEncoder = new SbsEncoder(aircraftTracker);
                break;
            }
            case BroadcastFormat.Json:
            {
                // JSON format REQUIRES aircraft tracker (like SBS)
                if (aircraftTracker == null)
                {
                    throw new ArgumentNullException(nameof(aircraftTracker),
                        "Aircraft state tracker is required for JSON format");
                }
                _jsonEncoder = new JsonEncoder(aircraftTracker);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    /// <summary>
    /// Starts the TCP broadcaster (begins accepting clients and broadcasting frames).
    /// </summary>
    /// <param name="cancellationToken">Token to stop the broadcaster</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Create linked cancellation token that responds to both external cancellation and internal disposal
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Subscribe to frame stream BEFORE starting listener
        // This ensures we're ready to receive data when devices start
        _dataReader = _frameStream.Subscribe();

        // Start TCP listener on specified bind address and port
        _listener = new TcpListener(_bindAddress, _port);
        _listener.Start();

        Log.Information("TCP broadcaster started on {BindAddress}:{Port} (format: {Format})", _bindAddress, _port, _format.ToString());

        // Launch background task to accept new client connections
        // This task runs until cancellation or disposal
        // Use Task.Run without redundant async/await wrapper
        _acceptTask = Task.Run(() => AcceptClientsAsync(_cts.Token), cancellationToken);

        // Launch background task to broadcast frames to all clients
        // This task runs until cancellation or disposal
        _broadcastTask = Task.Run(() => BroadcastFramesAsync(_cts.Token), cancellationToken);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Background task that accepts new client connections.
    /// Runs until cancellation or listener disposal.
    /// Uses fully blocking synchronous accept to avoid ALL async socket APIs.
    ///
    /// BLOCKING SYNCHRONOUS ACCEPT PATTERN:
    /// Wraps blocking AcceptTcpClient() in Task.Run() to offload blocking call to thread pool.
    /// This avoids ALL async socket operations (AcceptTcpClientAsync, Pending, Poll) which can
    /// corrupt SocketAsyncEventArgs internal memory under concurrent load, causing AccessViolationException.
    /// The corruption occurs when multiple async socket operations run concurrently (accept + read/write).
    /// Fully synchronous accept eliminates this memory corruption risk entirely.
    /// Cancellation: Disposing listener unblocks AcceptTcpClient() with SocketException (see DisposeAsync).
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                // Blocking synchronous accept on thread pool
                // This blocks until a client connects OR listener is disposed
                TcpClient? client = await Task.Run(() =>
                {
                    try
                    {
                        return _listener?.AcceptTcpClient();
                    }
                    catch (InvalidOperationException)
                    {
                        // Listener disposed during accept
                        return null;
                    }
                    catch (SocketException)
                    {
                        // Listener stopped/disposed
                        return null;
                    }
                }, ct);

                if (client == null)
                {
                    // Listener disposed - exit loop
                    break;
                }

                // Add client to list (thread-safe via lock)
                lock (_clientsLock)
                {
                    _clients.Add(client);
                    _clientsSnapshot = [.. _clients];
                    Log.Information("[{Format}] Client connected from {Remote} on port {Port} (total: {Count})",
                        _format.ToString(), client.Client.RemoteEndPoint, _port, _clients.Count);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - break loop cleanly
                break;
            }
            catch (Exception ex)
            {
                // Log transient errors but continue accepting clients
                Log.Warning(ex, "Error accepting TCP client");
            }
        }
    }

    /// <summary>
    /// Background task that broadcasts frames to all connected clients.
    /// Reads from subscribed channel, encodes based on format, and writes to all clients.
    /// Runs until cancellation or stream ends.
    ///
    /// ENCODING LOGIC:
    /// - Beast format: Encodes ValidatedFrame (raw binary with timestamps)
    /// - JSON format: Encodes ModeSMessage (parsed, line-delimited JSON)
    /// - SBS format: Encodes ModeSMessage (parsed, BaseStation CSV)
    /// Encoder returns null for unparseable frames or unsupported message types.
    ///
    /// CLIENT MANAGEMENT:
    /// Uses snapshot pattern to avoid holding lock during slow network writes.
    /// Detects disconnections via write failures and cleans up after broadcast loop.
    /// </summary>
    private async Task BroadcastFramesAsync(CancellationToken ct)
    {
        if (_dataReader == null)
        {
            throw new InvalidOperationException("Data reader not initialized. Call StartAsync() first.");
        }

        // Read aggregated data from subscribed channel
        await foreach (ProcessedFrame data in _dataReader.ReadAllAsync(ct))
        {
            // Step 1: Encode frame based on broadcaster's format
            // Beast uses raw ValidatedFrame, JSON/SBS use parsed ModeSMessage
            // SBS encoder returns List<ReadOnlyMemory<byte>> (may contain AIR, ID, and MSG messages)
            // Beast encoder returns ReadOnlyMemory<byte>, JSON returns ReadOnlyMemory<byte>? (null if skipped)
            List<ReadOnlyMemory<byte>> messagesToBroadcast = [];

            switch (_format)
            {
                case BroadcastFormat.Beast:
                {
                    // Skip MLAT-computed positions: mlat-client sends synthetic DF 17 frames
                    // with a magic timestamp back on port 30104. If re-broadcast on the Beast
                    // feed port (30005), mlat-client detects the loop and warns "MLAT magic timestamp".
                    if (data.Source == FrameSource.Mlat)
                    {
                        continue;
                    }

                    ReadOnlyMemory<byte> encoded = _beastEncoder!.Encode(data.Frame);
                    messagesToBroadcast.Add(encoded);
                    break;
                }
                case BroadcastFormat.Json:
                {
                    ReadOnlyMemory<byte>? encoded = _jsonEncoder!.Encode(data);
                    if (encoded.HasValue)
                    {
                        messagesToBroadcast.Add(encoded.Value);
                    }

                    break;
                }
                case BroadcastFormat.Sbs:
                {
                    List<ReadOnlyMemory<byte>> encoded = _sbsEncoder!.Encode(data);
                    messagesToBroadcast.AddRange(encoded);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown format: {_format}");
            }

            // Step 2: Skip if no messages to broadcast
            // Happens for unparseable frames (JSON/SBS) or unsupported message types (SBS)
            if (messagesToBroadcast.Count == 0)
            {
                continue;
            }

            // Step 3: Copy-on-write snapshot: volatile read is lock-free and allocation-free
            // Snapshot array is rebuilt only when clients connect or disconnect
            TcpClient[] clientsSnapshot = _clientsSnapshot;

            // Step 4: Send receiver ID once at start (Beast format only)
            // Transmits 0xe3 message containing first 64 bits of receiver UUID
            // Purpose: Enables MLAT networks to correlate timing data from this receiver across reconnections
            // Timing: Sent once per broadcaster lifecycle (before first frame, shared across all clients)
            // Note: Late-connecting clients don't receive receiver ID (they join mid-stream)
            if (_format == BroadcastFormat.Beast &&
                _receiverUuid.HasValue &&
                !_receiverIdSent)
            {
                byte[] receiverIdMessage = BeastEncoder.EncodeReceiverId(_receiverUuid.Value);

                // Broadcast receiver ID to all clients in snapshot
                foreach (TcpClient client in clientsSnapshot)
                {
                    try
                    {
                        await client.GetStream().WriteAsync(receiverIdMessage, ct);
                    }
                    catch
                    {
                        // Silently ignore write failures - will be detected on frame send below
                    }
                }

                _receiverIdSent = true;
                Log.Information("Sent receiver ID [{Format}] to {Count} client(s)", _format, clientsSnapshot.Length);
            }

            // Track clients that fail during write (for cleanup)
            var disconnected = new List<TcpClient>();

            // Step 5: Write encoded messages to all clients
            // SBS format may have multiple messages (AIR, ID, MSG) for a single frame
            // Beast and JSON formats have single message per frame
            foreach (ReadOnlyMemory<byte> message in messagesToBroadcast)
            {
                foreach (TcpClient client in clientsSnapshot)
                {
                    try
                    {
                        // Write with timeout to prevent slow clients from blocking all broadcasts
                        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        writeCts.CancelAfter(WriteTimeout);
                        await client.GetStream().WriteAsync(message, writeCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Write timed out — client is too slow, disconnect it
                        if (!disconnected.Contains(client))
                        {
                            Log.Warning("[{Format}] Slow client {Remote} on port {Port} — write timed out, disconnecting",
                                _format.ToString(), client.Client.RemoteEndPoint, _port);
                            disconnected.Add(client);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Parent cancellation — propagate to stop broadcast loop immediately
                        throw;
                    }
                    catch (Exception)
                    {
                        // Client disconnected or write failed (network error, buffer full, etc.)
                        // Don't log here - will be logged at Information level when cleaned up below
                        if (!disconnected.Contains(client))
                        {
                            disconnected.Add(client);
                        }
                    }
                }
            }

            // Step 6: Clean up disconnected clients
            if (disconnected.Count > 0)
            {
                lock (_clientsLock)
                {
                    foreach (TcpClient client in disconnected)
                    {
                        // Log individual disconnection with remote endpoint before disposal
                        Log.Information("[{Format}] Client disconnected from {Remote} on port {Port}",
                            _format.ToString(), client.Client.RemoteEndPoint, _port);

                        _clients.Remove(client);
                        client.Close();
                        client.Dispose();
                    }

                    _clientsSnapshot = [.. _clients];
                    Log.Information("[{Format}] Total clients on port {Port}: {Remaining}",
                        _format.ToString(), _port, _clients.Count);
                }
            }
        }
    }

    /// <summary>
    /// Disposes the broadcaster and all connected clients.
    /// Ensures proper shutdown order: cancel → wait for tasks → dispose clients.
    /// </summary>
    /// <remarks>
    /// DISPOSAL ORDER (Critical for Blocking Accept Pattern):
    /// 1. Signal cancellation to background tasks (CancelAsync)
    /// 2. Dispose listener FIRST to unblock AcceptTcpClient() calls
    /// 3. Wait for AcceptClientsAsync to complete (now unblocked by listener disposal)
    /// 4. Wait for BroadcastFramesAsync to complete (must finish before disposing clients)
    /// 5. Dispose clients (safe after broadcast task stopped)
    /// 6. Unsubscribe from frame stream
    /// 7. Dispose cancellation token source
    ///
    /// RATIONALE FOR ORDER:
    /// - AcceptTcpClient() is blocking and ignores cancellation tokens
    /// - Only way to unblock: dispose the listener (triggers SocketException)
    /// - AcceptClientsAsync catches SocketException, returns null, and exits loop
    /// - Must dispose listener BEFORE awaiting AcceptClientsAsync (otherwise hangs indefinitely)
    /// - BroadcastFramesAsync is fully async and responds to cancellation normally
    /// - Clients must be disposed AFTER broadcast task stops (prevents mid-write disposal)
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // Step 1: Signal cancellation to both background tasks
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        // Step 2: Dispose listener FIRST to unblock blocking accept call
        // CRITICAL ORDER: Must dispose listener BEFORE awaiting AcceptClientsAsync
        // Why: AcceptTcpClient() is blocking and ignores cancellation tokens
        // Disposing listener triggers SocketException that unblocks the accept call immediately
        // AcceptClientsAsync catches the exception and exits cleanly
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Server?.Close();
            _listener.Server?.Dispose();
        }

        // Step 3: Wait for AcceptClientsAsync to complete (now unblocked by listener disposal above)
        // The accept loop exits after catching SocketException from disposed listener
        if (_acceptTask != null)
        {
            try { await _acceptTask; }
            catch (OperationCanceledException) { } // Expected from cancellation token
            catch (ObjectDisposedException) { } // Possible if listener disposed during accept
        }

        // Step 4: Wait for BroadcastFramesAsync to complete
        // This task is fully async and responds to cancellation token normally
        // Must complete before disposing clients to prevent mid-write socket errors
        if (_broadcastTask != null)
        {
            try { await _broadcastTask; }
            catch (OperationCanceledException) { } // Expected from cancellation token
        }

        // Step 5: Dispose all connected clients (safe after broadcast task stopped)
        // No more writes will occur, so closing connections won't cause errors
        lock (_clientsLock)
        {
            foreach (TcpClient client in _clients)
            {
                client.Close();
                client.Dispose();
            }
            _clients.Clear();
            _clientsSnapshot = [];
        }

        // Step 6: Dispose encoders
        _sbsEncoder?.Dispose();
        _jsonEncoder?.Dispose();

        // Step 7: Unsubscribe from frame stream
        if (_dataReader != null)
        {
            _frameStream.Unsubscribe(_dataReader);
        }

        // Step 8: Dispose cancellation token source
        _cts?.Dispose();
    }

    /// <summary>
    /// Gets the current number of connected TCP clients for this broadcaster.
    /// Thread-safe: reads from volatile copy-on-write snapshot (no lock required).
    /// Useful for monitoring active connections and debugging connectivity issues.
    /// </summary>
    public int ClientCount => _clientsSnapshot.Length;
}
