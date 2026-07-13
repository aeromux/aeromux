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

using Aeromux.Infrastructure.Database;

namespace Aeromux.CLI.Commands.Database;

/// <summary>
/// Renders <see cref="DownloadProgress"/> updates to the console for the interactive
/// <c>database update</c> command. On a TTY it rewrites a single in-place progress line;
/// otherwise it prints at 25% intervals. Reports are handled synchronously on the calling
/// thread so output stays ordered with surrounding status messages.
/// </summary>
public sealed class ConsoleDownloadProgressReporter : IProgress<DownloadProgress>
{
    private readonly bool _isTty = !Console.IsOutputRedirected;
    private int _lastReportedPercent = -1;
    private bool _started;

    /// <inheritdoc />
    public void Report(DownloadProgress value)
    {
        // Print an empty line so the cursor starts below the progress area (TTY only)
        if (!_started)
        {
            _started = true;
            if (_isTty)
            {
                Console.WriteLine();
            }
        }

        double percentage = value.TotalBytes > 0 ? (double)value.BytesRead / value.TotalBytes * 100 : 0;
        string progress = $"  {FormatBytes(value.BytesRead)} / {FormatBytes(value.TotalBytes)} ({percentage:F0}%)";

        if (_isTty)
        {
            // Move cursor up, clear the line, write progress, move cursor back down
            Console.Write($"\x1b[A\x1b[2K{progress}\n");
        }
        else
        {
            // Non-TTY: print progress at 25% intervals to avoid flooding
            int percentBucket = (int)(percentage / 25) * 25;
            if (percentBucket > _lastReportedPercent)
            {
                _lastReportedPercent = percentBucket;
                Console.WriteLine(progress);
            }
        }
    }

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., <c>142.8 MB</c>).
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
