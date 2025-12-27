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
/// MessageParser partial class: Basic surveillance messages (DF 0/4/5/11).
/// Handles short air-air surveillance, altitude/identity replies, and all-call messages.
/// </summary>
public sealed partial class MessageParser
{
    // ========================================
    // Basic Surveillance
    // ========================================

    /// <summary>
    /// Parses Surveillance Altitude Reply message (DF 4).
    /// Extracts flight status and altitude (Gillham or Q-bit encoding).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Surveillance altitude reply message with altitude and flight status.</returns>
    /// <remarks>
    /// DF 4 messages contain barometric altitude encoded in 13 bits (AC field).
    /// Four encoding modes: all-zeros (invalid), M=1 (meters), Q=1 (25-ft), Q=0 (Gillham code).
    /// Flight status indicates airborne/ground and alert/SPI conditions.
    /// </remarks>
    private ModeSMessage? ParseSurveillanceAltitudeReply(ValidatedFrame frame)
    {
        // Extract Flight Status (FS) field from bits 6-8 (byte 0, bits 0-2)
        int  flightStatusRaw = ExtractBits(frame.Data, 6, 3);
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 4 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Altitude Code (AC) field from bits 20-32
        int altitudeCode = ExtractBits(frame.Data, 20, 13);

        // Decode altitude (null if invalid or unavailable)
        Altitude? altitude = DecodeAltitudeAC13(altitudeCode);

        return new SurveillanceAltitudeReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            altitude,
            flightStatus);
    }

    /// <summary>
    /// Parses All-Call Reply message (DF 11).
    /// Extracts transponder capability field from bits 6-8.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>All-call reply message with capability, or null if invalid.</returns>
    /// <remarks>
    /// All-call replies are transmitted in response to Mode S all-call interrogations.
    /// They announce the aircraft's presence and ICAO address with basic capability information.
    /// Capability values:
    ///   0 = Level 1 transponder (basic Mode S)
    ///   1-3 = Reserved (not assigned, rejected if encountered)
    ///   4 = Level 2+ transponder, on-ground
    ///   5 = Level 2+ transponder, airborne
    ///   6 = Level 2+ transponder, on-ground or airborne status uncertain
    ///   7 = Downlink Request value is 0, or Flight Status is 2, 3, 4, or 5 (alert/SPI/emergency condition)
    /// </remarks>
    private ModeSMessage? ParseAllCallReply(ValidatedFrame frame)
    {
        // Extract Capability (CA) field from bits 6-8 (byte 0, bits 0-2)
        int capabilityRaw = ExtractBits(frame.Data, 6, 3);

        // Validate capability value (0-7 are defined in TransponderCapability enum)
        if (!Enum.IsDefined(typeof(TransponderCapability), capabilityRaw))
        {
            Log.Debug("Invalid capability value {Capability} in DF 11 from {Icao}",
                capabilityRaw, frame.IcaoAddress);
            return null;
        }

        var capability = (TransponderCapability)capabilityRaw;

        // Extract ICAO from AA field (bit 9-32)
        int extractedRawIcao = ExtractBits(frame.Data, 9, 24);
        string extractedIcao = $"{extractedRawIcao:X6}";

        return new AllCallReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            extractedIcao,
            capability);
    }

    /// <summary>
    /// Parses surveillance identity reply from Downlink Format 5.
    /// Extracts flight status and squawk code (identity code).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Surveillance identity reply message with squawk code.</returns>
    /// <remarks>
    /// DF 5 messages contain a 13-bit identity code (squawk code) that requires
    /// the bit rearrangement to extract the 4-digit octal code.
    /// </remarks>
    private ModeSMessage? ParseSurveillanceIdentityReply(ValidatedFrame frame)
    {
        // Extract Flight Status (FS) field from bits 6-8 (byte 0, bits 0-2)
        int flightStatusRaw = ExtractBits(frame.Data, 6, 3);
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 5 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Identity Code (ID) field from bits 20-32
        int identityCode = ExtractBits(frame.Data, 20, 13);

        // Decode squawk code (4-digit octal string)
        string squawkCode = DecodeSquawkCode(identityCode);

        return new SurveillanceIdentityReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            squawkCode,
            flightStatus);
    }

    /// <summary>
    /// Decodes 13-bit identity code (squawk code) from DF 5 messages.
    /// Returns 4-digit octal string (e.g., "7700" for emergency).
    /// </summary>
    /// <param name="id13">13-bit identity code field</param>
    /// <returns>4-digit octal squawk code (e.g., "7700")</returns>
    /// <remarks>
    /// The 13-bit field contains interleaved C/A/B/D bits that must be rearranged:
    /// ID field: C1 A1 C2 A2 C4 A4 X B1 D1 B2 D2 B4 D4
    /// Squawk:   A4 A2 A1 | B4 B2 B1 | C4 C2 C1 | D4 D2 D1
    /// Each group forms one octal digit (0-7).
    /// </remarks>
    private static string DecodeSquawkCode(int id13)
    {
        // Extract individual bits from 13-bit field
        // ID field: C1 A1 C2 A2 C4 A4 X B1 D1 B2 D2 B4 D4
        //           12 11 10  9  8  7 6  5  4  3  2  1  0

        int c1 = (id13 >> 12) & 0x01;
        int a1 = (id13 >> 11) & 0x01;
        int c2 = (id13 >> 10) & 0x01;
        int a2 = (id13 >>  9) & 0x01;
        int c4 = (id13 >>  8) & 0x01;
        int a4 = (id13 >>  7) & 0x01;
        // Skip X bit (bit 6) - not used
        int b1 = (id13 >>  5) & 0x01;
        int d1 = (id13 >>  4) & 0x01;
        int b2 = (id13 >>  3) & 0x01;
        int d2 = (id13 >>  2) & 0x01;
        int b4 = (id13 >>  1) & 0x01;
        int d4 = (id13 >>  0) & 0x01;

        // Rearrange into 4 octal digits (3 bits each)
        int digitA = (a4 << 2) | (a2 << 1) | a1;  // A4 A2 A1
        int digitB = (b4 << 2) | (b2 << 1) | b1;  // B4 B2 B1
        int digitC = (c4 << 2) | (c2 << 1) | c1;  // C4 C2 C1
        int digitD = (d4 << 2) | (d2 << 1) | d1;  // D4 D2 D1

        // Format as 4-digit octal string
        return $"{digitA}{digitB}{digitC}{digitD}";
    }

}
