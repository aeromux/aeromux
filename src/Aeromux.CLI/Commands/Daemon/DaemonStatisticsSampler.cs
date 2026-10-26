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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Infrastructure.Streaming;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Reads the receiver's statistics once a second and keeps the last minute of readings,
/// so the REST API can report rates over a sliding 60-second window and tell how long
/// each source has been failing.
/// </summary>
/// <remarks>
/// A lifetime average hides a receiver that stopped five minutes ago behind hours of
/// good traffic, and a monitoring tool polling every 30 to 60 seconds wants the rate
/// over the period it polled across. Sixty seconds rather than ten, so the window does
/// not alias against such a poll.
/// </remarks>
public sealed class DaemonStatisticsSampler : IAsyncDisposable
{
    /// <summary>Sixty seconds of one-second readings, plus the current one.</summary>
    private const int WindowReadings = 61;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Func<ReceiverStatistics?> _source;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private readonly Queue<Reading> _readings = new(WindowReadings);

    // Per source, kept for the whole session rather than the window: a device can stall
    // for longer than a minute, and a source's first appearance matters for as long as
    // it has never connected.
    private readonly Dictionary<string, (long Samples, DateTime IncreasedAt)> _deviceSamples = [];
    private readonly Dictionary<string, DateTime> _beastFirstSeen = [];

    private CancellationTokenSource? _cts;
    private Task? _loop;

    private readonly record struct Reading(DateTime Time, ReceiverStatistics Statistics);

