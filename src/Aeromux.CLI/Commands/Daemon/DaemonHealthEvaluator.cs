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

using System.Globalization;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Judges the daemon's health from a statistics snapshot: which input sources are
/// failing, and whether any are still working.
/// </summary>
/// <remarks>
/// <para>
/// An SDR device fails when it delivers no IQ samples, not when it extracts no frames: a
/// receiver with nothing in range (at night, in a quiet area) still delivers samples and
/// is working. A Beast source fails when it is not connected; connected but silent is
/// the same quiet sky. MLAT input is not judged, because mlat-client is optional and
/// reconnects on its own.
/// </para>
/// <para>
/// A source must fail for <see cref="Threshold"/> before it counts. That covers the first
/// Beast connection attempt (retries start after 5 seconds) and a USB hiccup, and a
/// monitor polling every 30 seconds still sees a failure on its next poll.
/// </para>
/// </remarks>
internal static class DaemonHealthEvaluator
{
    /// <summary>How long a source must fail before it is reported.</summary>
    internal static readonly TimeSpan Threshold = TimeSpan.FromSeconds(10);

    internal const string Ok = "OK";
    internal const string Degraded = "Degraded";
    internal const string Unhealthy = "Unhealthy";

    /// <summary>
    /// Evaluates a snapshot at the given time.
    /// </summary>
    /// <param name="snapshot">The sampler's latest snapshot.</param>
    /// <param name="nowUtc">The current time, on the sampler's clock.</param>
    /// <returns>
    /// <see cref="Ok"/> when no source fails, <see cref="Unhealthy"/> when every SDR device
    /// and Beast source fails, <see cref="Degraded"/> otherwise; with one message per
    /// failing source.
    /// </returns>
    internal static HealthVerdict Evaluate(StatisticsSnapshot snapshot, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var problems = new List<string>();

        foreach (DeviceSnapshot device in snapshot.Devices)
        {
            TimeSpan stalled = nowUtc - device.SamplesLastIncreased;
            if (stalled >= Threshold)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture,
                    $"SDR device '{device.Device.Name}' (index {device.Device.DeviceIndex}) has delivered no samples for {WholeSeconds(stalled)} s"));
            }
        }

        foreach (BeastSourceSnapshot beast in snapshot.BeastSources)
        {
            if (beast.Source.Connected)
            {
                continue;
            }

            // Since is null only while the first attempt is under way, which then counts
            // from when the source first appeared
            TimeSpan disconnected = nowUtc - (beast.Source.Since ?? beast.FirstSeen);
            if (disconnected >= Threshold)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Beast source {beast.Source.Host}:{beast.Source.Port} has been disconnected for {WholeSeconds(disconnected)} s"));
            }
        }

        int sources = snapshot.Devices.Count + snapshot.BeastSources.Count;
        string status = problems.Count == 0 ? Ok
            : problems.Count == sources ? Unhealthy
            : Degraded;

        return new HealthVerdict(status, problems);
    }

    private static long WholeSeconds(TimeSpan duration) => (long)duration.TotalSeconds;
}

/// <summary>
/// The outcome of a health evaluation.
/// </summary>
/// <param name="Status"><c>OK</c>, <c>Degraded</c> or <c>Unhealthy</c>.</param>
/// <param name="Problems">One message per failing source, empty when <c>OK</c>.</param>
internal sealed record HealthVerdict(string Status, IReadOnlyList<string> Problems);
