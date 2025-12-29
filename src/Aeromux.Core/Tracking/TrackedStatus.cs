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

using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft tracking status and metadata group.
/// Contains signal quality, message counters, timing information, and rate limiting.
/// Sources: Signal processing, message counters, timing information, TC 31 (Operational Status).
/// </summary>
public sealed record TrackedStatus
{
    /// <summary>
    /// Signal strength / RSSI (Received Signal Strength Indicator).
    /// Relative scale 0-255 from rtl_sdr, higher = stronger signal.
    /// Used for signal quality assessment and antenna positioning.
    /// Updated on every received frame.
    /// </summary>
    public double? SignalStrength { get; init; }

    /// <summary>
    /// Total count of all messages received from this aircraft.
    /// Incremented on every frame, regardless of message type.
    /// Used for tracking reception quality and coverage.
    /// </summary>
    public int TotalMessages { get; init; }

    /// <summary>
    /// Count of position messages received (TC 5-8 surface, TC 9-18 airborne, TC 20-22 airborne GNSS).
    /// Used to assess position update rate and coverage quality.
    /// Typical rate: 0.5-2 Hz (every 0.5-2 seconds).
    /// </summary>
    public int PositionMessages { get; init; }

    /// <summary>
    /// Count of velocity messages received (TC 19 airborne velocity).
    /// Used to assess velocity update rate.
    /// Typical rate: 0.5-2 Hz (every 0.5-2 seconds).
    /// </summary>
    public int VelocityMessages { get; init; }

    /// <summary>
    /// Count of identification messages received (TC 1-4 aircraft identification).
    /// Used to assess callsign update frequency.
    /// Typical rate: 0.1-0.2 Hz (every 5-10 seconds).
    /// </summary>
    public int IdentificationMessages { get; init; }

    /// <summary>
    /// Timestamp when this aircraft was first detected and added to tracking.
    /// Never changes after aircraft creation.
    /// Used for calculating total tracking duration (SeenSeconds).
    /// </summary>
    public DateTime FirstSeen { get; init; }

    /// <summary>
    /// Timestamp of most recent message received from this aircraft.
    /// Updated on every frame.
    /// Used for expiration checking (IsExpired) and staleness calculation.
    /// </summary>
    public DateTime LastSeen { get; init; }

    /// <summary>
    /// Total duration in seconds since aircraft was first seen.
    /// Calculated as: (now - FirstSeen).TotalSeconds.
    /// Updated on every frame.
    /// </summary>
    public double SeenSeconds { get; init; }

    /// <summary>
    /// Seconds since last position update (last time Coordinate or altitude changed).
    /// Null if no position data ever received.
    /// Used for determining position data staleness.
    /// Matches readsb's "seen_pos" field.
    /// </summary>
    public double? SeenPosSeconds { get; init; }

    /// <summary>
    /// Rate limiting timestamp for JSON broadcast output.
    /// Next time this aircraft can be broadcasted in JSON format.
    /// Prevents flooding network with too-frequent updates.
    /// Updated based on adaptive rate limiting rules.
    /// </summary>
    public DateTime NextJsonOutput { get; init; }
}
