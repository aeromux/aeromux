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
using Aeromux.Infrastructure.Network.Enums;
using Aeromux.Infrastructure.Streaming;
using Aeromux.Infrastructure.Network.Protocols;
using Serilog;

namespace Aeromux.Infrastructure.Network;

/// <summary>
/// TCP broadcaster that sends aggregated data to multiple clients.
/// Supports Beast, JSON, and SBS formats (dump1090/readsb/tar1090 compatible).
/// Phase 6: Broadcasts data from IFrameStream to all connected clients.
/// Beast uses raw ValidatedFrame, JSON/SBS use parsed ModeSMessage.
/// </summary>
/// <remarks>
/// Architecture:
/// - One TcpBroadcaster instance per format/port (e.g., Beast on 30005, JSON on 30006)
/// - Each instance accepts multiple concurrent clients
/// - All clients receive the same data stream from IFrameStream
/// - Client disconnections are detected and cleaned up automatically
///
/// Format Selection:
/// The BroadcastFormat enum determines which encoder is used:
/// - Beast: BeastEncoder.Encode(data.Frame) - binary protocol
/// - JSON: JsonEncoder.Encode(data.ParsedMessage) - line-delimited JSON
/// - SBS: SbsEncoder.Encode(data.ParsedMessage) - BaseStation CSV
///
/// Threading Model:
/// - AcceptClientsAsync: Background task accepting new connections
/// - BroadcastFramesAsync: Background task reading from IFrameStream and writing to all clients
/// - Both tasks run concurrently and coordinate via thread-safe client list
///
/// Disposal Order:
/// 1. Cancel background tasks (CancelAsync)
/// 2. Wait for tasks to complete (await with try-catch)
/// 3. Dispose clients (now safe, no tasks accessing them)
/// </remarks>
public sealed class TcpBroadcaster : IAsyncDisposable
{
    private readonly int _port;
    private readonly IPAddress _bindAddress;
    private readonly IFrameStream _frameStream;
    private readonly BroadcastFormat _format;

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
    /// <param name="frameStream">Frame stream providing aggregated data to broadcast</param>
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

        // Start TCP listener on specified bind address and port
        _listener = new TcpListener(_bindAddress, _port);
        _listener.Start();

        Log.Information("TCP broadcaster started on {BindAddress}:{Port} (format: {Format})", _bindAddress, _port, _format);

        // Launch background task to accept new client connections
        // This task runs until cancellation or disposal
        _acceptTask = Task.Run(async () => await AcceptClientsAsync(_cts.Token), _cts.Token);

        // Launch background task to broadcast frames to all clients
        // This task runs until cancellation or disposal
        _broadcastTask = Task.Run(async () => await BroadcastFramesAsync(_cts.Token), _cts.Token);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Background task that accepts new client connections.
    /// Runs until cancellation or listener disposal.
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                // Wait for new client connection (blocks until client connects)
                TcpClient client = await _listener.AcceptTcpClientAsync(ct);

                // Add client to list (thread-safe via lock)
                lock (_clientsLock)
                {
                    _clients.Add(client);
                    Log.Information("TCP client connected from {Remote} (total clients: {Count})",
                        client.Client.RemoteEndPoint, _clients.Count);
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
    /// Reads from IFrameStream, encodes based on format, and writes to all clients.
    /// Runs until cancellation or stream ends.
    /// </summary>
    private async Task BroadcastFramesAsync(CancellationToken ct)
    {
        // Read aggregated data from frame stream (async enumerable)
        await foreach (AggregatedData data in _frameStream.GetDataAsync(ct))
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
                catch (Exception ex)
                {
                    // Client disconnected or write failed (network error, buffer full, etc.)
                    // Log at Debug level since disconnections are normal events
                    Log.Debug(ex, "Failed to write to TCP client {Remote}",
                        client.Client.RemoteEndPoint);
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
                        _clients.Remove(client);
                        client.Close();
                        client.Dispose();
                    }

                    Log.Information("Removed {Count} disconnected clients (remaining: {Remaining})",
                        disconnected.Count, _clients.Count);
                }
            }
        }
    }

    /// <summary>
    /// Disposes the broadcaster and all connected clients.
    /// Ensures proper shutdown order: cancel → wait for tasks → dispose clients.
    /// </summary>
    /// <remarks>
    /// Disposal Order (Critical for Thread Safety):
    /// 1. Signal cancellation to background tasks (CancelAsync)
    /// 2. Stop accepting new connections (Stop listener)
    /// 3. Wait for background tasks to complete (await with try-catch)
    /// 4. NOW safe to dispose clients (no tasks accessing them)
    /// 5. Dispose cancellation token source
    ///
    /// Why This Order:
    /// We must wait for background tasks to complete BEFORE disposing clients,
    /// otherwise we risk disposing TcpClients that are still being accessed by
    /// BroadcastFramesAsync (causing ObjectDisposedException).
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // Step 1: Signal cancellation to both background tasks
        await _cts?.CancelAsync()!;

        // Step 2: Stop listener (prevents new connections)
        _listener?.Stop();

        // Step 3: Wait for AcceptClientsAsync to complete
        // Task will see cancellation and exit cleanly
        if (_acceptTask != null)
        {
            try { await _acceptTask; }
            catch (OperationCanceledException) { } // Expected during cancellation
        }

        // Step 4: Wait for BroadcastFramesAsync to complete
        // Task will see cancellation and stop reading/writing
        if (_broadcastTask != null)
        {
            try { await _broadcastTask; }
            catch (OperationCanceledException) { } // Expected during cancellation
        }

        // Step 5: NOW safe to dispose clients (no tasks accessing them)
        lock (_clientsLock)
        {
            foreach (TcpClient client in _clients)
            {
                client.Close();
                client.Dispose();
            }
            _clients.Clear();
        }

        // Step 6: Dispose cancellation token source
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
