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
/// MessageParser partial class: ACAS coordination messages (DF 0, DF 16).
/// Handles short and long air-air surveillance with ACAS field decoding.
/// </summary>
public sealed partial class MessageParser
{
    /// <summary>
    /// Parses short air-air surveillance from Downlink Format 0.
    /// ACAS coordination message containing altitude and ACAS status fields.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Short air-air surveillance message with ACAS data, or <see langword="null"/> if invalid.</returns>
    /// <remarks>
    /// DF 0 structure (56 bits total, per ICAO Annex 10 Vol IV):
    /// - Bits 1-5: DF (Downlink Format)
    /// - Bit 6: Vertical Status (VS)
    /// - Bit 7: Cross-link Capability (CC)
    /// - Bit 8: Reserved
    /// - Bits 9-11: Sensitivity Level (SL)
    /// - Bits 12-13: Reserved
    /// - Bits 14-17: Reply Information (RI) - ACAS status or maximum airspeed
    /// - Bits 18-19: Reserved
    /// - Bits 20-32: Altitude Code (AC)
    /// - Bits 33-56: Address Parity (AP)
    ///
    /// DF 0 is used for air-air surveillance (aircraft-to-aircraft).
    /// Unlike DF 4 (ground surveillance), DF 0 contains air-air specific fields (VS, CC, SL, RI).
    /// The RI field indicates either ACAS operational capabilities (0,2,3,4) or maximum airspeed (8-14).
    /// </remarks>
    private ModeSMessage? ParseShortAirAirSurveillance(ValidatedFrame frame)
    {
        // Extract Vertical Status (VS) - bit 6 (1 bit, 0=airborne, 1=ground)
        VerticalStatus verticalStatus = ExtractBits(frame.Data, 6, 1) == 0
            ? VerticalStatus.Airborne
            : VerticalStatus.Ground;

        // Extract Cross-link Capability (CC) - bit 7 (1 bit)
        bool crossLinkCapability = ExtractBits(frame.Data, 7, 1) != 0;

        // Extract Sensitivity Level (SL) - bits 9-11 (3 bits)
        int sensitivityLevel = ExtractBits(frame.Data, 9, 3);

        // Extract Reply Information (RI) - bits 14-17 (4 bits)
        // RI indicates either ACAS operational status (0,2,3,4) or maximum airspeed capability (8-14)
        int riRaw = ExtractBits(frame.Data, 14, 4);

        // Validate RI field (valid values: 0, 2, 3, 4, 8-14; reserved: 1, 5, 6, 7, 15)
        if (!Enum.IsDefined(typeof(AcasReplyInformation), riRaw))
        {
            Log.Debug("Reserved RI value {RI} in DF 0 from {Icao} (valid: 0,2,3,4,8-14), frame: {Frame}",
                riRaw, frame.IcaoAddress, frame.Data);
            return null;
        }

        var replyInformation = (AcasReplyInformation)riRaw;

        // Extract Altitude Code (AC) - bits 20-32 (13 bits)
        int altitudeCode = ExtractBits(frame.Data, 20, 13);

        // Decode altitude (null if invalid or unavailable)
        Altitude? altitude = DecodeAltitudeAC13(altitudeCode);

        return new ShortAirAirSurveillance(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            altitude,
            verticalStatus,
            crossLinkCapability,
            sensitivityLevel,
            replyInformation);
    }

