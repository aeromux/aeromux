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
/// MessageParser partial class: Extended Squitter messages (DF 17/18/19 - ADS-B).
/// Handles all Type Codes for identification, position, velocity, and status messages.
/// </summary>
public sealed partial class MessageParser
{
    /// <summary>
    /// Parses Extended Squitter (DF 17/18) with Type Code routing.
    /// </summary>
    private ModeSMessage? ParseExtendedSquitter(ValidatedFrame frame)
    {
        // Extract Type Code (TC) from ME field (bits 33-37 of message)
        int tc = (frame.Data[4] >> 3) & 0x1F;

        // Track TC statistics
        _messagesByTC.TryGetValue(tc, out long count);
        _messagesByTC[tc] = count + 1;

        return tc switch
        {
            // Essential ADS-B messages
            >= 1 and <= 4 => ParseAircraftIdentification(frame, tc),            // Aircraft ID & Category
            >= 9 and <= 18 => ParseAirbornePosition(frame, tc, false),    // Airborne Position (Barometric)
            19 => ParseAirborneVelocity(frame),                                 // Airborne Velocity

            // GNSS altitude positions
            >= 20 and <= 22 => ParseAirbornePosition(frame, tc, true),    // Airborne Position (GNSS)

            // Surface and status
            >= 5 and <= 8 => ParseSurfacePosition(frame, tc),                   // Surface Position
            28 => ParseAircraftStatus(frame),                                   // Aircraft Status
            31 => ParseAircraftOperationStatus(frame),                          // Operation Status

            // Target state and trajectory
            29 => ParseTargetStateAndStatus(frame),                             // Target State
            >= 23 and <= 27 => null,                                            // Reserved (not used)

            // Not implemented
            _ => LogUnsupportedTC(frame, tc)
        };
    }

    // ========================================
    // Essential ADS-B
    // ========================================

    /// <summary>
    /// Parses aircraft identification (callsign and category) from Type Code 1-4.
    /// Extracts 8-character callsign using 6-bit AIS encoding.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type code (1-4, determines aircraft category).</param>
    /// <returns>AircraftIdentification message with callsign and category.</returns>
    private ModeSMessage? ParseAircraftIdentification(ValidatedFrame frame, int tc)
    {
        // Extract Category (CA) field - bits 38-40 (byte 4, bits 0-2)
        int category = frame.Data[4] & 0x07;

        // Extract 8 characters (48 bits, starting at bit 41), each encoded as 6 bits
        char[] callsign = new char[8];
        int bitIndex = 41;  // Start at bit 41 (first callsign bit in ME field)

        for (int i = 0; i < 8; i++)
        {
            int charValue = ExtractBits(frame.Data, bitIndex, 6);
            callsign[i] = DecodeAisCharacter(charValue);
            bitIndex += 6;
        }

        // Trim trailing spaces and convert to string
        string callsignStr = new string(callsign).TrimEnd();

        // Map TC + CA to aircraft category enum
        AircraftCategory aircraftCategory = GetAircraftCategory(tc, category);

        return new AircraftIdentification(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            callsignStr,
            aircraftCategory);
    }

    /// <summary>
    /// Parses airborne position with CPR-encoded coordinates from Type Code 9-18 or 20-22.
    /// Decodes altitude (Q-bit method or Gillham) and CPR lat/lon (requires frame pairing).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type code (9-18 for barometric, 20-22 for GNSS).</param>
    /// <param name="isGnss">True if GNSS altitude, false if barometric.</param>
    /// <returns>AirbornePosition message with altitude and CPR-encoded coordinates.</returns>
    private ModeSMessage? ParseAirbornePosition(ValidatedFrame frame, int tc, bool isGnss)
    {
        // Extract Surveillance Status (SS) - bits 38-39 (2 bits)
        int ssRaw = ExtractBits(frame.Data, 38, 2);
        var surveillanceStatus = (SurveillanceStatus)ssRaw;

        // Extract single antenna flag - bit 40 (1 bit)
        var antenna = (AntennaFlag)ExtractBits(frame.Data, 40, 1);

        // Extract altitude - bits 41-52 (12 bits)
        int altRaw = ExtractBits(frame.Data, 41, 12);
        Altitude? altitude = DecodeAltitude(altRaw, isGnss ? AltitudeType.Geometric : AltitudeType.Barometric);

        // Extract CPR format (F bit) - bit 54 (0=even, 1=odd)
        int cprFormatRaw = ExtractBits(frame.Data, 54, 1);
        var cprFormat = (CprFormat)cprFormatRaw;

        // Extract CPR latitude - bits 55-71 (17 bits)
        int cprLat = ExtractBits(frame.Data, 55, 17);

        // Extract CPR longitude - bits 72-88 (17 bits)
        int cprLon = ExtractBits(frame.Data, 72, 17);

        // Attempt CPR decoding (may return null if frame pair incomplete)
        GeographicCoordinate? position = _cprDecoder.DecodePosition(
            frame.IcaoAddress,
            cprLat,
            cprLon,
            cprFormat,
            frame.Timestamp);

        return new AirbornePosition(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            position,
            altitude,
            antenna,
            surveillanceStatus);
    }

