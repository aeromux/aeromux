using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// MessageParser partial class: Helper and utility methods.
/// Contains bit extraction, character decoding, and altitude decoding helpers.
/// </summary>
public sealed partial class MessageParser
{
    // ========================================
    // Altitude Decoding Helpers (DF 4, 20)
    // ========================================

    /// <summary>
    /// Decodes 13-bit AC altitude code (DF 4, 20).
    /// Supports four encoding modes:
    /// - Mode 1: All-zeros (altitude unavailable)
    /// - Mode 2: M=1 (metric altitude, 12-bit value)
    /// - Mode 3: Q=1 (25-foot increments, 11-bit value)
    /// - Mode 4: Q=0 (Gillham/Gray code, 100-foot increments)
    /// </summary>
    /// <param name="ac13">13-bit altitude code field</param>
    /// <returns>Decoded altitude, or null if invalid or unavailable</returns>
    /// <remarks>
    /// Algorithm verified against readsb mode_s.c decodeAC13Field function.
    /// Gillham decoding matches readsb mode_ac.c bit-perfect.
    /// </remarks>
    private Altitude? DecodeAltitudeAC13(int ac13)
    {
        // Mode 1: All zeros = altitude unavailable
        if (ac13 == 0)
        {
            return null;
        }

        int mBit = (ac13 >> 6) & 0x01;  // Bit 26 (position 7 in 13-bit field)
        int qBit = (ac13 >> 4) & 0x01;  // Bit 28 (position 9 in 13-bit field)

        // Mode 2: M=1 (metric altitude)
        if (mBit == 1)
        {
            int altitudeMeters = ac13 & 0x0FFF;  // Lower 12 bits
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
    /// Decodes Gillham-coded altitude (Gray code).
    /// Used for all altitudes when Q=0 (most commonly for extreme altitudes > 50,187 feet).
    /// Algorithm from readsb mode_ac.c internalModeAToModeC function.
    /// </summary>
    /// <param name="ac13Field">13-bit altitude code field</param>
    /// <returns>Altitude in 100-foot increments, or -9999 if invalid</returns>
    /// <remarks>
    /// Gillham code uses Gray code (reflected binary) encoding where adjacent values
    /// differ by only one bit. This reduces errors during altitude changes.
    /// Bit rearrangement: AC field → Gillham format → Gray code conversion → altitude
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
    /// Reference: readsb mode_ac.c internalModeAToModeC function.
    /// </summary>
    /// <param name="modeA">Gillham-encoded value (hex format)</param>
    /// <returns>Altitude in 100-foot increments (signed), or -9999 if invalid</returns>
    /// <remarks>
    /// Gray code decoding: XOR operations convert reflected binary to standard binary.
    /// Formula: ((fiveHundreds * 5) + oneHundreds - 13) gives altitude in 100-foot units.
    /// Valid range: -1200 to 126700 feet (-12 to 1267 in 100-foot units).
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

        // Remove 7s from oneHundreds (make 7→5 and 5→7)
        if ((oneHundreds & 5) == 5)
        {
            oneHundreds ^= 2;
        }

        // Check for invalid codes, only 1 to 5 are valid
        if (oneHundreds < 1 || oneHundreds > 5)
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

        // Final altitude calculation
        // Formula: ((fiveHundreds * 5) + oneHundreds - 13) * 100 feet
        return (fiveHundreds * 5) + oneHundreds - 13;
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
        if (value < 0 || value > 63)
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
    /// <returns>Decoded altitude, or null if unavailable.</returns>
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
        else
        {
            // Q=0: 100-foot Gillham code (rare, used for > 50,175 ft)
            // TODO Priority 3: Implement Gillham decoding
            // For now, return null (most aircraft use Q=1 encoding)
            return null;
        }
    }
}
