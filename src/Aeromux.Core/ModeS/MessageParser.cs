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
                DownlinkFormat.ShortAirAirSurveillance => ParseSurveillanceAltitude(frame),
                DownlinkFormat.AllCallReply => ParseAllCallReply(frame),

                // Priority 3: Additional surveillance formats
                DownlinkFormat.LongAirAirSurveillance => ParseLongAirAirSurveillance(frame),
                DownlinkFormat.SurveillanceAltitudeReply => ParseSurveillanceAltitude(frame),
                DownlinkFormat.SurveillanceIdentityReply => ParseSurveillanceIdentity(frame),

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
    /// Parses surveillance altitude reply from Downlink Format 0 or 4.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>SurveillanceReply message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseSurveillanceAltitude(ValidatedFrame frame)
    {
        // TODO Priority 2: Implement altitude decoding from DF 0/4 (Gillham code)
        return null;
    }

    /// <summary>
    /// Parses all-call reply from Downlink Format 11.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>AllCallReply message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseAllCallReply(ValidatedFrame frame)
    {
        // TODO Priority 2: Implement DF 11 all-call reply (capability extraction)
        return null;
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
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Surveillance identity message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseSurveillanceIdentity(ValidatedFrame frame)
    {
        // TODO Priority 3: Implement DF 5 identity reply (squawk code)
        return null;
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
