using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// Parses validated Mode S frames into structured message objects.
/// Implements the Coordinator Pattern (ADR-009) for statistics.
/// </summary>
/// <remarks>
/// Phase 5 Foundation: Complete DF/TC routing skeleton with statistics.
/// Priority 1-4: All parsers implemented across partial classes:
/// - MessageParser.cs: Core routing and statistics
/// - MessageParser.ExtendedSquitter.cs: DF 17/18/19 (ADS-B)
/// - MessageParser.Surveillance.cs: DF 0/4/5/11 (Basic surveillance)
/// - MessageParser.Acas.cs: DF 16 (ACAS coordination)
/// - MessageParser.CommB.cs: DF 20/21/24 (Comm-B + BDS registers)
/// - MessageParser.Helpers.cs: Utility and decoding methods
///
/// Statistics are exposed via properties and logged by the coordinator (DeviceWorker).
/// This class focuses on parsing and counting; logging is the coordinator's responsibility.
/// Uses Serilog for structured logging (ADR-007).
/// </remarks>
public sealed partial class MessageParser
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
    private long _validationFailures;   // Expected failures: invalid data, returns null
    private long _unexpectedErrors;     // Unexpected exceptions: bugs, should be 0
    private long _unsupportedMessages;  // Unsupported DF/TC (DF 24 Comm-D, rare formats)
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
    public void SetReceiverLocation(GeographicCoordinate receiverLocation) =>
        _surfaceCprDecoder.SetReceiverLocation(receiverLocation);

    /// <summary>
    /// Parses a validated frame into a structured message.
    /// </summary>
    /// <param name="frame">Validated frame from CrcValidator.</param>
    /// <returns>Parsed message, or null if parsing failed or message type is unsupported (DF 24).</returns>
    public ModeSMessage? ParseMessage(ValidatedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _messagesParsed++;
        _messagesByDF[frame.DownlinkFormat]++;

        try
        {
            ModeSMessage? message = frame.DownlinkFormat switch
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

            // Track validation failures (expected failures where parser returns null)
            // This includes: invalid field values, unsupported DF/TC, corrupt data
            if (message == null)
            {
                _validationFailures++;
            }

            return message;
        }
        catch (Exception ex)
        {
            // Track unexpected exceptions (bugs - should never happen in production)
            // Examples: IndexOutOfRangeException, NullReferenceException, ArgumentException
            _unexpectedErrors++;
            Log.Error(ex, "Unexpected exception parsing DF {DownlinkFormat} from ICAO {Icao24}",
                frame.DownlinkFormat, frame.IcaoAddress);
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
    /// Total number of validation failures (parser returned null due to invalid data).
    /// Expected failures include: invalid field values, corrupt data, unsupported messages.
    /// High count is normal in noisy RF environments.
    /// </summary>
    public long ValidationFailures => _validationFailures;

    /// <summary>
    /// Total number of unexpected exceptions (bugs in parser code).
    /// Should be zero in production. Non-zero indicates a programming error.
    /// </summary>
    public long UnexpectedErrors => _unexpectedErrors;

    /// <summary>
    /// Total number of unsupported messages (DF 24 Comm-D, rare formats).
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
