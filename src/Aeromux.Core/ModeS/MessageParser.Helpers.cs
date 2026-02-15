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
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS;

/// <summary>
/// MessageParser partial class: Helper and utility methods.
/// Contains the bit extraction, character decoding, and altitude decoding helpers.
/// </summary>
public sealed partial class MessageParser
{
    // ========================================
    // Altitude Decoding Helpers (DF 4, 20)
    // ========================================

    /// <summary>
    /// Decodes 13-bit AC (Altitude Code) field from DF 4/20 surveillance messages.
    /// Supports four encoding modes:
    /// - Mode 1: All-zeros (altitude unavailable)
    /// - Mode 2: M=1 (metric altitude, 12-bit value, 25-meter increments)
    /// - Mode 3: Q=1 (25-foot increments, 11-bit value, range: -1000 to +50,175 feet)
    /// - Mode 4: Q=0 (Gillham/Gray code, 100-foot increments, for altitudes > 50,187 feet)
    /// </summary>
    /// <param name="ac13">13-bit altitude code field from DF 4 or DF 20 message</param>
    /// <returns>Decoded altitude, or <see langword="null"/> if invalid or unavailable.</returns>
    /// <remarks>
    /// Implements all four encoding modes per ICAO Annex 10, Volume IV, Section 3.1.2.6.5.4.
    /// Gillham (Gray code) decoding handles extreme altitudes above 50,187 feet.
    /// Formula for Q=1 mode: Altitude (ft) = N × 25 - 1000, where N is the 11-bit value.
    /// </remarks>
    private Altitude? DecodeAltitudeAC13(int ac13)
    {
        // Mode 1: All zeros = altitude unavailable
        if (ac13 == 0)
        {
            return null;
        }

        int mBit = (ac13 >> 6) & 0x01;  // bit 26 (position 7 in 13-bit field)
        int qBit = (ac13 >> 4) & 0x01;  // bit 28 (position 9 in 13-bit field)

        // Mode 2: M=1 (metric altitude)
        if (mBit == 1)
        {
            // Extract altitude value - bits 20-31 (12 bits, lower 12 bits of AC field)
            int altitudeMeters = ac13 & 0x0FFF;
            return Altitude.FromMeters(altitudeMeters, AltitudeType.Barometric);
        }

        // Mode 3: Q=1 (25-foot increments)
        if (qBit == 1)
        {
            // Remove Q and M bits, reconstruct 11-bit value
            int n = ((ac13 & 0x1F80) >> 2) |   // Bits above Q (5 bits)
                    ((ac13 & 0x0020) >> 1) |   // Bits between Q and M
                    (ac13 & 0x000F);           // Bits below M (4 bits)

            int altitudeFeet = (n * 25) - 1000;

            return Altitude.FromFeet(altitudeFeet, AltitudeType.Barometric);
        }

        // Mode 4: Q=0 (Gillham code - full implementation)
        int gillhamResult = DecodeGillham(ac13);
        if (gillhamResult == -9999)
        {
            return null;  // Invalid Gillham code
        }

        int gillhamAltitudeFeet = gillhamResult * 100;

        return Altitude.FromFeet(gillhamAltitudeFeet, AltitudeType.Barometric);
    }