    /// <summary>
    /// Creates a sampler. It reads nothing until <see cref="Start"/>.
    /// </summary>
    /// <param name="source">Returns the current reading, or null while the receiver is not running.</param>
    /// <param name="timeProvider">Clock and timer source; the system clock when null.</param>
    public DaemonStatisticsSampler(Func<ReceiverStatistics?> source, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the current time on the sampler's clock (UTC).</summary>
    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Takes a first reading at once, then one a second until disposed or canceled.
    /// </summary>
    /// <param name="cancellationToken">Stops sampling.</param>
    public void Start(CancellationToken cancellationToken)
    {
        if (_loop != null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token = _cts.Token;
        _loop = Task.Run(() => RunAsync(token), CancellationToken.None);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(Interval, _timeProvider);
            do
            {
                try
                {
                    Sample();
                }
                catch (Exception ex)
                {
                    // One bad reading must not end sampling for the rest of the session
                    Log.Warning(ex, "Statistics sampling failed");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <summary>
    /// Takes one reading. Called by the timer, and directly by tests.
    /// </summary>
    internal void Sample()
    {
        ReceiverStatistics? statistics = _source();
        if (statistics == null)
        {
            return;
        }

        DateTime now = UtcNow;
        lock (_lock)
        {
            if (_readings.Count == WindowReadings)
            {
                _readings.Dequeue();
            }
            _readings.Enqueue(new Reading(now, statistics));

            foreach (SdrDeviceStatistics device in statistics.Sdr?.Devices ?? [])
            {
                if (!_deviceSamples.TryGetValue(device.Name, out (long Samples, DateTime IncreasedAt) last)
                    || device.Samples > last.Samples)
                {
                    _deviceSamples[device.Name] = (device.Samples, now);
                }
            }

            foreach (string key in BeastKeys(statistics.BeastSources))
            {
                _beastFirstSeen.TryAdd(key, now);
            }
        }
    }

    /// <summary>
    /// Gets the latest reading with its rates over the window. Before the first reading,
    /// returns <see cref="StatisticsSnapshot.Empty"/>.
    /// </summary>
    public StatisticsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            if (_readings.Count == 0)
            {
                return StatisticsSnapshot.Empty;
            }

            Reading latest = _readings.Last();
            ReceiverStatistics current = latest.Statistics;

            // A counter's rate from the oldest reading in the window that has it: a section is
            // missing from earlier readings only when its source appeared later.
            double WindowRate(Func<ReceiverStatistics, long?> counter)
            {
                foreach (Reading reading in _readings)
                {
                    if (counter(reading.Statistics) is { } start)
                    {
                        return Rate(start, counter(current) ?? start, latest.Time - reading.Time);
                    }
                }
                return 0;
            }

            // Each code's rate, from the oldest reading that has the source. A code missing
            // from that reading counts as 0 there (GetValueOrDefault), so a code first seen
            // inside the window counts from zero rather than from its first reading.
            MessageTypeRates TypeRates(Func<ReceiverStatistics, MessageTypeCounts?> types)
            {
                if (types(current) is not { } now)
                {
                    return MessageTypeRates.Empty;
                }

                return new MessageTypeRates(
                    now.DownlinkFormats.Keys.ToDictionary(
                        code => code, code => WindowRate(s => types(s)?.DownlinkFormats.GetValueOrDefault(code))),
                    now.TypeCodes.Keys.ToDictionary(
                        code => code, code => WindowRate(s => types(s)?.TypeCodes.GetValueOrDefault(code))),
                    now.BdsCodes.Keys.ToDictionary(
                        code => code, code => WindowRate(s => types(s)?.BdsCodes.GetValueOrDefault(code))));
            }

            IReadOnlyList<DeviceSnapshot> devices =
            [
                .. (current.Sdr?.Devices ?? []).Select(device => new DeviceSnapshot(
                    device,
                    WindowRate(s => FindDevice(s, device.Name)?.Samples),
                    WindowRate(s => FindDevice(s, device.Name)?.Extracted),
                    WindowRate(s => FindDevice(s, device.Name)?.Corrected),
                    WindowRate(s => FindDevice(s, device.Name)?.Duplicates),
                    WindowRate(s => FindDevice(s, device.Name)?.Messages),
                    _deviceSamples[device.Name].IncreasedAt,
                    TypeRates(s => FindDevice(s, device.Name)?.MessageTypes)))
            ];

            // Every reading's Beast keys once, rather than again for each counter and code
            // that looks a source up in it
            var beastKeysByReading = new Dictionary<ReceiverStatistics, string[]>(ReferenceEqualityComparer.Instance);
            foreach (Reading reading in _readings)
            {
                beastKeysByReading[reading.Statistics] = BeastKeys(reading.Statistics.BeastSources);
            }

            BeastSourceStatistics? FindBeast(ReceiverStatistics statistics, string key)
            {
                int index = Array.IndexOf(beastKeysByReading[statistics], key);
                return index < 0 ? null : statistics.BeastSources[index];
            }

            string[] beastKeys = beastKeysByReading[current];
            IReadOnlyList<BeastSourceSnapshot> beastSources =
            [
                .. current.BeastSources.Select((source, i) => new BeastSourceSnapshot(
                    source,
                    WindowRate(s => FindBeast(s, beastKeys[i])?.Messages),
                    _beastFirstSeen[beastKeys[i]],
                    TypeRates(s => FindBeast(s, beastKeys[i])?.MessageTypes)))
            ];

            MlatSnapshot? mlat = current.Mlat is { } currentMlat
                ? new MlatSnapshot(
                    currentMlat,
                    WindowRate(s => s.Mlat?.Messages),
                    TypeRates(s => s.Mlat?.MessageTypes))
                : null;

            return new StatisticsSnapshot(
                current,
                latest.Time,
                WindowRate(s => s.Messages),
                WindowRate(s => s.Sdr?.Devices.Sum(d => d.Extracted)),
                WindowRate(s => s.Sdr?.Devices.Sum(d => d.Corrected)),
                WindowRate(s => s.Sdr?.Devices.Sum(d => d.Duplicates)),
                WindowRate(s => s.Sdr?.Devices.Sum(d => d.Messages)),
                devices,
                beastSources,
                mlat);
        }
    }

    private static double Rate(long start, long end, TimeSpan elapsed) =>
        elapsed > TimeSpan.Zero ? (end - start) / elapsed.TotalSeconds : 0;

    private static SdrDeviceStatistics? FindDevice(ReceiverStatistics statistics, string name) =>
        statistics.Sdr?.Devices.FirstOrDefault(d => d.Name == name);

    /// <summary>
    /// Identifies Beast sources by host and port. The same source listed twice is unusual
    /// but allowed, so repeats get their occurrence number to stay distinct.
    /// </summary>
    private static string[] BeastKeys(IReadOnlyList<BeastSourceStatistics> sources)
    {
        var seen = new Dictionary<string, int>();
        string[] keys = new string[sources.Count];
        for (int i = 0; i < sources.Count; i++)
        {
            string endpoint = $"{sources[i].Host}:{sources[i].Port}";
            int occurrence = seen.GetValueOrDefault(endpoint);
            seen[endpoint] = occurrence + 1;
            keys[i] = occurrence == 0 ? endpoint : $"{endpoint}#{occurrence}";
        }
        return keys;
    }

    /// <summary>
    /// Stops sampling and waits for the loop to finish.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_cts == null)
        {
            return;
        }

        await _cts.CancelAsync();
        if (_loop != null)
        {
            await _loop;
        }
        _cts.Dispose();
        _cts = null;
    }
}

/// <summary>
/// The latest statistics reading with its rates over the sampling window, and what
/// health evaluation needs to tell how long a source has been failing.
/// </summary>
/// <param name="Statistics">The latest reading, or null before the first.</param>
/// <param name="TakenAt">When the latest reading was taken (UTC), or null before the first.</param>
/// <param name="MessagesPerSecond">Messages handed to the tracker from every source, per second.</param>
/// <param name="SdrExtractedPerSecond">Frames extracted by all SDR devices together, per second.</param>
/// <param name="SdrCorrectedPerSecond">Extracted frames that needed a CRC correction, all SDR devices together, per second.</param>
/// <param name="SdrDuplicatesPerSecond">Duplicate frames dropped by all SDR devices together, per second.</param>
/// <param name="SdrMessagesPerSecond">Messages from all SDR devices together, per second.</param>
/// <param name="Devices">One entry per SDR device, in the order of the reading.</param>
/// <param name="BeastSources">One entry per Beast source, in the order of the reading.</param>
/// <param name="Mlat">The MLAT input, or null when it is disabled or before the first reading.</param>
public sealed record StatisticsSnapshot(
    ReceiverStatistics? Statistics,
    DateTime? TakenAt,
    double MessagesPerSecond,
    double SdrExtractedPerSecond,
    double SdrCorrectedPerSecond,
    double SdrDuplicatesPerSecond,
    double SdrMessagesPerSecond,
    IReadOnlyList<DeviceSnapshot> Devices,
    IReadOnlyList<BeastSourceSnapshot> BeastSources,
    MlatSnapshot? Mlat)
{
    /// <summary>The snapshot before the first reading: no sources and no rates.</summary>
    public static StatisticsSnapshot Empty { get; } = new(null, null, 0, 0, 0, 0, 0, [], [], null);
}

/// <summary>
/// One SDR device in a <see cref="StatisticsSnapshot"/>.
/// </summary>
/// <param name="Device">The device's counters from the latest reading.</param>
/// <param name="SamplesPerSecond">IQ samples received per second over the window.</param>
/// <param name="ExtractedPerSecond">Frames extracted per second over the window.</param>
/// <param name="CorrectedPerSecond">Extracted frames that needed a CRC correction, per second over the window.</param>
/// <param name="DuplicatesPerSecond">Duplicate frames dropped per second over the window.</param>
/// <param name="MessagesPerSecond">Messages handed to the tracker per second over the window.</param>
/// <param name="SamplesLastIncreased">
/// When the sample count last went up (UTC), or when the device first appeared if it never has.
/// </param>
/// <param name="MessageTypes">Messages per second over the window, by kind and code.</param>
public sealed record DeviceSnapshot(
    SdrDeviceStatistics Device,
    double SamplesPerSecond,
    double ExtractedPerSecond,
    double CorrectedPerSecond,
    double DuplicatesPerSecond,
    double MessagesPerSecond,
    DateTime SamplesLastIncreased,
    MessageTypeRates MessageTypes);

/// <summary>
/// One Beast source in a <see cref="StatisticsSnapshot"/>.
/// </summary>
/// <param name="Source">The source's state and counters from the latest reading.</param>
/// <param name="MessagesPerSecond">Messages handed on per second over the window.</param>
/// <param name="FirstSeen">When a reading first contained the source (UTC).</param>
/// <param name="MessageTypes">Messages per second over the window, by kind and code.</param>
public sealed record BeastSourceSnapshot(
    BeastSourceStatistics Source,
    double MessagesPerSecond,
    DateTime FirstSeen,
    MessageTypeRates MessageTypes);

/// <summary>
/// The MLAT input in a <see cref="StatisticsSnapshot"/>.
/// </summary>
/// <param name="Mlat">The input's counters from the latest reading.</param>
/// <param name="MessagesPerSecond">MLAT messages per second over the window.</param>
/// <param name="MessageTypes">Messages per second over the window, by kind and code.</param>
public sealed record MlatSnapshot(
    MlatStatistics Mlat,
    double MessagesPerSecond,
    MessageTypeRates MessageTypes);

/// <summary>
/// Messages per second over the sampling window, by downlink format, type code and Comm-B
/// register, for the codes a source has seen.
/// </summary>
/// <param name="DownlinkFormats">Per second by downlink format.</param>
/// <param name="TypeCodes">Per second by ADS-B type code.</param>
/// <param name="BdsCodes">Per second by Comm-B register.</param>
public sealed record MessageTypeRates(
    IReadOnlyDictionary<int, double> DownlinkFormats,
    IReadOnlyDictionary<int, double> TypeCodes,
    IReadOnlyDictionary<BdsCode, double> BdsCodes)
{
    /// <summary>No codes.</summary>
    public static MessageTypeRates Empty { get; } = new(
        new Dictionary<int, double>(),
        new Dictionary<int, double>(),
        new Dictionary<BdsCode, double>());
}
