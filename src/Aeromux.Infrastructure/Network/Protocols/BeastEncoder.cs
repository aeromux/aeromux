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
    /// Writes a byte to the output buffer, escaping it if it equals the ESC byte (0x1A).
    /// Beast protocol requires ALL bytes (timestamp, signal, and data) to be escaped.
    /// </summary>
    /// <param name="buffer">Output buffer</param>
    /// <param name="position">Current write position</param>
    /// <param name="value">Byte value to write</param>
    /// <returns>Updated write position after writing byte and potential escape</returns>
    private static int WriteEscapedByte(byte[] buffer, int position, byte value)
    {
        buffer[position++] = value;
        if (value == ESC)
        {
            buffer[position++] = ESC; // Double the escape byte
        }
        return position;
    }

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
        // Structure: 1 (ESC) + 1 (type) + 6*2 (timestamp, escaped) + 1*2 (signal, escaped) + data*2 (escaped)
        // Worst case: All 6 timestamp bytes + 1 signal byte + all data bytes are 0x1A = each doubled
        int maxLength = 1 + 1 + (6 * 2) + (1 * 2) + (frame.Data.Length * 2);
        byte[] output = new byte[maxLength];

        int pos = 0;

        // Write frame start marker (ESC byte)
        output[pos++] = ESC;

        // Write message type indicator ('2' for short 56-bit frames, '3' for long 112-bit frames)
        output[pos++] = isLong ? (byte)'3' : (byte)'2';

        // Write 48-bit timestamp as 12 MHz counter (big-endian encoding, with escaping)
        // Convert .NET DateTime ticks (100ns resolution) to 12 MHz counter
        // Calculation: ticks * 12 MHz / 10 MHz = ticks * 1.2
        // Each timestamp byte must be escaped if it equals 0x1A to maintain frame boundaries
        ulong timestamp12MHz = (ulong)(frame.Timestamp.Ticks * 12.0 / TimeSpan.TicksPerMicrosecond);
        for (int i = 0; i < 6; i++)
        {
            // Extract bytes from MSB to LSB (big-endian)
            // Shift: 40, 32, 24, 16, 8, 0 bits
            byte timestampByte = (byte)(timestamp12MHz >> (40 - (i * 8)));
            pos = WriteEscapedByte(output, pos, timestampByte);
        }

        // Write signal strength with square root transform for better dynamic range
        // The square root transform compresses strong signals while preserving weak signal resolution
        // This optimizes the 8-bit range: weak signals (important for edge-of-range reception)
        // get more precision, while strong signals (already easy to detect) are compressed
        // Transform: normalize to 0-1 range → apply sqrt → scale back to 0-255
        // Mathematical rationale: sqrt is a concave function that grows quickly near zero
        // but slowly at high values, redistributing the available bit space toward weak signals
        // Example transformations: 255→255, 128→181, 64→128, 16→64, 4→32
        // Without transform: uniform spacing across full range
        // With transform: more resolution for signals below 50% strength
        byte signalByte = (byte)Math.Round(Math.Sqrt(frame.SignalStrength / 255.0) * 255.0);
        pos = WriteEscapedByte(output, pos, signalByte);

        // Write raw Mode S frame data with escape handling
        // If any data byte equals ESC (0x1A), it must be doubled for framing transparency
        foreach (byte b in frame.Data)
        {
            pos = WriteEscapedByte(output, pos, b);
        }

        // Return slice of actual used bytes (may be less than maxLength if no ESC bytes present)
        return output[..pos];
    }

    /// <summary>
    /// Encodes receiver ID message (0xe3 type) for MLAT identification.
    /// Sent once per client connection, contains first 64 bits of receiver UUID.
    /// </summary>
    /// <param name="receiverUuid">RFC 4122 compliant UUID identifying this receiver</param>
    /// <returns>Beast-encoded receiver ID message with escaping</returns>
    /// <remarks>
    /// Beast receiver ID message format:
    /// - Escape byte (0x1A)
    /// - Message type (0xe3 = receiver ID)
    /// - 8-byte receiver ID (first 64 bits of UUID, big-endian, with escaping)
    ///
    /// Each of the 8 bytes must be escaped if it equals 0x1A.
    /// Worst case: all 8 bytes are 0x1A = 18 bytes total (1 ESC + 1 type + 16 data).
    /// </remarks>
    public static byte[] EncodeReceiverId(Guid receiverUuid)
    {
        // Extract first 64 bits of UUID
        byte[] uuidBytes = receiverUuid.ToByteArray();
        ulong receiverId = BitConverter.ToUInt64(uuidBytes, 0);

        // Beast receiver ID message: ESC 0xe3 [8 bytes with escaping]
        byte[] buffer = new byte[18]; // Worst case: all 8 bytes escaped
        int pos = 0;

        buffer[pos++] = ESC;
        buffer[pos++] = 0xe3;

        // Write 8-byte receiver ID with escaping (big-endian)
        for (int i = 7; i >= 0; i--)
        {
            byte idByte = (byte)((receiverId >> (i * 8)) & 0xFF);
            pos = WriteEscapedByte(buffer, pos, idByte);
        }

        // Return actual length (may be less than 18 if no escaping needed)
        return buffer[0..pos];
    }
}
