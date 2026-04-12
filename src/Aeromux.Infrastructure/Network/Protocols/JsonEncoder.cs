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

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aeromux.Core.ModeS;
using Aeromux.Core.Tracking;

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Encodes Aircraft objects to JSON format with rate limiting.
/// Uses aircraft state tracker to access consolidated aircraft data for each frame.
/// Implements per-aircraft rate limiting (max 1 update/second) internally.
/// </summary>
/// <remarks>
/// ARCHITECTURE:
/// - Takes ProcessedFrame from frame stream
/// - Extracts ICAO address from frame's parsed message
/// - Looks up Aircraft from state tracker
/// - Applies rate limiting per ICAO
/// - Serializes Aircraft to JSON (not just the message)
///
/// OUTPUT FORMAT (formatted for readability, transmitted as single line over TCP):
/// Same JSON structure as the REST API detail endpoint (/api/v1/aircraft/{icao}).
/// {
///   "Timestamp": "2026-01-04T15:30:45.123Z",
///   "Identification": {
///     "ICAO": "440CF8",
///     "Callsign": "UAL1234 ",
///     "Squawk": "1200",
///     "Category": "LargeTransport",
///     "EmergencyState": "NoEmergency",
///     "FlightStatus": "AirborneNormal",
///     "AdsbVersion": "DO260B"
///   },
///   "DatabaseRecord": {
///     "Registration": "D-AIZZ",
///     "TypeCode": "A320",
///     ...
///   },
///   "Status": {
///     "FirstSeen": "2026-01-04T15:25:30.000Z",
///     "LastSeen": "2026-01-04T15:30:45.123Z",
///     "TotalMessages": 1234,
///     "PositionMessages": 456,
///     "VelocityMessages": 78,
///     "IdentificationMessages": 12,
///     "SignalStrength": -28.4
///   },
///   "Position": {
///     "Coordinate": { "Latitude": 37.6213, "Longitude": -122.3790 },
///     "BarometricAltitude": { "Feet": 35000, "Meters": 10668, "Type": "Barometric" },
///     "GeometricAltitude": null,
///     "GeometricBarometricDelta": null,
///     "IsOnGround": false,
///     "MovementCategory": null,
///     "Source": "Sdr",
///     "HadMlatPosition": false,
///     "LastUpdate": "2026-01-04T15:30:45.123Z"
///   },
///   "VelocityAndDynamics": {
///     "Speed": { "Knots": 450, "MetersPerSecond": 231.5, "Type": "GroundSpeed" },
///     "IndicatedAirspeed": null,
///     "TrueAirspeed": null,
///     "GroundSpeed": null,
///     "MachNumber": null,
///     "Track": 268.2,
///     "TrackAngle": null,
///     "MagneticHeading": null,
///     "TrueHeading": null,
///     "Heading": 270.5,
///     "HeadingType": null,
///     "HorizontalReference": null,
///     "VerticalRate": 0,
///     "BarometricVerticalRate": null,
///     "InertialVerticalRate": null,
///     "RollAngle": null,
///     "TrackRate": null,
///     "SpeedOnGround": null,
///     "TrackOnGround": null,
///     "LastUpdate": "2026-01-04T15:30:45.123Z"
///   },
///   "Autopilot": null,          // null until TC 29 or BDS 4,0 messages received
///   "Meteorology": null,        // null until BDS 4,4/4,5 messages received
///   "Acas": null,               // null until ACAS messages received (DF 0, 16, TC 29)
///   "Capabilities": null,       // null until DF 11, TC 31, or BDS 1,0/1,7 messages received
///   "DataQuality": null         // null until TC 31, TC 29 V2, or DF 20/21 messages received
/// }
///
/// NOTES:
/// - Format matches the REST API detail endpoint for consumer consistency
/// - Fields are renamed, regrouped, and combined from multiple tracked classes
/// - OperationalMode fields distributed into Acas, Capabilities, and DataQuality sections
/// - Null sections included as "null" (not omitted) - become non-null when relevant messages arrive
/// - Enums as string names (e.g., "NoEmergency", "Barometric")
/// - Transmitted as single line with \n terminator (NDJSON format)
/// - Rate limited: max 1 update/second per aircraft
/// </remarks>
public sealed class JsonEncoder : IDisposable
{
    private readonly IAircraftStateTracker _tracker;
    private readonly ConcurrentDictionary<string, DateTime> _lastOutputTimes;
    private readonly TimeSpan _minimumInterval = TimeSpan.FromSeconds(1);
    private readonly JsonSerializerOptions _serializerOptions;

