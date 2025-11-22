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
using System.Net.Sockets;
using Aeromux.Core.ModeS;

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Parses Beast binary format received from TCP stream.
/// Used by DaemonStream to decode frames from daemon's Beast broadcaster.
/// This is the inverse operation of BeastEncoder - reconstructs ValidatedFrame from binary.
/// </summary>
/// <remarks>
/// Beast Binary Format Parsing:
/// - Reads escape byte (0x1A) to identify frame start
/// - Reads message type ('2' for short 7-byte, '3' for long 14-byte frames)
/// - Reads 48-bit timestamp (12 MHz counter, big-endian)
/// - Reads signal level (0-255 relative magnitude)
/// - Reads frame data with escape sequence handling (0x1A 0x1A → 0x1A)
///
/// Timestamp Handling:
/// Beast timestamps are encoded as 12 MHz counters representing absolute time.
/// BeastEncoder converts DateTime.Ticks to 12 MHz via multiplication by 1.2.
/// BeastParser reverses this encoding to reconstruct the original DateTime.
/// This preserves the exact frame capture time from the sender.
///
/// ICAO Address Extraction:
/// Uses ValidatedFrameFactory to properly extract ICAO for both PI and AP modes.
/// - PI mode (DF 11, 17, 18, 19): ICAO in AA field (bytes 1-3)
/// - AP mode (DF 0, 4, 5, 16, 20, 21): ICAO encoded in CRC field
/// </remarks>
public sealed class BeastParser
{
    /// <summary>
    /// Escape byte used for frame delimiting in Beast protocol.
    /// </summary>
    private const byte ESC = 0x1A;

    /// <summary>
    /// Factory for creating ValidatedFrame instances with proper ICAO extraction and CRC validation.
    /// </summary>
    private readonly ValidatedFrameFactory _validatedFrameFactory = new();

    /// <summary>
    /// Parses Beast format stream and yields ValidatedFrame objects.
    /// Handles escape byte sequences and reconstructs frames with timestamps.
    /// </summary>
    /// <param name="stream">Network stream containing Beast binary protocol data</param>
    /// <param name="cancellationToken">Token to cancel the async enumeration</param>
    /// <returns>Async enumerable of ValidatedFrame objects parsed from stream</returns>
    /// <remarks>
    /// Parsing is stateful and continues until stream ends or cancellation is requested.
    /// Invalid frames (incomplete reads, malformed data) are silently skipped.
    /// </remarks>
    public async IAsyncEnumerable<ValidatedFrame> ParseStreamAsync(
        NetworkStream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Buffer sized for worst-case Beast message with all data bytes escaped
        // Structure: 1 (ESC) + 1 (type) + 6 (timestamp) + 1 (signal) + 14 (data) + 14 (escapes) = 37 bytes
        byte[] buffer = new byte[32];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Step 1: Read frame start marker (escape byte 0x1A)
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
            if (bytesRead == 0)
            {
                break; // End of stream - sender closed connection
            }

            if (buffer[0] != ESC)
            {
                continue; // Not a Beast message start - skip byte and continue scanning
            }

            // Step 2: Read message type indicator
            // '2' = short frame (56 bits / 7 bytes)
            // '3' = long frame (112 bits / 14 bytes)
            // 0xe3 = receiver ID message (8 bytes, MLAT identification)
            bytesRead = await stream.ReadAsync(buffer.AsMemory(1, 1), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            // Handle receiver ID message (0xe3) - consumed but not processed
            // Receiver ID messages contain the first 64 bits of the sender's UUID
            // Format: ESC 0xe3 [8 bytes with escaping applied]
            // We consume these messages to maintain stream synchronization but don't use the UUID
            // (our use case is receiving frames, not correlating multi-receiver MLAT timing)
            if (buffer[1] == 0xe3)
            {
                // Read 8-byte UUID payload with escape sequence handling
                // Must read with unescaping to maintain proper stream position for next message
                for (int i = 0; i < 8; i++)
                {
                    bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                    if (bytesRead == 0)
                    {
                        break; // Stream ended mid-message
                    }

                    byte b = buffer[0];
                    if (b == ESC)
                    {
                        // Doubled escape byte - read the actual data byte that follows
                        bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                        if (bytesRead == 0)
                        {
                            break;
                        }
                    }
                }
                continue; // Message consumed, proceed to next Beast message
            }

            bool isLong = buffer[1] == '3';
            int frameLength = isLong ? 14 : 7;

            // Step 3: Read 48-bit timestamp (big-endian 12 MHz counter)
            bytesRead = await stream.ReadAsync(buffer.AsMemory(2, 6), cancellationToken);
            if (bytesRead < 6)
            {
                break; // Incomplete frame header
            }

            // Step 4: Read signal strength indicator (0-255 relative magnitude)
            bytesRead = await stream.ReadAsync(buffer.AsMemory(8, 1), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            // Step 5: Read frame data with escape sequence handling
            // Frame data may contain 0x1A bytes which are transmitted as 0x1A 0x1A
            // We must un-escape these sequences during reading
            byte[] frameData = new byte[frameLength];
            int dataPos = 0;
            while (dataPos < frameLength)
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                if (bytesRead == 0)
                {
                    break; // Stream ended mid-frame
                }

                byte b = buffer[0];

                // Beast escape decoding: if we encounter ESC byte, read next byte
                // The next byte is the actual data byte (which could be ESC itself or any other byte)
                // Examples:
                //   Wire: 0x1A 0x1A → Data: 0x1A (escaped ESC byte in frame data)
                //   Wire: 0x1A 0x5D → Data: 0x5D (normal frame byte that happens to follow ESC marker)
                if (b == ESC)
                {
                    bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    b = buffer[0];  // This is the actual data byte to store
                }

                frameData[dataPos++] = b;
            }

            // Step 6: Decode timestamp from 48-bit big-endian 12 MHz counter
            // BeastEncoder converts DateTime.Ticks to 12 MHz counter via: ticks * 1.2
            // We reverse this operation to reconstruct the original absolute timestamp
            ulong timestamp12MHz = 0;
            for (int i = 0; i < 6; i++)
            {
                // Reconstruct 48-bit value from big-endian bytes (MSB first)
                timestamp12MHz = (timestamp12MHz << 8) | buffer[2 + i];
            }

            // Reverse the encoding: Convert 12 MHz counter back to .NET ticks
            // Original encoding: timestamp12MHz = ticks * 12.0 / TimeSpan.TicksPerMicrosecond
            // Reverse operation: ticks = timestamp12MHz * TimeSpan.TicksPerMicrosecond / 12.0
            long ticks = (long)(timestamp12MHz * TimeSpan.TicksPerMicrosecond / 12.0);
            var timestamp = new DateTime(ticks, DateTimeKind.Utc);

            byte signalStrength = buffer[8];

            // Step 7: Extract ICAO address using ValidatedFrameFactory
            // This properly handles both PI mode (ICAO in AA field) and AP mode (ICAO in CRC)
            // Create temporary RawFrame and validate to get proper ICAO extraction
            var rawFrame = new RawFrame(frameData, timestamp);
            ValidatedFrame? validatedFrame = _validatedFrameFactory.ValidateFrame(rawFrame, signalStrength);

            // Step 8: Yield reconstructed ValidatedFrame
            // If validation fails (corrupted frame), skip it
            // Note: Beast format doesn't include error correction info, so we use validator's result
            if (validatedFrame != null)
            {
                yield return validatedFrame;
            }
        }
    }
}
