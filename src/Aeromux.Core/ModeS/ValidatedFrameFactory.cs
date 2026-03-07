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
/// Factory for creating ValidatedFrame instances from RawFrame data.
/// Validates Mode S frames using CRC-24, extracts ICAO addresses, and attempts error correction.
/// </summary>
/// <remarks>
/// Factory Responsibilities:
/// 1. CRC Validation - Verifies frame integrity using ICAO Annex 10 Volume IV CRC-24 standard
/// 2. ICAO Extraction - Extracts aircraft addresses from validated frames (PI and AP modes)
/// 3. Error Correction - Attempts single-bit error correction for corrupted frames
/// 4. Frame Transformation - Converts RawFrame → ValidatedFrame (or null if irreparably corrupted)
///
/// CRC Polynomial: 0xFFF409
/// G(x) = x²⁴ + x²³ + x²² + x²¹ + x²⁰ + x¹⁹ + x¹⁸ + x¹⁷ + x¹⁶ + x¹⁵ + x¹⁴ + x¹³ + x¹² + x¹⁰ + x³ + 1
/// (ICAO Annex 10, Volume IV, Section 3.1.2.3.7.1)
///
/// Two validation modes:
/// - PI (Parity/Interrogator): ICAO in AA field (DF 11, 17, 18, 19) - CRC remainder must be 0
/// - AP (Address/Parity): ICAO encoded in CRC (DF 0, 4, 5, 16, 20, 21) - ICAO = CRC XOR transmitted
///
/// Performance: Uses lookup table for fast CRC calculation (~7-14 table lookups per frame)
/// Error correction: Single-bit errors corrected via syndrome lookup table (O(1) per frame, PI mode only)
/// </remarks>
public sealed class ValidatedFrameFactory
{
    private const uint CrcPolynomial = 0xFFF409;  // ICAO CRC-24 polynomial
    private readonly uint[] _crcTable = new uint[256];  // Lookup table for fast CRC
    private readonly Dictionary<uint, int> _syndromesShort;  // Syndrome → bit position for 56-bit frames
    private readonly Dictionary<uint, int> _syndromesLong;   // Syndrome → bit position for 112-bit frames
    private readonly Dictionary<uint, string> _icaoStringCache = new();  // ICAO uint → hex string cache

    // Statistics (exposed as properties for DeviceWorker to log)
    private long _framesChecked;
    private long _framesValid;
    private long _framesCorrected;
    private long _framesInvalid;

    /// <summary>
    /// Initializes the factory and pre-computes CRC lookup table and syndrome tables.
    /// </summary>
    public ValidatedFrameFactory()
    {
        InitializeCrcTable();
        _syndromesShort = InitializeSyndromeTable(ModeSFrameLength.ShortBytes);
        _syndromesLong = InitializeSyndromeTable(ModeSFrameLength.LongBytes);
    }

    /// <summary>
    /// Validates a raw frame and extracts ICAO address if valid.
    /// Attempts single-bit error correction if frame is initially invalid.
    /// </summary>
    /// <param name="rawFrame">Raw frame from preamble detection</param>
    /// <param name="signalStrength">Signal strength for this frame (0.0-255.0)</param>
    /// <returns>ValidatedFrame if valid or correctable, null if corrupted beyond repair</returns>
    public ValidatedFrame? ValidateFrame(RawFrame rawFrame, double signalStrength)
    {
        ArgumentNullException.ThrowIfNull(rawFrame);

        _framesChecked++;

        byte[] data = rawFrame.Data;
        DownlinkFormat df = rawFrame.DownlinkFormat;

        // Determine CRC mode (PI or AP)
        bool isPIMode = UsesPIMode(df);

        // Try validating frame as-is
        if (TryValidate(data, isPIMode, out string? icaoAddress))
        {
            _framesValid++;
            return new ValidatedFrame(data, rawFrame.Timestamp, icaoAddress!, signalStrength, false);
        }

        // Try single-bit error correction (only for PI mode - AP mode has no validation)
        if (isPIMode && TryCorrectSingleBitError(data, out icaoAddress))
        {
            _framesCorrected++;
            return new ValidatedFrame(data, rawFrame.Timestamp, icaoAddress!, signalStrength, true);
        }

        // Frame is corrupted beyond repair
        _framesInvalid++;
        return null;
    }

