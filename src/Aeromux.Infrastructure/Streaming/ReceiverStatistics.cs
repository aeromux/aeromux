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

namespace Aeromux.Infrastructure.Streaming;

/// <summary>
/// One reading of every input of a <see cref="ReceiverStream"/>. Counters are cumulative
/// since their source started; each is read without stopping the pipeline, so a reading
/// can be a few messages behind on one counter compared to another.
/// </summary>
/// <param name="Messages">Messages handed to the aircraft tracker from every source (SDR, Beast and MLAT).</param>
/// <param name="Sdr">The SDR devices, or null when none runs.</param>
/// <param name="BeastSources">One entry per Beast input, in configuration order.</param>
/// <param name="Mlat">The MLAT input, or null when it is disabled.</param>
public sealed record ReceiverStatistics(
    long Messages,
    SdrStatistics? Sdr,
    IReadOnlyList<BeastSourceStatistics> BeastSources,
    MlatStatistics? Mlat);

/// <summary>
/// The SDR devices of a receiver. Totals over the devices are their sums, computed where
/// they are shown.
/// </summary>
/// <param name="Devices">One entry per running device, in configuration order.</param>
public sealed record SdrStatistics(IReadOnlyList<SdrDeviceStatistics> Devices);

/// <summary>
/// One SDR device: how it fares on the radio side, and the messages it delivers.
/// </summary>
/// <param name="Name">Configured device name.</param>
/// <param name="DeviceIndex">Configured RTL-SDR device index.</param>
/// <param name="Samples">IQ samples received.</param>
/// <param name="Extracted">Frames the preamble detector extracted with a valid CRC, after a single-bit correction if needed.</param>
/// <param name="Corrected">Extracted frames that needed the single-bit correction.</param>
/// <param name="Duplicates">Frames the duplicate filter dropped.</param>
/// <param name="Messages">
/// Messages handed to the aircraft tracker. The extracted frames that are neither
/// duplicates nor messages were held back because their address belongs to no confirmed
/// aircraft yet.
/// </param>
/// <param name="MessageTypes">The messages by downlink format, type code and Comm-B register.</param>
public sealed record SdrDeviceStatistics(
    string Name,
    int DeviceIndex,
    long Samples,
    long Extracted,
    long Corrected,
    long Duplicates,
    long Messages,
    MessageTypeCounts MessageTypes);

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
/// <param name="Messages">Messages handed on from this source.</param>
/// <param name="MessageTypes">The messages by downlink format, type code and Comm-B register.</param>
public sealed record BeastSourceStatistics(
    string Host,
    int Port,
    bool Connected,
    DateTime? Since,
    int Reconnects,
    long Messages,
    MessageTypeCounts MessageTypes);

/// <summary>
/// The MLAT input.
/// </summary>
/// <param name="Clients">Connected mlat-client instances.</param>
/// <param name="Messages">MLAT messages handed on.</param>
/// <param name="MessageTypes">The messages by downlink format, type code and Comm-B register.</param>
public sealed record MlatStatistics(int Clients, long Messages, MessageTypeCounts MessageTypes);
