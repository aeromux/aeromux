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

using System.Threading.Channels;
using Aeromux.Core.ModeS;

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// Abstraction for streaming processed Mode S frames from any source.
/// Implementations: DeviceStream (local RTL-SDR devices), DaemonStream (TCP client), FileStream (future replay).
///
/// LIFECYCLE PATTERN:
/// 1. StartAsync() - Initializes resources and begins internal data production
/// 2. Subscribe() - Creates independent channel for each consumer (call multiple times for multiple consumers)
/// 3. DisposeAsync() - Stops production and cleans up resources
///
/// BROADCAST PATTERN:
/// Multiple subscribers can receive the same data stream concurrently.
/// Each subscriber gets their own channel and reads independently.
/// Internal implementation handles fan-out from single source to multiple channels.
/// This enables multiple output formats (Beast/JSON/SBS) from single device stream.
///
/// DATA FORMAT:
/// All frames are delivered as ProcessedFrame records containing both raw and parsed representations.
/// See ProcessedFrame.cs for details on the parse-once architecture.
/// </summary>
public interface IFrameStream : IAsyncDisposable
{
    /// <summary>
    /// Starts the stream and initializes resources.
    /// MUST be called once before any Subscribe() calls.
    /// Idempotent: Safe to call multiple times, only initializes once.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to the data stream and returns a dedicated channel for this subscriber.
    /// Multiple subscribers can call this to receive the same data stream concurrently.
    /// Each subscriber receives ALL data independently via their own channel.
    /// StartAsync() must be called first, otherwise throws InvalidOperationException.
    /// </summary>
    ChannelReader<ProcessedFrame> Subscribe();

    /// <summary>
    /// Unsubscribes a channel from the data stream.
    /// Called by consumers when they no longer need data (typically in dispose).
    /// Safe to call multiple times with same reader (idempotent).
    /// </summary>
    void Unsubscribe(ChannelReader<ProcessedFrame> reader);

    /// <summary>
    /// Gets current statistics snapshot (if available).
    /// Returns null if stream doesn't provide statistics (e.g., remote streams).
    /// Statistics are aggregated from all underlying devices.
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