    /// <summary>
    /// Decodes Gillham-coded altitude (Gray code) from AC field.
    /// Used for all altitudes when Q=0 (most commonly for extreme altitudes > 50,187 feet).
    /// Implements ICAO Annex 10 Gillham code decoding with bit rearrangement and Gray-to-binary conversion.
    /// </summary>
    /// <param name="ac13Field">13-bit altitude code field from DF 4 or DF 20 message</param>
    /// <returns>Altitude in 100-foot increments, or -9999 if invalid code</returns>
    /// <remarks>
    /// Gillham code uses Gray code (reflected binary) encoding where adjacent altitude values
    /// differ by only one bit. This reduces transient errors during altitude changes (e.g., when
    /// transitioning from FL500 to FL501, only one bit changes instead of multiple bits).
    ///
    /// Decoding process:
    /// 1. Rearrange AC field bits (C1,A1,C2,A2,C4,A4,M,B1,Q,B2,D2,B4,D4) to Gillham format
    /// 2. Convert Gray code to binary using XOR reduction
    /// 3. Map binary value to altitude using 100-foot increments
    ///
    /// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.6.5.4 (Mode C altitude encoding).
    /// </remarks>
    private static int DecodeGillham(int ac13Field)
    {
        // Rearrange bits from AC field to Gillham format
        // AC field: C1 A1 C2 A2 C4 A4 M B1 Q B2 D2 B4 D4
        // Gillham:  C1 A1 C2 A2 C4 A4 0 B1 0 B2 D2 B4 D4
        //           Bit positions in output (hex notation)

        int gillham = 0;

        if ((ac13Field & 0x1000) != 0)
        {
            gillham |= 0x0010;  // C1 → bit 4
        }

        if ((ac13Field & 0x0800) != 0)
        {
            gillham |= 0x1000;  // A1 → bit 12
        }

        if ((ac13Field & 0x0400) != 0)
        {
            gillham |= 0x0020;  // C2 → bit 5
        }

        if ((ac13Field & 0x0200) != 0)
        {
            gillham |= 0x2000;  // A2 → bit 13
        }

        if ((ac13Field & 0x0100) != 0)
        {
            gillham |= 0x0040;  // C4 → bit 6
        }

        if ((ac13Field & 0x0080) != 0)
        {
            gillham |= 0x4000;  // A4 → bit 14
        }

        // Skip M bit (0x0040) - not used in Gillham
        if ((ac13Field & 0x0020) != 0)
        {
            gillham |= 0x0100;  // B1 → bit 8
        }

        // Skip Q bit (0x0010) - not used in Gillham
        if ((ac13Field & 0x0008) != 0)
        {
            gillham |= 0x0200;  // B2 → bit 9
        }

        if ((ac13Field & 0x0004) != 0)
        {
            gillham |= 0x0002;  // D2 → bit 1
        }

        if ((ac13Field & 0x0002) != 0)
        {
            gillham |= 0x0400;  // B4 → bit 10
        }

        if ((ac13Field & 0x0001) != 0)
        {
            gillham |= 0x0004;  // D4 → bit 2
        }

        // Convert Gillham (Gray code) to binary altitude
        return GillhamToBinary(gillham);
    }

