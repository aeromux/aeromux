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
/// Immutable aircraft state record with grouped properties.
/// Represents complete state for one tracked aircraft identified by ICAO address.
/// Thread-safe: All properties are read-only, updates create new instances.
/// Properties are organized into semantic groups (Identification, Position, Velocity, Status, History).
/// </summary>
/// <remarks>
/// <para>Design Notes:</para>
/// <list type="bullet">
/// <item>CPR decoding state (even/odd frame pairs) maintained separately in CprDecoder</item>
/// <item>This record stores only decoded results, not intermediate decoding state</item>
/// <item>Immutable pattern: Use 'with' expression to create updated copies</item>
/// <item>All updates performed by AircraftStateTracker, not by this class</item>
/// </list>
/// </remarks>
public sealed record Aircraft
{
    /// <summary>
    /// Aircraft identification information (ICAO, callsign, squawk, category, emergency).
    /// Required field - every aircraft must have at minimum an ICAO address.
    /// Sources: TC 1-4 (identification), TC 28 (status), DF 5 (surveillance identity).
    /// </summary>
    public required TrackedIdentification Identification { get; init; }

    /// <summary>
    /// Aircraft position information (coordinates, altitude, accuracy).
    /// Initially empty until first position message received.
    /// Sources: TC 9-18 (airborne position), TC 20-22 (surface position), DF 4 (altitude reply).
    /// </summary>
    public TrackedPosition Position { get; init; } = new();

    /// <summary>
    /// Aircraft velocity information (speed, heading, track, vertical rate).
    /// Initially empty until first velocity message received.
    /// Source: TC 19 (airborne velocity).
    /// </summary>
    public TrackedVelocity Velocity { get; init; } = new();

    /// <summary>
    /// Aircraft tracking status and metadata (signal strength, counters, timestamps).
    /// Required field - tracking state always present.
    /// Includes message counts, first/last seen times, and signal quality metrics.
    /// </summary>
    public required TrackedStatus Status { get; init; }

    /// <summary>
    /// Aircraft historical data (position, altitude, velocity over time).
    /// Circular buffers for time-series data, used for trail visualization and graphs.
    /// Individual buffers within History may be null if history tracking disabled in configuration.
    /// </summary>
    public TrackedHistory History { get; init; } = new();

    /// <summary>
    /// Aircraft autopilot/FMS intent information (selected altitude, heading, modes).
    /// Optional - may be null until first TC 29 or BDS 4,0 message received.
    /// Sources: TC 29 (Target State and Status), BDS 4,0 (Selected Vertical Intention).
    /// </summary>
    public TrackedAutopilot? Autopilot { get; init; }

    /// <summary>
    /// Aircraft ACAS/TCAS collision avoidance information (operational status, RA state, threat coordination).
    /// Optional - may be null until first DF 0, DF 16, or TC 29 ACAS message received.
    /// Sources: DF 0 (Short Air-Air), DF 16 (Long Air-Air), TC 29 (Target State).
    /// </summary>
    public TrackedAcas? Acas { get; init; }

    /// <summary>
    /// Aircraft flight dynamics (roll, magnetic heading, vertical rates, mach, track rate).
    /// Optional - may be null until first BDS 5,0/5,3/6,0 message received.
    /// Sources: BDS 5,0 (Track and Turn), BDS 5,3/6,0 (Air-Referenced State), BDS 6,0 (Heading and Speed).
    /// </summary>
    public TrackedFlightDynamics? FlightDynamics { get; init; }

    /// <summary>
    /// Aircraft meteorological information (wind, temperature, pressure, hazards).
    /// Optional - may be null until first BDS 4,4 or BDS 4,5 message received.
    /// Sources: BDS 4,4 (Meteorological Routine), BDS 4,5 (Meteorological Hazard).
    /// </summary>
    public TrackedMeteo? Meteo { get; init; }

    /// <summary>
    /// Aircraft system capabilities and configuration metadata.
    /// Contains transponder capabilities, ADS-B capabilities, Comm-B capabilities, and physical configuration.
    /// Optional - may be null until first DF 11, TC 31, or BDS 1,0/1,7 message received.
    /// Sources: DF 11 (All-Call Reply), TC 31 (Operational Status), BDS 1,0 (Data Link Capability), BDS 1,7 (GICB Capability).
    /// </summary>
    public TrackedCapabilities? Capabilities { get; init; }

    /// <summary>
    /// Data quality, accuracy, and integrity indicators.
    /// Contains navigation accuracy categories, integrity levels, and reference system indicators.
    /// Optional - may be null until first TC 31 or TC 29 V2 message received.
    /// Sources: TC 31 (Operational Status), TC 29 V2 (Target State and Status Version 2).
    /// </summary>
    public TrackedDataQuality? DataQuality { get; init; }

    /// <summary>
    /// Real-time operational status and ATC coordination information.
    /// Contains cockpit mode indicators, system status, GPS antenna configuration, and ATC coordination metadata.
    /// Optional - may be null until first TC 31 or DF 20/21 message received.
    /// Sources: TC 31 (Operational Status), DF 20/21 (Comm-B Altitude/Identity Reply).
    /// </summary>
    public TrackedOperationalMode? OperationalMode { get; init; }

    /// <summary>
    /// Applies an update from a processed frame to create a new aircraft state.
    /// NOTE: This method is a placeholder and currently not used.
    /// Actual updates are performed by AircraftStateTracker.ApplyUpdate() instead.
    /// </summary>
    /// <param name="frame">Processed frame with parsed message data</param>
    /// <returns>Current aircraft state (unchanged - this is a placeholder)</returns>
    /// <remarks>
    /// This method was originally intended for self-updating aircraft records,
    /// but the design shifted to centralized updates in AircraftStateTracker
    /// for better separation of concerns and testability.
    /// Consider removing this method in future refactoring.
    /// </remarks>
    public Aircraft ApplyUpdate(ProcessedFrame frame)
    {
        // Placeholder implementation - actual updates handled by AircraftStateTracker
        return this;
    }

    /// <summary>
    /// Checks if this aircraft has expired (not seen within timeout period).
    /// Used by tracker to determine when to remove stale aircraft entries.
    /// </summary>
    /// <param name="timeout">Expiration timeout duration (default: 60 seconds)</param>
    /// <returns>True if time since LastSeen exceeds the timeout period</returns>
    /// <example>
    /// <code>
    /// if (aircraft.IsExpired(TimeSpan.FromSeconds(60)))
    /// {
    ///     // Remove from tracking
    /// }
    /// </code>
    /// </example>
    public bool IsExpired(TimeSpan timeout)
    {
        TimeSpan age = DateTime.UtcNow - Status.LastSeen;
        return age > timeout;
    }
}
