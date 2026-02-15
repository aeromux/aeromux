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

namespace Aeromux.Core.ModeS;

/// <summary>
/// Constants for Mode S frame lengths.
/// Mode S messages come in two standard lengths: short (56 bits) and long (112 bits).
/// </summary>
/// <remarks>
/// Frame structure:
/// - Short frame: 56 bits = 7 bytes (DF 0, 4, 5, 11, 16, 24)
/// - Long frame: 112 bits = 14 bytes (DF 17, 18, 19, 20, 21)
///
/// At 2.4 MSPS sampling rate (standard):
/// - Mode S data rate: 1 Mbit/s
/// - Samples per bit: 2.4 samples (requiring phase tracking for the bit detection)
/// - Short frame: ~134 samples
/// - Long frame: ~269 samples
/// </remarks>
public static class ModeSFrameLength
{
    /// <summary>
    /// Short frame length in bits.
    /// Used by: DF 0, 4, 5, 11, 16, 24
    /// </summary>
    public const int Short = 56;

    /// <summary>
    /// Long frame length in bits.
    /// Used by: DF 17, 18, 19, 20, 21
    /// </summary>
    public const int Long = 112;

    /// <summary>
    /// Short frame length in bytes.
    /// </summary>
    public const int ShortBytes = Short / 8;  // 7 bytes

    /// <summary>
    /// Long frame length in bytes.
    /// </summary>
    public const int LongBytes = Long / 8;  // 14 bytes
}