    /// <summary>
    /// Converts Gillham-encoded value to binary altitude (100-foot increments).
    /// Reference: ICAO Annex 10 Volume IV Gillham code specification.
    /// </summary>
    /// <param name="modeA">Gillham-encoded value (hex format)</param>
    /// <returns>Altitude in 100-foot increments (signed), or -9999 if invalid</returns>
    /// <remarks>
    /// <para>Gray code decoding: XOR operations convert reflected binary to standard binary.</para>
    /// <para>
    /// Formula: ((fiveHundreds * 5) + oneHundreds - 13) gives altitude in 100-foot units.
    /// The -13 offset is defined by ICAO Annex 10 Volume IV to establish the Gillham code origin point.
    /// This offset ensures that encoded value 0 corresponds to -1300 feet, allowing representation
    /// of altitudes below sea level and establishing proper alignment with the Gray code sequence.
    /// </para>
    /// <para>Valid range: -1200 to 126700 feet (-12 to 1267 in 100-foot units).</para>
    /// </remarks>
    private static int GillhamToBinary(int modeA)
    {
        // Check for invalid patterns
        // Zero bits must be zero, D1 set is illegal, C1-C4 cannot be all zero
        if ((modeA & 0xFFFF8889) != 0 ||  // Check zero bits are zero, D1 set is illegal
            (modeA & 0x000000F0) == 0)     // C1-C4 cannot be zero
        {
            return -9999;  // INVALID_ALTITUDE
        }

        // Decode C bits (100s) using Gray code
        int oneHundreds = 0;

        if ((modeA & 0x0010) != 0)
        {
            oneHundreds ^= 0x007;  // C1
        }

        if ((modeA & 0x0020) != 0)
        {
            oneHundreds ^= 0x003;  // C2
        }

        if ((modeA & 0x0040) != 0)
        {
            oneHundreds ^= 0x001;  // C4
        }

        // Gray code hundreds encoding only supports values 1-5 (100-500 ft increments).
        // ICAO Annex 10: Invalid decoded values 6 or 7 must be corrected (7→5, 5→7).
        // This bit manipulation performs the swap: if bit pattern is ...101, flip bit 1.
        if ((oneHundreds & 5) == 5)
        {
            oneHundreds ^= 2;
        }

        // Check for invalid codes, only 1 to 5 are valid
        if (oneHundreds is < 1 or > 5)
        {
            return -9999;  // INVALID_ALTITUDE
        }

        // Decode D and A/B bits (500s) using Gray code
        int fiveHundreds = 0;

        // D1 is never used for altitude (bit 0x0001)
        if ((modeA & 0x0002) != 0)
        {
            fiveHundreds ^= 0x0FF;  // D2
        }

        if ((modeA & 0x0004) != 0)
        {
            fiveHundreds ^= 0x07F;  // D4
        }

        if ((modeA & 0x1000) != 0)
        {
            fiveHundreds ^= 0x03F;  // A1
        }

        if ((modeA & 0x2000) != 0)
        {
            fiveHundreds ^= 0x01F;  // A2
        }

        if ((modeA & 0x4000) != 0)
        {
            fiveHundreds ^= 0x00F;  // A4
        }

        if ((modeA & 0x0100) != 0)
        {
            fiveHundreds ^= 0x007;  // B1
        }

        if ((modeA & 0x0200) != 0)
        {
            fiveHundreds ^= 0x003;  // B2
        }

        if ((modeA & 0x0400) != 0)
        {
            fiveHundreds ^= 0x001;  // B4
        }

        // Correct order of oneHundreds
        if ((fiveHundreds & 1) != 0)
        {
            oneHundreds = 6 - oneHundreds;
        }

        // Final altitude calculation using ICAO-defined Gillham offset
        // Formula: Altitude (100 ft units) = (fiveHundreds × 5) + oneHundreds - 13
        // Then multiply by 100 to get feet: Altitude (ft) = result × 100
        // GillhamOffset = 13: ICAO Annex 10 Volume IV establishes origin at -1300 feet
        // This allows encoding altitudes from -1200 ft to +126,700 ft in 100-foot increments
        // Reference: ICAO Annex 10, Volume IV, Section 3.1.2.6.5.4, Figure 3-11
        const int gillhamOffset = 13;
        return (fiveHundreds * 5) + oneHundreds - gillhamOffset;
    }

    // ========================================
    // Helper Methods
    // ========================================

    /// <summary>
    /// Extracts a specified number of bits from a byte array starting at a given bit position.
    /// </summary>
    /// <param name="data">Source byte array.</param>
    /// <param name="startBit">Starting bit position (1-indexed as per Mode S specification).</param>
    /// <param name="bitCount">Number of bits to extract.</param>
    /// <returns>Extracted value as integer.</returns>
    private static int ExtractBits(byte[] data, int startBit, int bitCount)
    {
        // Convert to 0-indexed
        int bitIndex = startBit - 1;
        int byteIndex = bitIndex / 8;
        int bitOffset = bitIndex % 8;

        // Read enough bytes to contain all requested bits (up to 4 bytes for 32-bit values)
        ulong accumulator = 0;
        int bytesNeeded = (bitOffset + bitCount + 7) / 8;

        for (int i = 0; i < bytesNeeded && (byteIndex + i) < data.Length; i++)
        {
            accumulator = (accumulator << 8) | data[byteIndex + i];
        }

        // Shift to align and mask to extract requested bits
        int totalBitsRead = bytesNeeded * 8;
        int rightShift = totalBitsRead - bitOffset - bitCount;
        ulong mask = (1UL << bitCount) - 1;

        return (int)((accumulator >> rightShift) & mask);
    }