    /// <summary>
    /// Parses airborne velocity (ground speed or airspeed) from Type Code 19.
    /// Decodes 4 subtypes: ground speed (1-2) and airspeed (3-4).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>AirborneVelocity message with speed, heading, and vertical rate.</returns>
    private ModeSMessage? ParseAirborneVelocity(ValidatedFrame frame)
    {
        // Extract subtype (ST) - bits 38-40 (3 bits)
        int subtypeRaw = ExtractBits(frame.Data, 38, 3);

        // Validate subtype range (1-4, values 0/5-7 are reserved)
        if (subtypeRaw < 1 || subtypeRaw > 4)
        {
            Log.Debug("Invalid TC 19 velocity subtype {Subtype} from {Icao} (reserved/undefined)",
                subtypeRaw, frame.IcaoAddress);
            return null;
        }

        var subtype = (VelocitySubtype)subtypeRaw;

        // Extract NACv (Navigation Accuracy Category - Velocity, bits 43-45, 3 bits)
        int nacvRaw = ExtractBits(frame.Data, 43, 3);
        NavigationAccuracyCategoryVelocity? nacv = null;
        if (Enum.IsDefined(typeof(NavigationAccuracyCategoryVelocity), nacvRaw))
        {
            nacv = (NavigationAccuracyCategoryVelocity)nacvRaw;
        }

        // Extract vertical rate first (common to all subtypes)
        int? verticalRate = ParseVerticalRate(frame);

        // Route to subtype-specific parsers
        return subtype switch
        {
            VelocitySubtype.GroundSpeedSubsonic or VelocitySubtype.GroundSpeedSupersonic
                => ParseGroundSpeedVelocity(frame, subtype, verticalRate, nacv),
            VelocitySubtype.AirspeedSubsonic or VelocitySubtype.AirspeedSupersonic
                => ParseAirspeedVelocity(frame, subtype, verticalRate, nacv),
            _ => null  // Unknown subtype
        };
    }

    /// <summary>
    /// Parses ground speed velocity (subtypes 1-2).
    /// </summary>
    private ModeSMessage? ParseGroundSpeedVelocity(ValidatedFrame frame, VelocitySubtype subtype, int? verticalRate, NavigationAccuracyCategoryVelocity? nacv)
    {
        // Extract EW direction and velocity - bits 46 and 47-56
        int sew = ExtractBits(frame.Data, 46, 1);
        int vew = ExtractBits(frame.Data, 47, 10);

        // Extract NS direction and velocity - bits 57 and 58-67
        int sns = ExtractBits(frame.Data, 57, 1);
        int vns = ExtractBits(frame.Data, 58, 10);

        // Check for "no data" (all zeros)
        if (vew == 0 || vns == 0)
        {
            return null;
        }

        // Calculate velocity components (knots)
        int multiplier = (subtype == VelocitySubtype.GroundSpeedSupersonic) ? 4 : 1;  // Supersonic multiplier
        int vx = (sew == 0 ? 1 : -1) * (vew - 1) * multiplier;  // East = positive
        int vy = (sns == 0 ? 1 : -1) * (vns - 1) * multiplier;  // North = positive

        // Calculate ground speed (magnitude)
        int groundSpeed = (int)Math.Sqrt((vx * vx) + (vy * vy));

        // Calculate track angle (0-360°, North = 0°)
        double trackAngle = Math.Atan2(vx, vy) * 180.0 / Math.PI;
        double normalizedHeading = ((trackAngle % 360) + 360) % 360;

        return new AirborneVelocity(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            Velocity.FromKnots(groundSpeed, VelocityType.GroundSpeed),
            normalizedHeading,
            verticalRate,
            subtype,
            nacv);
    }

    /// <summary>
    /// Parses airspeed velocity (subtypes 3-4).
    /// </summary>
    private ModeSMessage? ParseAirspeedVelocity(ValidatedFrame frame, VelocitySubtype subtype, int? verticalRate, NavigationAccuracyCategoryVelocity? nacv)
    {
        // Extract heading status and value - bit 46 and bits 47-56
        int sh = ExtractBits(frame.Data, 46, 1);
        int hdg = ExtractBits(frame.Data, 47, 10);

        // Extract airspeed type and value - bit 57 and bits 58-67
        int asType = ExtractBits(frame.Data, 57, 1);
        int asRaw = ExtractBits(frame.Data, 58, 10);

        // Calculate heading (0-360°)
        double? heading = null;
        if (sh == 1 && hdg != 0)
        {
            heading = hdg * 360.0 / 1024.0;
        }

        // Calculate airspeed
        Velocity? velocity = null;
        if (asRaw != 0)
        {
            int multiplier = (subtype == VelocitySubtype.AirspeedSupersonic) ? 4 : 1;  // Supersonic multiplier
            int airspeed = (asRaw - 1) * multiplier;

            VelocityType velType = (asType == 1)
                ? VelocityType.TrueAirspeed
                : VelocityType.IndicatedAirspeed;

            velocity = Velocity.FromKnots(airspeed, velType);
        }

        return new AirborneVelocity(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            velocity,
            heading,
            verticalRate,
            subtype,
            nacv);
    }

    /// <summary>
    /// Extracts vertical rate from TC 19 message (common to all subtypes).
    /// </summary>
    private int? ParseVerticalRate(ValidatedFrame frame)
    {
        // VrSrc (bit 68): 0=GNSS, 1=Barometric
        int vrSrc = ExtractBits(frame.Data, 68, 1);  // Not used currently

        // SVr (bit 69): 0=climb, 1=descent
        int svr = ExtractBits(frame.Data, 69, 1);

        // VR (bits 70-78): 9-bit value
        int vr = ExtractBits(frame.Data, 70, 9);

        // Check for "no data" (all zeros)
        if (vr == 0)
        {
            return null;
        }

        // Calculate vertical rate (64 ft/min resolution)
        int verticalRate = (vr - 1) * 64;

        // Apply sign (0=climb=positive, 1=descent=negative)
        if (svr == 1)
        {
            verticalRate = -verticalRate;
        }

        return verticalRate;
    }

