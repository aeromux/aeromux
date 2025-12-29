namespace Aeromux.Core.Tests.TestData;

/// <summary>
/// BDS (Comm-B Data Selector) test frames from "The 1090MHz Riddle" by Junzi Sun.
/// All frames are validated examples from the book's "Try it out" sections.
/// </summary>
/// <remarks>
/// Source: "The 1090MHz Riddle: Understanding Mode S and ADS-B Data" by Junzi Sun
/// https://mode-s.org/decode/
///
/// Each constant includes:
/// - Source reference (chapter and page number)
/// - Complete field breakdown
/// - Binary representation where relevant
/// - Expected decoded values
/// - Full hex message
/// </remarks>
public static class BdsFrames
{
    // ========================================
    // BDS 1,7: Common Usage GICB Capability Report
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) with BDS 1,7 - GICB Capability Report
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 16.2, Page 120
    ///
    /// Message Structure:
    /// - ICAO: 000063 (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - BDS Code: 1,7 (0x17 - Common Usage GICB Capability Report)
    /// - MB Field: FA81C100000000 (56 bits)
    /// - MB Binary: 11111010100000011100000100000000000000000000000000000000
    ///
    /// Capability Bits Analysis:
    /// Bits set to 1: 1, 2, 3, 4, 5, 7, 9, 16, 17, 18, 24
    ///
    /// Supported BDS codes:
    /// - Bit 1 (MB:1)   = 1 → BDS 0,5 (Extended squitter airborne position)
    /// - Bit 2 (MB:2)   = 1 → BDS 0,6 (Extended squitter surface position)
    /// - Bit 3 (MB:3)   = 1 → BDS 0,7 (Extended squitter status)
    /// - Bit 4 (MB:4)   = 1 → BDS 0,8 (Extended squitter identification and category)
    /// - Bit 5 (MB:5)   = 1 → BDS 0,9 (Extended squitter airborne velocity information)
    /// - Bit 7 (MB:7)   = 1 → BDS 2,0 (Aircraft identification)
    /// - Bit 9 (MB:9)   = 1 → BDS 4,0 (Selected vertical intention)
    /// - Bit 16 (MB:16) = 1 → BDS 5,0 (Track and turn report)
    /// - Bit 17 (MB:17) = 1 → BDS 5,1 (Position coarse)
    /// - Bit 18 (MB:18) = 1 → BDS 5,2 (Position fine)
    /// - Bit 24 (MB:24) = 1 → BDS 6,0 (Heading and speed report)
    ///
    /// Expected Results:
    /// - BDS Code: 1,7
    /// - Capability Mask: 0xFA81C100000000 (as 56-bit value)
    /// - Supported BDS codes list: [BDS05, BDS06, BDS07, BDS08, BDS09, BDS20, BDS40, BDS50, BDS51, BDS52, BDS60]
    ///
    /// Full Message: A0000638FA81C10000000081A92F
    /// </remarks>
    public const string Bds17_Gicb_000063 = "A0000638FA81C10000000081A92F";

    // ========================================
    // BDS 2,0: Aircraft Identification
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) with BDS 2,0 - Aircraft Identification "KLM1017"
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 16.3, Pages 121-122
    ///
    /// Message Structure:
    /// - ICAO: 000083 (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - BDS Code: 2,0 (0x20 - Aircraft Identification)
    /// - MB Field: 202CC371C31DE0 (56 bits)
    /// - MB Binary: 0010 0000 001011 001100 001101 110001 110000 110001 110111 100000
    ///
    /// Callsign Decoding (6-bit characters):
    /// - BDS Code:  0010 0000  (bits 1-8)   → 0x20
    /// - Character 1: 001011   (bits 9-14)  → 11 → 'K'
    /// - Character 2: 001100   (bits 15-20) → 12 → 'L'
    /// - Character 3: 001101   (bits 21-26) → 13 → 'M'
    /// - Character 4: 110001   (bits 27-32) → 49 → '1'
    /// - Character 5: 110000   (bits 33-38) → 48 → '0'
    /// - Character 6: 110001   (bits 39-44) → 49 → '1'
    /// - Character 7: 110111   (bits 45-50) → 55 → '7'
    /// - Character 8: 100000   (bits 51-56) → 32 → ' ' (space)
    ///
    /// Character Map: #ABCDEFGHIJKLMNOPQRSTUVWXYZ##### ###############0123456789######
    ///
    /// Expected Results:
    /// - BDS Code: 2,0
    /// - Callsign: "KLM1017" (trailing space trimmed, or "KLM1017_" with underscore)
    ///
    /// Full Message: A000083E202CC371C31DE0AA1CCF
    /// </remarks>
    public const string Bds20_AircraftId_000083_KLM1017 = "A000083E202CC371C31DE0AA1CCF";

    // ========================================
    // BDS 4,0: Selected Vertical Intention
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) with BDS 4,0 - Selected Vertical Intention
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 17.1, Pages 128-129
    ///
    /// Message Structure:
    /// - ICAO: 8001EB (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - BDS Code: 4,0 (inferred from structure - no fixed identifier byte)
    /// - MB Field: AEE57730A80106 (56 bits)
    /// - MB Binary: 1 010111011011100 1 010111011011100 1 100001010100000 00000000 1 0 0 0 00 1 10
    ///
    /// Field Breakdown:
    ///
    /// MCP/FCU Selected Altitude (bits 1-13):
    /// - Status (bit 1): 1 (valid)
    /// - Value (bits 2-13): 010111011011100 = 1500 (decimal)
    /// - Result: 1500 × 16 ft = 24000 ft
    ///
    /// FMS Selected Altitude (bits 14-26):
    /// - Status (bit 14): 1 (valid)
    /// - Value (bits 15-26): 010111011011100 = 1500 (decimal)
    /// - Result: 1500 × 16 ft = 24000 ft
    ///
    /// Barometric Pressure Setting (bits 27-39):
    /// - Status (bit 27): 1 (valid)
    /// - Value (bits 28-39): 100001010100000 = 2132 (decimal)
    /// - Formula: (value × 0.1) + 800 mb
    /// - Result: (2132 × 0.1) + 800 = 1013.2 mb
    ///
    /// Reserved (bits 40-47):
    /// - Value: 00000000 (must be all zeros)
    ///
    /// MCP/FCU Mode Status (bits 48-56):
    /// - Status (bit 48): 1 (valid)
    /// - VNAV mode (bit 49): 0 (off)
    /// - Alt hold mode (bit 50): 0 (off)
    /// - Approach mode (bit 51): 0 (off)
    /// - Reserved (bits 52-53): 00 (zeros)
    /// - Target alt source status (bit 54): 1 (valid)
    /// - Target alt source (bits 55-56): 10 = FCU/MCP selected altitude
    ///
    /// Expected Results:
    /// - BDS Code: 4,0
    /// - MCP/FCU Selected Altitude: 24000 ft
    /// - FMS Selected Altitude: 24000 ft
    /// - Barometric Pressure: 1013.2 mb
    ///
    /// Full Message: A8001EBCAEE57730A80106DE1344
    /// </remarks>
    public const string Bds40_VerticalIntention_8001EB = "A8001EBCAEE57730A80106DE1344";

    // ========================================
    // BDS 5,0: Track and Turn Report
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) with BDS 5,0 - Track and Turn Report
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 17.2, Pages 130-131
    ///
    /// Message Structure:
    /// - ICAO: 80006A (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - BDS Code: 5,0 (inferred from structure)
    /// - MB Field: F9363D3BBF9CE9 (56 bits)
    /// - MB Binary: 1 1 111001001 1 0 1100011110 1 0011101110 1 1 111110011 1 0011101001
    ///
    /// Field Breakdown:
    ///
    /// Roll Angle (bits 1-11):
    /// - Status (bit 1): 1 (valid)
    /// - Sign (bit 2): 1 (negative)
    /// - Value (bits 3-11): 111001001 = 457 (decimal)
    /// - Two's complement: 457 - 2^9 = 457 - 512 = -55
    /// - LSB: 45/256 degrees
    /// - Result: -55 × (45/256) = -9.70 degrees
    ///
    /// True Track Angle (bits 12-23):
    /// - Status (bit 12): 1 (valid)
    /// - Sign (bit 13): 0 (positive)
    /// - Value (bits 14-23): 1100011110 = 798 (decimal)
    /// - LSB: 90/512 degrees
    /// - Result: 798 × (90/512) = 140.27 degrees
    ///
    /// Ground Speed (bits 24-34):
    /// - Status (bit 24): 1 (valid)
    /// - Value (bits 25-34): 0011101110 = 238 (decimal)
    /// - LSB: 2 kt
    /// - Result: 238 × 2 = 476 kt
    ///
    /// Track Angle Rate (bits 35-45):
    /// - Status (bit 35): 1 (valid)
    /// - Sign (bit 36): 1 (negative)
    /// - Value (bits 37-45): 111110011 = 499 (decimal)
    /// - Two's complement: 499 - 2^9 = 499 - 512 = -13
    /// - LSB: 8/256 degrees/second
    /// - Result: -13 × (8/256) = -0.40625 ≈ -0.406 deg/s
    ///
    /// True Airspeed (bits 46-56):
    /// - Status (bit 46): 1 (valid)
    /// - Value (bits 47-56): 0011101001 = 233 (decimal)
    /// - LSB: 2 kt
    /// - Result: 233 × 2 = 466 kt
    ///
    /// Expected Results:
    /// - BDS Code: 5,0
    /// - Roll Angle: -9.7 degrees
    /// - True Track Angle: 140.273 degrees (or 140.27)
    /// - Ground Speed: 476 kt
    /// - Track Angle Rate: -0.406 deg/s
    /// - True Airspeed: 466 kt
    ///
    /// Full Message: A80006ACF9363D3BBF9CE98F1E1D
    /// </remarks>
    public const string Bds50_TrackAndTurn_80006A = "A80006ACF9363D3BBF9CE98F1E1D";

    // ========================================
    // BDS 6,0: Heading and Speed Report
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) with BDS 6,0 - Heading and Speed Report
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 17.3, Pages 132-134
    ///
    /// Message Structure:
    /// - ICAO: 80004A (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - BDS Code: 6,0 (inferred from structure)
    /// - MB Field: A74A072BFDEFC1 (56 bits)
    /// - MB Binary: 1 0 1001110100 1 0100000011 1 0010101111 1 1 110111110 1 1 111000001
    ///
    /// Field Breakdown:
    ///
    /// Magnetic Heading (bits 1-12):
    /// - Status (bit 1): 1 (valid)
    /// - Sign (bit 2): 0 (positive)
    /// - Value (bits 3-12): 1001110100 = 628 (decimal)
    /// - LSB: 90/512 degrees
    /// - Result: 628 × (90/512) = 110.39 degrees
    ///
    /// Indicated Airspeed (bits 13-23):
    /// - Status (bit 13): 1 (valid)
    /// - Value (bits 14-23): 0100000011 = 259 (decimal)
    /// - LSB: 1 kt
    /// - Result: 259 × 1 = 259 kt
    ///
    /// Mach Number (bits 24-34):
    /// - Status (bit 24): 1 (valid)
    /// - Value (bits 25-34): 0010101111 = 175 (decimal)
    /// - LSB: 0.004
    /// - Result: 175 × 0.004 = 0.7
    ///
    /// Barometric Altitude Rate (bits 35-45):
    /// - Status (bit 35): 1 (valid)
    /// - Sign (bit 36): 1 (negative)
    /// - Value (bits 37-45): 110111110 = 445 (decimal)
    /// - Two's complement: 445 - 2^9 = 445 - 512 = -67
    /// - LSB: 32 ft/min
    /// - Result: -67 × 32 = -2144 ft/min
    ///
    /// Inertial Vertical Velocity (bits 46-56):
    /// - Status (bit 46): 1 (valid)
    /// - Sign (bit 47): 1 (negative)
    /// - Value (bits 48-56): 111000001 = 449 (decimal)
    /// - Two's complement: 449 - 2^9 = 449 - 512 = -63
    /// - LSB: 32 ft/min
    /// - Result: -63 × 32 = -2016 ft/min
    ///
    /// Expected Results:
    /// - BDS Code: 6,0
    /// - Magnetic Heading: 110.391 degrees (or 110.39)
    /// - Indicated Airspeed (IAS): 259 kt
    /// - Mach Number: 0.7
    /// - Barometric Altitude Rate: -2144 ft/min
    /// - Inertial Vertical Velocity: -2016 ft/min
    ///
    /// Notes:
    /// - Magnetic heading is relative to magnetic north (not true north)
    /// - Barometric altitude rate: from air data system (noisier, unfiltered)
    /// - Inertial vertical velocity: from flight management computer (smoother, filtered)
    ///
    /// Full Message: A80004AAA74A072BFDEFC1D5CB4F
    /// </remarks>
    public const string Bds60_HeadingAndSpeed_80004A = "A80004AAA74A072BFDEFC1D5CB4F";

    // ========================================
    // BDS 4,4: Meteorological Routine Air Report
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) with BDS 4,4 - Meteorological Routine Air Report
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 18.1, Pages 136-137
    ///
    /// Message Structure:
    /// - ICAO: 000169 (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - BDS Code: 4,4 (inferred from structure)
    /// - MB Field: 185BD5CF400000 (56 bits)
    /// - MB Binary: 0001 1 000010110 111101010 1 1100111101 0 00000000000 0 00 0 000000
    ///
    /// Field Breakdown:
    ///
    /// Figure of Merit (FOM) / Source (bits 1-4):
    /// - Value: 0001 = 1 (decimal)
    /// - Meaning: 1 = INS (Inertial Navigation System)
    /// - Valid values: 0=Invalid, 1=INS, 2=GNSS, 3=DME/DME, 4=VOR/DME, 5-15=Reserved
    ///
    /// Wind Speed (bits 5-14):
    /// - Status (bit 5): 1 (valid)
    /// - Value (bits 6-14): 000010110 = 22 (decimal)
    /// - LSB: 1 kt
    /// - Result: 22 × 1 = 22 kt
    ///
    /// Wind Direction (bits 15-24):
    /// - Status (bit 15): 1 (valid - but encoded in value bits, not separate)
    /// - Value (bits 16-24): 111101010 = 490 (decimal)
    /// - LSB: 180/256 degrees
    /// - Result: 490 × (180/256) = 344.53 degrees
    ///
    /// Static Air Temperature (bits 25-35):
    /// - Status (bit 25): 1 (valid)
    /// - Sign (bit 26): 1 (negative)
    /// - Value (bits 27-35): 100111101 = 317 (9-bit value within 10-bit field)
    /// - Full 10-bit value: 1100111101 = 829 (decimal)
    /// - Two's complement: 829 - 2^10 = 829 - 1024 = -195
    /// - LSB: 0.25°C
    /// - Result: -195 × 0.25 = -48.75°C
    /// - Note: Temperature range is -80°C to +60°C
    ///
    /// Average Static Pressure (bits 36-47):
    /// - Status (bit 36): 0 (not available)
    /// - Value: Not decoded when status = 0
    ///
    /// Turbulence (bits 48-49):
    /// - Status (bit 48): 0 (not available)
    /// - Value: Not decoded when status = 0
    ///
    /// Humidity (bits 50-56):
    /// - Status (bit 50): 0 (not available)
    /// - Value: Not decoded when status = 0
    ///
    /// Expected Results:
    /// - BDS Code: 4,4
    /// - FOM/Source: 1 (INS)
    /// - Wind Speed: 22 kt
    /// - Wind Direction: 344.5 degrees (or 344.53)
    /// - Static Air Temperature: -48.75°C
    /// - Average Static Pressure: null (not available)
    /// - Turbulence: null (not available)
    /// - Humidity: null (not available)
    ///
    /// Notes:
    /// - pyModeS returns wind as tuple: (22, 344.5)
    /// - pyModeS returns temp as tuple: (-48.75, -24.375) where second value is unknown
    /// - BDS 4,4 messages are relatively rare in practice
    ///
    /// Full Message: A0001692185BD5CF400000DFC696
    /// </remarks>
    public const string Bds44_Meteorological_000169 = "A0001692185BD5CF400000DFC696";

    // ========================================
    // BDS Inference: Distinguishing BDS 5,0 from 6,0
    // ========================================

    /// <summary>
    /// Comm-B Altitude Reply (DF 20) - Ambiguous structure, inferred as BDS 6,0
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 19.3, Pages 143-144
    ///
    /// Message Structure:
    /// - ICAO: 000183 (extracted from CRC parity)
    /// - DF: 20 (Comm-B Altitude Reply)
    /// - Altitude (from AC field, bits 20-32): 38000 ft (used for validation)
    /// - MB Field: E519F331602401
    /// - Ambiguity: Structure matches both BDS 5,0 and BDS 6,0 patterns
    ///
    /// BDS Inference Analysis:
    ///
    /// If BDS 5,0 (Track and Turn Report):
    /// - Ground Speed: 394 kt
    /// - True Airspeed: 2 kt
    /// - GS-TAS Difference: 394 - 2 = 392 kt
    /// - Validation: FAIL - difference exceeds 200 kt threshold ❌
    /// - Conclusion: Unreasonable wind speed (~400 kt), likely corrupted or wrong BDS
    ///
    /// If BDS 6,0 (Heading and Speed Report):
    /// - Indicated Airspeed (IAS): 249 kt
    /// - Mach Number: 0.788
    /// - Altitude: 38000 ft (from DF 20 AC field)
    /// - Calibrated Airspeed (CAS): calculated from Mach and altitude under ISA
    /// - CAS = f(Mach=0.788, Alt=38000ft) ≈ 249 kt
    /// - IAS-CAS Difference: 249 - 249 = 0 kt
    /// - Validation: PASS - difference within 15 kt threshold ✓
    /// - Conclusion: Reasonable values, consistent with altitude
    ///
    /// Inference Result: BDS 6,0
    ///
    /// Expected Results:
    /// - BDS Code: 6,0 (inferred)
    /// - Magnetic Heading: ~110 degrees (from BDS 6,0 decoding)
    /// - IAS: 249 kt
    /// - Mach: 0.788
    ///
    /// Validation Logic:
    /// 1. Try BDS 5,0: Check if |GS - TAS| ≤ 200 kt (accounting for wind)
    /// 2. Try BDS 6,0: Convert Mach to CAS using altitude, check if |IAS - CAS| ≤ 15 kt
    /// 3. Select BDS with passing validation
    ///
    /// Full Message: A0001838E519F33160240142D7FA
    /// </remarks>
    public const string BdsInference_000183_Is60 = "A0001838E519F33160240142D7FA";

    /// <summary>
    /// Comm-B Identity Reply (DF 21) - Ambiguous structure, inferred as BDS 5,0 using ADS-B reference
    /// </summary>
    /// <remarks>
    /// Source: "The 1090MHz Riddle" Chapter 19.3, Page 144
    ///
    /// Message Structure:
    /// - ICAO: 8001EB (extracted from CRC parity)
    /// - DF: 21 (Comm-B Identity Reply)
    /// - Squawk (from ID field, bits 20-32): Not decoded in example
    /// - MB Field: FFFB23286004A7
    /// - Ambiguity: Structure matches both BDS 5,0 and BDS 6,0 patterns
    /// - Challenge: DF 21 does NOT include altitude code → cannot use Mach-to-CAS validation
    ///
    /// ADS-B Reference Data (from same aircraft):
    /// - Ground Speed: 320 kt
    /// - Track Angle: 250 degrees
    /// - Altitude: 14000 ft (for context, not used in validation)
    ///
    /// BDS Inference Analysis:
    ///
    /// If BDS 5,0 (Track and Turn Report):
    /// - Ground Speed (decoded): 322 kt
    /// - Track Angle (decoded): 250 degrees
    /// - Comparison with ADS-B:
    ///   - GS difference: |322 - 320| = 2 kt ✓
    ///   - Track difference: |250 - 250| = 0° ✓
    /// - Validation: PASS - values match ADS-B reference ✓
    /// - Conclusion: Consistent with ADS-B data
    ///
    /// If BDS 6,0 (Heading and Speed Report):
    /// - IAS (decoded): 401 kt
    /// - Heading (decoded): 359 degrees
    /// - Comparison with ADS-B:
    ///   - Speed mismatch: IAS (401 kt) vs ADS-B GS (320 kt) - significant difference
    ///   - Heading vs Track: 359° vs 250° - large angular difference
    /// - Validation: FAIL - values do not match ADS-B reference ❌
    /// - Conclusion: Inconsistent with ADS-B data
    ///
    /// Inference Result: BDS 5,0
    ///
    /// Expected Results:
    /// - BDS Code: 5,0 (inferred using ADS-B reference)
    /// - Ground Speed: 322 kt (matches ADS-B 320 kt)
    /// - Track Angle: 250 degrees (matches ADS-B 250°)
    ///
    /// Validation Logic (DF 21 without altitude):
    /// 1. Obtain ADS-B reference data from same aircraft (recent message)
    /// 2. Try BDS 5,0: Compare GS and Track with ADS-B GS and Track
    /// 3. Try BDS 6,0: Compare heading/speed with ADS-B data (considering wind)
    /// 4. Select BDS with best match to ADS-B reference
    ///
    /// Notes:
    /// - DF 21 inference requires external reference (ADS-B or other sources)
    /// - Without ADS-B reference, BDS 5,0 vs 6,0 ambiguity cannot be resolved reliably
    /// - Wind estimation can improve BDS 6,0 validation (IAS + wind ≈ GS)
    ///
    /// Full Message: A8001EBCFFFB23286004A73F6A5B
    /// </remarks>
    public const string BdsInference_8001EB_Is50 = "A8001EBCFFFB23286004A73F6A5B";
}
