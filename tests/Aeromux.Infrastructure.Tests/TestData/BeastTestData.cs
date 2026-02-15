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

namespace Aeromux.Infrastructure.Tests.TestData;

/// <summary>
/// Centralized test data and helper methods for Beast protocol tests.
/// Contains frame constants, special test cases, and utilities for creating test streams.
/// Uses real Mode-S frames from RealFrames.cs (validated CRCs).
/// </summary>
public static class BeastTestData
{
    // Beast protocol constants
    public const byte ESC = 0x1A;
    public const byte TYPE_SHORT = (byte)'2';
    public const byte TYPE_LONG = (byte)'3';
    public const byte TYPE_RECEIVER_ID = 0xe3;

    // Real Mode-S frames with valid CRCs (from RealFrames.cs)

    /// <summary>
    /// DF 0 (Short Air-Air Surveillance) - 7 bytes, AP mode, ICAO: 4BCE08
    /// </summary>
    public const string ShortFrame_DF0 = RealFrames.ShortAirAir_4BCE08;

    /// <summary>
    /// DF 4 (Surveillance Altitude Reply) - 7 bytes, AP mode, ICAO: 49D414
    /// </summary>
    public const string ShortFrame_DF4 = RealFrames.Surveillance_Altitude_49D414;

    /// <summary>
    /// DF 5 (Surveillance Identity Reply) - 7 bytes, AP mode, ICAO: 80073B, Squawk 3205
    /// </summary>
    public const string ShortFrame_DF5 = RealFrames.Surveillance_Identity_80073B;

    /// <summary>
    /// DF 17 (ADS-B Aircraft Identification) - 14 bytes, PI mode, ICAO: 471DBC, Callsign: WZZ476
    /// </summary>
    public const string LongFrame_DF17 = RealFrames.AircraftId_471DBC;

    /// <summary>
    /// DF 20 (Comm-B Altitude Reply) - 14 bytes, AP mode, ICAO: 4D2407
    /// </summary>
    public const string LongFrame_DF20 = RealFrames.CommB_Altitude_4D2407;

    // Special test cases for escape byte handling

    /// <summary>
    /// Frame data with all bytes set to ESC (0x1A) - pathological case for testing
    /// NOTE: This does NOT have a valid CRC - use for encoder-only tests
    /// </summary>
    public const string AllEscapeBytes = "1A1A1A1A1A1A1A";

    /// <summary>
    /// Frame data with no ESC bytes - use real frame instead
    /// DF 4 Surveillance Altitude Reply, ICAO: 49D414
    /// </summary>
    public const string NoEscapeBytes = RealFrames.Surveillance_Altitude_49D414;

    /// <summary>
    /// Frame data with ESC bytes in various positions
    /// Pattern: [0x1A, 0x5D, 0x1A, 0x48, 0x1A, 0x4B, 0x1A]
    /// NOTE: This does NOT have a valid CRC - use for encoder-only tests
    /// </summary>
    public const string MultipleEscapeBytes = "1A5D1A481A4B1A";

    // Test UUIDs for receiver ID testing

    /// <summary>
    /// Zero UUID - all bytes are 0x00
    /// </summary>
    public static readonly Guid ZeroUuid = Guid.Parse("00000000-0000-0000-0000-000000000000");

    /// <summary>
    /// Max UUID - all bytes are 0xFF
    /// </summary>
    public static readonly Guid MaxUuid = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

    /// <summary>
    /// UUID with all bytes set to 0x1A - tests escape handling in receiver ID
    /// </summary>
    public static readonly Guid EscapeUuid = Guid.Parse("1A1A1A1A-1A1A-1A1A-1A1A-1A1A1A1A1A1A");

    /// <summary>
    /// Standard test UUID for general testing
    /// </summary>
    public static readonly Guid StandardUuid = Guid.Parse("12345678-90AB-CDEF-1234-567890ABCDEF");

    // Helper Methods