    // Reusable encode buffer for UTF-8 output — avoids per-frame byte[] allocations
    // Grows to accommodate largest JSON payload, then stays at that size
    private byte[] _encodeBuffer = new byte[4096];

    /// <summary>
    /// Initializes a new JSON encoder with aircraft state tracker integration.
    /// </summary>
    /// <param name="tracker">Aircraft state tracker for accessing consolidated aircraft data</param>
    public JsonEncoder(IAircraftStateTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _lastOutputTimes = new ConcurrentDictionary<string, DateTime>();

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Preserve C# names for consistency with models and easier debugging
            DefaultIgnoreCondition = JsonIgnoreCondition.Never, // Include nulls to show field schema to clients
            Converters = { new JsonStringEnumConverter() } // Enums as strings for human readability
        };
    }

    /// <summary>
    /// Encodes a processed frame to JSON format with rate limiting.
    /// Returns null if message is unparseable, aircraft not found, or rate limited.
    /// </summary>
    /// <param name="frame">Processed frame containing validated frame and parsed message</param>
    /// <returns>UTF-8 encoded JSON with newline terminator, or null if skipped</returns>
    public ReadOnlyMemory<byte>? Encode(ProcessedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // Skip if message failed to parse
        if (frame.ParsedMessage == null)
        {
            return null;
        }

        string icao = frame.ParsedMessage.IcaoAddress;
        DateTime now = DateTime.UtcNow;

        // Apply rate limiting
        if (!ShouldOutput(icao, now))
        {
            return null; // Skip this update, too frequent
        }

        // Get aircraft state from tracker
        Aircraft? aircraft = _tracker.GetAircraft(icao);
        if (aircraft == null)
        {
            return null; // Aircraft not tracked yet
        }

        // Record output time
        _lastOutputTimes[icao] = now;

        // Map aircraft state to broadcast format (same structure as REST API)
        var response = new Dictionary<string, object?>
        {
            ["Timestamp"] = DateTime.UtcNow,
            ["Identification"] = JsonBroadcastMapper.ToIdentification(aircraft),
            ["DatabaseRecord"] = JsonBroadcastMapper.ToDatabaseRecord(aircraft),
            ["Status"] = JsonBroadcastMapper.ToStatus(aircraft),
            ["Position"] = JsonBroadcastMapper.ToPosition(aircraft),
            ["VelocityAndDynamics"] = JsonBroadcastMapper.ToVelocityAndDynamics(aircraft),
            ["Autopilot"] = JsonBroadcastMapper.ToAutopilot(aircraft),
            ["Meteorology"] = JsonBroadcastMapper.ToMeteorology(aircraft),
            ["Acas"] = JsonBroadcastMapper.ToAcas(aircraft),
            ["Capabilities"] = JsonBroadcastMapper.ToCapabilities(aircraft),
            ["DataQuality"] = JsonBroadcastMapper.ToDataQuality(aircraft)
        };

        // Serialize to JSON and encode into reusable buffer
        string json = JsonSerializer.Serialize(response, _serializerOptions);
        int byteCount = Encoding.UTF8.GetByteCount(json) + 1; // +1 for \n
        if (byteCount > _encodeBuffer.Length)
        {
            _encodeBuffer = new byte[byteCount * 2];
        }

        int written = Encoding.UTF8.GetBytes(json, _encodeBuffer);
        _encodeBuffer[written] = (byte)'\n';
        return _encodeBuffer.AsMemory(0, written + 1);
    }

    /// <summary>
    /// Checks if output is allowed for this aircraft based on rate limiting.
    /// </summary>
    private bool ShouldOutput(string icao, DateTime now)
    {
        if (!_lastOutputTimes.TryGetValue(icao, out DateTime lastOutput))
        {
            return true; // First output for this aircraft
        }

        return now - lastOutput >= _minimumInterval;
    }

    /// <summary>
    /// Disposes the encoder and clears rate limiting state.
    /// Releases memory used for tracking per-aircraft output timestamps.
    /// </summary>
    public void Dispose() =>
        _lastOutputTimes.Clear();
}
