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

using System.Net.Sockets;
using RtlSdrManager.Exceptions;
using Serilog;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Centralized exception handler for live command errors.
/// Provides a top-level handler for ExecuteAsync and separate methods for standalone
/// and client modes, as they have entirely different exception types and user-facing error messages.
/// </summary>
public static class LiveExceptionHandler
{
    /// <summary>
    /// Handles top-level exceptions from ExecuteAsync.
    /// Maps validation errors (InvalidOperationException) to concise messages
    /// and logs unexpected errors with full context.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <returns>Exit code (always 1 for error).</returns>
    public static int HandleException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        switch (ex)
        {
            // Validation failures from LiveConfigValidator
            case InvalidOperationException:
                Log.Error(ex.Message);
                Console.WriteLine($"Error: {ex.Message}");
                break;

            // Unexpected errors
            default:
                Log.Error(ex, "Unexpected error in Live command");
                Console.WriteLine(ex);
                break;
        }

        return 1;
    }

    /// <summary>
    /// Handles exceptions from standalone mode (direct RTL-SDR access).
    /// Maps RTL-SDR library exceptions to user-friendly error messages.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <returns>Exit code (always 1 for error).</returns>
    /// <remarks>
    /// Uses string-based type matching (GetType().Name.Contains("RtlSdr")) for some exception types
    /// because the RTL-SDR library defines multiple exception classes that don't share a common base type.
    /// Only RtlSdrLibraryExecutionException (device-in-use) can be matched directly by type.
    /// </remarks>
    public static int HandleStandaloneException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        switch (ex)
        {
            // RTL-SDR device already in use by another process
            case RtlSdrLibraryExecutionException:
                Log.Error(ex, "RTL-SDR device already in use");
                Console.WriteLine("Error: Cannot open RTL-SDR device (already in use)");
                Console.WriteLine("This usually means another instance is running.");
                Console.WriteLine("Try:");
                Console.WriteLine("  1. Connect to daemon: aeromux live --connect localhost:30005");
                Console.WriteLine("  2. Stop daemon: aeromux daemon stop");
                break;

            // RTL-SDR device not found (string-based matching for external library exceptions)
            case Exception when ex.GetType().Name.Contains("RtlSdr") && ex.Message.Contains("not found"):
                Log.Error(ex, "RTL-SDR device not found");
                Console.WriteLine("Error: RTL-SDR device not found");
                Console.WriteLine("Please check:");
                Console.WriteLine("  1. Device is connected via USB");
                Console.WriteLine("  2. Drivers are installed (librtlsdr)");
                Console.WriteLine("  3. Run 'rtl_test' to verify device detection");
                break;

            // Other RTL-SDR errors (string-based matching for external library exceptions)
            case Exception when ex.GetType().Name.Contains("RtlSdr"):
                Log.Error(ex, "RTL-SDR error");
                Console.WriteLine($"RTL-SDR Error: {ex.Message}");
                break;

            // Unexpected errors
            default:
                Log.Error(ex, "Unexpected error in standalone mode");
                Console.WriteLine($"Unexpected error: {ex.Message}");
                break;
        }

        return 1;
    }

    /// <summary>
    /// Handles exceptions from client mode (Beast TCP connection).
    /// Maps socket exceptions to user-friendly connection error messages.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="host">The Beast source hostname that was being connected to.</param>
    /// <param name="port">The Beast source port that was being connected to.</param>
    /// <returns>Exit code (always 1 for error).</returns>
    /// <remarks>
    /// Distinguishes between connection timeout (SocketError.TimedOut) and connection refused
    /// to provide targeted troubleshooting guidance. Timeout typically indicates network/firewall issues,
    /// while refused means the Beast source (readsb, dump1090, aeromux daemon) is not running.
    /// </remarks>
    public static int HandleClientException(Exception ex, string host, int port)
    {
        ArgumentNullException.ThrowIfNull(ex);

        switch (ex)
        {
            // Connection timeout
            case SocketException { SocketErrorCode: SocketError.TimedOut }:
                Log.Error(ex, "Connection timeout to Beast source: {Host}:{Port}", host, port);
                Console.WriteLine("Error: Connection timeout (5 seconds)");
                Console.WriteLine($"Cannot connect to {host}:{port}");
                Console.WriteLine("Please check:");
                Console.WriteLine("  - Host address is correct");
                Console.WriteLine("  - Port is correct (default Beast port is 30005)");
                Console.WriteLine("  - Beast source is running and accessible");
                break;

            // Connection refused or other socket errors
            case SocketException:
                Log.Error(ex, "Failed to connect to Beast source: {Host}:{Port}", host, port);
                Console.WriteLine("Error: Connection refused");
                Console.WriteLine("Beast source is not running or not accessible.");
                Console.WriteLine("Examples:");
                Console.WriteLine("  - Start readsb: readsb --net");
                Console.WriteLine("  - Start aeromux: aeromux daemon");
                break;

            // Other errors
            default:
                Log.Error(ex, "Failed to connect to Beast source: {Host}:{Port}", host, port);
                Console.WriteLine("Error: Other");
                Console.WriteLine("Other error has happened during connection to the Beast source.");
                break;
        }

        return 1;
    }
}
