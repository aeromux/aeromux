// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// Parses validated Mode S frames into structured message objects.
/// Implements the Coordinator Pattern for statistics.
/// </summary>
/// <remarks>
/// Complete DF/TC routing skeleton with statistics.
/// All parsers implemented across partial classes:
/// - MessageParser.cs: Core routing and statistics
/// - MessageParser.ExtendedSquitter.cs: DF 17/18/19 (ADS-B, Extended Squitter)
/// - MessageParser.Surveillance.cs: DF 4/5 (Surveillance)
/// - MessageParser.AllCall.cs: DF 11 (All-Call reply)
/// - MessageParser.Acas.cs: DF 0/16 (ACAS coordination)
/// - MessageParser.CommB.cs: DF 20/21/24 (Comm-B + BDS registers)
/// - MessageParser.Helpers.cs: Utility and decoding methods
///
/// Supported ADS-B Type Codes (TC) for DF 17/18:
/// - TC 1-4: Aircraft identification and category (callsign)
/// - TC 5-8: Surface position (CPR - Compact Position Reporting encoded, requires local decoding)
/// - TC 9-18: Airborne position (barometric altitude, CPR encoded)
/// - TC 19: Airborne velocity (ground speed, heading, vertical rate)
/// - TC 20-22: Airborne position (GNSS altitude, CPR encoded)
/// - TC 28: Aircraft status (emergency/priority, TCAS RA - Resolution Advisory)
/// - TC 29: Target state and status (selected altitude, autopilot modes)
/// - TC 31: Operational status (version, capabilities, NIC/NACp/SIL)
///
/// Statistics are exposed via properties and logged by the coordinator (DeviceWorker).
/// This class focuses on parsing and counting; logging is the coordinator's responsibility.
/// Uses Serilog for structured logging.
/// </remarks>
public sealed partial class MessageParser
{
    // Mode S character set for callsign decoding (6-bit to ASCII mapping)
    // Used for aircraft identification in BDS 2,0 and TC 1-4 messages
    // Reference: ICAO Annex 10, Volume IV, Table A-2-6
    private static readonly char[] AisCharset =
        "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_ !\"#$%&'()*+,-./0123456789:;<=>?".ToCharArray();

    // CPR (Compact Position Reporting) decoders for position messages
    private readonly CprDecoder _cprDecoder = new();
    private readonly SurfaceCprDecoder _surfaceCprDecoder = new();

    // Device context for logging (optional - if not set, logs without device prefix)
    private readonly string? _deviceName;
    private readonly int? _deviceIndex;

    // Statistics
    private long _messagesParsed;
    private long _validationFailures;   // Expected failures: invalid data, returns null
    private long _unexpectedErrors;     // Unexpected exceptions: bugs, should be 0
    private long _unsupportedMessages;  // Unsupported DF/TC (DF 24 Comm-D, rare formats)

    // Message counts by kind, indexed by code: DF and TC are 5-bit fields, BDS by its enum
    // value. Arrays rather than dictionaries, incremented with Interlocked, because the
    // REST API reads them from another thread while the source writes, and the MLAT input
    // parses the frames of every connected mlat-client through one parser at once.
    private readonly long[] _messagesByDF = new long[32];
    private readonly long[] _messagesByTC = new long[32];
    private readonly long[] _messagesByBDS = new long[Enum.GetValues<BdsCode>().Max(b => (int)b) + 1];

    public MessageParser(string? deviceName = null, int? deviceIndex = null)
    {
        _deviceName = deviceName;
        _deviceIndex = deviceIndex;
    }

    /// <summary>
    /// Sets the receiver location for surface position decoding (TC 5-8).
    /// Must be called if receiver location is configured in settings.
    /// </summary>
    /// <param name="receiverLocation">Receiver geographic coordinates.</param>
    public void SetReceiverLocation(GeographicCoordinate receiverLocation)
    {
        _surfaceCprDecoder.SetReceiverLocation(receiverLocation);
        _cprDecoder.SetReceiverLocation(receiverLocation);
    }

    /// <summary>
    /// Parses a validated frame into a structured message.
    /// </summary>
    /// <param name="frame">Validated frame from ValidatedFrameFactory.</param>
    /// <returns>Parsed message, or <see langword="null"/> if parsing failed or message type is unsupported (DF 24).</returns>
    public ModeSMessage? ParseMessage(ValidatedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _messagesParsed++;
        Interlocked.Increment(ref _messagesByDF[(int)frame.DownlinkFormat]);

        try
        {
            ModeSMessage? message = frame.DownlinkFormat switch
            {
                // ADS-B Extended Squitter
                DownlinkFormat.ExtendedSquitter => ParseExtendedSquitter(frame),
                DownlinkFormat.ExtendedSquitterNonTransponder => ParseNonTransponderExtendedSquitter(frame),

                // Basic surveillance
                DownlinkFormat.SurveillanceAltitudeReply => ParseSurveillanceAltitudeReply(frame),
                DownlinkFormat.SurveillanceIdentityReply => ParseSurveillanceIdentityReply(frame),
                DownlinkFormat.AllCallReply => ParseAllCallReply(frame),

                // Additional surveillance formats
                DownlinkFormat.ShortAirAirSurveillance => ParseShortAirAirSurveillance(frame),
                DownlinkFormat.LongAirAirSurveillance => ParseLongAirAirSurveillance(frame),

                // Less common formats
                DownlinkFormat.CommBAltitudeReply => ParseCommBAltitudeReply(frame),
                DownlinkFormat.CommBIdentityReply => ParseCommBIdentityReply(frame),
                DownlinkFormat.CommDExtendedLength => ParseCommDExtendedLength(frame),

                // Not implemented: rare formats, and DF 19 (military, body not standardized)
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
            Log.Error(ex, "Unexpected exception parsing DF {DownlinkFormat} from ICAO {IcaoAddress}",
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
        Log.Debug("Unsupported DF {DownlinkFormat} from ICAO {IcaoAddress}",
            frame.DownlinkFormat, frame.IcaoAddress);
        return null;
    }

    private ModeSMessage? LogUnsupportedTC(ValidatedFrame frame, int tc)
    {
        _unsupportedMessages++;
        Log.Debug("Unsupported TC {TypeCode} in DF {DownlinkFormat} from ICAO {IcaoAddress}",
            tc, frame.DownlinkFormat, frame.IcaoAddress);
        return null;
    }

    // ========================================
    // Statistics Properties
    // ========================================

    /// <summary>
    /// Total number of messages parsed (successfully or with errors).
    /// </summary>
    public long MessagesParsed => _messagesParsed;

    /// <summary>
    /// Total number of validation failures (parser returned <see langword="null"/> due to invalid data).
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
    /// Gets the messages parsed so far by downlink format, type code and Comm-B register,
    /// each kind holding only the codes seen at least once. Safe to call from any thread.
    /// </summary>
    public MessageTypeCounts GetMessageTypeCounts() => new(
        Read(_messagesByDF, code => code),
        Read(_messagesByTC, code => code),
        Read(_messagesByBDS, code => (BdsCode)code));

    private static Dictionary<TKey, long> Read<TKey>(long[] counts, Func<int, TKey> key)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, long>();
        for (int code = 0; code < counts.Length; code++)
        {
            long count = Interlocked.Read(ref counts[code]);
            if (count > 0)
            {
                result[key(code)] = count;
            }
        }
        return result;
    }
}
