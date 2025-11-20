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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Encodes parsed ModeSMessage to JSON format for web clients.
/// Produces line-delimited JSON (NDJSON) suitable for streaming over TCP.
/// </summary>
/// <remarks>
/// IMPORTANT - Implementation Status:
/// This is a Phase 6 implementation that outputs individual message serialization.
/// Phase 7 (Aircraft Tracking) will introduce aircraft state aggregation similar to
/// readsb's --net-json-port, which outputs consolidated aircraft objects with 50+ fields
/// (position, velocity, identification, navigation data) aggregated from multiple messages.
/// See Issue #002 for details.
///
/// Current Behavior (Phase 6):
/// - Outputs every parsed message immediately (no aggregation)
/// - Message-level granularity (not aircraft-level)
/// - No rate limiting per aircraft
/// - Useful for message stream logging and debugging
///
/// Future Behavior (Phase 7):
/// - Will output aggregated aircraft state objects
/// - Aircraft-level granularity with state tracking
/// - Rate-limited per aircraft (configurable interval)
/// - Compatible with readsb/dump1090 ecosystem (tar1090, VRS)
///
/// JSON Format Specification:
/// - Each message is serialized as a single JSON object
/// - Properties use camelCase naming convention (web-friendly)
/// - Null properties are omitted from output (reduces bandwidth)
/// - Each JSON object is terminated with a newline character (\n)
/// - This creates NDJSON (Newline Delimited JSON) format for streaming
///
/// Why Line-Delimited JSON:
/// TCP streaming requires clear message boundaries. Line-delimited format allows
/// clients to parse messages incrementally by reading line-by-line, without needing
/// to implement a streaming JSON parser or custom framing protocol.
///
/// Null Handling:
/// Unparseable frames (null ModeSMessage) return null to skip broadcasting.
/// This filters out corrupted or incomplete frames at the encoder level.
/// </remarks>
public static class JsonEncoder
{
    /// <summary>
    /// Encodes a parsed message to JSON format with newline delimiter.
    /// Returns null if message is null (unparseable frame that should be skipped).
    /// </summary>
    /// <param name="message">Parsed Mode S message to encode</param>
    /// <returns>UTF-8 encoded JSON with newline terminator, or null if message is null</returns>
    public static byte[]? Encode(ModeSMessage? message)
    {
        if (message == null)
        {
            // Skip unparseable frames - return null to signal TcpBroadcaster to skip this frame
            return null;
        }

        // Serialize message to JSON using System.Text.Json
        // Options configured for web-friendly output:
        // - camelCase: JavaScript convention (e.g., "icaoAddress" not "IcaoAddress")
        // - WhenWritingNull: Omit null properties to reduce bandwidth and clutter
        string json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Append newline for line-delimited JSON streaming (NDJSON format)
        // This allows clients to parse by reading line-by-line
        return Encoding.UTF8.GetBytes(json + "\n");
    }
}
