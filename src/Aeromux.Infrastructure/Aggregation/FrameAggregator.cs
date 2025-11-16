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
using Aeromux.Infrastructure.Streaming;

namespace Aeromux.Infrastructure.Aggregation;

/// <summary>
/// Lazy aggregation of data from multiple DeviceWorkers.
/// Used internally by DeviceStream for multi-device scenarios.
/// Phase 6: Simple pass-through with no deduplication (intentional design decision).
/// Deduplication deferred to Phase 7+ when requirements are clearer.
/// </summary>
public sealed class FrameAggregator : IDisposable
{
    private long _totalFrames;

    private readonly Channel<AggregatedData> _dataChannel = Channel.CreateUnbounded<AggregatedData>(new UnboundedChannelOptions
    {
        SingleReader = false,  // Multiple readers: DeviceStream or 3 TcpBroadcasters
        AllowSynchronousContinuations = false
    });

    /// <summary>
    /// Adds aggregated data from a device. Called by DeviceWorker callback.
    /// Phase 6: Lazy aggregation - all data passes through without deduplication checks.
    /// </summary>
    public void AddData(AggregatedData data)
    {
        Interlocked.Increment(ref _totalFrames);
        _dataChannel.Writer.TryWrite(data);
    }

    /// <summary>
    /// Gets aggregated data as async enumerable for TCP broadcaster.
    /// </summary>
    public IAsyncEnumerable<AggregatedData> GetDataAsync(CancellationToken ct = default) =>
        _dataChannel.Reader.ReadAllAsync(ct);

    public long TotalFrames => _totalFrames;

    /// <summary>
    /// Completes the channel writer to signal no more data will be added.
    /// This allows broadcasters to exit their read loops gracefully.
    /// </summary>
    public void Dispose() =>
        _dataChannel.Writer.Complete();
}
