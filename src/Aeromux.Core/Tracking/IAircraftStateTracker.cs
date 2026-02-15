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

using Aeromux.Core.ModeS;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Thread-safe aircraft state tracking service.
/// Maintains comprehensive state for all tracked aircraft including current values
/// and historical data (position, altitude, velocity over time).
/// Provides synchronous queries and event-based notifications.
/// </summary>
public interface IAircraftStateTracker
{
    // === State Updates ===

    /// <summary>
    /// Updates aircraft state with data from a processed frame.
    /// Creates new aircraft entry if ICAO address not seen before.
    /// Updates existing aircraft state if already tracked.
    /// Thread-safe: Uses ConcurrentDictionary internally.
    /// </summary>
    /// <param name="frame">Processed frame containing raw Mode S data and parsed message</param>
    void Update(ProcessedFrame frame);

    // === State Queries ===

    /// <summary>
    /// Gets all currently tracked aircraft (excludes expired).
    /// Returns a snapshot copy ordered by ICAO address (thread-safe for iteration).
    /// Expired aircraft (not seen within AircraftTimeout) are filtered out.
    /// </summary>
    /// <returns>Read-only list of all non-expired tracked aircraft, sorted by ICAO</returns>
    IReadOnlyList<Aircraft> GetAllAircraft();

    /// <summary>
    /// Gets a specific aircraft by ICAO address.
    /// Returns null if aircraft not found or has expired.
    /// </summary>
    /// <param name="icao">24-bit ICAO address as 6-character hex string (e.g., "440CF8")</param>
    /// <returns>Aircraft state if found and not expired, otherwise null</returns>
    Aircraft? GetAircraft(string icao);

    /// <summary>
    /// Gets the count of currently tracked aircraft (excludes expired).
    /// Note: This is a computed property that filters expired aircraft on each access.
    /// </summary>
    int Count { get; }

    // === Event Notifications ===

    /// <summary>
    /// Fired when an aircraft's state is updated with new data.
    /// Provides both previous and updated state for comparison.
    /// Fired on EVERY frame update - subscribers should compare Previous vs Updated to filter changes.
    /// </summary>
    event EventHandler<AircraftUpdateEventArgs>? OnAircraftUpdated;

    /// <summary>
    /// Fired when a new aircraft is first detected and added to tracking.
    /// Occurs on first frame received for a previously unseen ICAO address.
    /// </summary>
    event EventHandler<AircraftEventArgs>? OnAircraftAdded;

    /// <summary>
    /// Fired when an aircraft is removed due to timeout expiration.
    /// Occurs during background cleanup when aircraft not seen within AircraftTimeout period.
    /// </summary>
    event EventHandler<AircraftEventArgs>? OnAircraftExpired;

    // === Configuration ===

    /// <summary>
    /// Gets or sets the aircraft timeout duration.
    /// Aircraft not seen within this time are considered expired and removed during cleanup.
    /// Typical values: 60s (default), 300s (5min for slow update rates), 30s (fast turnover).
    /// Default: 60 seconds
    /// </summary>
    TimeSpan AircraftTimeout { get; set; }

    /// <summary>
    /// Gets or sets the cleanup interval for expired aircraft removal.
    /// Background timer runs at this frequency to scan and remove stale entries.
    /// Should be significantly shorter than AircraftTimeout for responsive cleanup.
    /// Default: 10 seconds
    /// </summary>
    TimeSpan CleanupInterval { get; set; }
}
