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
            // Priority 1: Essential ADS-B messages
            >= 1 and <= 4 => ParseAircraftIdentification(frame, tc),      // Aircraft ID & Category
            >= 9 and <= 18 => ParseAirbornePosition(frame, tc, false),    // Airborne Position (Barometric)
            19 => ParseAirborneVelocity(frame),                            // Airborne Velocity

            // Priority 2: GNSS altitude positions
            >= 20 and <= 22 => ParseAirbornePosition(frame, tc, true),    // Airborne Position (GNSS)

            // Priority 3: Surface and status
            >= 5 and <= 8 => ParseSurfacePosition(frame, tc),             // Surface Position
            28 => ParseAircraftStatus(frame),                              // Aircraft Status
            31 => ParseAircraftOperationStatus(frame),                     // Operation Status

            // Priority 4: Target state and trajectory
            29 => ParseTargetStateAndStatus(frame),                        // Target State
            >= 23 and <= 27 => null,                                       // Reserved (not used)

            // Not implemented
            _ => LogUnsupportedTC(frame, tc)
        };
    }

    // ========================================
    // Priority 1: Essential ADS-B (IMPLEMENTED)
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

        // Extract vertical rate first (common to all subtypes)
        int? verticalRate = ParseVerticalRate(frame);

        // Route to subtype-specific parsers
        return subtype switch
        {
            VelocitySubtype.GroundSpeedSubsonic or VelocitySubtype.GroundSpeedSupersonic
                => ParseGroundSpeedVelocity(frame, subtype, verticalRate),
            VelocitySubtype.AirspeedSubsonic or VelocitySubtype.AirspeedSupersonic
                => ParseAirspeedVelocity(frame, subtype, verticalRate),
            _ => null  // Unknown subtype
        };
    }

    /// <summary>
    /// Parses ground speed velocity (subtypes 1-2).
    /// </summary>
    private ModeSMessage? ParseGroundSpeedVelocity(ValidatedFrame frame, VelocitySubtype subtype, int? verticalRate)
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
        if (trackAngle < 0)
        {
            trackAngle += 360;
        }

        return new AirborneVelocity(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            Velocity.FromKnots(groundSpeed, VelocityType.GroundSpeed),
            trackAngle,
            verticalRate,
            subtype);
    }

    /// <summary>
    /// Parses airspeed velocity (subtypes 3-4).
    /// </summary>
    private ModeSMessage? ParseAirspeedVelocity(ValidatedFrame frame, VelocitySubtype subtype, int? verticalRate)
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
            subtype);
    }

    /// <summary>
    /// Extracts vertical rate from TC 19 message (common to all subtypes).
    /// </summary>
    private int? ParseVerticalRate(ValidatedFrame frame)
    {
        // VrSrc (bit 68): 0=GNSS, 1=Barometric
        // int vrSrc = ExtractBits(frame.Data, 68, 1);  // Not used currently

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
    // Priority 3: Additional Formats (IMPLEMENTED)
    // ========================================

    /// <summary>
    /// Parses surface position with movement from Type Code 5-8.
    /// Extracts movement, ground track, and CPR-encoded position.
    /// Requires receiver location for CPR decoding.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type code (5-8).</param>
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
            // For Priority 3: Just acknowledge the RA, full decoding in Priority 4
            Log.Debug("TCAS RA from {Icao} (full RA decoding deferred to Priority 4)",
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
    /// Parses Operational Status message (TC 31).
    /// Priority 3: Implements essential fields (version, NACp, NICbaro, SIL).
    /// </summary>
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

        // Version number (bits 75-77, 3 bits)
        int versionRaw = ExtractBits(frame.Data, 75, 3);
        var version = (AdsbVersion)versionRaw;  // All values 0-7 are defined

        // NACp (Navigation Accuracy Category - Position, bits 59-62, 4 bits)
        int nacpRaw = ExtractBits(frame.Data, 59, 4);
        var nacp = (NavigationAccuracyCategory)nacpRaw;  // All values 0-15 are defined

        // NACv (Navigation Accuracy Category - Velocity, bits 43-45, 3 bits)
        // 0 = unknown, 1 = <10 m/s, 2 = <3 m/s, 3 = <1 m/s, 4 = <0.3 m/s
        int nacv = ExtractBits(frame.Data, 43, 3);

        // NICbaro (NIC barometric altitude integrity, bit 63, 1 bit)
        int nicBaroRaw = ExtractBits(frame.Data, 63, 1);
        bool nicBaro = nicBaroRaw == 1;

        // SIL (Surveillance Integrity Level, bits 64-65, 2 bits)
        int silRaw = ExtractBits(frame.Data, 64, 2);
        var sil = (SurveillanceIntegrityLevel)silRaw;  // All values 0-3 are defined

        return new OperationalStatus(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            subtype,
            version,
            nacp,
            nacv,
            nicBaro,
            sil);
    }

    // ========================================
    // Priority 4: Less Common Formats (IMPLEMENTED)
    // ========================================

    /// <summary>
    /// Parses Target State and Status message (TC 29).
    /// Priority 3: FULL implementation of all fields for both Version 1 and Version 2.
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
        string? altitudeSource = null;
        double? targetHeading = null;
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
        SurveillanceIntegrityLevel? sil = null;
        NavigationAccuracyCategory? nacp = null;
        bool? nicBaro = null;

        if (subtype == TargetStateSubtype.Version1)
        {
            // Version 1: Target altitude, target angle, modes, emergency

            // Target altitude available (bits 40-41, 2 bits)
            int altAvail = ExtractBits(frame.Data, 40, 2);
            if (altAvail != 0)
            {
                altitudeSource = altAvail switch
                {
                    // Altitude source
                    1 => "MCP/FCU",
                    2 => "Holding mode",
                    _ => "FMS/RNAV"
                };

                // Altitude reference (bit 42, 0=FL, 1=MSL)
                int altRef = ExtractBits(frame.Data, 42, 1);
                string altRefStr = altRef == 0 ? "FL" : "MSL";
                altitudeSource += $" ({altRefStr})";

                // Target altitude (bits 48-57, 10 bits)
                int altRaw = ExtractBits(frame.Data, 48, 10);
                int altitudeFeet = -1000 + (altRaw * 100);
                targetAltitude = Altitude.FromFeet(altitudeFeet, AltitudeType.Barometric);
            }

            // Vertical mode (bits 60-61, 2 bits)
            int verticalModeRaw = ExtractBits(frame.Data, 60, 2);
            if (verticalModeRaw != 0)
            {
                verticalMode = (VerticalMode)verticalModeRaw;
            }

            // Horizontal mode (bits 58-59, 2 bits)
            int horizontalModeRaw = ExtractBits(frame.Data, 58, 2);
            if (horizontalModeRaw != 0)
            {
                horizontalMode = (HorizontalMode)horizontalModeRaw;
            }

            // Target angle available (bits 62-63, 2 bits)
            int angleAvail = ExtractBits(frame.Data, 62, 2);
            if (angleAvail != 0)
            {
                // Target angle (bits 64-72, 9 bits)
                int angleRaw = ExtractBits(frame.Data, 64, 9);

                // Angle type (bit 73, 0=track, 1=heading)
                // int angleType = ExtractBits(frame.Data, 73, 1);

                targetHeading = angleRaw * (360.0 / 512.0);
            }

            // TCAS operational (bit 84, 1 bit)
            tcasOperational = ExtractBits(frame.Data, 84, 1) == 0;  // 0=operational

            // TCAS RA active (bit 85, 1 bit)
            tcasRaActive = ExtractBits(frame.Data, 85, 1) == 1;

            // Emergency/priority status (bits 86-88, 3 bits)
            int esRaw = ExtractBits(frame.Data, 86, 3);
            if (Enum.IsDefined(typeof(EmergencyState), esRaw))
            {
                emergencyPriority = (EmergencyState)esRaw;
            }
        }
        else // subtype == 1
        {
            // Version 2: Selected altitude, heading, pressure, modes, accuracy

            // SIL (Surveillance Integrity Level, bits 40-41, 2 bits)
            int silRaw = ExtractBits(frame.Data, 40, 2);
            sil = (SurveillanceIntegrityLevel)silRaw;

            // Selected altitude source (bit 42, 0=MCP/FCU, 1=FMS)
            int altSrc = ExtractBits(frame.Data, 42, 1);
            altitudeSource = altSrc == 0 ? "MCP/FCU" : "FMS";

            // Selected altitude (bits 43-53, 11 bits)
            int altRaw = ExtractBits(frame.Data, 43, 11);
            if (altRaw != 0)
            {
                int altitudeFeet = (altRaw - 1) * 32;
                targetAltitude = Altitude.FromFeet(altitudeFeet, AltitudeType.Barometric);
            }

            // Barometric pressure setting (bits 54-62, 9 bits)
            int baroRaw = ExtractBits(frame.Data, 54, 9);
            if (baroRaw != 0)
            {
                barometricPressure = 800.0 + ((baroRaw - 1) * 0.8);
            }

            // Selected heading (bits 63-72, 10 bits total: status + sign + value)
            int hdgStatus = ExtractBits(frame.Data, 63, 1);
            if (hdgStatus == 1)
            {
                int hdgSign = ExtractBits(frame.Data, 64, 1);
                int hdgRaw = ExtractBits(frame.Data, 65, 8);
                targetHeading = (hdgSign + 1) * hdgRaw * (180.0 / 256.0);
            }

            // NACp (Navigation Accuracy Category - Position, bits 73-76, 4 bits)
            int nacpRaw = ExtractBits(frame.Data, 73, 4);
            nacp = (NavigationAccuracyCategory)nacpRaw;

            // NICbaro (NIC barometric altitude integrity, bit 77, 1 bit)
            int nicBaroRaw = ExtractBits(frame.Data, 77, 1);
            nicBaro = nicBaroRaw == 1;

            // Autopilot engaged (bit 78, 1 bit)
            autopilotEngaged = ExtractBits(frame.Data, 78, 1) == 1;

            // VNAV mode (bit 79, 1 bit)
            vnavMode = ExtractBits(frame.Data, 79, 1) == 1;

            // Altitude hold mode (bit 80, 1 bit)
            altitudeHoldMode = ExtractBits(frame.Data, 80, 1) == 1;

            // Approach mode (bit 82, 1 bit)
            approachMode = ExtractBits(frame.Data, 82, 1) == 1;

            // TCAS/ACAS operational (bit 83, 1 bit)
            tcasOperational = ExtractBits(frame.Data, 83, 1) == 1;

            // LNAV mode (bit 84, 1 bit)
            lnavMode = ExtractBits(frame.Data, 84, 1) == 1;
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
