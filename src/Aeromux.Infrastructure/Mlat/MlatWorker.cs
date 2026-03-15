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
using System.Net;
using System.Net.Sockets;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Infrastructure.Network.Protocols;
using Serilog;

namespace Aeromux.Infrastructure.Mlat;

/// <summary>
/// TCP listener that receives MLAT-computed position frames from mlat-client via Beast protocol.
/// Parallel to DeviceWorker but accepts Beast-encoded frames instead of processing raw IQ data.
/// </summary>
/// <remarks>
/// MLAT Worker Architecture:
/// - TCP Listener: Accepts connections from mlat-client on configured port (default 30104)
/// - Beast Parser: Decodes incoming Beast-formatted frames
/// - Message Parser: Extracts aircraft data from validated frames
/// - Confidence Marking: Marks MLAT ICAOs as confident for SDR workers
///
/// Key Differences from DeviceWorker:
/// - NO IQ demodulation (receives pre-processed frames)
/// - NO preamble detection (frames already detected)
/// - NO frame deduplication (mlat-client already deduplicated)
/// - NO confidence filtering (all MLAT frames are pre-validated)
/// - YES confidence marking (marks ICAOs so SDR workers trust them)
///
/// MLAT frames are:
/// - Pre-validated by MLAT network (high-quality positions)
/// - Pre-deduplicated by mlat-client (no duplicates)
/// - Marked as FrameSource.Mlat for downstream consumers
///
/// Multiple mlat-client instances can connect simultaneously.
/// </remarks>
public sealed class MlatWorker : IDisposable
{
    private readonly int _port;
    private readonly IPAddress _bindAddress;
    private readonly BeastParser _beastParser = new();
    private readonly MessageParser _messageParser;
    private readonly IcaoConfidenceTracker? _confidenceTracker;
    private readonly Action<ValidatedFrame, ModeSMessage?>? _onDataParsed;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private readonly ConcurrentBag<Task> _clientTasks = [];

    // Statistics
    private long _framesReceived;
    private int _connectedClients;
    private bool _firstFrameLogged;
    private readonly HashSet<string> _loggedIcaos = [];

    /// <summary>
    /// Initializes a new MLAT worker to receive Beast-encoded frames from mlat-client.
    /// </summary>
    /// <param name="port">TCP port to listen on (default: 30104)</param>
    /// <param name="bindAddress">IP address to bind to (IPAddress.Any accepts from all interfaces)</param>
    /// <param name="receiverConfig">Receiver configuration for message parsing</param>
    /// <param name="confidenceTracker">Shared confidence tracker to mark MLAT ICAOs as confident</param>
    /// <param name="onDataParsed">Callback invoked for each parsed frame</param>
    public MlatWorker(
        int port,
        IPAddress bindAddress,
        ReceiverConfig? receiverConfig,
        IcaoConfidenceTracker? confidenceTracker,
        Action<ValidatedFrame, ModeSMessage?>? onDataParsed = null)
    {
        _port = port;
        _bindAddress = bindAddress;
        _messageParser = new MessageParser("mlat", null);
        _confidenceTracker = confidenceTracker;
        _onDataParsed = onDataParsed;

        // Store receiver config for potential future use (e.g., local decoding fallback)
        _ = receiverConfig;
    }

    /// <summary>
    /// Starts the TCP listener and begins accepting mlat-client connections.
    /// </summary>
    /// <param name="cancellationToken">Token to stop the listener</param>
    public void Start(CancellationToken cancellationToken)
    {
        if (_listener != null)
        {
            return; // Already started
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listener = new TcpListener(_bindAddress, _port);
        _listener.Start();

        Log.Information("MLAT Worker: TCP listener started on {Address}:{Port}", _bindAddress, _port);

        _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);
    }

    /// <summary>
    /// Accepts incoming mlat-client connections and spawns a task for each.
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client = await _listener!.AcceptTcpClientAsync(ct);

                Interlocked.Increment(ref _connectedClients);
                Log.Information("MLAT Worker: Client connected from {RemoteEndPoint} (total: {Count})",
                    client.Client.RemoteEndPoint, _connectedClients);

                // Spawn task to handle this client
                var clientTask = Task.Run(() => ProcessClientAsync(client, ct), ct);
                _clientTasks.Add(clientTask);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            Log.Information("MLAT Worker: Listener task cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MLAT Worker: Error accepting clients");
        }
    }

    /// <summary>
    /// Processes Beast-encoded frames from a single mlat-client connection.
    /// </summary>
    private async Task ProcessClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                Log.Debug("MLAT Worker: Processing frames from {RemoteEndPoint}", client.Client.RemoteEndPoint);

                await foreach (ValidatedFrame validatedFrame in _beastParser.ParseStreamAsync(stream, ct))
                {
                    long frameCount = Interlocked.Increment(ref _framesReceived);

                    // Debug: Log first MLAT frame received as milestone
                    if (frameCount == 1 && !_firstFrameLogged)
                    {
                        _firstFrameLogged = true;
                        Log.Debug("MLAT: First frame received from mlat-client (ICAO: {IcaoAddress}, DF: {DF})",
                            validatedFrame.IcaoAddress, (int)validatedFrame.DownlinkFormat);
                    }

                    // Mark ICAO as confident (MLAT frames are pre-validated by MLAT network)
                    // This enables SDR workers to immediately trust frames from this ICAO
                    _confidenceTracker?.MarkAsConfident(validatedFrame.IcaoRaw, validatedFrame.Timestamp);

                    // Debug: Log first MLAT frame per ICAO (shows MLAT is marking ICAOs as confident)
                    lock (_loggedIcaos)
                    {
                        if (_loggedIcaos.Add(validatedFrame.IcaoAddress))
                        {
                            Log.Debug("MLAT: Received position for {IcaoAddress} (marked as confident for SDR workers)",
                                validatedFrame.IcaoAddress);
                        }
                    }

                    // Parse message (maybe null if frame format not supported)
                    ModeSMessage? message = _messageParser.ParseMessage(validatedFrame);

                    // Invoke callback (ReceiverStream will wrap in ProcessedFrame with Source=Mlat)
                    _onDataParsed?.Invoke(validatedFrame, message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
            Log.Debug("MLAT Worker: Client task cancelled for {RemoteEndPoint}", client.Client.RemoteEndPoint);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MLAT Worker: Error processing client {RemoteEndPoint}", client.Client.RemoteEndPoint);
        }
        finally
        {
            Interlocked.Decrement(ref _connectedClients);
            Log.Information("MLAT Worker: Client disconnected from {RemoteEndPoint} (remaining: {Count})",
                client.Client.RemoteEndPoint, _connectedClients);
        }
    }

    /// <summary>
    /// Gets the total number of MLAT frames received.
    /// </summary>
    public long FramesReceived => Interlocked.Read(ref _framesReceived);

    /// <summary>
    /// Gets the current number of connected mlat-client instances.
    /// </summary>
    public int ConnectedClients => _connectedClients;

    /// <summary>
    /// Disposes resources and stops accepting new connections.
    /// </summary>
    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();

            // Wait for listener task
            try
            {
                _listenerTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Expected during cancellation
            }

            // Wait for all client tasks
            try
            {
                Task.WaitAll(_clientTasks.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Expected during cancellation
            }

            _cts.Dispose();
        }

        _listener?.Stop();

        Log.Information("MLAT Worker disposed (received {FrameCount} frames)", FramesReceived);
    }
}
