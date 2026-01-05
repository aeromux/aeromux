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
/// {
///   "Identification": {
///     "ICAO": "440CF8",
///     "Callsign": "UAL1234 ",
///     "Squawk": "1200",
///     "Category": "LargeTransport",
///     "EmergencyState": "NoEmergency",
///     "FlightStatus": "AirborneNormal",
///     "Version": 2
///   },
///   "Position": {
///     "Coordinate": {
///       "Latitude": 37.6213,
///       "Longitude": -122.3790
///     },
///     "BarometricAltitude": {
///       "Feet": 35000,
///       "Meters": 10668,
///       "Type": "Barometric"
///     },
///     "GeometricAltitude": null,
///     "IsOnGround": false,
///     "NACp": 9,
///     "NICbaro": true,
///     "LastUpdate": "2026-01-04T15:30:45.123Z"
///   },
///   "Velocity": {
///     "GroundSpeed": {
///       "Knots": 450,
///       "MetersPerSecond": 231.5
///     },
///     "Heading": 270.5,
///     "Track": 268.2,
///     "VerticalRate": 0,
///     "LastUpdate": "2026-01-04T15:30:45.123Z"
///   },
///   "Status": {
///     "SignalStrength": 125.6,
///     "TotalMessages": 1234,
///     "PositionMessages": 456,
///     "FirstSeen": "2026-01-04T15:25:30.000Z",
///     "LastSeen": "2026-01-04T15:30:45.123Z"
///   },
///   "Autopilot": {
///     "SelectedAltitude": {
///       "Feet": 36000,
///       "Type": "MCP"
///     },
///     "AutopilotEngaged": true,
///     "VNAVMode": true
///   },
///   "Acas": null,               // null in this example - populated when ACAS messages received (DF 0, 16, TC 29)
///   "FlightDynamics": null,     // null in this example - populated when BDS 5,0/5,3/6,0 messages received
///   "Meteo": null,              // null in this example - populated when BDS 4,4/4,5 messages received
///   "Capabilities": {
///     "TransponderLevel": "Level5",
///     "TCASCapability": true
///   },
///   "DataQuality": null,        // null in this example - populated when TC 31 or TC 29 V2 messages received
///   "OperationalMode": null     // null in this example - populated when TC 31 or DF 20/21 messages received
/// }
///
/// NOTES:
/// - Original C# property names preserved (ICAO, TCASOperational, etc.)
/// - Null properties included as "null" (not omitted) - properties become non-null when relevant messages are received
/// - Enums as string names (e.g., "NoEmergency", "Barometric")
/// - History excluded (for bandwidth efficiency)
/// - Transmitted as single line with \n terminator (NDJSON format)
/// - Rate limited: max 1 update/second per aircraft
/// </remarks>
public sealed class JsonEncoder : IDisposable
{
    private readonly IAircraftStateTracker _tracker;
    private readonly ConcurrentDictionary<string, DateTime> _lastOutputTimes;
    private readonly TimeSpan _minimumInterval = TimeSpan.FromSeconds(1);
    private readonly JsonSerializerOptions _serializerOptions;

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
    public byte[]? Encode(ProcessedFrame frame)
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

        // Exclude History by creating anonymous object
        var aircraftData = new
        {
            aircraft.Identification,
            aircraft.Position,
            aircraft.Velocity,
            aircraft.Status,
            aircraft.Autopilot,
            aircraft.Acas,
            aircraft.FlightDynamics,
            aircraft.Meteo,
            aircraft.Capabilities,
            aircraft.DataQuality,
            aircraft.OperationalMode
        };

        // Serialize to JSON
        string json = JsonSerializer.Serialize(aircraftData, _serializerOptions);
        return Encoding.UTF8.GetBytes(json + "\n");
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