    // ========================================
    // Additional Formats
    // ========================================

    /// <summary>
    /// Parses surface position with movement from Type Code 5-8.
    /// Extracts movement, ground track, and CPR-encoded position.
    /// Requires receiver location for CPR decoding.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type Code (5-8).</param>
    /// <returns>Surface position message with movement, track, and CPR coordinates.</returns>
    private ModeSMessage? ParseSurfacePosition(ValidatedFrame frame, int tc)
    {
        // Extract Movement field - bits 38-44 (7 bits)
        int movementRaw = ExtractBits(frame.Data, 38, 7);

        // Map raw value to SurfaceMovement enum
        var movement = (SurfaceMovement)movementRaw;

        // Decode movement to ground speed (knots)
        int? groundSpeed = DecodeMovement(movementRaw);

        // Extract Ground Track Status (S) - bit 45 (1 bit)
        int trackStatus = ExtractBits(frame.Data, 45, 1);

        // Extract Ground Track - bits 46-52 (7 bits)
        double? groundTrack = null;
        if (trackStatus == 1)
        {
            int trackRaw = ExtractBits(frame.Data, 46, 7);
            // Convert to degrees (360/128 = 2.8125° resolution)
            groundTrack = trackRaw * (360.0 / 128.0);
        }

        // Extract Time flag (T) - bit 53 (1 bit)
        // (currently unused - indicates time synchronization)

        // Extract CPR format (F bit) - bit 54 (0=even, 1=odd)
        int cprFormatRaw = ExtractBits(frame.Data, 54, 1);
        var cprFormat = (CprFormat)cprFormatRaw;

        // Extract CPR latitude - bits 55-71 (17 bits)
        int cprLat = ExtractBits(frame.Data, 55, 17);

        // Extract CPR longitude - bits 72-88 (17 bits)
        int cprLon = ExtractBits(frame.Data, 72, 17);

        // Attempt surface CPR decoding (requires receiver location)
        GeographicCoordinate? position = _surfaceCprDecoder.DecodePosition(
            cprLat,
            cprLon,
            cprFormat);

        return new SurfacePosition(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            position,
            groundSpeed,
            groundTrack,
            movement);
    }

    /// <summary>
    /// Decodes movement field (7 bits) to ground speed in knots.
    /// Uses non-linear quantization table from ICAO Annex 10, Volume IV, Table 2-14.
    /// </summary>
    /// <param name="movementRaw">Raw movement value (0-127).</param>
    /// <returns>Ground speed in knots (midpoint of range), or null if no information.</returns>
    private static int? DecodeMovement(int movementRaw)
    {
        return movementRaw switch
        {
            0 => null,      // No information
            1 => 0,         // Stopped
            2 => 0,         // 0-0.125 kt (use 0 as midpoint)
            3 => 0,         // 0.125-1 kt (use 0 as approximation)
            4 => 1,         // 1-2 kt
            5 => 3,         // 2-5 kt (midpoint: 3.5 ≈ 3)
            6 => 7,         // 5-10 kt (midpoint: 7.5 ≈ 7)
            7 => 12,        // 10-15 kt (midpoint: 12.5 ≈ 12)
            8 => 17,        // 15-20 kt (midpoint: 17.5 ≈ 17)
            9 => 25,        // 20-30 kt
            10 => 35,       // 30-40 kt
            11 => 45,       // 40-50 kt
            12 => 55,       // 50-60 kt
            13 => 65,       // 60-70 kt
            14 => 75,       // 70-80 kt
            15 => 85,       // 80-90 kt
            16 => 95,       // 90-100 kt
            17 => 105,      // 100-110 kt
            18 => 115,      // 110-120 kt
            19 => 125,      // 120-130 kt
            20 => 135,      // 130-140 kt
            21 => 145,      // 140-150 kt
            22 => 155,      // 150-160 kt
            23 => 167,      // 160-175 kt (midpoint: 167.5 ≈ 167)
            24 => 187,      // 175-199 kt (midpoint: 187)
            124 => 199,     // ≥ 199 kt (use 199 as approximation)
            _ => null       // Reserved or invalid
        };
    }

    /// <summary>
    /// Parses Aircraft Status message (TC 28).
    /// Handles emergency/priority status (subtype 1) and TCAS RA (subtype 2).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Aircraft status message, or null if subtype not supported.</returns>
    private ModeSMessage? ParseAircraftStatus(ValidatedFrame frame)
    {
        // Extract subtype from bits 38-40 (3 bits)
        int subtypeRaw = ExtractBits(frame.Data, 38, 3);

        // Validate subtype
        if (!Enum.IsDefined(typeof(AircraftStatusSubtype), subtypeRaw))
        {
            // Subtype 3-7 reserved or no information
            Log.Debug("Unsupported TC 28 subtype {Subtype} from {Icao}",
                subtypeRaw, frame.IcaoAddress);
            return null;
        }

        var subtype = (AircraftStatusSubtype)subtypeRaw;

        EmergencyState? emergencyState = null;
        string? squawkCode = null;

        if (subtype == AircraftStatusSubtype.EmergencyPriority)
        {
            // Emergency/priority status
            // Extract emergency state from bits 41-43 (3 bits)
            int esRaw = ExtractBits(frame.Data, 41, 3);

            if (!Enum.IsDefined(typeof(EmergencyState), esRaw))
            {
                Log.Debug("Invalid emergency state {ES} in TC 28 from {Icao}",
                    esRaw, frame.IcaoAddress);
                return null;
            }

            emergencyState = (EmergencyState)esRaw;

            // Extract squawk code from bits 44-56 (13 bits)
            int identityCode = ExtractBits(frame.Data, 44, 13);

            if (identityCode != 0)
            {
                squawkCode = DecodeSquawkCode(identityCode);
            }
        }
        else if (subtype == AircraftStatusSubtype.TcasResolutionAdvisory)
        {
            // TCAS/ACAS Resolution Advisory
            Log.Debug("TCAS RA from {Icao} (full RA decoding is deferred)",
                frame.IcaoAddress);
        }

        return new AircraftStatus(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            subtype,
            emergencyState,
            squawkCode);
    }

