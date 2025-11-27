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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handler for a specific Mode S message type.
/// Encapsulates extraction and merging logic for one message type following the Strategy pattern.
/// Each message type has exactly ONE handler that knows what fields it provides and how to extract them.
/// </summary>
/// <remarks>
/// Design Principle: One Message Type = One Handler
/// <para>
/// This eliminates switch statements and makes the code self-documenting.
/// Each handler is responsible for:
/// - Extracting fields from its specific message type
/// - Merging those fields into aircraft state
/// - Tracking what changed for event notifications
/// </para>
/// </remarks>
public interface ITrackingHandler
{
    /// <summary>
    /// The Mode S message type this handler processes.
    /// Used for handler registration and lookup in the registry.
    /// </summary>
    Type MessageType { get; }

    /// <summary>
    /// Applies this message's data to aircraft tracking state.
    /// Updates only the fields that this message type provides.
    /// </summary>
    /// <param name="aircraft">Current aircraft state to update</param>
    /// <param name="message">Parsed Mode S message (guaranteed to be of MessageType)</param>
    /// <param name="frame">Complete processed frame (for signal strength, metadata, etc.)</param>
    /// <param name="timestamp">Current UTC timestamp</param>
    /// <returns>Tuple containing updated aircraft state and set of changed field names</returns>
    /// <remarks>
    /// Changed field names use dot notation for nested properties (e.g., "Identification.Callsign").
    /// The returned aircraft instance may be the same as input if no fields changed.
    /// </remarks>
    (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp);
}