    /// <summary>
    /// Decodes a 6-bit AIS character to ASCII.
    /// </summary>
    /// <param name="value">6-bit character value (0-63).</param>
    /// <returns>ASCII character, or '#' for invalid values.</returns>
    private static char DecodeAisCharacter(int value)
    {
        if (value is < 0 or > 63)
        {
            return '#';
        }

        return AisCharset[value];
    }

    /// <summary>
    /// Maps Type Code (TC) and Category (CA) to AircraftCategory enum.
    /// </summary>
    /// <param name="tc">Type code (1-4).</param>
    /// <param name="ca">Category field (3 bits, 0-7).</param>
    /// <returns>Aircraft category enum value.</returns>
    private static AircraftCategory GetAircraftCategory(int tc, int ca)
    {
        // Combine TC and CA into lookup value (TC * 10 + CA)
        int combined = (tc * 10) + ca;

        // Map to enum (enum values are defined as TC * 10 + CA)
        if (Enum.IsDefined(typeof(AircraftCategory), combined))
        {
            return (AircraftCategory)combined;
        }

        // Unknown combination - return Reserved
        return AircraftCategory.Reserved;
    }

    /// <summary>
    /// Decodes 12-bit altitude field for TC 9-18 (barometric) or TC 20-22 (GNSS).
    /// TC 9-18: Uses Q-bit method (25-foot increments) or Gillham code (100-foot increments).
    /// TC 20-22: Direct 12-bit value in meters (no Q-bit, no Gillham).
    /// </summary>
    /// <param name="altRaw">12-bit altitude field (bits 41-52).</param>
    /// <param name="altitudeType">Altitude type (Barometric or Geometric).</param>
    /// <returns>Decoded altitude, or <see langword="null"/> if unavailable.</returns>
    private static Altitude? DecodeAltitude(int altRaw, AltitudeType altitudeType)
    {
        // Special case: all zeros means altitude unavailable
        if (altRaw == 0)
        {
            return null;
        }

        // TC 20-22: GNSS altitude (direct 12-bit value in meters)
        if (altitudeType == AltitudeType.Geometric)
        {
            // Direct 12-bit value in meters (0-4095 meters)
            // No Q-bit, no Gillham code - just use the value directly
            return Altitude.FromMeters(altRaw, AltitudeType.Geometric);
        }

        // TC 9-18: Barometric altitude (Q-bit or Gillham encoding)
        // Extract Q-bit (bit 4 of the 12-bit field, 0-indexed = bit 8)
        // altRaw bit layout: [b11 b10 b9 b8 b7 b6 b5 b4(Q) b3 b2 b1 b0]
        bool qBit = (altRaw & 0x0010) != 0;

        if (qBit)
        {
            // Q=1: 25-foot increments
            // Remove Q-bit and reconstruct 11-bit value
            int n = ((altRaw & 0x0FE0) >> 1) |  // Bits above Q (7 bits)
                    (altRaw & 0x000F);           // Bits below Q (4 bits)

            int altitudeFeet = (n * 25) - 1000;
            return Altitude.FromFeet(altitudeFeet, altitudeType);
        }

        // Q=0: Gillham code (100-foot increments)
        // Used for altitudes > 50,175 feet (U-2, ER-2, high-altitude jets, balloons)
        // Convert AC12 to AC13 format by inserting M=0 at bit position 6
        // AC12 format: [b11 b10 b9 b8 b7 b6 b5 b4(Q=0) b3 b2 b1 b0]
        // AC13 format: [b12 b11 b10 b9 b8 b7 b6(M=0) b5 b4(Q=0) b3 b2 b1 b0]
        int ac13Equivalent = ((altRaw & 0x0FC0) << 1) |  // Upper 6 bits (shift left to make room for M)
                             (altRaw & 0x003F);          // Lower 6 bits (Q and below)

        int gillhamResult = DecodeGillham(ac13Equivalent);
        if (gillhamResult == -9999)
        {
            return null;  // Invalid Gillham code
        }

        return Altitude.FromFeet(gillhamResult * 100, altitudeType);
    }
}
