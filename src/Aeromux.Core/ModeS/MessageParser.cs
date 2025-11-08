using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// Parses validated Mode S frames into structured message objects.
/// Implements the Coordinator Pattern (ADR-009) for statistics.
/// </summary>
/// <remarks>
/// Phase 5 Foundation: Complete DF/TC routing skeleton with statistics.
/// Priority 1-4: Implement specific parsers (TC 1-4, 9-18, 19 for Priority 1).
///
/// Statistics are exposed via properties and logged by the coordinator (DeviceWorker).
/// This class focuses on parsing and counting; logging is the coordinator's responsibility.
/// Uses Serilog for structured logging (ADR-007).
/// </remarks>
public sealed class MessageParser
{
    // AIS character set for callsign decoding (6-bit to ASCII mapping)
    // Reference: ICAO Annex 10, Volume IV, Table A-2-6
    private static readonly char[] AisCharset =
        "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_ !\"#$%&'()*+,-./0123456789:;<=>?".ToCharArray();

    // CPR decoder for position messages
    private readonly CprDecoder _cprDecoder = new();

    // Statistics (Coordinator Pattern - ADR-009)
    private long _messagesParsed;
    private long _parseErrors;
    private long _unsupportedMessages;  // Unsupported DF/TC (not implemented yet)
    private readonly Dictionary<DownlinkFormat, long> _messagesByDF = new();
    private readonly Dictionary<int, long> _messagesByTC = new();

    public MessageParser()
    {
        // Initialize all DF counters to 0
        foreach (DownlinkFormat df in Enum.GetValues<DownlinkFormat>())
        {
            _messagesByDF[df] = 0;
        }
    }

    /// <summary>
    /// Parses a validated frame into a structured message.
    /// </summary>
    /// <param name="frame">Validated frame from CrcValidator.</param>
    /// <returns>Parsed message, or null if parsing failed or message type is not yet implemented.</returns>
    public ModeSMessage? ParseMessage(ValidatedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _messagesParsed++;
        _messagesByDF[frame.DownlinkFormat]++;

        try
        {
            return frame.DownlinkFormat switch
            {
                // Priority 1: ADS-B Extended Squitter (most common)
                DownlinkFormat.ExtendedSquitter => ParseExtendedSquitter(frame),
                DownlinkFormat.ExtendedSquitterNonTransponder => ParseExtendedSquitter(frame),

                // Priority 2: Basic surveillance
                DownlinkFormat.ShortAirAirSurveillance => ParseShortAirAirSurveillance(frame),
                DownlinkFormat.SurveillanceAltitudeReply => ParseSurveillanceAltitudeReply(frame),
                DownlinkFormat.SurveillanceIdentityReply => ParseSurveillanceIdentityReply(frame),
                DownlinkFormat.AllCallReply => ParseAllCallReply(frame),

                // Priority 3: Additional surveillance formats
                DownlinkFormat.LongAirAirSurveillance => ParseLongAirAirSurveillance(frame),

                // Priority 4: Less common formats
                DownlinkFormat.MilitaryExtendedSquitter => ParseExtendedSquitter(frame),
                DownlinkFormat.CommBAltitudeReply => ParseCommBAltitudeReply(frame),
                DownlinkFormat.CommBIdentityReply => ParseCommBIdentityReply(frame),
                DownlinkFormat.CommDExtendedLength => ParseCommDExtendedLength(frame),

                // Not implemented (rare formats)
                _ => LogUnsupportedDF(frame)
            };
        }
        catch (Exception ex)
        {
            _parseErrors++;
            Log.Warning(ex, "Failed to parse DF {DownlinkFormat} from ICAO {Icao24}",
                frame.DownlinkFormat, frame.IcaoAddress);
            return null;
        }
    }

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
    // Priority 1: Essential ADS-B (TO IMPLEMENT)
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

        // Extract 8 characters (48 bits = 6 bytes, starting at bit 41)
        // Each character is 6 bits, packed across bytes 5-10
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
            cprLat,
            cprLon,
            cprFormat,
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

