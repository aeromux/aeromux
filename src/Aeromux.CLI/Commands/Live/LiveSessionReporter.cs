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

using Aeromux.Infrastructure.Streaming;
using Serilog;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Logs session start/end separators and summary statistics for the live command session.
/// Provides clear visual markers in log files for easy identification of live instances.
/// </summary>
public static class LiveSessionReporter
{
    /// <summary>
    /// Logs the session start separator with mode info and timestamp.
    /// </summary>
    /// <param name="isClientMode">True if running in client mode, false for standalone mode.</param>
    public static void LogSessionStart(bool isClientMode)
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Aeromux Live Command Starting");
        Log.Information("Mode: {Mode}", isClientMode ? "Client" : "Standalone");
        Log.Information("Session: {SessionStart:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Logs the session end separator with duration and timestamp.
    /// </summary>
    /// <param name="sessionStart">UTC timestamp when the session started.</param>
    public static void LogSessionEnd(DateTime sessionStart)
    {
        TimeSpan sessionDuration = DateTime.UtcNow - sessionStart;
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Aeromux Live Command Stopped");
        Log.Information("Session duration: {Duration}", sessionDuration.ToString(@"hh\:mm\:ss"));
        Log.Information("Session End: {SessionEnd:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Logs aggregated session statistics from the live display session.
    /// </summary>
    /// <param name="sessionStart">UTC timestamp when the session started.</param>
    /// <param name="isClientMode">True if running in client mode, false for standalone mode.</param>
    /// <param name="stats">Stream statistics, or null if unavailable.</param>
    /// <param name="trackedAircraftCount">Number of aircraft tracked during the session.</param>
    /// <remarks>
    /// Called from LiveTuiDisplay's finally block to ensure summary is logged even on abnormal exit.
    /// Statistics are only available in standalone mode (ReceiverStream); client mode (BeastStream)
    /// does not expose frame-level statistics since decoding happens on the remote source.
    /// </remarks>
    public static void LogSessionSummary(
        DateTime sessionStart,
        bool isClientMode,
        StreamStatistics? stats,
        int trackedAircraftCount)
    {
        TimeSpan sessionDuration = DateTime.UtcNow - sessionStart;

        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Live Command Session Summary");
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Session duration: {Duration}", sessionDuration.ToString(@"hh\:mm\:ss"));
        Log.Information("Mode: {Mode}", isClientMode ? "Client" : "Standalone");

        if (stats != null)
        {
            Log.Information("Total frames: {TotalFrames:N0}", stats.TotalFrames);
            Log.Information("Valid frames: {ValidFrames:N0}", stats.ValidFrames);
            Log.Information("Corrected frames: {CorrectedFrames:N0}", stats.CorrectedFrames);
            Log.Information("Messages parsed: {ParsedMessages:N0}", stats.ParsedMessages);
        }
        else
        {
            Log.Information("Statistics not available (client mode)");
        }

        Log.Information("Total aircraft tracked: {AircraftCount}", trackedAircraftCount);
        Log.Information("═══════════════════════════════════════════════════════════════");
    }
}
