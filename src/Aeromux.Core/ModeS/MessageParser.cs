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
    private readonly SurfaceCprDecoder _surfaceCprDecoder = new();

    // Device context for logging (optional - if not set, logs without device prefix)
    private readonly string? _deviceName;
    private readonly int? _deviceIndex;

    // Statistics (Coordinator Pattern - ADR-009)
    private long _messagesParsed;
    private long _parseErrors;
    private long _unsupportedMessages;  // Unsupported DF/TC (not implemented yet)
    private readonly Dictionary<DownlinkFormat, long> _messagesByDF = new();
    private readonly Dictionary<int, long> _messagesByTC = new();

    public MessageParser(string? deviceName = null, int? deviceIndex = null)
    {
        _deviceName = deviceName;
        _deviceIndex = deviceIndex;

        // Initialize all DF counters to 0
        foreach (DownlinkFormat df in Enum.GetValues<DownlinkFormat>())
        {
            _messagesByDF[df] = 0;
        }
    }

    /// <summary>
    /// Sets the receiver location for surface position decoding (TC 5-8).
    /// Must be called if receiver location is configured in settings.
    /// </summary>
    /// <param name="receiverLocation">Receiver geographic coordinates.</param>
    public void SetReceiverLocation(GeographicCoordinate receiverLocation)
    {
        _surfaceCprDecoder.SetReceiverLocation(receiverLocation);
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

            // Log unusual high-altitude traffic (military/research aircraft)
            // Commercial aircraft typically don't exceed 50,000 feet
            if (altitudeFeet > 60000)
            {
                if (_deviceName != null && _deviceIndex.HasValue)
                {
                    Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}): High altitude detected: {Altitude} feet (above typical commercial ceiling of 60,000 feet)",
                        _deviceName, _deviceIndex.Value, altitudeFeet);
                }
                else
                {
                    Log.Debug("High altitude detected: {Altitude} feet (above typical commercial ceiling of 60,000 feet)", altitudeFeet);
                }
            }

            return Altitude.FromFeet(altitudeFeet, AltitudeType.Barometric);
        }

        // Mode 4: Q=0 (Gillham code - full implementation)
        int gillhamResult = DecodeGillham(ac13);
        if (gillhamResult == -9999)
        {
            if (_deviceName != null && _deviceIndex.HasValue)
            {
                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}): Invalid Gillham altitude code, AC={AC:X3}",
                    _deviceName, _deviceIndex.Value, ac13);
            }
            else
            {
                Log.Debug("Invalid Gillham altitude code, AC={AC:X3}", ac13);
            }
            return null;  // Invalid Gillham code
        }

        int gillhamAltitudeFeet = gillhamResult * 100;

        // Log unusual high-altitude traffic (military/research aircraft)
        // Commercial aircraft typically don't exceed 50,000 feet
        if (gillhamAltitudeFeet > 60000)
        {
            if (_deviceName != null && _deviceIndex.HasValue)
            {
                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}): High altitude detected: {Altitude} feet (above typical commercial ceiling of 60,000 feet)",
                    _deviceName, _deviceIndex.Value, gillhamAltitudeFeet);
            }
            else
            {
                Log.Debug("High altitude detected: {Altitude} feet (above typical commercial ceiling of 60,000 feet)", gillhamAltitudeFeet);
            }
        }

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
    // Priority 3: Additional Formats (TO IMPLEMENT)
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
            cprLat,
            cprLon,
            cprFormat,
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
            nicBaro,
            sil);
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
                // Altitude source
                if (altAvail == 1) altitudeSource = "MCP/FCU";
                else if (altAvail == 2) altitudeSource = "Holding mode";
                else altitudeSource = "FMS/RNAV";

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
