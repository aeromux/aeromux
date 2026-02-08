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

using Aeromux.Infrastructure.Streaming;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Logs session start/end separators and summary statistics for the daemon session.
/// Provides clear visual markers in log files for easy identification of daemon instances.
/// </summary>
public static class DaemonSessionSummaryReporter
{
    /// <summary>
    /// Logs the session start separator with timestamp.
    /// </summary>
    public static void LogSessionStart()
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Aeromux Daemon Starting");
        Log.Information("Session: {SessionStart:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Logs aggregated session statistics from all devices.
    /// </summary>
    /// <param name="sessionStart">UTC timestamp when the session started.</param>
    /// <param name="stats">Stream statistics from all devices, or null if unavailable.</param>
    public static void LogSessionSummary(DateTime sessionStart, StreamStatistics? stats)
    {
        if (stats != null)
        {
            TimeSpan sessionDuration = DateTime.UtcNow - sessionStart;
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("Aeromux Session Summary");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("Session duration: {Duration}", sessionDuration.ToString(@"hh\:mm\:ss"));
            Log.Information("Total frames: {TotalFrames:N0}", stats.TotalFrames);
            Log.Information("Valid frames: {ValidFrames:N0}", stats.ValidFrames);
            Log.Information("Corrected frames: {CorrectedFrames:N0}", stats.CorrectedFrames);
            Log.Information("Messages parsed: {ParsedMessages:N0}", stats.ParsedMessages);
            Log.Information("MLAT frames: {MlatFrames:N0}", stats.MlatFrames);
            Log.Information("═══════════════════════════════════════════════════════════════");
        }
    }

    /// <summary>
    /// Logs the session end separator with timestamp.
    /// </summary>
    public static void LogSessionEnd()
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Aeromux Daemon Stopped");
        Log.Information("Session End: {SessionEnd:yyyy-MM-dd HH:mm:ss zzz}", DateTime.Now);
        Log.Information("═══════════════════════════════════════════════════════════════");
    }
}