    /// <summary>
    /// Attempts to validate a frame and extract ICAO address.
    /// </summary>
    private bool TryValidate(byte[] data, bool isPIMode, out string? icaoAddress)
    {
        if (isPIMode)
        {
            // PI mode (DF 11, 17, 18, 19): Calculate CRC over entire message, should be 0
            uint crc = CalculateCrc(data, data.Length);
            if (crc == 0)
            {
                // Valid! ICAO is in AA field (bytes 1-3)
                icaoAddress = ExtractIcaoFromAA(data);
                return true;
            }
        }
        else
        {
            // AP mode (DF 0, 4, 5, 16, 20, 21): ICAO encoded in CRC
            // Calculate CRC over data bytes (excluding last 3)
            uint rem = 0;
            for (int i = 0; i < data.Length - 3; i++)
            {
                uint index = data[i] ^ ((rem & 0xFF0000) >> 16);
                rem = ((rem << 8) ^ _crcTable[index]) & 0xFFFFFF;
            }

            // Extract transmitted CRC from last 3 bytes
            uint transmittedCrc = ExtractTransmittedCrc(data);

            // ICAO = calculated_CRC XOR transmitted_CRC
            uint icao = rem ^ transmittedCrc;

            // AP mode has no direct validation for single message
            // Validation comes from ICAO consistency across multiple messages
            icaoAddress = FormatIcaoAddress(icao);
            return true;  // AP mode always "valid" (we extract ICAO from CRC)
        }

        icaoAddress = null;
        return false;
    }

    /// <summary>
    /// Attempts to correct a single-bit error using syndrome lookup.
    /// The CRC remainder of a corrupted message is the error syndrome — a unique value
    /// determined solely by the bit position of the error (CRC linearity property).
    /// Pre-computed syndrome tables enable O(1) error correction instead of brute-force O(N).
    /// Only applicable to PI mode messages (AP mode has no validation to check against).
    /// </summary>
    private bool TryCorrectSingleBitError(byte[] data, out string? icaoAddress)
    {
        // The CRC remainder IS the error syndrome for single-bit errors
        uint syndrome = CalculateCrc(data, data.Length);

        Dictionary<uint, int> table =
            data.Length == ModeSFrameLength.ShortBytes ? _syndromesShort : _syndromesLong;

        if (table.TryGetValue(syndrome, out int bitPosition))
        {
            // Flip the errored bit (MSB-first bit ordering)
            data[bitPosition / 8] ^= (byte)(1 << (7 - (bitPosition % 8)));
            icaoAddress = ExtractIcaoFromAA(data);
            return true;
        }

        // Syndrome not in table → multi-bit error, uncorrectable
        icaoAddress = null;
        return false;
    }

    /// <summary>
    /// Calculates CRC-24 over message bytes using lookup table optimization.
    /// </summary>
    /// <param name="data">Message bytes</param>
    /// <param name="lengthBytes">Total message length in bytes</param>
    /// <returns>24-bit CRC remainder (0 for valid PI mode messages)</returns>
    /// <remarks>
    /// Algorithm:
    /// 1. Process all bytes except last 3 through lookup table
    /// 2. XOR result with last 3 bytes (CRC field)
    /// For PI mode: Result should be 0 for valid message
    /// For AP mode: Result XOR transmitted_CRC gives ICAO address
    /// This lookup table approach provides ~20x speedup over bit-by-bit calculation
    /// </remarks>
    private uint CalculateCrc(byte[] data, int lengthBytes)
    {
        uint rem = 0;
        int n = lengthBytes;

        // Process all bytes except last 3 through lookup table
        for (int i = 0; i < n - 3; i++)
        {
            uint index = data[i] ^ ((rem & 0xFF0000) >> 16);
            rem = ((rem << 8) ^ _crcTable[index]) & 0xFFFFFF;
        }

        // XOR with last 3 bytes (CRC field)
        rem = rem ^ ((uint)data[n - 3] << 16) ^ ((uint)data[n - 2] << 8) ^ data[n - 1];

        return rem;
    }