        // Validate subtype range (1-4)
        if (subtypeRaw < 1 || subtypeRaw > 4)
        {
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
    // Priority 2: Basic Surveillance (TO IMPLEMENT)
    // ========================================

    /// <summary>
    /// Parses Short Air-Air Surveillance message (DF 0).
    /// Extracts flight status and altitude for ACAS coordination.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Short air-air surveillance message with altitude and flight status.</returns>
    /// <remarks>
    /// DF 0 is used for ACAS (Airborne Collision Avoidance System) coordination between aircraft.
    /// Structure identical to DF 4, but semantic purpose is aircraft-to-aircraft coordination.
    /// </remarks>
    private ModeSMessage? ParseShortAirAirSurveillance(ValidatedFrame frame)
    {
        // Extract Flight Status (FS) field from bits 6-8 (byte 0, bits 0-2)
        int flightStatusRaw = frame.Data[0] & 0x07;
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 0 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Altitude Code (AC) field from bits 20-32
        int altitudeCode = ((frame.Data[2] & 0x1F) << 8) | (frame.Data[3] >> 1);

        // Decode altitude (null if invalid or unavailable)
        Altitude? altitude = DecodeAltitudeAC13(altitudeCode);

        return new ShortAirAirSurveillance(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            altitude,
            flightStatus);
    }

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
        int flightStatusRaw = frame.Data[0] & 0x07;
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 4 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Altitude Code (AC) field from bits 20-32
        int altitudeCode = ((frame.Data[2] & 0x1F) << 8) | (frame.Data[3] >> 1);

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
    /// Capability values: 0=Level 1, 4=Level 2+ on-ground, 5=Level 2+ airborne, 6=uncertain, 7=special condition.
    /// Reserved values (1-3) are logged and rejected.
    /// </remarks>
    private ModeSMessage? ParseAllCallReply(ValidatedFrame frame)
    {
        // Extract Capability (CA) field from bits 6-8 (byte 0, bits 0-2)
        int capabilityRaw = frame.Data[0] & 0x07;

        // Validate capability value (0-7 are defined in TransponderCapability enum)
        if (!Enum.IsDefined(typeof(TransponderCapability), capabilityRaw))
        {
            Log.Debug("Invalid capability value {Capability} in DF 11 from {Icao}",
                capabilityRaw, frame.IcaoAddress);
            return null;
        }

        var capability = (TransponderCapability)capabilityRaw;

        return new AllCallReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            capability);
    }

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
    private static Altitude? DecodeAltitudeAC13(int ac13)
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
        if (gillhamResult < -12)
        {
            Log.Debug("Invalid Gillham altitude code, AC={AC:X3}", ac13);
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
        if (oneHundreds > 5)
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
    // Priority 3: Additional Formats (TO IMPLEMENT)
    // ========================================

    /// <summary>
    /// Parses surface position with movement from Type Code 5-8.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type code (5-8).</param>
    /// <returns>Surface position message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseSurfacePosition(ValidatedFrame frame, int tc)
    {
        // TODO Priority 3: Implement surface position decoding (CPR + ground track)
        return null;
    }

    /// <summary>
    /// Parses aircraft status from Type Code 28.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Aircraft status message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseAircraftStatus(ValidatedFrame frame)
    {
        // TODO Priority 3: Implement TC 28 status decoding (emergency, TCAS RA)
        return null;
    }

    /// <summary>
    /// Parses aircraft operation status from Type Code 31.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Operation status message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseAircraftOperationStatus(ValidatedFrame frame)
    {
        // TODO Priority 3: Implement TC 31 operation status (version, capabilities)
        return null;
    }

    /// <summary>
    /// Parses long air-air surveillance from Downlink Format 16.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Long surveillance message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseLongAirAirSurveillance(ValidatedFrame frame)
    {
        // TODO Priority 3: Implement DF 16 long surveillance (ACAS)
        return null;
    }

    /// <summary>
    /// Parses surveillance identity reply from Downlink Format 5.
    /// Extracts flight status and squawk code (identity code).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Surveillance identity reply message with squawk code.</returns>
    /// <remarks>
    /// DF 5 messages contain a 13-bit identity code (squawk code) that requires
    /// bit rearrangement to extract the 4-digit octal code.
    /// </remarks>
    private ModeSMessage? ParseSurveillanceIdentityReply(ValidatedFrame frame)
    {
        // Extract Flight Status (FS) field from bits 6-8 (byte 0, bits 0-2)
        int flightStatusRaw = frame.Data[0] & 0x07;
        if (!Enum.IsDefined(typeof(FlightStatus), flightStatusRaw))
        {
            Log.Debug("Invalid flight status {FS} in DF 5 from {Icao}",
                flightStatusRaw, frame.IcaoAddress);
            return null;
        }

        var flightStatus = (FlightStatus)flightStatusRaw;

        // Extract Identity Code (ID) field from bits 20-32
        int identityCode = ((frame.Data[2] & 0x1F) << 8) | (frame.Data[3] >> 1);

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
    /// Algorithm verified against pyModeS common.idcode and common.squawk functions.
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

    // ========================================
    // Priority 4: Less Common Formats (TO IMPLEMENT)
    // ========================================

    /// <summary>
    /// Parses target state and status from Type Code 29.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Target state message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseTargetStateAndStatus(ValidatedFrame frame)
    {
        // TODO Priority 4: Implement TC 29 target state (selected altitude, heading)
        return null;
    }

    /// <summary>
    /// Parses Comm-B altitude reply from Downlink Format 20.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Comm-B altitude message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseCommBAltitudeReply(ValidatedFrame frame)
    {
        // TODO Priority 4: Implement DF 20 Comm-B altitude (altitude + 56-bit data)
        return null;
    }

    /// <summary>
    /// Parses Comm-B identity reply from Downlink Format 21.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Comm-B identity message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseCommBIdentityReply(ValidatedFrame frame)
    {
        // TODO Priority 4: Implement DF 21 Comm-B identity (squawk + 56-bit data)
        return null;
    }

    /// <summary>
    /// Parses Comm-D extended length message from Downlink Format 24.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Comm-D message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseCommDExtendedLength(ValidatedFrame frame)
    {
        // TODO Priority 4: Implement DF 24 Comm-D Extended Length (ELM protocol)
        return null;
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
    /// Decodes 12-bit altitude field using Q-bit method.
    /// </summary>
    /// <param name="altRaw">12-bit altitude field (bits 41-52).</param>
    /// <param name="altitudeType">Altitude type (Barometric or GNSS).</param>
    /// <returns>Decoded altitude, or null if unavailable.</returns>
    private static Altitude? DecodeAltitude(int altRaw, AltitudeType altitudeType)
    {
        // Special case: all zeros means altitude unavailable
        if (altRaw == 0)
        {
            return null;
        }

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

    // ========================================
    // Logging Helpers
    // ========================================

    private ModeSMessage? LogUnsupportedDF(ValidatedFrame frame)
    {
        _unsupportedMessages++;
        Log.Debug("Unsupported DF {DownlinkFormat} from ICAO {Icao24}",
            frame.DownlinkFormat, frame.IcaoAddress);
        return null;
    }

    private ModeSMessage? LogUnsupportedTC(ValidatedFrame frame, int tc)
    {
        _unsupportedMessages++;
        Log.Debug("Unsupported TC {TypeCode} in DF {DownlinkFormat} from ICAO {Icao24}",
            tc, frame.DownlinkFormat, frame.IcaoAddress);
        return null;
    }

    // ========================================
    // Statistics Properties (Coordinator Pattern - ADR-009)
    // ========================================

    /// <summary>
    /// Total number of messages parsed (successfully or with errors).
    /// </summary>
    public long MessagesParsed => _messagesParsed;

    /// <summary>
    /// Total number of parse errors encountered (exceptions during parsing).
    /// </summary>
    public long ParseErrors => _parseErrors;

    /// <summary>
    /// Total number of unsupported messages (DF/TC not yet implemented).
    /// These are valid messages that are logged but not parsed.
    /// </summary>
    public long UnsupportedMessages => _unsupportedMessages;

    /// <summary>
    /// Message count by Downlink Format.
    /// </summary>
    public IReadOnlyDictionary<DownlinkFormat, long> MessagesByDF => _messagesByDF;

    /// <summary>
    /// Message count by Type Code (for DF 17/18 only).
    /// </summary>
    public IReadOnlyDictionary<int, long> MessagesByTC => _messagesByTC;
}
