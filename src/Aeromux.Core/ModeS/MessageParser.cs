using Aeromux.Core.ModeS.Messages;
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
    // Statistics (Coordinator Pattern - ADR-009)
    private long _messagesParsed;
    private long _parseErrors;
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
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type code (1-4).</param>
    /// <returns>AircraftIdentification message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseAircraftIdentification(ValidatedFrame frame, int tc)
    {
        // TODO Priority 1: Implement callsign decoding (8-character extraction from ME field)
        return null;
    }

    /// <summary>
    /// Parses airborne position with CPR-encoded coordinates from Type Code 9-18 or 20-22.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <param name="tc">Type code (9-18 for barometric, 20-22 for GNSS).</param>
    /// <param name="isGnss">True if GNSS altitude, false if barometric.</param>
    /// <returns>AirbornePosition message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseAirbornePosition(ValidatedFrame frame, int tc, bool isGnss)
    {
        // TODO Priority 1: Implement CPR position decoding (requires even/odd frame pairing)
        return null;
    }

    /// <summary>
    /// Parses airborne velocity (ground speed or airspeed) from Type Code 19.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>AirborneVelocity message, or null (currently unimplemented).</returns>
    private ModeSMessage? ParseAirborneVelocity(ValidatedFrame frame)
    {
        // TODO Priority 1: Implement velocity decoding (subtype 1-4: ground speed, TAS, IAS)
        return null;
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
    // Logging Helpers
    // ========================================

    private ModeSMessage? LogUnsupportedDF(ValidatedFrame frame)
    {
        Log.Debug("Unsupported DF {DownlinkFormat} from ICAO {Icao24}",
            frame.DownlinkFormat, frame.IcaoAddress);
        return null;
    }

    private ModeSMessage? LogUnsupportedTC(ValidatedFrame frame, int tc)
    {
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
    /// Total number of parse errors encountered.
    /// </summary>
    public long ParseErrors => _parseErrors;

    /// <summary>
    /// Message count by Downlink Format.
    /// </summary>
    public IReadOnlyDictionary<DownlinkFormat, long> MessagesByDF => _messagesByDF;

    /// <summary>
    /// Message count by Type Code (for DF 17/18 only).
    /// </summary>
    public IReadOnlyDictionary<int, long> MessagesByTC => _messagesByTC;
}
