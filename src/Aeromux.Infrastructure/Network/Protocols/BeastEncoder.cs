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

using Aeromux.Core.ModeS;

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Encodes ValidatedFrame to Beast binary format (dump1090/readsb compatible).
/// Beast format transmits raw Mode S frame bytes with timestamp and signal level.
/// </summary>
/// <remarks>
/// Beast Binary Format Specification:
/// - Byte 0: ESC (0x1A) - frame start marker
/// - Byte 1: Message type ('2' = short 7-byte frame, '3' = long 14-byte frame)
/// - Bytes 2-7: 48-bit timestamp (12 MHz counter, big-endian)
/// - Byte 8: Signal level (0-255 relative magnitude)
/// - Bytes 9+: Raw Mode S frame data (7 or 14 bytes)
///
/// Escape Handling:
/// Any 0x1A byte in the frame data must be doubled (0x1A 0x1A) to prevent
/// confusion with frame start markers. This is transparent to the receiver
/// which undoes the escaping during parsing.
/// </remarks>
public static class BeastEncoder
{
    /// <summary>
    /// Escape byte used for frame delimiting in Beast protocol.
    /// </summary>
    private const byte ESC = 0x1A;

    /// <summary>
    /// Encodes a ValidatedFrame to Beast binary format.
    /// </summary>
    /// <param name="frame">Validated Mode S frame to encode</param>
    /// <returns>Beast-encoded byte array ready for TCP transmission</returns>
    /// <remarks>
    /// Output format: [ESC][Type][Timestamp:6][Signal:1][Data:7/14]
    /// Actual size may be larger than minimal due to escape byte doubling in data.
    /// </remarks>
    public static byte[] Encode(ValidatedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // Determine frame length (7 bytes for short, 14 bytes for long)
        bool isLong = frame.Data.Length == 14;

        // Allocate worst-case buffer size: header + data with all bytes escaped
        // Structure: 1 (ESC) + 1 (type) + 6 (timestamp) + 1 (signal) + data (doubled for worst case)
        int maxLength = 1 + 1 + 6 + 1 + (frame.Data.Length * 2);
        byte[] output = new byte[maxLength];

        int pos = 0;

        // Write frame start marker (ESC byte)
        output[pos++] = ESC;

        // Write message type indicator ('2' for short 56-bit frames, '3' for long 112-bit frames)
        output[pos++] = isLong ? (byte)'3' : (byte)'2';

        // Write 48-bit timestamp as 12 MHz counter (big-endian encoding)
        // Convert .NET DateTime ticks (100ns resolution) to 12 MHz counter
        // Calculation: ticks * 12 MHz / 10 MHz = ticks * 1.2
        ulong timestamp12MHz = (ulong)(frame.Timestamp.Ticks * 12.0 / TimeSpan.TicksPerMicrosecond);
        for (int i = 0; i < 6; i++)
        {
            // Extract bytes from MSB to LSB (big-endian)
            // Shift: 40, 32, 24, 16, 8, 0 bits
            output[pos++] = (byte)(timestamp12MHz >> (40 - (i * 8)));
        }

        // Write signal strength indicator (0-255 relative magnitude from demodulator)
        output[pos++] = frame.SignalStrength;

        // Write raw Mode S frame data with escape handling
        // If any data byte equals ESC (0x1A), it must be doubled for framing transparency
        foreach (byte b in frame.Data)
        {
            output[pos++] = b;

            // Escape handling: duplicate any ESC bytes found in data
            if (b == ESC)
            {
                output[pos++] = ESC;
            }
        }

        // Return slice of actual used bytes (may be less than maxLength if no ESC bytes in data)
        return output[..pos];
    }
}
