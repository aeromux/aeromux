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

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Aggregation;
using Aeromux.Infrastructure.Sdr;

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// Streams aggregated data from local RTL-SDR device(s).
/// Handles both single-device and multi-device configurations automatically.
/// Single device: Direct streaming without aggregator overhead.
/// Multiple devices: Uses FrameAggregator for lazy aggregation.
/// Used by 'aeromux live' standalone mode and 'aeromux daemon'.
/// </summary>
public sealed class DeviceStream : IFrameStream
{
    private readonly List<DeviceConfig> _deviceConfigs;
    private readonly TrackingConfig _trackingConfig;
    private readonly ReceiverConfig? _receiverConfig;
    private FrameAggregator? _aggregator;  // Only created for multi-device
    private Channel<AggregatedData>? _singleDeviceChannel;  // Only created for single device
    private readonly List<DeviceWorker> _workers = [];
    private bool _started;

    public DeviceStream(
        List<DeviceConfig> deviceConfigs,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig)
    {
        if (deviceConfigs == null || deviceConfigs.Count == 0)
        {
            throw new ArgumentException("At least one device configuration is required", nameof(deviceConfigs));
        }

        _deviceConfigs = deviceConfigs;
        _trackingConfig = trackingConfig;
        _receiverConfig = receiverConfig;
    }

    // Convenience constructor for single device
    public DeviceStream(
        DeviceConfig deviceConfig,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig)
        : this([deviceConfig], trackingConfig, receiverConfig)
    {
    }

    public async IAsyncEnumerable<AggregatedData> GetDataAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            // LAZY INITIALIZATION: Determine strategy based on device count
            if (_deviceConfigs.Count == 1)
            {
                // Single device: Use channel directly (no aggregator overhead)
                _singleDeviceChannel = Channel.CreateUnbounded<AggregatedData>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true
                });

                var worker = new DeviceWorker(
                    _deviceConfigs[0],
                    _trackingConfig,
                    _receiverConfig,
                    onDataParsed: (frame, message) =>
                        _singleDeviceChannel.Writer.TryWrite(new AggregatedData(frame, message, DateTime.UtcNow)));

                worker.OpenDevice();
                worker.StartReceiving(cancellationToken);
                _workers.Add(worker);
            }
            else
            {
                // Multiple devices: Use FrameAggregator
                _aggregator = new FrameAggregator();

                foreach (DeviceConfig deviceConfig in _deviceConfigs)
                {
                    var worker = new DeviceWorker(
                        deviceConfig,
                        _trackingConfig,
                        _receiverConfig,
                        onDataParsed: (frame, message) =>
                            _aggregator.AddData(new AggregatedData(frame, message, DateTime.UtcNow)));

                    worker.OpenDevice();
                    worker.StartReceiving(cancellationToken);
                    _workers.Add(worker);
                }
            }

            _started = true;
        }

        // Stream from appropriate source
        if (_singleDeviceChannel != null)
        {
            await foreach (AggregatedData data in _singleDeviceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return data;
            }
        }
        else if (_aggregator != null)
        {
            await foreach (AggregatedData data in _aggregator.GetDataAsync(cancellationToken))
            {
                yield return data;
            }
        }
    }

    public StreamStatistics? GetStatistics()
    {
        if (_workers.Count == 0)
        {
            return null;
        }

        // Aggregate statistics from all workers
        return new StreamStatistics(
            _workers.Sum(w => w.PreambleDetector.FramesExtracted),
            _workers.Sum(w => w.ConfidenceTracker.ConfidentFrames),
            _workers.Sum(w => w.ValidatedFrameFactory.FramesCorrected),
            _workers.Sum(w => w.MessageParser.MessagesParsed),
            DateTime.UtcNow - _workers.Min(w => w.StartTime));
    }

    public async ValueTask DisposeAsync()
    {
        // Complete channels first to stop any pending writes
        _singleDeviceChannel?.Writer.Complete();
        _aggregator?.Dispose();

        // Then dispose workers
        foreach (DeviceWorker worker in _workers)
        {
            worker.Dispose();
        }

        await Task.CompletedTask;
    }
}