    /// <summary>
    /// Converts a hex string to a byte array.
    /// </summary>
    public static byte[] HexToBytes(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        hex = hex.Replace(" ", "").Replace("-", "");
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Converts a byte array to a hex string.
    /// </summary>
    public static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "");
    }

    /// <summary>
    /// Creates a MemoryStream from Beast-encoded binary data.
    /// Stream position is set to 0, ready for reading.
    /// </summary>
    public static MemoryStream CreateStreamFromBytes(byte[] data)
    {
        var stream = new MemoryStream(data);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Creates a MemoryStream containing a manually-constructed Beast frame.
    /// Useful for testing parser with specific byte patterns.
    /// </summary>
    /// <param name="messageType">Message type byte ('2', '3', or 0xe3)</param>
    /// <param name="timestamp">6-byte timestamp (big-endian 12 MHz counter)</param>
    /// <param name="signal">Signal strength byte</param>
    /// <param name="data">Frame data bytes</param>
    /// <param name="applyEscaping">If true, applies ESC byte doubling to all fields</param>
    public static MemoryStream CreateBeastStream(
        byte messageType,
        byte[] timestamp,
        byte signal,
        byte[] data,
        bool applyEscaping = true)
    {
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(data);

        var buffer = new List<byte> {
            // Frame start marker
            ESC, // Message type
            messageType };

        if (messageType == TYPE_RECEIVER_ID)
        {
            // Receiver ID message: just 8 bytes of data (no timestamp/signal)
            AddBytesWithEscaping(buffer, data, applyEscaping);
        }
        else
        {
            // Normal frame: timestamp + signal + data
            AddBytesWithEscaping(buffer, timestamp, applyEscaping);
            AddByteWithEscaping(buffer, signal, applyEscaping);
            AddBytesWithEscaping(buffer, data, applyEscaping);
        }

        return CreateStreamFromBytes(buffer.ToArray());
    }

    /// <summary>
    /// Adds a single byte to the buffer, with optional ESC doubling.
    /// </summary>
    private static void AddByteWithEscaping(List<byte> buffer, byte value, bool applyEscaping)
    {
        buffer.Add(value);
        if (applyEscaping && value == ESC)
        {
            buffer.Add(ESC);
        }
    }

    /// <summary>
    /// Adds multiple bytes to the buffer, with optional ESC doubling.
    /// </summary>
    private static void AddBytesWithEscaping(List<byte> buffer, byte[] values, bool applyEscaping)
    {
        foreach (byte b in values)
        {
            AddByteWithEscaping(buffer, b, applyEscaping);
        }
    }

    /// <summary>
    /// Creates a 48-bit big-endian timestamp from a DateTime.
    /// Uses the same formula as BeastEncoder: ticks * 12.0 / TimeSpan.TicksPerMicrosecond
    /// Masks to 48 bits to match BeastEncoder behavior.
    /// </summary>
    public static byte[] CreateTimestamp(DateTime dateTime)
    {
        ulong timestamp12MHz = (ulong)(dateTime.Ticks * 12.0 / TimeSpan.TicksPerMicrosecond);
        timestamp12MHz &= 0xFFFFFFFFFFFF;  // Mask to 48 bits
        byte[] timestamp = new byte[6];

        for (int i = 0; i < 6; i++)
        {
            timestamp[i] = (byte)(timestamp12MHz >> (40 - (i * 8)));
        }

        return timestamp;
    }

    /// <summary>
    /// Creates a signal strength byte with square root transform applied.
    /// Uses the same formula as BeastEncoder: sqrt(signalStrength / 255.0) * 255.0
    /// </summary>
    public static byte CreateSignalByte(byte signalStrength)
    {
        return (byte)Math.Round(Math.Sqrt(signalStrength / 255.0) * 255.0);
    }

    /// <summary>
    /// Creates a complete Beast frame stream for testing (timestamp + signal + data).
    /// Applies proper ESC byte doubling.
    /// </summary>
    public static MemoryStream CreateCompleteFrameStream(
        bool isLongFrame,
        DateTime timestamp,
        byte signalStrength,
        byte[] frameData)
    {
        byte messageType = isLongFrame ? TYPE_LONG : TYPE_SHORT;
        byte[] timestampBytes = CreateTimestamp(timestamp);
        byte signalByte = CreateSignalByte(signalStrength);

        return CreateBeastStream(messageType, timestampBytes, signalByte, frameData, applyEscaping: true);
    }
}
