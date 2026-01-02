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
/// <remarks>
/// Supported BDS codes (10 total):
///
/// Elementary Surveillance (ELS):
/// - BDS 1,0: Data link capability report
/// - BDS 1,7: Common usage GICB capability report
/// - BDS 2,0: Aircraft identification
/// - BDS 3,0: ACAS active resolution advisory
///
/// Enhanced Surveillance (EHS):
/// - BDS 4,0: Selected vertical intention
/// - BDS 4,4: Meteorological routine air report
/// - BDS 4,5: Meteorological hazard report
/// - BDS 5,0: Track and turn report
/// - BDS 5,3: Air-referenced state vector
/// - BDS 6,0: Heading and speed report
///
/// See InferBdsCode method for BDS inference strategy and validation rules.
/// </remarks>
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
        // Note: The bit alignment is handled by ExtractBits function within BDS parsers
        byte[] mb = new byte[7];
        Array.Copy(frame.Data, 4, mb, 0, 7);

        // Infer BDS code and parse data
        (BdsCode bdsCode, BdsData? bdsData) = InferBds(mb);

        // Track BDS code statistics
        _messagesByBDS[bdsCode]++;

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
        // Note: The bit alignment is handled by ExtractBits function within BDS parsers
        byte[] mb = new byte[7];
        Array.Copy(frame.Data, 4, mb, 0, 7);

        // Infer BDS code and parse data
        (BdsCode bdsCode, BdsData? bdsData) = InferBds(mb);

        // Track BDS code statistics
        _messagesByBDS[bdsCode]++;

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
    /// <returns>Always <see langword="null"/> - DF 24 is intentionally not implemented.</returns>
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
    /// <returns>Tuple of (BdsCode enum, parsed BdsData or <see langword="null"/>).</returns>
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

        // Try BDS 1,7 (Common usage GICB capability report) - uses inference, not explicit ID
        Bds17GicbCapability? bds17 = TryParseBds17(mb);
        if (bds17 != null)
        {
            return (BdsCode.Bds17, bds17);
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

        // Reserved bits 10-14 must be 0 (per pyModeS line 30 and readsb line 113)
        // Extract reserved field - bits 10-14 (5 bits)
        int reserved = ExtractBits(mb, 10, 5);
        if (reserved != 0)
        {
            return null;
        }

        // Overlay capability conflict validation (per pyModeS lines 34-37)
        // Bit 15 is overlay capability indicator
        // Bits 17-23 contain DTI (Data link Terminal Identifier)
        int overlayCapable = ExtractBits(mb, 15, 1);
        int dti = ExtractBits(mb, 17, 7);

        // If overlay capable, DTI must be >= 5; if not overlay capable, DTI must be <= 4
        if (overlayCapable == 1 && dti < 5)
        {
            return null; // Invalid: overlay capable but DTI < 5
        }
        if (overlayCapable == 0 && dti > 4)
        {
            return null; // Invalid: not overlay capable but DTI > 4
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
        // BDS 1,7 validation per readsb lines 132-134:
        // Reserved bits 25-56 must all be zero
        // Extract reserved field - bits 25-56 (32 bits)
        int reserved = ExtractBits(mb, 25, 32);
        if (reserved != 0)
        {
            return null;
        }

        // BDS 2,0 (Aircraft Identification) capability is required (per pyModeS lines 37-38)
        // Bit 7 in the capability mask corresponds to BDS 2,0
        int bds20Capability = ExtractBits(mb, 7, 1);
        if (bds20Capability == 0)
        {
            return null; // BDS20 support is mandatory for valid BDS 1,7
        }

        // Extract capability mask (bits 1-24, representing which BDS codes are supported)
        // Each bit corresponds to a BDS register capability
        ulong capabilityMask = 0;
        for (int i = 0; i < 3; i++) // First 3 bytes = bits 1-24
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

        // Allow empty callsign (bits 9-56 all zero) per pyModeS lines 28-29
        bool isEmpty = mb[1] == 0 && mb[2] == 0 && mb[3] == 0 &&
                       mb[4] == 0 && mb[5] == 0 && mb[6] == 0;
        if (isEmpty)
        {
            return new Bds20AircraftIdentification(string.Empty);
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
    /// </summary>
    private Bds30AcasResolutionAdvisory? TryParseBds30(byte[] mb)
    {
        // BDS 3,0: First byte (VDS) = 0x30
        if (mb[0] != 0x30)
        {
            return null;
        }

        // Threat type validation: bits 29-30 must not be "11" (3 = not assigned)
        // per pyModeS lines 28-29
        int threatType = ExtractBits(mb, 29, 2);
        if (threatType == 3)
        {
            return null; // Threat type 3 is not assigned
        }

        // ACAS III reserved check: bits 16-22 must be < 48 (reserved for future ACAS III)
        // per pyModeS lines 32-33
        int acasReserved = ExtractBits(mb, 16, 7);
        if (acasReserved >= 48)
        {
            return null; // Reserved for ACAS III (far future)
        }

        return new Bds30AcasResolutionAdvisory();
    }

    /// <summary>
    /// Tries to parse BDS 4,0 (Selected vertical intention).
    /// </summary>
    private Bds40SelectedVerticalIntention? TryParseBds40(byte[] mb)
    {
        // BDS 4,0: No fixed identifier, use status bits and range checks (per ICAO Annex 10 Vol IV)
        // Structure:
        // - Bit 1: MCP/FCU selected altitude status
        // - Bits 2-13: MCP/FCU selected altitude (12 bits, range: 0-65,520 ft, resolution: 16 ft)
        // - Bit 14: FMS selected altitude status
        // - Bits 15-26: FMS selected altitude (12 bits, range: 0-65,520 ft, resolution: 16 ft)
        // - Bit 27: Barometric pressure setting status
        // - Bits 28-39: Barometric pressure (12 bits, range: 0-204.775 inHg or 0-6931 mb, resolution: 0.05 inHg or 0.8 mb)
        // - Bits 40-47: Reserved (should be 0)
        // - Bit 48: Mode status
        // - Bits 49-51: Mode (3 bits, values: 0=Unknown, 1=VNAV, 2=ALT HOLD, 3=APPROACH, 4=Reserved)
        // - Bits 52-53: Reserved (should be 0)
        // - Bit 54: Source status
        // - Bits 55-56: Source (2 bits, values: 0=Unknown, 1=Aircraft, 2=MCP/FCU, 3=FMS)

        int mcpStatus = ExtractBits(mb, 1, 1);
        int mcpAltRaw = ExtractBits(mb, 2, 12);
        int fmsStatus = ExtractBits(mb, 14, 1);
        int fmsAltRaw = ExtractBits(mb, 15, 12);
        int baroStatus = ExtractBits(mb, 27, 1);
        int baroRaw = ExtractBits(mb, 28, 12);

        // Validation: check reserved bits 40-47 (per pyModeS line 46 and readsb line 361)
        int reserved1 = ExtractBits(mb, 40, 8);
        if (reserved1 != 0)
        {
            return null;
        }

        // Extract mode and source fields (per readsb lines 362-366)
        int modeStatus = ExtractBits(mb, 48, 1);
        int modeRaw = ExtractBits(mb, 49, 3);

        // Validation: check reserved bits 52-53 (per pyModeS line 49 and readsb line 364)
        int reserved2 = ExtractBits(mb, 52, 2);
        if (reserved2 != 0)
        {
            return null;
        }

        int sourceStatus = ExtractBits(mb, 54, 1);
        int sourceRaw = ExtractBits(mb, 55, 2);

        // Decode MCP/FCU selected altitude
        int? mcpAlt = null;
        if (mcpStatus == 1 && mcpAltRaw != 0)
        {
            // 16 ft resolution, range 0-65520 ft
            mcpAlt = mcpAltRaw * 16;
            if (mcpAlt is < 0 or > 65520)
            {
                return null; // Outside valid range
            }
        }

        // Decode FMS selected altitude
        int? fmsAlt = null;
        if (fmsStatus == 1 && fmsAltRaw != 0)
        {
            // 16 ft resolution, range 0-65520 ft
            fmsAlt = fmsAltRaw * 16;
            if (fmsAlt is < 0 or > 65520)
            {
                return null; // Outside valid range
            }
        }

        // Decode barometric pressure setting
        double? baro = null;
        if (baroStatus == 1 && baroRaw != 0)
        {
            // 0.1 mbar resolution, offset 800 mbar, range 800-1209.4 mbar
            baro = 800.0 + (baroRaw * 0.1);
            if (baro is < 800 or > 1200)
            {
                return null; // Outside reasonable atmospheric pressure range
            }
        }

        // Decode navigation mode (VNAV, ALT HOLD, APPROACH flags)
        Bds40NavigationMode? navMode = null;
        if (modeStatus == 1)
        {
            // 3-bit field represents active navigation modes as flags
            navMode = (Bds40NavigationMode)modeRaw;
        }

        // Decode altitude source (Unknown, Aircraft, MCP/FCU, FMS)
        Bds40AltitudeSource? altSource = null;
        if (sourceStatus == 1)
        {
            // Only decode if value maps to a defined enum member
            if (Enum.IsDefined(typeof(Bds40AltitudeSource), sourceRaw))
            {
                altSource = (Bds40AltitudeSource)sourceRaw;
            }
        }

        // BDS 4,0 requires at least one valid field to be considered a match
        // This prevents false positives when trying to infer the BDS code
        if (mcpAlt == null && fmsAlt == null && baro == null && navMode == null && altSource == null)
        {
            return null; // No valid data found
        }

        return new Bds40SelectedVerticalIntention(mcpAlt, fmsAlt, baro, navMode, altSource);
    }

    /// <summary>
    /// Tries to parse BDS 4,4 (Meteorological routine report).
    /// </summary>
    private Bds44MeteorologicalRoutine? TryParseBds44(byte[] mb)
    {
        // BDS 4,4 structure (per ICAO Annex 10 Vol IV, 1090MHz Riddle, readsb):
        // - Bits 1-4: Figure of Merit / Source (4 bits, range: 0-6, no status bit)
        // - Bit 5: Wind valid (applies to BOTH speed and direction)
        // - Bits 6-14: Wind speed (9 bits, range: 0-511 kt, resolution: 1 kt)
        // - Bits 15-23: Wind direction (9 bits, range: 0-360°, resolution: 180/256 deg, NO separate status bit)
        // - Bit 24: Temperature sign (1=negative, part of temp field, NOT a status bit)
        // - Bits 25-34: Temperature (10 bits total with sign, range: -128 to +128°C, resolution: 0.25°C)
        // - Bit 35: Pressure status
        // - Bits 36-46: Pressure (11 bits, range: 0-2048 hPa, resolution: 1 hPa)
        // - Bit 47: Turbulence status
        // - Bits 48-49: Turbulence (2 bits, values: 0=NIL, 1=Light, 2=Moderate, 3=Severe)
        // - Bit 50: Humidity status
        // - Bits 51-56: Humidity (6 bits, range: 0-100%, resolution: 100/64 = 1.5625%)

        int fomRaw = ExtractBits(mb, 1, 4);
        int windValid = ExtractBits(mb, 5, 1);
        int windSpeedRaw = ExtractBits(mb, 6, 9);
        int windDirRaw = ExtractBits(mb, 15, 9);
        int tempSign = ExtractBits(mb, 24, 1);
        int tempRaw = ExtractBits(mb, 25, 10);
        int pressureStatus = ExtractBits(mb, 35, 1);
        int pressureRaw = ExtractBits(mb, 36, 11);
        int turbulenceStatus = ExtractBits(mb, 47, 1);
        int turbulenceRaw = ExtractBits(mb, 48, 2);
        int humidityStatus = ExtractBits(mb, 50, 1);
        int humidityRaw = ExtractBits(mb, 51, 6);

        // Decode Figure of Merit (data quality/source indicator)
        // Always present (no status bit), valid range 0-6
        if (fomRaw is < 0 or > 6)
        {
            return null; // Invalid FOM value
        }
        int? fom = fomRaw;

        // Decode wind speed and direction (both controlled by single status bit)
        int? windSpeed = null;
        double? windDir = null;
        if (windValid == 1)
        {
            // Wind speed in knots, range 0-511 kt
            windSpeed = windSpeedRaw;
            if (windSpeed is < 0 or > 511)
            {
                return null; // Unrealistic wind speed
            }

            // Wind direction with 180/256 degree resolution (0.703125°/LSB)
            windDir = windDirRaw * (180.0 / 256.0);
            if (windDir is < 0 or > 360)
            {
                return null; // Invalid direction
            }
        }
        else if (windSpeedRaw != 0)
        {
            // Validation: if wind not valid, raw speed must be zero
            return null;
        }

        // Decode static air temperature (always present, no status bit)
        // Sign-magnitude representation: bit 24 is sign, bits 25-34 are magnitude
        double? temp = null;
        int tempValue = tempSign == 1 ? tempRaw - 1024 : tempRaw;
        temp = tempValue * 0.25; // 0.25°C resolution
        if (temp is < -128 or > 128)
        {
            return null; // Outside reasonable temperature range (-128°C to +128°C)
        }

        // Decode static pressure
        double? pressure = null;
        if (pressureStatus == 1)
        {
            // Direct value in hPa (millibars), range 0-2048 hPa
            pressure = pressureRaw;
            if (pressure is < 0 or > 2048)
            {
                return null; // Outside valid pressure range
            }
        }
        else if (pressureRaw != 0)
        {
            // Validation: if pressure not valid, raw value must be zero
            return null;
        }

        // Decode turbulence severity (0=Nil, 1=Light, 2=Moderate, 3=Severe)
        Severity? turbulence = null;
        if (turbulenceStatus == 1)
        {
            if (!Enum.IsDefined(typeof(Severity), turbulenceRaw))
            {
                return null; // Invalid severity value
            }
            turbulence = (Severity)turbulenceRaw;
        }
        else if (turbulenceRaw != 0)
        {
            // Validation: if turbulence not valid, raw value must be zero
            return null;
        }

        // Decode relative humidity
        double? humidity = null;
        if (humidityStatus == 1)
        {
            // Convert 6-bit value to percentage: raw * (100/64) = raw * 1.5625%
            humidity = humidityRaw * (100.0 / 64.0);
            if (humidity is < 0 or > 100)
            {
                return null; // Invalid percentage
            }
        }
        else if (humidityRaw != 0)
        {
            // Validation: if humidity not valid, raw value must be zero
            return null;
        }

        // BDS 4,4 requires at least one valid field to be considered a match
        if (fom == null && windSpeed == null && windDir == null && temp == null &&
            pressure == null && turbulence == null && humidity == null)
        {
            return null; // No valid data found
        }

        return new Bds44MeteorologicalRoutine(fom, windSpeed, windDir, temp, pressure, turbulence, humidity);
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

        // Validate all severity enums before decoding (valid: 0-3, 2-bit fields)
        // This prevents invalid enum casts that could cause runtime errors
        if (turbStatus == 1 && !Enum.IsDefined(typeof(Severity), turb))
        {
            return null; // Invalid turbulence severity
        }
        if (wsStatus == 1 && !Enum.IsDefined(typeof(Severity), ws))
        {
            return null; // Invalid wind shear severity
        }
        if (mbStatus == 1 && !Enum.IsDefined(typeof(Severity), mburst))
        {
            return null; // Invalid microburst severity
        }
        if (iceStatus == 1 && !Enum.IsDefined(typeof(Severity), ice))
        {
            return null; // Invalid icing severity
        }
        if (wvStatus == 1 && !Enum.IsDefined(typeof(Severity), wv))
        {
            return null; // Invalid wake vortex severity
        }

        // Decode hazard severity levels (only if status bit indicates presence)
        Severity? turbulence = turbStatus == 1 ? (Severity)turb : null;
        Severity? windShear = wsStatus == 1 ? (Severity)ws : null;
        Severity? microburst = mbStatus == 1 ? (Severity)mburst : null;
        Severity? icing = iceStatus == 1 ? (Severity)ice : null;
        Severity? wakeVortex = wvStatus == 1 ? (Severity)wv : null;

        // Decode static air temperature
        double? temp = null;
        if (tempStatus == 1)
        {
            // Two's complement for 10-bit signed value, 0.25°C resolution
            int value = (tempRaw & 0x200) != 0 ? tempRaw - 1024 : tempRaw;
            temp = value * 0.25;
            if (temp is < -80 or > 60)
            {
                return null; // Outside reasonable atmospheric temperature range
            }
        }

        // Decode static pressure
        double? pressure = null;
        if (pressStatus == 1)
        {
            // Direct value in hPa (millibars), range 100-1200 hPa
            pressure = pressRaw;
            if (pressure is < 100 or > 1200)
            {
                return null; // Outside valid pressure range
            }
        }

        // Decode radio altimeter height
        int? radioHeight = null;
        if (rhStatus == 1)
        {
            // 16 ft resolution, measures height above ground
            radioHeight = rhRaw * 16;
            if (radioHeight is < 0 or > 65520)
            {
                return null; // Outside valid range
            }
        }

        // BDS 4,5 requires at least one valid field to be considered a match
        if (turbulence == null && windShear == null && microburst == null &&
            icing == null && wakeVortex == null && temp == null &&
            pressure == null && radioHeight == null)
        {
            return null; // No valid data found
        }

        return new Bds45MeteorologicalHazard(
            turbulence, windShear, microburst, icing, wakeVortex, temp, pressure, radioHeight);
    }

    /// <summary>
    /// Tries to parse BDS 5,0 (Track and turn report).
    /// </summary>
    private Bds50TrackAndTurn? TryParseBds50(byte[] mb)
    {
        // BDS 5,0 structure (per ICAO Annex 10 Vol IV):
        // - Bit 1: Roll angle status
        // - Bits 2-11: Roll angle (10 bits, signed, range: -90 to +90°, resolution: 45/256 deg = 0.176°)
        // - Bit 12: Track angle status
        // - Bits 13-23: Track angle (11 bits, signed, range: -180 to +180°, resolution: 90/512 deg = 0.176°)
        // - Bit 24: Ground speed status
        // - Bits 25-34: Ground speed (10 bits, range: 0-2046 kt, resolution: 2 kt)
        // - Bit 35: Track angle rate status
        // - Bits 36-45: Track angle rate (10 bits, signed, range: -16 to +16 deg/s, resolution: 8/256 deg/s)
        // - Bit 46: True airspeed status
        // - Bits 47-56: True airspeed (10 bits, range: 0-2046 kt, resolution: 2 kt)

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

        // BDS 5,0 validation: ALL four primary fields must have valid status bits
        // This is a key discriminator for BDS 5,0 vs other registers
        if (rollStatus == 0 || trackStatus == 0 || gsStatus == 0 || tasStatus == 0)
        {
            return null; // Missing required status bits
        }

        // Decode roll angle
        double? roll = null;
        if (rollStatus == 1)
        {
            // Two's complement for 10-bit signed value
            // Resolution: 45/256 = 0.175781 degrees per LSB
            int value = (rollRaw & 0x200) != 0 ? rollRaw - 1024 : rollRaw;
            roll = value * (45.0 / 256.0);
            if (Math.Abs(roll.Value) > 50)
            {
                return null; // Unrealistic roll angle
            }
        }

        // Decode track angle (true track over ground)
        double? track = null;
        if (trackStatus == 1)
        {
            // Two's complement for 11-bit signed value
            // Resolution: 90/512 = 0.175781 degrees per LSB
            int value = (trackRaw & 0x400) != 0 ? trackRaw - 2048 : trackRaw;
            track = value * (90.0 / 512.0);
            // Normalize negative angles to 0-360 range
            if (track < 0)
            {
                track += 360;
            }

            if (track is < 0 or >= 360)
            {
                return null; // Invalid track angle
            }
        }

        // Decode ground speed
        int? gs = null;
        if (gsStatus == 1)
        {
            // 2 knot resolution, range 0-2046 kt
            gs = gsRaw * 2;
            if (gs is < 0 or > 2046)
            {
                return null; // Unrealistic ground speed
            }
        }

        // Decode track angle rate of change
        double? trackRate = null;
        if (trackRateStatus == 1)
        {
            // Two's complement for 10-bit signed value
            // Resolution: 8/256 = 0.03125 degrees per second per LSB
            int value = (trackRateRaw & 0x200) != 0 ? trackRateRaw - 1024 : trackRateRaw;
            trackRate = value * (8.0 / 256.0);

            // Typical aircraft turn rates: ±10 deg/s maximum
            // Standard rate turn is 3°/s, tight turns up to 10°/s
            if (Math.Abs(trackRate.Value) > 10)
            {
                return null; // Unrealistic turn rate
            }
        }

        // Decode true airspeed
        int? tas = null;
        if (tasStatus == 1)
        {
            // 2 knot resolution, range 0-2046 kt
            tas = tasRaw * 2;
            if (tas is < 0 or > 2046)
            {
                return null; // Unrealistic airspeed
            }
        }

        // Cross-field validation: ground speed and true airspeed should be reasonably close
        // Difference is due to wind; excessive difference indicates corrupt data
        if (gs != null && tas != null)
        {
            int delta = Math.Abs(gs.Value - tas.Value);
            if (delta > 200)
            {
                return null; // Unrealistic GS-TAS difference (>200 kt wind)
            }
        }

        // BDS 5,0 requires at least one valid field to be considered a match
        if (roll == null && track == null && gs == null && trackRate == null && tas == null)
        {
            return null; // No valid data found
        }

        return new Bds50TrackAndTurn(roll, track, gs, tas, trackRate);
    }

    /// <summary>
    /// Tries to parse BDS 5,3 (Air-referenced state vector).
    /// </summary>
    private Bds53AirReferencedState? TryParseBds53(byte[] mb)
    {
        // BDS 5,3 structure (per ICAO Annex 10 Vol IV):
        // - Bit 1: Magnetic heading status
        // - Bits 2-12: Magnetic heading (11 bits sign-magnitude, range: 0-360°, resolution: 90/512 deg = 0.176°)
        // - Bit 13: IAS status
        // - Bits 14-23: IAS (10 bits, range: 0-1023 kt, resolution: 1 kt)
        // - Bit 24: Mach status
        // - Bits 25-33: Mach (9 bits, range: 0.0-4.088, resolution: 0.008 Mach)
        // - Bit 34: TAS status
        // - Bits 35-46: TAS (12 bits, range: 0-2047.5 kt, resolution: 0.5 kt)
        // - Bit 47: Vertical rate status
        // - Bit 48: Vertical rate sign (1=descending, 0=climbing)
        // - Bits 49-56: Vertical rate magnitude (8 bits, range: 0-16,320 ft/min, resolution: 64 ft/min)

        int hdgStatus = ExtractBits(mb, 1, 1);
        int hdgRaw = ExtractBits(mb, 2, 11);
        int iasStatus = ExtractBits(mb, 13, 1);
        int iasRaw = ExtractBits(mb, 14, 10);
        int machStatus = ExtractBits(mb, 24, 1);
        int machRaw = ExtractBits(mb, 25, 9);
        int tasStatus = ExtractBits(mb, 34, 1);
        int tasRaw = ExtractBits(mb, 35, 12);
        int vrStatus = ExtractBits(mb, 47, 1);
        int vrSign = ExtractBits(mb, 48, 1);
        int vrRaw = ExtractBits(mb, 49, 8);

        // Decode magnetic heading
        double? hdg = null;
        if (hdgStatus == 1)
        {
            // Resolution: 90/512 = 0.175781 degrees per LSB
            hdg = hdgRaw * (90.0 / 512.0);
            if (hdg is < 0 or >= 360)
            {
                return null; // Invalid heading
            }
        }

        // Decode indicated airspeed
        int? ias = null;
        if (iasStatus == 1)
        {
            // 1 knot resolution, range 0-500 kt
            ias = iasRaw;
            if (ias is < 0 or > 500)
            {
                return null; // Unrealistic IAS
            }
        }

        // Decode Mach number
        double? mach = null;
        if (machStatus == 1)
        {
            // Resolution: 0.008 Mach per LSB
            mach = machRaw * 0.008;
            if (mach is < 0 or > 1.0)
            {
                return null; // Unrealistic Mach (subsonic aircraft limit)
            }
        }

        // Decode true airspeed
        int? tas = null;
        if (tasStatus == 1)
        {
            // 0.5 knot resolution, range 0-2000 kt
            tas = (int)(tasRaw * 0.5);
            if (tas is < 0 or > 500)
            {
                return null; // Unrealistic TAS
            }
        }

        // Decode inertial vertical rate
        int? vr = null;
        if (vrStatus == 1)
        {
            // Special encoding: all zeros (0) or all ones (255) means 0 ft/min
            if (vrRaw == 0 || vrRaw == 255)
            {
                vr = 0;
            }
            else
            {
                // Sign-magnitude encoding: sign bit determines positive/negative
                int value = vrSign == 1 ? -vrRaw : vrRaw;
                vr = value * 64; // 64 ft/min resolution

                // Typical aircraft vertical rates: ±8000 ft/min
                if (Math.Abs(vr.Value) > 8000)
                {
                    return null; // Unrealistic climb/descent rate
                }
            }
        }

        // BDS 5,3 requires at least one valid field to be considered a match
        if (hdg == null && ias == null && mach == null && tas == null && vr == null)
        {
            return null; // No valid data found
        }

        return new Bds53AirReferencedState(hdg, ias, mach, tas, vr);
    }

    /// <summary>
    /// Tries to parse BDS 6,0 (Heading and speed report).
    /// </summary>
    private Bds60HeadingAndSpeed? TryParseBds60(byte[] mb)
    {
        // BDS 6,0 structure (per ICAO Annex 10 Vol IV):
        // - Bit 1: Magnetic heading status
        // - Bits 2-12: Magnetic heading (11 bits, range: 0-360°, resolution: 90/512 deg = 0.176°)
        // - Bit 13: IAS status
        // - Bits 14-23: IAS (10 bits, range: 0-1023 kt, resolution: 1 kt)
        // - Bit 24: Mach status
        // - Bits 25-34: Mach (10 bits, range: 0.0-8.184, resolution: 0.008 Mach)
        // - Bit 35: Barometric vertical rate status
        // - Bits 36-45: Baro VR (10 bits, signed, range: ±16,320 ft/min, resolution: 32 ft/min)
        // - Bit 46: Inertial vertical rate status
        // - Bits 47-56: Inertial VR (10 bits, signed, range: ±16,320 ft/min, resolution: 32 ft/min)

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

        // Decode magnetic heading
        double? hdg = null;
        if (hdgStatus == 1)
        {
            // Unsigned value, resolution: 90/512 = 0.175781 degrees per LSB
            hdg = hdgRaw * (90.0 / 512.0);
            if (hdg is < 0 or >= 360)
            {
                return null; // Invalid heading
            }
        }

        // Decode indicated airspeed
        int? ias = null;
        if (iasStatus == 1)
        {
            // 1 knot resolution, range 0-500 kt
            ias = iasRaw;
            if (ias is < 0 or > 500)
            {
                return null; // Unrealistic IAS
            }
        }

        // Decode Mach number
        double? mach = null;
        if (machStatus == 1)
        {
            // Resolution: 0.004 Mach per LSB (BDS 6,0 uses 0.004, BDS 5,3 uses 0.008)
            mach = machRaw * 0.004;
            if (mach is < 0 or > 1.0)
            {
                return null; // Unrealistic Mach (subsonic aircraft limit)
            }
        }

        // Decode barometric vertical rate
        int? baroVr = null;
        if (baroVrStatus == 1)
        {
            // Two's complement for 10-bit signed value
            // Resolution: 32 ft/min per LSB
            int value = (baroVrRaw & 0x200) != 0 ? baroVrRaw - 1024 : baroVrRaw;
            baroVr = value * 32;
            if (Math.Abs(baroVr.Value) > 6000)
            {
                return null; // Unrealistic climb/descent rate
            }
        }

        // Decode inertial vertical rate
        int? inerVr = null;
        if (inerVrStatus == 1)
        {
            // Two's complement for 10-bit signed value
            // Resolution: 32 ft/min per LSB
            int value = (inerVrRaw & 0x200) != 0 ? inerVrRaw - 1024 : inerVrRaw;
            inerVr = value * 32;
            if (Math.Abs(inerVr.Value) > 6000)
            {
                return null; // Unrealistic climb/descent rate
            }
        }

        // BDS 6,0 requires at least one valid field to be considered a match
        if (hdg == null && ias == null && mach == null && baroVr == null && inerVr == null)
        {
            return null; // No valid data found
        }

        return new Bds60HeadingAndSpeed(hdg, ias, mach, baroVr, inerVr);
    }

}