    /// <summary>
    /// Parses Operational Status message (TC 31, Version 0/1/2).
    /// Extracts capability codes, operational modes, version, and accuracy parameters.
    /// </summary>
    /// <remarks>
    /// Based on ADS-B Version 1/2 specification (DO-260A/B).
    /// </remarks>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Operational status message, or null if subtype not supported.</returns>
    private ModeSMessage? ParseAircraftOperationStatus(ValidatedFrame frame)
    {
        // Extract subtype from bits 38-40 (3 bits)
        int subtypeRaw = ExtractBits(frame.Data, 38, 3);

        // Validate subtype (0=airborne, 1=surface, 2-7=reserved)
        if (!Enum.IsDefined(typeof(OperationalStatusSubtype), subtypeRaw))
        {
            Log.Debug("Reserved TC 31 subtype {Subtype} from {Icao}",
                subtypeRaw, frame.IcaoAddress);
            return null;
        }

        var subtype = (OperationalStatusSubtype)subtypeRaw;

        // Version number (bits 73-75, 3 bits)
        var version = (AdsbVersion)ExtractBits(frame.Data, 73, 3);

        // Variables for parsing CC
        CapabilityClass? capabilityClass = null;
        bool? ccTcasOperational = null;
        bool? ccCdtiCapability = null;
        bool? ccAdsb1090EsCapability = null;
        bool? ccArvCapability = null;
        bool? ccTsCapability = null;
        TrajectoryChangeReportCapability? ccTcCapabilityLevel = null;
        bool? ccUatCapability = null;
        bool? ccPoa = null;
        bool? ccB2Low = null;
        NavigationAccuracyCategoryVelocity? ccNacv = null;
        bool? ccNicSupplementC = null;

        // Variables for parsing OM
        OperationalMode? operationalMode = null;
        bool? omTcasRaActive = null;
        bool? omIdentSwitchActive = null;
        bool? omAtcServices = null;
        AntennaFlag? omSingleAntenna = null;
        SdaSupportedFailureCondition? omSda = null;
        LateralGpsAntennaOffset? omGpsLatOffset = null;
        LongitudinalGpsAntennaOffset? omGpsLongOffset = null;

        // Other variables
        AircraftLengthAndWidth? aircraftLengthWidth = null;
        bool? nicSupplementA =  null;
        NavigationAccuracyCategoryPosition? nacp = null;
        SourceIntegrityLevel? sil = null;
        GeometricVerticalAccuracy? gva = null;
        BarometricAltitudeIntegrityCode? nicBaro = null;
        TargetHeadingType? trkHdg = null;
        HorizontalReferenceDirection? hrd = null;
        SilSupplement? silSupplement = null;

        // Parse based on the version and CC/OM content
        int ccContent;
        int omContent;

        switch (version)
        {
            // Version 0
            case AdsbVersion.DO260:
                ccContent = ExtractBits(frame.Data, 41, 2);
                if (subtype == OperationalStatusSubtype.Airborne && ccContent == 0)
                {
                    // TCAS/ACAS not installed or not operational
                    ccTcasOperational = ExtractBits(frame.Data, 43, 1) != 0;

                    // CDTI Traffic Display capability
                    ccCdtiCapability = ExtractBits(frame.Data, 44, 1) == 1;
                }

                capabilityClass = new CapabilityClass
                {
                    TcasOperational = ccTcasOperational,
                    CdtiCapability = ccCdtiCapability
                };
                break;

            // Version 1
            case AdsbVersion.DO260A:
                // Get Capability Class content bits (only 0,0 applies)
                ccContent = ExtractBits(frame.Data, 41, 2);

                // Get Operational Mode content bits (only O,O applies)
                omContent = ExtractBits(frame.Data, 57, 2);

                // CC - Airborne
                if (subtype == OperationalStatusSubtype.Airborne && ccContent == 0)
                {

                    // TCAS is operational or not operational
                    ccTcasOperational = ExtractBits(frame.Data, 43, 1) == 1;

                    // Capability to receive ADS-B 1090ES
                    ccAdsb1090EsCapability = ExtractBits(frame.Data, 44, 1) == 1;

                    // Capability of sending messages to support Air-Referenced Velocity reports
                    ccArvCapability = ExtractBits(frame.Data, 47, 1) == 1;

                    // Capability to send Target State reports
                    ccTsCapability = ExtractBits(frame.Data, 48, 1) == 1;

                    // Trajectory Change Report capability level
                    ccTcCapabilityLevel = (TrajectoryChangeReportCapability)ExtractBits(frame.Data, 49, 2);

                    // Capability to receive ADS-B UAT messages.
                    ccUatCapability = ExtractBits(frame.Data, 51, 1) == 1;

                    capabilityClass = new CapabilityClass()
                    {
                        TcasOperational = ccTcasOperational,
                        Adsb1090EsCapability = ccAdsb1090EsCapability,
                        ArvCapability = ccArvCapability,
                        TsCapability = ccTsCapability,
                        TcCapabilityLevel = ccTcCapabilityLevel,
                        UatCapability = ccUatCapability
                    };
                }

                // CC - Surface
                if (subtype == OperationalStatusSubtype.Surface && ccContent == 0)
                {
                    // The position transmitted in the Surface Position Message is known to
                    // be referenced to the ADS-B Position Reference Point of the A/V
                    ccPoa = ExtractBits(frame.Data, 43, 1) == 1;

                    // Capability to receive ADS-B 1090ES
                    ccAdsb1090EsCapability = ExtractBits(frame.Data, 44, 1) == 1;

                    // Non-Transponder-Based Transmitting Subsystem on a Ground Vehicle meets
                    // the requirements of Class B2, except that it transmits with less than 70 watts of power.
                    ccB2Low = ExtractBits(frame.Data, 47, 1) == 1;

                    // Capability to receive ADS-B UAT messages.
                    ccUatCapability = ExtractBits(frame.Data, 48, 1) == 1;

                    // Navigation Accuracy Category for Velocity
                    ccNacv = (NavigationAccuracyCategoryVelocity)ExtractBits(frame.Data, 49, 3);

                    // NIC Supplement-C for NIC decoding (together with Surface Position Messages and NIC Supplement-A)
                    ccNicSupplementC = ExtractBits(frame.Data, 52, 1) == 1;

                    capabilityClass = new CapabilityClass()
                    {
                        Poa = ccPoa,
                        Adsb1090EsCapability = ccAdsb1090EsCapability,
                        B2Low = ccB2Low,
                        UatCapability = ccUatCapability,
                        Nacv = ccNacv,
                        NicSupplementC = ccNicSupplementC
                    };
                }

                // OM - Airborne
                if (subtype == OperationalStatusSubtype.Airborne && omContent == 0)
                {
                    // TCAS II or ACAS Resolution Advisory is known to be in effect
                    omTcasRaActive = ExtractBits(frame.Data, 59, 1) == 1;

                    // The IDENT switch is active for a period of 18 ± 1 seconds
                    omIdentSwitchActive = ExtractBits(frame.Data, 60, 1) == 1;

                    // Receiving ATC Services within the last 5 seconds
                    omAtcServices = ExtractBits(frame.Data, 61, 1) == 1;

                    // Indicate that the ADS-B Transmitting Subsystem is operating with a single antenna
                    omSingleAntenna = (AntennaFlag)ExtractBits(frame.Data, 62, 1);

                    // Defines the failure condition that the ADS-B system is designed to support
                    omSda = (SdaSupportedFailureCondition)ExtractBits(frame.Data, 63, 2);

                    operationalMode = new OperationalMode()
                    {
                        TcasRaActive = omTcasRaActive,
                        IdentSwitchActive = omIdentSwitchActive,
                        AtcServices = omAtcServices,
                        SingleAntenna = omSingleAntenna,
                        Sda = omSda
                    };
                }

                // OM - Surface
                if (subtype == OperationalStatusSubtype.Surface && omContent == 0)
                {
                    // TCAS II or ACAS Resolution Advisory is known to be in effect
                    omTcasRaActive = ExtractBits(frame.Data, 59, 1) == 1;

                    // The IDENT switch is active for a period of 18 ± 1 seconds
                    omIdentSwitchActive = ExtractBits(frame.Data, 60, 1) == 1;

                    // Receiving ATC Services within the last 5 seconds
                    omAtcServices = ExtractBits(frame.Data, 61, 1) == 1;

                    // Indicate that the ADS-B Transmitting Subsystem is operating with a single antenna
                    omSingleAntenna = (AntennaFlag)ExtractBits(frame.Data, 62, 1);

                    // Defines the failure condition that the ADS-B system is designed to support
                    omSda = (SdaSupportedFailureCondition)ExtractBits(frame.Data, 63, 2);

                    // Position of the GPS antenna (if POA is 0, value is provided otherwise all zero)
                    int tempGpsLat = ExtractBits(frame.Data, 65, 3);
                    omGpsLatOffset = new LateralGpsAntennaOffset(tempGpsLat);
                    int tempGpsLon = ExtractBits(frame.Data, 68, 5);
                    omGpsLongOffset = new LongitudinalGpsAntennaOffset(tempGpsLon);

                    operationalMode = new OperationalMode()
                    {
                        TcasRaActive = omTcasRaActive,
                        IdentSwitchActive = omIdentSwitchActive,
                        AtcServices = omAtcServices,
                        SingleAntenna = omSingleAntenna,
                        Sda = omSda,
                        GpsLatOffset = omGpsLatOffset,
                        GpsLongOffset = omGpsLongOffset
                    };
                }

                // GVA and NICb - Airborne only
                if (subtype == OperationalStatusSubtype.Airborne)
                {
                    // Geometrical Vertical Accuracy
                    gva = (GeometricVerticalAccuracy)ExtractBits(frame.Data, 81, 2);

                    // Barometric Altitude Integrity Code
                    nicBaro = (BarometricAltitudeIntegrityCode)ExtractBits(frame.Data, 85, 1);
                }

                // LW and TRK/HDG - Surface only
                if (subtype == OperationalStatusSubtype.Surface)
                {
                    // Aircraft/Vehicle (A/V) Length and Width based on the actual dimensions
                    // of the transmitting aircraft or surface vehicle
                    aircraftLengthWidth = new AircraftLengthAndWidth(ExtractBits(frame.Data, 53, 4));

                    // Track Angle or Heading
                    trkHdg = (TargetHeadingType)ExtractBits(frame.Data, 85, 1);
                }

                // Everything else

                // NIC Supplement-A for NIC decoding (together with Surface Position Messages and NIC Supplement-C)
                nicSupplementA = ExtractBits(frame.Data, 86, 1) == 1;

                // Navigation Accuracy Category for Position (NACp)
                nacp = (NavigationAccuracyCategoryPosition)ExtractBits(frame.Data, 77, 4);

                // Source Integrity Level
                sil = (SourceIntegrityLevel)ExtractBits(frame.Data, 83, 2);

                // Horizontal Reference Direction
                hrd = (HorizontalReferenceDirection)ExtractBits(frame.Data, 86, 1);

                break;

            // Version 2
            case AdsbVersion.DO260B:
                // Get Capability Class content bits (only 0,0 applies)
                ccContent = ExtractBits(frame.Data, 41, 2);

                // Get Operational Mode content bits (only O,O applies)
                omContent = ExtractBits(frame.Data, 57, 2);

                // CC - Airborne
                if (subtype == OperationalStatusSubtype.Airborne && ccContent == 0)
                {

                    // TCAS is operational or not operational
                    ccTcasOperational = ExtractBits(frame.Data, 43, 1) == 1;

                    // Capability to receive ADS-B 1090ES
                    ccAdsb1090EsCapability = ExtractBits(frame.Data, 44, 1) == 1;

                    // Capability of sending messages to support Air-Referenced Velocity reports
                    ccArvCapability = ExtractBits(frame.Data, 47, 1) == 1;

                    // Capability to send Target State reports
                    ccTsCapability = ExtractBits(frame.Data, 48, 1) == 1;

                    // Trajectory Change Report capability level
                    ccTcCapabilityLevel = (TrajectoryChangeReportCapability)ExtractBits(frame.Data, 49, 2);

                    // Capability to receive ADS-B UAT messages.
                    ccUatCapability = ExtractBits(frame.Data, 51, 1) == 1;

                    capabilityClass = new CapabilityClass()
                    {
                        TcasOperational = ccTcasOperational,
                        Adsb1090EsCapability = ccAdsb1090EsCapability,
                        ArvCapability = ccArvCapability,
                        TsCapability = ccTsCapability,
                        TcCapabilityLevel = ccTcCapabilityLevel,
                        UatCapability = ccUatCapability
                    };
                }

                // CC - Surface
                if (subtype == OperationalStatusSubtype.Surface && ccContent == 0)
                {
                    // The position transmitted in the Surface Position Message is known to
                    // be referenced to the ADS-B Position Reference Point of the A/V
                    ccPoa = ExtractBits(frame.Data, 43, 1) == 1;

                    // Capability to receive ADS-B 1090ES
                    ccAdsb1090EsCapability = ExtractBits(frame.Data, 44, 1) == 1;

                    // Non-Transponder-Based Transmitting Subsystem on a Ground Vehicle meets
                    // the requirements of Class B2, except that it transmits with less than 70 watts of power.
                    ccB2Low = ExtractBits(frame.Data, 47, 1) == 1;

                    // Capability to receive ADS-B UAT messages.
                    ccUatCapability = ExtractBits(frame.Data, 48, 1) == 1;

                    // Navigation Accuracy Category for Velocity
                    ccNacv = (NavigationAccuracyCategoryVelocity)ExtractBits(frame.Data, 49, 3);

                    // NIC Supplement-C for NIC decoding (together with Surface Position Messages and NIC Supplement-A)
                    ccNicSupplementC = ExtractBits(frame.Data, 52, 1) == 1;

                    capabilityClass = new CapabilityClass()
                    {
                        Poa = ccPoa,
                        Adsb1090EsCapability = ccAdsb1090EsCapability,
                        B2Low = ccB2Low,
                        UatCapability = ccUatCapability,
                        Nacv = ccNacv,
                        NicSupplementC = ccNicSupplementC
                    };
                }

                // OM - Airborne
                if (subtype == OperationalStatusSubtype.Airborne && omContent == 0)
                {
                    // TCAS II or ACAS Resolution Advisory is known to be in effect
                    omTcasRaActive = ExtractBits(frame.Data, 59, 1) == 1;

                    // The IDENT switch is active for a period of 18 ± 1 seconds
                    omIdentSwitchActive = ExtractBits(frame.Data, 60, 1) == 1;

                    // Receiving ATC Services within the last 5 seconds
                    omAtcServices = ExtractBits(frame.Data, 61, 1) == 1;

                    // Indicate that the ADS-B Transmitting Subsystem is operating with a single antenna
                    omSingleAntenna = (AntennaFlag)ExtractBits(frame.Data, 62, 1);

                    // Defines the failure condition that the ADS-B system is designed to support
                    omSda = (SdaSupportedFailureCondition)ExtractBits(frame.Data, 63, 2);

                    operationalMode = new OperationalMode()
                    {
                        TcasRaActive = omTcasRaActive,
                        IdentSwitchActive = omIdentSwitchActive,
                        AtcServices = omAtcServices,
                        SingleAntenna = omSingleAntenna,
                        Sda = omSda
                    };
                }

                // OM - Surface
                if (subtype == OperationalStatusSubtype.Surface && omContent == 0)
                {
                    // TCAS II or ACAS Resolution Advisory is known to be in effect
                    omTcasRaActive = ExtractBits(frame.Data, 59, 1) == 1;

                    // The IDENT switch is active for a period of 18 ± 1 seconds
                    omIdentSwitchActive = ExtractBits(frame.Data, 60, 1) == 1;

                    // Receiving ATC Services within the last 5 seconds
                    omAtcServices = ExtractBits(frame.Data, 61, 1) == 1;

                    // Indicate that the ADS-B Transmitting Subsystem is operating with a single antenna
                    omSingleAntenna = (AntennaFlag)ExtractBits(frame.Data, 62, 1);

                    // Defines the failure condition that the ADS-B system is designed to support
                    omSda = (SdaSupportedFailureCondition)ExtractBits(frame.Data, 63, 2);

                    // Position of the GPS antenna (if POA is 0, value is provided otherwise all zero)
                    int tempGpsLat = ExtractBits(frame.Data, 65, 3);
                    omGpsLatOffset = new LateralGpsAntennaOffset(tempGpsLat);
                    int tempGpsLon = ExtractBits(frame.Data, 68, 5);
                    omGpsLongOffset = new LongitudinalGpsAntennaOffset(tempGpsLon);

                    operationalMode = new OperationalMode()
                    {
                        TcasRaActive = omTcasRaActive,
                        IdentSwitchActive = omIdentSwitchActive,
                        AtcServices = omAtcServices,
                        SingleAntenna = omSingleAntenna,
                        Sda = omSda,
                        GpsLatOffset = omGpsLatOffset,
                        GpsLongOffset = omGpsLongOffset
                    };
                }

                // GVA and NICb - Airborne only
                if (subtype == OperationalStatusSubtype.Airborne)
                {
                    // Geometrical Vertical Accuracy
                    gva = (GeometricVerticalAccuracy)ExtractBits(frame.Data, 81, 2);

                    // Barometric Altitude Integrity Code
                    nicBaro = (BarometricAltitudeIntegrityCode)ExtractBits(frame.Data, 85, 1);
                }

                // LW and TRK/HDG - Surface only
                if (subtype == OperationalStatusSubtype.Surface)
                {
                    // Aircraft/Vehicle (A/V) Length and Width based on the actual dimensions
                    // of the transmitting aircraft or surface vehicle
                    aircraftLengthWidth = new AircraftLengthAndWidth(ExtractBits(frame.Data, 53, 4));

                    // Track Angle or Heading
                    trkHdg = (TargetHeadingType)ExtractBits(frame.Data, 85, 1);
                }

                // Everything else

                // NIC Supplement-A for NIC decoding (together with Surface Position Messages and NIC Supplement-C)
                nicSupplementA = ExtractBits(frame.Data, 86, 1) == 1;

                // Navigation Accuracy Category for Position (NACp)
                nacp = (NavigationAccuracyCategoryPosition)ExtractBits(frame.Data, 77, 4);

                // Source Integrity Level
                sil = (SourceIntegrityLevel)ExtractBits(frame.Data, 83, 2);

                // Horizontal Reference Direction
                hrd = (HorizontalReferenceDirection)ExtractBits(frame.Data, 86, 1);

                // SIL Supplement
                silSupplement = (SilSupplement)ExtractBits(frame.Data, 87, 1);

                break;
            default:
                return null;
        }

        return new OperationalStatus(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            subtype,
            version,
            capabilityClass,
            operationalMode,
            aircraftLengthWidth,
            nicSupplementA,
            nacp,
            gva,
            sil,
            nicBaro,
            trkHdg,
            hrd,
            silSupplement
            );
    }

