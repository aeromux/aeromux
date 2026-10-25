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

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// One reading of every input of a <see cref="ReceiverStream"/>. Counters are cumulative
/// since their source started; each is read without stopping the pipeline, so a reading
/// can be a few frames behind on one counter compared to another.
/// </summary>
/// <param name="FramesDelivered">Frames handed on from every source (SDR, Beast and MLAT).</param>
/// <param name="Sdr">The SDR devices, or null when none runs.</param>
/// <param name="BeastSources">One entry per Beast input, in configuration order.</param>
/// <param name="Mlat">The MLAT input, or null when it is disabled.</param>
public sealed record ReceiverStatistics(
    long FramesDelivered,
    SdrStatistics? Sdr,
    IReadOnlyList<BeastSourceStatistics> BeastSources,
    MlatStatistics? Mlat);

/// <summary>
/// The SDR devices of a receiver. Confidence filtering is counted receiver-wide only,
/// because every device shares one confidence tracker.
/// </summary>
/// <param name="Unconfirmed">Frames that passed the CRC but not confidence filtering.</param>
/// <param name="Accepted">
/// Frames that passed confidence filtering, including those without an ICAO aircraft
/// address, which the filter passes on without judging them.
/// </param>
/// <param name="Devices">One entry per running device, in configuration order.</param>
public sealed record SdrStatistics(
    long Unconfirmed,
    long Accepted,
    IReadOnlyList<SdrDeviceStatistics> Devices);

/// <summary>
/// One SDR device, counted at each stage of its pipeline.
/// </summary>
/// <param name="Name">Configured device name.</param>
/// <param name="DeviceIndex">Configured RTL-SDR device index.</param>
/// <param name="StartTime">When the device started receiving samples (UTC).</param>
/// <param name="Samples">IQ samples received.</param>
/// <param name="Extracted">Frames the preamble detector extracted.</param>
/// <param name="CrcErrors">Extracted frames that failed the CRC and could not be corrected.</param>
/// <param name="Corrected">Frames that passed the CRC only after single-bit correction.</param>
/// <param name="Duplicates">Frames the duplicate filter dropped.</param>
/// <param name="Messages">Messages the parser decoded.</param>
public sealed record SdrDeviceStatistics(
    string Name,
    int DeviceIndex,
    DateTime StartTime,
    long Samples,
    long Extracted,
    long CrcErrors,
    long Corrected,
    long Duplicates,
    long Messages);

/// <summary>
/// One Beast TCP input and its connection state.
/// </summary>
/// <param name="Host">Configured host.</param>
/// <param name="Port">Configured port.</param>
/// <param name="Connected">Whether the source is connected now.</param>
/// <param name="Since">
/// When the current state began (UTC), or null while the first connection attempt is
/// still under way.
/// </param>
/// <param name="Reconnects">Successful connections after the first.</param>
/// <param name="Received">Frames decoded from the source.</param>
/// <param name="Unconfirmed">Frames dropped by confidence filtering.</param>
/// <param name="Accepted">Frames passed on, including those without an ICAO aircraft address.</param>
public sealed record BeastSourceStatistics(
    string Host,
    int Port,
    bool Connected,
    DateTime? Since,
    int Reconnects,
    long Received,
    long Unconfirmed,
    long Accepted);

/// <summary>
/// The MLAT input.
/// </summary>
/// <param name="Clients">Connected mlat-client instances.</param>
/// <param name="Received">MLAT frames received.</param>
public sealed record MlatStatistics(int Clients, long Received);
