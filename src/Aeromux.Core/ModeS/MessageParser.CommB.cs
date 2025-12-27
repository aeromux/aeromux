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

using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// MessageParser partial class: Comm-B messages (DF 20/21/24) and BDS register parsers.
/// Handles Comm-B altitude/identity replies with BDS inference for all 10 register types.
/// </summary>
public sealed partial class MessageParser
{
    /// <summary>
    /// Parses Comm-B altitude reply from Downlink Format 20.
    /// Response to Comm-B interrogation containing altitude + 56-bit BDS register data.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Comm-B altitude reply message with inferred BDS data.</returns>
    /// <remarks>
    /// DF 20 structure:
    /// - Bits 6-8: Flight Status (FS)
    /// - Bits 9-13: Downlink Request (DR)
    /// - Bits 14-19: Utility Message (UM)
    /// - Bits 20-32: Altitude Code (AC)
    /// - Bits 33-88: Message, Comm-B (MB) - 56-bit BDS register data
    ///
    /// BDS inference is required to determine the register type from MB field.
    /// </remarks>
    private ModeSMessage? ParseCommBAltitudeReply(ValidatedFrame frame)
    {
        // Extract Flight Status (FS) - bits 6-8 (3 bits)
        int flightStatusRaw = ExtractBits(frame.Data, 6, 3);
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 20 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Downlink Request (DR) - bits 9-13 (5 bits)
        int dr = ExtractBits(frame.Data, 9, 5);

        // Extract Utility Message (UM) - bits 14-19 (6 bits)
        int um = ExtractBits(frame.Data, 14, 6);

        // Extract Altitude Code (AC) - bits 20-32 (13 bits)
        int altitudeCode = ExtractBits(frame.Data, 20, 13);

        // Decode altitude (null if invalid or unavailable)
        Altitude? altitude = DecodeAltitudeAC13(altitudeCode);

        // Extract MB field (Message, Comm-B) - bits 33-88 (56 bits = 7 bytes)
        byte[] mb = new byte[7];
        Array.Copy(frame.Data, 4, mb, 0, 7);
        // Shift left by 1 bit to align MB field (bit 33 starts in the middle of byte 4)
        for (int i = 0; i < 6; i++)
        {
            mb[i] = (byte)((mb[i] << 1) | ((mb[i + 1] & 0x80) >> 7));
        }
        mb[6] = (byte)(mb[6] << 1);

        // Infer BDS code and parse data
        (BdsCode bdsCode, BdsData? bdsData) = InferBds(mb);

        return new CommBAltitudeReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            altitude,
            flightStatus,
            dr,
            um,
            bdsCode,
            bdsData);
    }

    /// <summary>
    /// Parses Comm-B identity reply from Downlink Format 21.
    /// Response to Comm-B interrogation containing squawk code + 56-bit BDS register data.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Comm-B identity reply message with inferred BDS data.</returns>
    /// <remarks>
    /// DF 21 structure:
    /// - Bits 6-8: Flight Status (FS)
    /// - Bits 9-13: Downlink Request (DR)
    /// - Bits 14-19: Utility Message (UM)
    /// - Bits 20-32: Identity Code (ID) - squawk code
    /// - Bits 33-88: Message, Comm-B (MB) - 56-bit BDS register data
    ///
    /// BDS inference is required to determine the register type from MB field.
    /// </remarks>
    private ModeSMessage? ParseCommBIdentityReply(ValidatedFrame frame)
    {
        // Extract Flight Status (FS) - bits 6-8 (3 bits)
        int flightStatusRaw = ExtractBits(frame.Data, 6, 3);
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 21 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Downlink Request (DR) - bits 9-13 (5 bits)
        int dr = ExtractBits(frame.Data, 9, 5);

        // Extract Utility Message (UM) - bits 14-19 (6 bits)
        int um = ExtractBits(frame.Data, 14, 6);

        // Extract Identity Code (ID) - bits 20-32 (13 bits)
        int identityCode = ExtractBits(frame.Data, 20, 13);

        // Decode squawk code (4-digit octal string)
        string squawkCode = DecodeSquawkCode(identityCode);

        // Extract MB field (Message, Comm-B) - bits 33-88 (56 bits = 7 bytes)
        byte[] mb = new byte[7];
        Array.Copy(frame.Data, 4, mb, 0, 7);
        // Shift left by 1 bit to align MB field (bit 33 starts in the middle of byte 4)
        for (int i = 0; i < 6; i++)
        {
            mb[i] = (byte)((mb[i] << 1) | ((mb[i + 1] & 0x80) >> 7));
        }
        mb[6] = (byte)(mb[6] << 1);

        // Infer BDS code and parse data
        (BdsCode bdsCode, BdsData? bdsData) = InferBds(mb);

        return new CommBIdentityReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            squawkCode,
            flightStatus,
            dr,
            um,
            bdsCode,
            bdsData);
    }

    /// <summary>
    /// Parses Comm-D extended length message from Downlink Format 24.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Always null - DF 24 is intentionally not implemented.</returns>
    /// <remarks>
    /// DF 24 (Comm-D Extended Length Message) is intentionally not implemented because:
    /// 1. Ground-to-ground communication only (not aircraft surveillance)
    /// 2. No ICAO aircraft address (cannot be tracked)
    /// 3. Uses ELM (Extended Length Message) protocol (complex, multi-segment)
    /// 4. Extremely rare in practice (less than 0.1% of Mode S traffic)
    /// 5. Provides no value for aircraft tracking applications
    ///
    /// The frame is detected, CRC-validated, and logged as unsupported, which is correct behavior.
    /// </remarks>
    private ModeSMessage? ParseCommDExtendedLength(ValidatedFrame frame)
    {
        // Intentionally not implemented - DF 24 is ground-to-ground communication without aircraft ICAO address
        _unsupportedMessages++;
        return null;
    }

    // ========================================
    // BDS Register Parsers (Comm-B Data)
    // ========================================

    /// <summary>
    /// Infers the BDS code from MB field (56-bit Comm-B message) and parses the data.
    /// Uses pattern matching against all 10 Comm-B BDS registers.
    /// </summary>
    /// <param name="mb">56-bit MB field (7 bytes).</param>
    /// <returns>Tuple of (BdsCode enum, parsed BdsData or null).</returns>
    /// <remarks>
    /// BDS inference strategy (standard pattern matching approach):
    /// 1. Check for all-zeros (empty response)
    /// 2. Check BDS 1,0/1,7 (first byte = 0x10 or 0x17)
    /// 3. Check BDS 2,0 (first byte = 0x20, valid callsign)
    /// 4. Check BDS 3,0 (first byte = 0x30, VDS validation)
    /// 5. Try EHS registers (4,0/5,0/5,3/6,0) with range checks
    /// 6. Try MRAR registers (4,4/4,5) with FOM/status checks
    /// 7. Return Unknown if no match
    /// </remarks>
    private (BdsCode BdsCode, BdsData? Data) InferBds(byte[] mb)
    {
        // Check for empty response (all zeros or invalid)
        bool allZeros = mb.All(t => t == 0);

        if (allZeros)
        {
            return (BdsCode.Empty, null);
        }

        // First byte is often the BDS identifier
        int firstByte = mb[0];

        switch (firstByte)
        {
            // Try BDS 1,0 (Data link capability report)
            case 0x10:
            {
                Bds10DataLinkCapability? result = TryParseBds10(mb);
                if (result != null)
                {
                    return (BdsCode.Bds10, result);
                }

                break;
            }
            // Try BDS 1,7 (Common usage GICB capability report)
            case 0x17:
            {
                Bds17GicbCapability? result = TryParseBds17(mb);
                if (result != null)
                {
                    return (BdsCode.Bds17, result);
                }

                break;
            }
            // Try BDS 2,0 (Aircraft identification)
            case 0x20:
            {
                Bds20AircraftIdentification? result = TryParseBds20(mb);
                if (result != null)
                {
                    return (BdsCode.Bds20, result);
                }

                break;
            }
            // Try BDS 3,0 (ACAS Resolution Advisory)
            case 0x30:
            {
                Bds30AcasResolutionAdvisory? result = TryParseBds30(mb);
                if (result != null)
                {
                    return (BdsCode.Bds30, result);
                }

                break;
            }
        }

        // Try EHS registers (4,0/5,0/5,3/6,0) - no fixed identifier, use range checks
        // Try in order of likelihood (based on common aircraft implementations)

        // Try BDS 4,0 (Selected vertical intention)
        Bds40SelectedVerticalIntention? bds40 = TryParseBds40(mb);
        if (bds40 != null)
        {
            return (BdsCode.Bds40, bds40);
        }

        // Try BDS 5,0 (Track and turn report)
        Bds50TrackAndTurn? bds50 = TryParseBds50(mb);
        if (bds50 != null)
        {
            return (BdsCode.Bds50, bds50);
        }

        // Try BDS 6,0 (Heading and speed report)
        Bds60HeadingAndSpeed? bds60 = TryParseBds60(mb);
        if (bds60 != null)
        {
            return (BdsCode.Bds60, bds60);
        }

        // Try BDS 5,3 (Air-referenced state vector)
        Bds53AirReferencedState? bds53 = TryParseBds53(mb);
        if (bds53 != null)
        {
            return (BdsCode.Bds53, bds53);
        }

        // Try MRAR registers (4,4/4,5) - meteorological reports
        Bds44MeteorologicalRoutine? bds44 = TryParseBds44(mb);
        if (bds44 != null)
        {
            return (BdsCode.Bds44, bds44);
        }

        Bds45MeteorologicalHazard? bds45 = TryParseBds45(mb);
        if (bds45 != null)
        {
            return (BdsCode.Bds45, bds45);
        }

        // No match found
        return (BdsCode.Unknown, null);
    }

    /// <summary>
    /// Tries to parse BDS 1,0 (Data link capability report).
    /// </summary>
    private Bds10DataLinkCapability? TryParseBds10(byte[] mb)
    {
        // BDS 1,0: First byte = 0x10
        if (mb[0] != 0x10)
        {
            return null;
        }

        // Extract capability bits (bits 9-24, 16 bits)
        int capability = (mb[1] << 8) | mb[2];

        return new Bds10DataLinkCapability(capability);
    }

    /// <summary>
    /// Tries to parse BDS 1,7 (Common usage GICB capability report).
    /// </summary>
    private Bds17GicbCapability? TryParseBds17(byte[] mb)
    {
        // BDS 1,7: First byte = 0x17
        if (mb[0] != 0x17)
        {
            return null;
        }

        // Extract capability mask (bits 9-56, 48 bits stored in 56-bit MB as bits 9-56)
        // MB is 56 bits = 7 bytes, bits 9-56 = 48 bits
        ulong capabilityMask = 0;
        for (int i = 1; i < 7; i++)
        {
            capabilityMask = (capabilityMask << 8) | mb[i];
        }

        return new Bds17GicbCapability(capabilityMask);
    }

    /// <summary>
    /// Tries to parse BDS 2,0 (Aircraft identification).
    /// </summary>
    private Bds20AircraftIdentification? TryParseBds20(byte[] mb)
    {
        // BDS 2,0: First byte = 0x20
        if (mb[0] != 0x20)
        {
            return null;
        }

        // Extract callsign (bits 9-56, 48 bits = 8 characters × 6 bits)
        // Decode 8 characters using AIS charset
        char[] callsign = new char[8];
        int bitOffset = 8; // Start after first byte (bits 1-8)

        for (int i = 0; i < 8; i++)
        {
            int charValue = ExtractBits(mb, bitOffset + 1, 6); // +1 for 1-indexed
            callsign[i] = DecodeAisCharacter(charValue);
            bitOffset += 6;
        }

        string callsignStr = new string(callsign).Trim();

        // Validate: check for invalid characters (should not contain '#' unless intentional)
        if (callsignStr.Contains('#') && callsignStr != "########")
        {
            return null; // Invalid callsign
        }

        return new Bds20AircraftIdentification(callsignStr);
    }

    /// <summary>
    /// Tries to parse BDS 3,0 (ACAS Resolution Advisory).
    /// Simplified validation - just check VDS field.
    /// </summary>
    private Bds30AcasResolutionAdvisory? TryParseBds30(byte[] mb)
    {
        // BDS 3,0: First byte (VDS) = 0x30
        if (mb[0] != 0x30)
        {
            return null;
        }

        // VDS validation is sufficient for BDS 3,0 identification
        return new Bds30AcasResolutionAdvisory();
    }

    /// <summary>
    /// Tries to parse BDS 4,0 (Selected vertical intention).
    /// </summary>
    private Bds40SelectedVerticalIntention? TryParseBds40(byte[] mb)
    {
        // BDS 4,0: No fixed identifier, use status bits and range checks
        // Structure:
        // - Bit 1: MCP/FCU selected altitude status
        // - Bits 2-13: MCP/FCU selected altitude (12 bits)
        // - Bit 14: FMS selected altitude status
        // - Bits 15-26: FMS selected altitude (12 bits)
        // - Bit 27: Barometric pressure setting status
        // - Bits 28-39: Barometric pressure (12 bits)
        // - Bits 40-47: Reserved (should be 0)
        // - Bits 48-56: Reserved

        int mcpStatus = ExtractBits(mb, 1, 1);
        int mcpAltRaw = ExtractBits(mb, 2, 12);
        int fmsStatus = ExtractBits(mb, 14, 1);
        int fmsAltRaw = ExtractBits(mb, 15, 12);
        int baroStatus = ExtractBits(mb, 27, 1);
        int baroRaw = ExtractBits(mb, 28, 12);

        // Validation: check reserved bits (bits 40-47 should be 0)
        int reserved = ExtractBits(mb, 40, 8);
        if (reserved != 0)
        {
            return null;
        }

        // Range validation
        int? mcpAlt = null;
        if (mcpStatus == 1 && mcpAltRaw != 0)
        {
            mcpAlt = mcpAltRaw * 16; // 16 ft resolution
            if (mcpAlt is < 0 or > 65520)
            {
                return null; // Reasonable range
            }
        }

        int? fmsAlt = null;
        if (fmsStatus == 1 && fmsAltRaw != 0)
        {
            fmsAlt = fmsAltRaw * 16; // 16 ft resolution
            if (fmsAlt is < 0 or > 65520)
            {
                return null;
            }
        }

        double? baro = null;
        if (baroStatus == 1 && baroRaw != 0)
        {
            baro = 800.0 + ((baroRaw - 1) * 0.1); // 0.1 mbar resolution, range 800-1209.4 mbar
            if (baro is < 800 or > 1200)
            {
                return null; // Reasonable range
            }
        }

        // At least one field must be valid for BDS 4,0
        if (mcpAlt == null && fmsAlt == null && baro == null)
        {
            return null;
        }

        return new Bds40SelectedVerticalIntention(mcpAlt, fmsAlt, baro);
    }

    /// <summary>
    /// Tries to parse BDS 4,4 (Meteorological routine report).
    /// </summary>
    private Bds44MeteorologicalRoutine? TryParseBds44(byte[] mb)
    {
        // BDS 4,4 structure:
        // - Bit 1: FOM status
        // - Bits 2-4: Figure of Merit (0-7)
        // - Bit 5: Wind speed status
        // - Bits 6-14: Wind speed (9 bits, knots)
        // - Bit 15: Wind direction status
        // - Bits 16-24: Wind direction (9 bits, degrees)
        // - Bit 25: Static air temperature status
        // - Bits 26-35: Temperature (10 bits, signed, 0.25°C)
        // - Bit 36: Pressure status
        // - Bits 37-47: Pressure (11 bits, hPa)
        // - Bits 48-56: Reserved

        int fomStatus = ExtractBits(mb, 1, 1);
        int fomRaw = ExtractBits(mb, 2, 3);
        int windSpeedStatus = ExtractBits(mb, 5, 1);
        int windSpeedRaw = ExtractBits(mb, 6, 9);
        int windDirStatus = ExtractBits(mb, 15, 1);
        int windDirRaw = ExtractBits(mb, 16, 9);
        int tempStatus = ExtractBits(mb, 25, 1);
        int tempRaw = ExtractBits(mb, 26, 10);
        int pressureStatus = ExtractBits(mb, 36, 1);
        int pressureRaw = ExtractBits(mb, 37, 11);

        // Reserved bits check
        int reserved = ExtractBits(mb, 48, 9);
        if (reserved != 0)
        {
            return null;
        }

        int? fom = fomStatus == 1 ? fomRaw : null;
        if (fom > 7)
        {
            return null;
        }

        int? windSpeed = null;
        if (windSpeedStatus == 1)
        {
            windSpeed = windSpeedRaw;
            if (windSpeed is < 0 or > 250)
            {
                return null; // Reasonable range
            }
        }

        double? windDir = null;
        if (windDirStatus == 1)
        {
            windDir = windDirRaw * (180.0 / 256.0); // Resolution
            if (windDir is < 0 or >= 360)
            {
                return null;
            }
        }

        double? temp = null;
        if (tempStatus == 1)
        {
            // Signed 10-bit value, 0.25°C resolution
            int sign = (tempRaw & 0x200) != 0 ? -1 : 1;
            int value = tempRaw & 0x1FF;
            temp = sign * value * 0.25;
            if (temp is < -80 or > 60)
            {
                return null; // Reasonable range
            }
        }

        double? pressure = null;
        if (pressureStatus == 1)
        {
            pressure = pressureRaw; // hPa
            if (pressure is < 100 or > 1200)
            {
                return null;
            }
        }

        // At least one field must be valid
        if (fom == null && windSpeed == null && windDir == null && temp == null && pressure == null)
        {
            return null;
        }

        return new Bds44MeteorologicalRoutine(fom, windSpeed, windDir, temp, pressure);
    }

    /// <summary>
    /// Tries to parse BDS 4,5 (Meteorological hazard report).
    /// </summary>
    private Bds45MeteorologicalHazard? TryParseBds45(byte[] mb)
    {
        // BDS 4,5 structure (simplified):
        // - Bit 1: Turbulence status
        // - Bits 2-3: Turbulence severity (0-3)
        // - Bit 4: Wind shear status
        // - Bits 5-6: Wind shear severity (0-3)
        // - Bit 7: Microburst status
        // - Bits 8-9: Microburst severity (0-3)
        // - Bit 10: Icing status
        // - Bits 11-12: Icing severity (0-3)
        // - Bit 13: Wake vortex status
        // - Bits 14-15: Wake vortex severity (0-3)
        // - Bit 16: Temperature status
        // - Bits 17-26: Temperature (10 bits, signed)
        // - Bit 27: Pressure status
        // - Bits 28-38: Pressure (11 bits)
        // - Bit 39: Radio height status
        // - Bits 40-51: Radio height (12 bits)
        // - Bits 52-56: Reserved

        int turbStatus = ExtractBits(mb, 1, 1);
        int turb = ExtractBits(mb, 2, 2);
        int wsStatus = ExtractBits(mb, 4, 1);
        int ws = ExtractBits(mb, 5, 2);
        int mbStatus = ExtractBits(mb, 7, 1);
        int mburst = ExtractBits(mb, 8, 2);
        int iceStatus = ExtractBits(mb, 10, 1);
        int ice = ExtractBits(mb, 11, 2);
        int wvStatus = ExtractBits(mb, 13, 1);
        int wv = ExtractBits(mb, 14, 2);
        int tempStatus = ExtractBits(mb, 16, 1);
        int tempRaw = ExtractBits(mb, 17, 10);
        int pressStatus = ExtractBits(mb, 27, 1);
        int pressRaw = ExtractBits(mb, 28, 11);
        int rhStatus = ExtractBits(mb, 39, 1);
        int rhRaw = ExtractBits(mb, 40, 12);

        Severity? turbulence = turbStatus == 1 ? (Severity)turb : null;
        Severity? windShear = wsStatus == 1 ? (Severity)ws : null;
        Severity? microburst = mbStatus == 1 ? (Severity)mburst : null;
        Severity? icing = iceStatus == 1 ? (Severity)ice : null;
        Severity? wakeVortex = wvStatus == 1 ? (Severity)wv : null;

        double? temp = null;
        if (tempStatus == 1)
        {
            int sign = (tempRaw & 0x200) != 0 ? -1 : 1;
            int value = tempRaw & 0x1FF;
            temp = sign * value * 0.25;
            if (temp is < -80 or > 60)
            {
                return null;
            }
        }

        double? pressure = null;
        if (pressStatus == 1)
        {
            pressure = pressRaw;
            if (pressure is < 100 or > 1200)
            {
                return null;
            }
        }

        int? radioHeight = null;
        if (rhStatus == 1)
        {
            radioHeight = rhRaw * 16; // 16 ft resolution
            if (radioHeight is < 0 or > 65520)
            {
                return null;
            }
        }

        // At least one field must be valid
        if (turbulence == null && windShear == null && microburst == null &&
            icing == null && wakeVortex == null && temp == null &&
            pressure == null && radioHeight == null)
        {
            return null;
        }

        return new Bds45MeteorologicalHazard(
            turbulence, windShear, microburst, icing, wakeVortex, temp, pressure, radioHeight);
    }

    /// <summary>
    /// Tries to parse BDS 5,0 (Track and turn report).
    /// </summary>
    private Bds50TrackAndTurn? TryParseBds50(byte[] mb)
    {
        // BDS 5,0 structure:
        // - Bit 1: Roll angle status
        // - Bits 2-11: Roll angle (10 bits, signed, 45/256 deg)
        // - Bit 12: Track angle status
        // - Bits 13-23: Track angle (11 bits, signed, 90/512 deg)
        // - Bit 24: Ground speed status
        // - Bits 25-34: Ground speed (10 bits, 2 kt resolution)
        // - Bit 35: Track angle rate status
        // - Bits 36-45: Track angle rate (10 bits, signed)
        // - Bit 46: True airspeed status
        // - Bits 47-56: True airspeed (10 bits, 2 kt resolution)

        int rollStatus = ExtractBits(mb, 1, 1);
        int rollRaw = ExtractBits(mb, 2, 10);
        int trackStatus = ExtractBits(mb, 12, 1);
        int trackRaw = ExtractBits(mb, 13, 11);
        int gsStatus = ExtractBits(mb, 24, 1);
        int gsRaw = ExtractBits(mb, 25, 10);
        int trackRateStatus = ExtractBits(mb, 35, 1);
        int trackRateRaw = ExtractBits(mb, 36, 10);
        int tasStatus = ExtractBits(mb, 46, 1);
        int tasRaw = ExtractBits(mb, 47, 10);

        double? roll = null;
        if (rollStatus == 1)
        {
            int sign = (rollRaw & 0x200) != 0 ? -1 : 1;
            int value = rollRaw & 0x1FF;
            roll = sign * value * (45.0 / 256.0);
            if (Math.Abs(roll.Value) > 50)
            {
                return null;
            }
        }

        double? track = null;
        if (trackStatus == 1)
        {
            int sign = (trackRaw & 0x400) != 0 ? -1 : 1;
            int value = trackRaw & 0x3FF;
            track = sign * value * (90.0 / 512.0);
            if (track < 0)
            {
                track += 360;
            }

            if (track is < 0 or >= 360)
            {
                return null;
            }
        }

        int? gs = null;
        if (gsStatus == 1)
        {
            gs = gsRaw * 2; // 2 kt resolution
            if (gs is < 0 or > 2046)
            {
                return null;
            }
        }

        // Track Rate (bits 35-45): Rate of change of ground track angle
        double? trackRate = null;
        if (trackRateStatus == 1)
        {
            // Track rate is signed 10-bit value with 1/4 degree/second resolution
            // Bit 36 is the sign bit, bits 37-45 are the magnitude
            int sign = (trackRateRaw & 0x200) != 0 ? -1 : 1;
            int value = trackRateRaw & 0x1FF;
            trackRate = sign * value * 0.25; // 1/4 degree/second (0.25°/s) resolution

            // Range validation: typical aircraft turn rates are within ±10 deg/s
            // Standard rate turn (3°/s) is most common, but military aircraft can exceed this
            if (Math.Abs(trackRate.Value) > 10)
            {
                return null; // Reject unreasonable values (likely corrupted data)
            }
        }

        int? tas = null;
        if (tasStatus == 1)
        {
            tas = tasRaw * 2; // 2 kt resolution
            if (tas is < 0 or > 2046)
            {
                return null;
            }
        }

        // At least one field must be valid
        if (roll == null && track == null && gs == null && trackRate == null && tas == null)
        {
            return null;
        }

        return new Bds50TrackAndTurn(roll, track, gs, tas, trackRate);
    }

    /// <summary>
    /// Tries to parse BDS 5,3 (Air-referenced state vector).
    /// </summary>
    private Bds53AirReferencedState? TryParseBds53(byte[] mb)
    {
        // BDS 5,3 structure:
        // - Bit 1: Magnetic heading status
        // - Bits 2-12: Magnetic heading (11 bits, 90/512 deg)
        // - Bit 13: IAS status
        // - Bits 14-23: IAS (10 bits, 1 kt resolution)
        // - Bit 24: Mach status
        // - Bits 25-34: Mach (10 bits, 0.008 resolution)
        // - Bit 35: TAS status
        // - Bits 36-45: TAS (10 bits, 2 kt resolution)
        // - Bits 46-56: Reserved

        int hdgStatus = ExtractBits(mb, 1, 1);
        int hdgRaw = ExtractBits(mb, 2, 11);
        int iasStatus = ExtractBits(mb, 13, 1);
        int iasRaw = ExtractBits(mb, 14, 10);
        int machStatus = ExtractBits(mb, 24, 1);
        int machRaw = ExtractBits(mb, 25, 10);
        int tasStatus = ExtractBits(mb, 35, 1);
        int tasRaw = ExtractBits(mb, 36, 10);

        double? hdg = null;
        if (hdgStatus == 1)
        {
            hdg = hdgRaw * (90.0 / 512.0);
            if (hdg is < 0 or >= 360)
            {
                return null;
            }
        }

        int? ias = null;
        if (iasStatus == 1)
        {
            ias = iasRaw;
            if (ias is < 0 or > 500)
            {
                return null;
            }
        }

        double? mach = null;
        if (machStatus == 1)
        {
            mach = machRaw * 0.008;
            if (mach is < 0 or > 1.0)
            {
                return null;
            }
        }

        int? tas = null;
        if (tasStatus == 1)
        {
            tas = tasRaw * 2;
            if (tas is < 0 or > 2046)
            {
                return null;
            }
        }

        // At least one field must be valid
        if (hdg == null && ias == null && mach == null && tas == null)
        {
            return null;
        }

        return new Bds53AirReferencedState(hdg, ias, mach, tas);
    }

    /// <summary>
    /// Tries to parse BDS 6,0 (Heading and speed report).
    /// </summary>
    private Bds60HeadingAndSpeed? TryParseBds60(byte[] mb)
    {
        // BDS 6,0 structure:
        // - Bit 1: Magnetic heading status
        // - Bits 2-12: Magnetic heading (11 bits, 90/512 deg)
        // - Bit 13: IAS status
        // - Bits 14-23: IAS (10 bits, 1 kt resolution)
        // - Bit 24: Mach status
        // - Bits 25-34: Mach (10 bits, 0.008 resolution)
        // - Bit 35: Barometric vertical rate status
        // - Bits 36-45: Baro VR (10 bits, signed, 32 ft/min resolution)
        // - Bit 46: Inertial vertical rate status
        // - Bits 47-56: Inertial VR (10 bits, signed, 32 ft/min resolution)

        int hdgStatus = ExtractBits(mb, 1, 1);
        int hdgRaw = ExtractBits(mb, 2, 11);
        int iasStatus = ExtractBits(mb, 13, 1);
        int iasRaw = ExtractBits(mb, 14, 10);
        int machStatus = ExtractBits(mb, 24, 1);
        int machRaw = ExtractBits(mb, 25, 10);
        int baroVrStatus = ExtractBits(mb, 35, 1);
        int baroVrRaw = ExtractBits(mb, 36, 10);
        int inerVrStatus = ExtractBits(mb, 46, 1);
        int inerVrRaw = ExtractBits(mb, 47, 10);

        double? hdg = null;
        if (hdgStatus == 1)
        {
            hdg = hdgRaw * (90.0 / 512.0);
            if (hdg is < 0 or >= 360)
            {
                return null;
            }
        }

        int? ias = null;
        if (iasStatus == 1)
        {
            ias = iasRaw;
            if (ias is < 0 or > 500)
            {
                return null;
            }
        }

        double? mach = null;
        if (machStatus == 1)
        {
            mach = machRaw * 0.008;
            if (mach is < 0 or > 1.0)
            {
                return null;
            }
        }

        int? baroVr = null;
        if (baroVrStatus == 1)
        {
            int sign = (baroVrRaw & 0x200) != 0 ? -1 : 1;
            int value = baroVrRaw & 0x1FF;
            baroVr = sign * value * 32; // 32 ft/min resolution
            if (Math.Abs(baroVr.Value) > 6000)
            {
                return null;
            }
        }

        int? inerVr = null;
        if (inerVrStatus == 1)
        {
            int sign = (inerVrRaw & 0x200) != 0 ? -1 : 1;
            int value = inerVrRaw & 0x1FF;
            inerVr = sign * value * 32; // 32 ft/min resolution
            if (Math.Abs(inerVr.Value) > 6000)
            {
                return null;
            }
        }

        // At least one field must be valid
        if (hdg == null && ias == null && mach == null && baroVr == null && inerVr == null)
        {
            return null;
        }

        return new Bds60HeadingAndSpeed(hdg, ias, mach, baroVr, inerVr);
    }

}
