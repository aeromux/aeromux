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

using System.Runtime.InteropServices;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Encapsulates CTRL+C / SIGTERM shutdown handling for the daemon.
/// Creates a linked CancellationTokenSource to handle both interactive (CTRL+C) and
/// service (SIGTERM) shutdown signals. Properly unregisters the Console.CancelKeyPress
/// handler and SIGTERM registration on disposal.
/// </summary>
public sealed class DaemonShutdownCoordinator : IDisposable
{
    private readonly CancellationTokenSource _shutdownCts;
    private readonly ConsoleCancelEventHandler? _cancelHandler;
    private readonly PosixSignalRegistration _sigtermRegistration;

    /// <summary>
    /// Creates a new shutdown coordinator linked to an external cancellation token.
    /// Registers a CTRL+C handler if running in interactive mode and a SIGTERM handler
    /// for systemd service shutdown.
    /// </summary>
    /// <param name="externalCancellationToken">External token (e.g., from Spectre.Console.Cli framework).</param>
    public DaemonShutdownCoordinator(CancellationToken externalCancellationToken)
    {
        _shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);

        // Handle CTRL+C in interactive mode (when running in terminal)
        // This doesn't fire when running as a systemd service (no console)
        if (!Console.IsInputRedirected)
        {
            _cancelHandler = (_, e) =>
            {
                Log.Information("CTRL+C received - requesting shutdown");
                e.Cancel = true; // Prevent immediate process termination
                try
                {
                    _shutdownCts.Cancel(); // Trigger graceful shutdown
                }
                catch (ObjectDisposedException)
                {
                    // CTS already disposed - shutdown already in progress
                }
            };
            Console.CancelKeyPress += _cancelHandler;
        }

        // Handle SIGTERM for service manager shutdown (systemctl stop, Docker stop, etc.)
        // PosixSignalRegistration is cross-platform: SIGTERM on Linux/macOS,
        // CTRL_SHUTDOWN_EVENT on Windows. Available since .NET 6.
        _sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
        {
            Log.Information("SIGTERM received - requesting shutdown");
            ctx.Cancel = true; // Prevent immediate process termination
            try
            {
                _shutdownCts.Cancel(); // Trigger graceful shutdown
            }
            catch (ObjectDisposedException)
            {
                // CTS already disposed - shutdown already in progress
            }
        });
    }

    /// <summary>
    /// Token that signals when shutdown has been requested via CTRL+C, SIGTERM, or external cancellation.
    /// </summary>
    public CancellationToken ShutdownToken => _shutdownCts.Token;

    /// <summary>
    /// Blocks until a shutdown signal is received from either CTRL+C or systemctl stop (SIGTERM).
    /// </summary>
    public async Task WaitForShutdownAsync()
    {
        Log.Debug("Entering wait loop for cancellation");

        try
        {
            // Wait for shutdown signal from either CTRL+C or systemctl stop (SIGTERM)
            await Task.Delay(Timeout.Infinite, _shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation - graceful shutdown
            Log.Information("Shutdown signal received");
        }
    }

    /// <summary>
    /// Unregisters the CTRL+C handler and disposes the linked CancellationTokenSource.
    /// </summary>
    public void Dispose()
    {
        if (_cancelHandler != null)
        {
            Console.CancelKeyPress -= _cancelHandler;
        }

        _sigtermRegistration.Dispose();
        _shutdownCts.Dispose();
    }
}