    /// <summary>
    /// Parses long air-air surveillance from Downlink Format 16.
    /// ACAS coordination message with partial MV decoding (extracts VDS, RAC, RAT, MTE only).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Long air-air surveillance message with ACAS data, or <see langword="null"/> if invalid.</returns>
    /// <remarks>
    /// DF 16 structure (112 bits total, per ICAO Annex 10 Vol IV):
    /// - Bits 1-5: DF (Downlink Format)
    /// - Bit 6: Vertical Status (VS)
    /// - Bits 7-8: Reserved
    /// - Bits 9-11: Sensitivity Level (SL)
    /// - Bits 12-13: Reserved
    /// - Bits 14-17: Reply Information (RI) - ACAS status or maximum airspeed
    /// - Bits 18-19: Reserved
    /// - Bits 20-32: Altitude Code (AC)
    /// - Bits 33-88: Message Vertical (MV) - ACAS data (56 bits)
    /// - Bits 89-112: Address Parity (AP)
    ///
    /// MV field structure (56 bits):
    /// - Bits 33-40: VDS (Vertical Data Source, VDS1+VDS2, 8 bits)
    /// - Bits 41-54: ARA (Active Resolution Advisories, 14 bits) - SKIPPED
    /// - Bits 55-58: RAC (Resolution Advisory Complement, 4 bits)
    /// - Bit 59: RAT (Resolution Advisory Terminated)
    /// - Bit 60: MTE (Multiple Threat Encounter)
    /// - Bits 61-88: Reserved
    ///
    /// Partial MV decoding approach:
    /// This implementation decodes essential ACAS fields but skips the complex ARA field (bits 41-54)
    /// which requires conditional parsing based on threat encounter flags and context.
    /// - Extract VDS field (bits 33-40), validate it's 0x30 for valid ACAS
    /// - Extract RAC (Resolution Advisory Complement, bits 55-58)
    /// - Extract RAT (Resolution Advisory Terminated, bit 59)
    /// - Extract MTE (Multiple Threat Encounter, bit 60)
    /// - ARA field decoding requires interpreting MB:9 and MB:28 threat types and contexts
    /// - See ICAO Annex 10 Vol IV, 3.1.2.6.10 for full ARA decoding specification
    /// </remarks>
    private ModeSMessage? ParseLongAirAirSurveillance(ValidatedFrame frame)
    {
        // Extract Vertical Status (VS) - bit 6 (1 bit, 0=airborne, 1=ground)
        VerticalStatus verticalStatus = ExtractBits(frame.Data, 6, 1) == 0
            ? VerticalStatus.Airborne
            : VerticalStatus.Ground;

        // Extract Sensitivity Level (SL) - bits 9-11 (3 bits)
        int sensitivityLevel = ExtractBits(frame.Data, 9, 3);

        // Extract Altitude Code (AC) - bits 20-32 (13 bits)
        int altitudeCode = ExtractBits(frame.Data, 20, 13);

        // Decode altitude (null if invalid or unavailable)
        Altitude? altitude = DecodeAltitudeAC13(altitudeCode);

        // Extract Reply Information (RI) - bits 14-17 (4 bits)
        // RI indicates either ACAS operational status (0,2,3,4) or maximum airspeed capability (8-14)
        int riRaw = ExtractBits(frame.Data, 14, 4);

        // Validate RI field (valid values: 0, 2, 3, 4, 8-14; reserved: 1, 5, 6, 7, 15)
        if (!Enum.IsDefined(typeof(AcasReplyInformation), riRaw))
        {
            Log.Debug("Reserved RI value {RI} in DF 16 from {Icao} (valid: 0,2,3,4,8-14), frame: {Frame}",
                riRaw, frame.IcaoAddress, frame.Data);
            return null;
        }

        var replyInformation = (AcasReplyInformation)riRaw;

        // Extract MV field (Message Vertical, bits 33-88, 56 bits)
        // Extract VDS (Vertical Data Source) - bits 33-40 (8 bits, VDS1+VDS2)
        int vds = ExtractBits(frame.Data, 33, 8);

        // Check if VDS indicates valid ACAS data (VDS = 0x30 = binary 0011 0000)
        // VDS1=3, VDS2=0 indicates coordinated ACAS reply with resolution advisory data
        bool acasValid = vds == 0x30;

        bool? resolutionAdvisoryTerminated = null;
        bool? multipleThreatEncounter = null;
        bool? racNotBelow = null;
        bool? racNotAbove = null;
        bool? racNotLeft = null;
        bool? racNotRight = null;

        if (acasValid)
        {
            // Extract RAC (Resolution Advisory Complement) - bits 55-58 (4 bits)
            int rac = ExtractBits(frame.Data, 55, 4);
            racNotBelow = (rac & 0x08) != 0;   // bit 55
            racNotAbove = (rac & 0x04) != 0;   // bit 56
            racNotLeft = (rac & 0x02) != 0;    // bit 57
            racNotRight = (rac & 0x01) != 0;   // bit 58

            // Extract RAT (Resolution Advisory Terminated, bit 59)
            resolutionAdvisoryTerminated = ExtractBits(frame.Data, 59, 1) != 0;

            // Extract MTE (Multiple Threat Encounter, bit 60)
            multipleThreatEncounter = ExtractBits(frame.Data, 60, 1) != 0;
        }

        return new LongAirAirSurveillance(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            altitude,
            verticalStatus,
            sensitivityLevel,
            replyInformation,
            acasValid,
            resolutionAdvisoryTerminated,
            multipleThreatEncounter,
            racNotBelow,
            racNotAbove,
            racNotLeft,
            racNotRight);
    }

}
