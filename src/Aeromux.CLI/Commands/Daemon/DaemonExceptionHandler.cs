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

using Aeromux.Infrastructure.Sdr;
using RtlSdrManager.Exceptions;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Centralized exception handler for daemon command errors.
/// Maps exception types to user-friendly error messages and appropriate exit codes.
/// Uses string-based type matching for external RTL-SDR library exceptions.
/// </summary>
public static class DaemonExceptionHandler
{
    /// <summary>
    /// Handles a daemon exception by logging it and displaying a user-friendly message.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <returns>Exit code (always 1 for error).</returns>
    public static int HandleException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        switch (ex)
        {
            // No RTL-SDR hardware present on the system (must precede the generic
            // InvalidOperationException case below, which it derives from)
            case NoRtlSdrDeviceException:
                Log.Error("No supported RTL-SDR device found on the system");
                Console.WriteLine("Error: No supported RTL-SDR device found. Please check:");
                Console.WriteLine("  1. Device is connected via USB");
                Console.WriteLine("  2. Drivers are installed (librtlsdr)");
                Console.WriteLine("  3. Run 'aeromux device' to verify detection");
                break;

            // Daemon precondition checks failed (device/port validation)
            case InvalidOperationException ioe
                when ioe.Message.Contains("device") || ioe.Message.Contains("port"):
                Log.Error("Daemon preconditions not met: {Message}", ex.Message);
                Console.WriteLine(ex.Message);
                break;

            // RTL-SDR device already in use by another process
            case RtlSdrLibraryExecutionException:
                Log.Error("RTL-SDR device already in use");
                Console.WriteLine("Error: Cannot open RTL-SDR device (already in use)");
                Console.WriteLine("This usually means another instance is running.");
                Console.WriteLine("Try:");
                Console.WriteLine("  1. Connect to daemon: aeromux live --beast-source localhost:30005");
                Console.WriteLine("  2. Stop daemon: aeromux daemon stop");
                break;

            // RTL-SDR device not found by index (string-based matching for external library exceptions)
            case Exception when ex.GetType().Name.Contains("RtlSdr") && ex.Message.Contains("does not exist"):
                Log.Error("RTL-SDR device not found with the given index");
                Console.WriteLine("Error: RTL-SDR device not found with the given index. Please check:");
                Console.WriteLine("  1. Device is connected via USB");
                Console.WriteLine("  2. Drivers are installed (librtlsdr)");
                Console.WriteLine("  3. Device index is correct in configuration");
                Console.WriteLine("  4. Run 'aeromux device' to verify detection");
                break;

            // Other RTL-SDR errors (string-based matching for external library exceptions)
            case Exception when ex.GetType().Name.Contains("RtlSdr"):
                Log.Error("RTL-SDR error: {Message}", ex.Message);
                Console.WriteLine($"RTL-SDR Error: {ex.Message}");
                break;

            // Unexpected errors
            default:
                Log.Error(ex, "Failed to start daemon");
                Console.WriteLine($"Unexpected error: {ex.Message}");
                break;
        }

        return 1;
    }
}
