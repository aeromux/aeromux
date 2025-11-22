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

namespace Aeromux.Infrastructure.Aggregation;

/// <summary>
/// Aggregates processed frames from multiple DeviceWorkers into a single stream.
/// Used internally by DeviceStream for multi-device scenarios only.
/// Phase 6: Simple pass-through with no deduplication (intentional design decision).
/// Deduplication deferred to Phase 7+ when requirements are clearer.
/// </summary>
/// <remarks>
/// AGGREGATION STRATEGY:
/// This is the component where "aggregation" actually happens - combining frames from
/// multiple RTL-SDR devices into a single output stream. Each device runs independently:
/// Device 1 → ProcessedFrame → ┐
/// Device 2 → ProcessedFrame → ├→ FrameAggregator → Single stream
/// Device N → ProcessedFrame → ┘
///
/// Currently implements lazy aggregation (pass-through):
/// - All frames pass through without deduplication
/// - Multi-device setups will see duplicate frames from overlapping coverage
/// - Frame order is non-deterministic (whichever device processes first)
///
/// This is intentional for Phase 6 - full deduplication requires:
/// - ICAO tracking across time windows
/// - Signal strength comparison for duplicate detection
/// - Timestamp-based frame merging logic
/// These requirements will be clearer after real-world multi-device usage.
/// </remarks>
public sealed class FrameAggregator : IDisposable
{
    private long _totalFrames;

    private readonly Channel<ProcessedFrame> _dataChannel = Channel.CreateUnbounded<ProcessedFrame>(new UnboundedChannelOptions
    {
        SingleReader = false,  // Multiple readers: DeviceStream broadcaster task
        AllowSynchronousContinuations = false
    });

    /// <summary>
    /// Adds a processed frame from a device. Called by DeviceWorker callback.
    /// Phase 6: Lazy aggregation - all frames pass through without deduplication checks.
    /// </summary>
    public void AddData(ProcessedFrame data)
    {
        Interlocked.Increment(ref _totalFrames);
        _dataChannel.Writer.TryWrite(data);
    }

    /// <summary>
    /// Gets aggregated frames as async enumerable.
    /// Used by DeviceStream's internal broadcaster task for multi-device scenarios.
    /// </summary>
    public IAsyncEnumerable<ProcessedFrame> GetDataAsync(CancellationToken ct = default) =>
        _dataChannel.Reader.ReadAllAsync(ct);

    public long TotalFrames => _totalFrames;

    /// <summary>
    /// Completes the channel writer to signal no more data will be added.
    /// This allows broadcasters to exit their read loops gracefully.
    /// </summary>
    public void Dispose() =>
        _dataChannel.Writer.Complete();
}
