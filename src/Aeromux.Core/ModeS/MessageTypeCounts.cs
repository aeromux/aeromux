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

using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.ModeS;

/// <summary>
/// The messages a <see cref="MessageParser"/> has parsed, by kind. Only codes seen at
/// least once are present.
/// </summary>
/// <param name="DownlinkFormats">Messages by downlink format (DF 0 to 24); the counts add up to <see cref="MessageParser.MessagesParsed"/>.</param>
/// <param name="TypeCodes">
/// ADS-B messages by type code (TC 0 to 31): DF 17, and DF 18 frames that carry ADS-B
/// with an ICAO address.
/// </param>
/// <param name="BdsCodes">
/// Comm-B replies (DF 20 and 21) by the register inferred from their content, with
/// <see cref="BdsCode.Empty"/> for an all-zero reply and <see cref="BdsCode.Unknown"/>
/// when no register matched.
/// </param>
public sealed record MessageTypeCounts(
    IReadOnlyDictionary<int, long> DownlinkFormats,
    IReadOnlyDictionary<int, long> TypeCodes,
    IReadOnlyDictionary<BdsCode, long> BdsCodes)
{
    /// <summary>No messages of any kind.</summary>
    public static MessageTypeCounts Empty { get; } = new(
        new Dictionary<int, long>(),
        new Dictionary<int, long>(),
        new Dictionary<BdsCode, long>());
}
