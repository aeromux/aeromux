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
/// - Reads 48-bit timestamp (12 MHz counter, big-endian) - consumed but not used
/// - Reads signal level (0-255 relative magnitude)
/// - Reads frame data with escape sequence handling (0x1A 0x1A → 0x1A)
///
/// Timestamp Handling:
/// Beast protocol encodes timestamps relative to sender's reference time (typically app start).
/// We cannot reconstruct absolute time without knowing the sender's reference point.
/// ValidatedFrame.Timestamp is set to reception time (DateTime.UtcNow) for logging and display.
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
    /// Parsing statistics tracking frames parsed vs validated.
    /// </summary>
    private long _framesParsed;
    private long _framesValidated;

    /// <summary>
    /// Total number of frames successfully parsed from Beast binary format.
    /// </summary>
    public long FramesParsed => _framesParsed;

    /// <summary>
    /// Total number of frames that passed validation (subset of FramesParsed).
    /// </summary>
    public long FramesValidated => _framesValidated;

    /// <summary>
    /// Total number of frames rejected by validation (CRC errors, invalid format, etc.).
    /// </summary>
    public long FramesRejected => _framesParsed - _framesValidated;

    /// <summary>
    /// Parses Beast format stream and yields ValidatedFrame objects.
    /// Handles escape byte sequences and creates frames with reception timestamps.
    /// </summary>
    /// <param name="stream">Stream containing Beast binary protocol data (NetworkStream, MemoryStream, etc.)</param>
    /// <param name="cancellationToken">Token to cancel the async enumeration</param>
    /// <returns>Async enumerable of ValidatedFrame objects parsed from stream</returns>
    /// <remarks>
    /// Parsing is stateful and continues until stream ends or cancellation is requested.
    /// Invalid frames (incomplete reads, malformed data) are silently skipped.
    /// Accepts any Stream-derived type for flexibility in testing and production use.
    /// </remarks>
    public async IAsyncEnumerable<ValidatedFrame> ParseStreamAsync(
        Stream stream,
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

            // Step 3: Read 48-bit timestamp (big-endian 12 MHz counter) with escape handling
            // CRITICAL: Timestamp bytes can contain 0x1A and must be unescaped
            byte[] timestampBytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                if (bytesRead == 0)
                {
                    break; // Stream ended mid-timestamp
                }

                byte b = buffer[0];

                // Beast escape decoding: if we encounter ESC byte, read next byte
                if (b == ESC)
                {
                    bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    b = buffer[0];  // This is the actual timestamp byte to store
                }

                timestampBytes[i] = b;
            }

            if (bytesRead == 0)
            {
                break; // Incomplete timestamp
            }

            // Step 4: Read signal strength indicator (0-255) with escape handling
            // CRITICAL: Signal byte can also be 0x1A and must be unescaped
            bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            byte signalByte = buffer[0];
            if (signalByte == ESC)
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }
                signalByte = buffer[0];
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

            // Step 6: Use reception time for timestamp
            // Beast timestamps are sender-relative (12MHz counter from sender's start time)
            // We cannot reconstruct absolute time without knowing the sender's reference point
            // Reception time is appropriate for logging, display, and frame correlation
            DateTime timestamp = DateTime.UtcNow;

            // Step 7: Decode signal strength with reverse square root transform
            // BeastEncoder applies: encodedSignal = sqrt(signalStrength / 255.0) * 255.0
            // We reverse this: signalStrength = (encodedSignal / 255.0)^2 * 255.0
            // This restores the original signal strength value from the compressed encoding
            double normalized = signalByte / 255.0;
            double squared = normalized * normalized;
            byte signalStrength = (byte)Math.Round(squared * 255.0);

            // Step 8: Create ValidatedFrame using ValidatedFrameFactory
            // This properly handles both PI mode (ICAO in AA field) and AP mode (ICAO in CRC)
            var rawFrame = new RawFrame(frameData, timestamp);
            ValidatedFrame? validatedFrame = _validatedFrameFactory.ValidateFrame(rawFrame, signalStrength);

            // Track parsing statistics
            Interlocked.Increment(ref _framesParsed);

            // Step 9: Yield ValidatedFrame if validation succeeded
            // Validation may fail for corrupted frames (CRC errors, invalid format, etc.)
            if (validatedFrame == null)
            {
                continue;
            }

            Interlocked.Increment(ref _framesValidated);
            yield return validatedFrame;
        }
    }
}