    // ========================================
    // Less Common Formats
    // ========================================

    /// <summary>
    /// Parses Target State and Status message (TC 29).
    /// FULL implementation of all fields for both Version 1 and Version 2.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Target state and status message, or null if subtype not supported.</returns>
    private ModeSMessage? ParseTargetStateAndStatus(ValidatedFrame frame)
    {
        // Extract subtype from bits 38-39 (2 bits)
        int subtypeRaw = ExtractBits(frame.Data, 38, 2);

        // Validate subtype (0=Version1, 1=Version2, 2-3=reserved)
        if (!Enum.IsDefined(typeof(TargetStateSubtype), subtypeRaw))
        {
            Log.Debug("Reserved TC 29 subtype {Subtype} from {Icao}",
                subtypeRaw, frame.IcaoAddress);
            return null;
        }

        var subtype = (TargetStateSubtype)subtypeRaw;

        Altitude? targetAltitude = null;
        AltitudeSource? altitudeSource = null;
        double? targetHeading = null;
        TargetHeadingType? targetHeadingType = null;
        double? barometricPressure = null;
        VerticalMode? verticalMode = null;
        HorizontalMode? horizontalMode = null;
        bool? autopilotEngaged = null;
        bool? vnavMode = null;
        bool? altitudeHoldMode = null;
        bool? approachMode = null;
        bool? lnavMode = null;
        bool? tcasOperational = null;
        bool? tcasRaActive = null;
        EmergencyState? emergencyPriority = null;
        SourceIntegrityLevel? sil = null;
        NavigationAccuracyCategoryPosition? nacp = null;
        BarometricAltitudeIntegrityCode? nicBaro = null;

        switch (subtype)
        {
            // Version 1
            case TargetStateSubtype.Version1:
            {
                // Target altitude available (bits 40-41, 2 bits)
                int altAvail = ExtractBits(frame.Data, 40, 2);
                if (altAvail != 0)
                {
                    altitudeSource = altAvail switch
                    {
                        // Altitude source
                        1 => AltitudeSource.McpFcu,
                        2 => AltitudeSource.HoldingMode,
                        _ => AltitudeSource.FmsRnav
                    };

                    // Target altitude (bits 48-57, 10 bits)
                    int altRaw = ExtractBits(frame.Data, 48, 10);
                    int altitudeFeet = -1000 + (altRaw * 100);
                    targetAltitude = Altitude.FromFeet(altitudeFeet, AltitudeType.Barometric);
                }

                // Vertical mode (bits 46-47, 2 bits)
                verticalMode = (VerticalMode)ExtractBits(frame.Data, 46, 2);

                // Target angle available (bits 58-59, 2 bits)
                int angleAvail = ExtractBits(frame.Data, 58, 2);
                if (angleAvail != 0)
                {
                    // Target angle (bits 60-68, 9 bits)
                    int angleRaw = ExtractBits(frame.Data, 60, 9);
                    targetHeading = angleRaw * (360.0 / 512.0);

                    // Angle type (bit 69, 1 bit, 0=track, 1=heading)
                    targetHeadingType = (TargetHeadingType)ExtractBits(frame.Data, 69, 1);
                }

                // Horizontal mode (bits 70-71, 2 bits)
                horizontalMode = (HorizontalMode)ExtractBits(frame.Data, 70, 2);

                // NACp (Navigation Accuracy Category - Position, bits 72-75, 4 bits)
                nacp = (NavigationAccuracyCategoryPosition)ExtractBits(frame.Data, 72, 4);

                // NICbaro (NIC barometric altitude integrity, bit 76, 1 bit)
                nicBaro = (BarometricAltitudeIntegrityCode)ExtractBits(frame.Data, 76, 1);

                // SIL (Source Integrity Level, bits 77-78, 2 bits)
                sil = (SourceIntegrityLevel)ExtractBits(frame.Data, 77, 2);

                // TCAS operational (bit 84, 1 bit)
                tcasOperational = ExtractBits(frame.Data, 84, 1) == 0;  // 0=operational

                // TCAS RA active (bit 85, 1 bit)
                tcasRaActive = ExtractBits(frame.Data, 85, 1) == 1;

                // Emergency/priority status (bits 86-88, 3 bits)
                emergencyPriority = (EmergencyState)ExtractBits(frame.Data, 86, 3);

                break;
            }
            // Version 2: Selected altitude, heading, pressure, modes, accuracy
            case TargetStateSubtype.Version2:
            {
                // Selected altitude source (bit 41, 0=MCP/FCU, 1=FMS)
                altitudeSource = ExtractBits(frame.Data, 41, 1) == 0
                    ? AltitudeSource.McpFcu : AltitudeSource.Fms;

                // Selected altitude (bits 42-52, 11 bits)
                int altitudeRaw = ExtractBits(frame.Data, 42, 11);
                if (altitudeRaw != 0)
                {
                    int altitudeFeet = (altitudeRaw - 1) * 32;
                    targetAltitude = Altitude.FromFeet(altitudeFeet, AltitudeType.Barometric);
                }

                // Barometric pressure setting (bits 53-61, 9 bits)
                int baroRaw = ExtractBits(frame.Data, 53, 9);
                if (baroRaw != 0)
                {
                    barometricPressure = 800.0 + ((baroRaw - 1) * 0.8);
                }

                // Selected heading (bits 63-72, 10 bits total: status + sign + value)
                int headingStatus = ExtractBits(frame.Data, 62, 1);
                if (headingStatus == 1)
                {
                    int hdgRaw = ExtractBits(frame.Data, 63, 9);
                    targetHeading = hdgRaw * (180.0 / 256.0);
                }

                // NACp (Navigation Accuracy Category - Position, bits 72-75, 4 bits)
                nacp = (NavigationAccuracyCategoryPosition)ExtractBits(frame.Data, 72, 4);

                // NICbaro (NIC barometric altitude integrity, bit 76, 1 bit)
                nicBaro = (BarometricAltitudeIntegrityCode)ExtractBits(frame.Data, 76, 1);

                // SIL (Surveillance Integrity Level, bits 77-78, 2 bits)
                sil = (SourceIntegrityLevel)ExtractBits(frame.Data, 77, 2);

                // Status of MCP/FCU (bit 79, 1 bit)
                int mcpFcuStatus = ExtractBits(frame.Data, 79, 1);
                if (mcpFcuStatus == 1)
                {
                    // Autopilot engaged (bit 80, 1 bit)
                    autopilotEngaged = ExtractBits(frame.Data, 80, 1) == 1;

                    // VNAV mode (bit 81, 1 bit)
                    vnavMode = ExtractBits(frame.Data, 81, 1) == 1;

                    // Altitude hold mode (bit 82, 1 bit)
                    altitudeHoldMode = ExtractBits(frame.Data, 82, 1) == 1;

                    // Approach mode (bit 84, 1 bit)
                    approachMode = ExtractBits(frame.Data, 84, 1) == 1;
                }

                // TCAS/ACAS operational (bit 85, 1 bit)
                tcasOperational = ExtractBits(frame.Data, 85, 1) == 1;

                // LNAV mode (bit 86, 1 bit)
                lnavMode = ExtractBits(frame.Data, 86, 1) == 1;

                break;
            }
        }

        return new TargetStateAndStatus(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            subtype,
            targetAltitude,
            altitudeSource,
            targetHeading,
            targetHeadingType,
            barometricPressure,
            verticalMode,
            horizontalMode,
            autopilotEngaged,
            vnavMode,
            altitudeHoldMode,
            approachMode,
            lnavMode,
            tcasOperational,
            tcasRaActive,
            emergencyPriority,
            sil,
            nacp,
            nicBaro);
    }

}
