// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
using Aeromux.Infrastructure.Network.Enums;
using Aeromux.Infrastructure.Streaming;
using Aeromux.Infrastructure.Network.Protocols;
using Serilog;

namespace Aeromux.Infrastructure.Network;

/// <summary>
/// TCP broadcaster that sends processed frames to multiple clients.
/// Supports Beast, JSON, and SBS formats (dump1090/readsb/tar1090 compatible).
/// Phase 6: Broadcasts ProcessedFrame data from IFrameStream to all connected clients.
/// Beast encoder uses raw ValidatedFrame, JSON/SBS encoders use parsed ModeSMessage.
/// </summary>
/// <remarks>
/// ARCHITECTURE:
/// - One TcpBroadcaster instance per format/port (e.g., Beast on 30005, JSON on 30006, SBS on 30104)
/// - Each instance accepts multiple concurrent clients
/// - All clients receive the same data stream from IFrameStream via Subscribe()
/// - Client disconnections are detected and cleaned up automatically
///
/// FORMAT SELECTION:
/// The BroadcastFormat enum determines which encoder is used:
/// - Beast: BeastEncoder.Encode(data.Frame) - binary protocol for raw frames
/// - JSON: JsonEncoder.Encode(data.ParsedMessage) - line-delimited JSON for web apps
/// - SBS: SbsEncoder.Encode(data.ParsedMessage) - BaseStation CSV for VRS compatibility
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
    private readonly int _port;
    private readonly IPAddress _bindAddress;
    private readonly IFrameStream _frameStream;
    private readonly BroadcastFormat _format;
    private ChannelReader<ProcessedFrame>? _dataReader;

    // Client management (thread-safe via Lock)
    private readonly List<TcpClient> _clients = [];
    private readonly Lock _clientsLock = new();

    // Background tasks and cancellation
    private TcpListener? _listener;
    private Task? _acceptTask;
    private Task? _broadcastTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a new TCP broadcaster for the specified format and port.
    /// </summary>
    /// <param name="port">TCP port to listen on (e.g., 30005 for Beast, 30006 for JSON)</param>
    /// <param name="bindAddress">IP address to bind to (IPAddress.Any, Loopback, or specific interface)</param>
    /// <param name="frameStream">Frame stream providing processed frames to broadcast</param>
    /// <param name="format">Broadcast format (determines which encoder is used)</param>
    public TcpBroadcaster(int port, IPAddress bindAddress, IFrameStream frameStream, BroadcastFormat format)
    {
        _port = port;
        _bindAddress = bindAddress;
        _frameStream = frameStream;
        _format = format;
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
    /// Uses synchronous accept with polling to avoid SocketAsyncEventArgs corruption.
    ///
    /// SYNCHRONOUS ACCEPT PATTERN:
    /// Uses Pending() + AcceptTcpClient() instead of AcceptTcpClientAsync().
    /// Async socket operations (AcceptTcpClientAsync) can corrupt SocketAsyncEventArgs memory
    /// when combined with concurrent async enumerators, causing AccessViolationException.
    /// Synchronous accept with polling avoids SocketAsyncEventArgs entirely.
    /// The 100ms polling delay is acceptable for connection acceptance (non-critical path).
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                // Check if a connection is pending (non-blocking)
                if (_listener.Pending())
                {
                    // Synchronous accept (no SocketAsyncEventArgs involved)
                    TcpClient client = await _listener.AcceptTcpClientAsync(ct);

                    // Add client to list (thread-safe via lock)
                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                        Log.Information("[{Format}] Client connected from {Remote} on port {Port} (total: {Count})",
                            _format.ToString(), client.Client.RemoteEndPoint, _port, _clients.Count);
                    }
                }
                else
                {
                    // No pending connection, wait before polling again
                    await Task.Delay(100, ct);
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
            // This is where format selection happens (controlled by constructor parameter)
            byte[]? encoded = _format switch
            {
                BroadcastFormat.Beast => BeastEncoder.Encode(data.Frame),
                BroadcastFormat.Json => JsonEncoder.Encode(data.ParsedMessage),
                BroadcastFormat.Sbs => SbsEncoder.Encode(data.ParsedMessage),
                _ => throw new InvalidOperationException($"Unknown format: {_format}")
            };

            // Step 2: Skip if encoder returned null
            // Happens for unparseable frames (JSON/SBS) or unsupported message types (SBS)
            if (encoded == null)
            {
                continue;
            }

            // Step 3: Create snapshot of client list for iteration
            // Lock protects against concurrent modifications during AcceptClientsAsync or cleanup
            // Snapshot allows us to iterate without holding lock during slow network writes
            List<TcpClient> clientsSnapshot;
            lock (_clientsLock)
            {
                clientsSnapshot = new List<TcpClient>(_clients);
            }

            // Track clients that fail during write (for cleanup)
            var disconnected = new List<TcpClient>();

            // Step 4: Write encoded data to all clients
            foreach (TcpClient client in clientsSnapshot)
            {
                try
                {
                    // Write encoded frame to client's network stream
                    await client.GetStream().WriteAsync(encoded, ct);
                }
                catch (OperationCanceledException)
                {
                    // Propagate cancellation to stop broadcast loop immediately
                    throw;
                }
                catch (Exception)
                {
                    // Client disconnected or write failed (network error, buffer full, etc.)
                    // Don't log here - will be logged at Information level when cleaned up below
                    // This avoids noisy Debug logs with stack traces for normal disconnections
                    disconnected.Add(client);
                }
            }

            // Step 5: Clean up disconnected clients
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
    /// DISPOSAL ORDER (Critical for Thread Safety):
    /// 1. Signal cancellation to background tasks (CancelAsync)
    /// 2. Wait for AcceptClientsAsync to complete FIRST (must finish before disposing listener)
    /// 3. Wait for BroadcastFramesAsync to complete (must finish before disposing clients)
    /// 4. NOW safe to dispose listener (no AcceptClientsAsync accessing it)
    /// 5. NOW safe to dispose clients (no BroadcastFramesAsync writing to them)
    /// 6. Unsubscribe from frame stream
    /// 7. Dispose cancellation token source
    ///
    /// WHY THIS ORDER:
    /// AcceptClientsAsync and BroadcastFramesAsync may be accessing resources concurrently.
    /// We must wait for both tasks to complete BEFORE disposing those resources,
    /// otherwise we get ObjectDisposedException from background tasks.
    /// Waiting for AcceptClientsAsync before disposing listener prevents socket disposal races.
    /// Waiting for BroadcastFramesAsync before disposing clients prevents TcpClient disposal races.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // Step 1: Signal cancellation to both background tasks
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        // Step 2: Wait for AcceptClientsAsync to complete FIRST
        // CRITICAL: Must wait for task before disposing listener
        if (_acceptTask != null)
        {
            try { await _acceptTask; }
            catch (OperationCanceledException) { } // Expected during cancellation
            catch (ObjectDisposedException) { } // May occur if listener disposed during accept
        }

        // Step 3: Wait for BroadcastFramesAsync to complete
        if (_broadcastTask != null)
        {
            try { await _broadcastTask; }
            catch (OperationCanceledException) { } // Expected during cancellation
        }

        // Step 4: NOW safe to dispose listener (no tasks using it)
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Server?.Close();
            _listener.Server?.Dispose();
        }

        // Step 5: Dispose clients (safe after broadcast task stopped)
        lock (_clientsLock)
        {
            foreach (TcpClient client in _clients)
            {
                client.Close();
                client.Dispose();
            }
            _clients.Clear();
        }

        // Step 6: Unsubscribe from frame stream
        if (_dataReader != null)
        {
            _frameStream.Unsubscribe(_dataReader);
        }

        // Step 7: Dispose cancellation token source
        _cts?.Dispose();
    }

    /// <summary>
    /// Gets the current number of connected clients.
    /// Thread-safe property.
    /// </summary>
    public int ClientCount
    {
        get
        {
            lock (_clientsLock)
            {
                return _clients.Count;
            }
        }
    }
}
