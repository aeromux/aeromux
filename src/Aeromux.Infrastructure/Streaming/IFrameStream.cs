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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// Aggregated data containing both raw frame and parsed message.
/// Enables multiple output formats: Beast (raw), JSON/SBS (parsed), Rich TUI (parsed).
/// Parse once in daemon, use everywhere.
/// </summary>
public record AggregatedData(
    ValidatedFrame Frame,           // Raw frame for Beast format broadcasting
    ModeSMessage? ParsedMessage,    // Parsed message for JSON/SBS/TUI (null if unparseable)
    DateTime Timestamp
);

/// <summary>
/// Abstraction for streaming aggregated Mode S data from any source.
/// Implementations: DeviceStream (local device), DaemonStream (TCP), FileStream (future replay).
/// </summary>
public interface IFrameStream : IAsyncDisposable
{
    /// <summary>
    /// Streams aggregated data containing both raw frame and parsed message.
    /// Null messages are included (for Beast broadcasting), parseable messages have ParsedMessage set.
    /// </summary>
    IAsyncEnumerable<AggregatedData> GetDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current statistics (if available). Returns null if stream doesn't provide statistics.
    /// </summary>
    StreamStatistics? GetStatistics();
}

/// <summary>
/// Statistics snapshot from a frame stream.
/// </summary>
public record StreamStatistics(
    long TotalFrames,
    long ValidFrames,
    long CorrectedFrames,
    long ParsedMessages,
    TimeSpan Uptime);