    /// <summary>
    /// Initializes CRC lookup table for fast calculation.
    /// Called once during construction.
    /// </summary>
    /// <remarks>
    /// Pre-computes CRC values for all single bytes (0-255).
    /// This reduces CRC calculation from ~200 operations to ~7-14 table lookups per frame.
    /// Standard optimization technique: trade memory (1KB table) for computation speed.
    /// </remarks>
    private void InitializeCrcTable()
    {
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i << 16;  // Place byte in upper 8 bits of 24-bit field

            for (int bit = 0; bit < 8; bit++)
            {
                if ((crc & 0x800000) != 0)  // Test MSB
                {
                    crc = ((crc << 1) ^ CrcPolynomial) & 0xFFFFFF;
                }
                else
                {
                    crc = (crc << 1) & 0xFFFFFF;
                }
            }

            _crcTable[i] = crc;
        }
    }

    /// <summary>
    /// Pre-computes syndrome lookup table for single-bit error correction.
    /// For each bit position, calculates the CRC remainder that a single-bit error at that position
    /// would produce. This enables O(1) error correction by looking up the syndrome.
    /// </summary>
    /// <param name="messageLengthBytes">Frame length in bytes (7 for short, 14 for long)</param>
    /// <returns>Dictionary mapping CRC syndrome to the bit position that caused it</returns>
    private Dictionary<uint, int> InitializeSyndromeTable(int messageLengthBytes)
    {
        int totalBits = messageLengthBytes * 8;
        var table = new Dictionary<uint, int>(totalBits);

        byte[] probe = new byte[messageLengthBytes];

        for (int bitPos = 0; bitPos < totalBits; bitPos++)
        {
            // Set single bit in probe message (MSB-first ordering)
            probe[bitPos / 8] = (byte)(1 << (7 - (bitPos % 8)));

            // The CRC of a single-bit message IS the syndrome for that bit position
            uint syndrome = CalculateCrc(probe, messageLengthBytes);
            table[syndrome] = bitPos;

            // Clear the bit for next iteration
            probe[bitPos / 8] = 0;
        }

        return table;
    }

    /// <summary>
    /// Determines if a DF type uses PI mode (ICAO in AA field) or AP mode (ICAO in CRC).
    /// </summary>
    private static bool UsesPIMode(DownlinkFormat df)
    {
        return df is
            DownlinkFormat.AllCallReply or                      // DF 11
            DownlinkFormat.ExtendedSquitter or                  // DF 17 (ADS-B)
            DownlinkFormat.ExtendedSquitterNonTransponder or    // DF 18 (TIS-B)
            DownlinkFormat.MilitaryExtendedSquitter;            // DF 19
    }

    /// <summary>
    /// Extracts ICAO address from AA field (bytes 1-3) for PI mode messages.
    /// </summary>
    private string ExtractIcaoFromAA(byte[] data)
    {
        uint icao = ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
        return FormatIcaoAddress(icao);
    }

    /// <summary>
    /// Extracts transmitted CRC from last 3 bytes of message.
    /// </summary>
    private static uint ExtractTransmittedCrc(byte[] data)
    {
        int offset = data.Length - 3;
        return ((uint)data[offset] << 16) | ((uint)data[offset + 1] << 8) | data[offset + 2];
    }

    /// <summary>
    /// Formats 24-bit ICAO address as 6-character hex string (uppercase).
    /// Caches results to avoid repeated string allocations for the same aircraft.
    /// </summary>
    private string FormatIcaoAddress(uint icao)
    {
        if (!_icaoStringCache.TryGetValue(icao, out string? cached))
        {
            cached = $"{icao:X6}";
            _icaoStringCache[icao] = cached;
        }
        return cached;
    }

    // Statistics properties for Coordinator Pattern
    public long FramesChecked => _framesChecked;
    public long FramesValid => _framesValid;
    public long FramesCorrected => _framesCorrected;
    public long FramesInvalid => _framesInvalid;
}
