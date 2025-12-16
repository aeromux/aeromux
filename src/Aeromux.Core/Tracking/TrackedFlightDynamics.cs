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

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft flight dynamics information group.
/// Contains roll angle, magnetic heading, and enhanced vertical rate data.
/// Sources: BDS 5,0 (Track and Turn), BDS 5,3/6,0 (Air-Referenced State), BDS 6,0 (Heading and Speed).
/// </summary>
public sealed record TrackedFlightDynamics
{
    /// <summary>
    /// Roll angle in degrees (BDS 5,0).
    /// Positive: right bank, Negative: left bank.
    /// Range: typically -50 to +50 degrees.
    /// Resolution: 45/256 degrees (~0.176°).
    /// Null if BDS 5,0 not received.
    /// </summary>
    public double? RollAngle { get; init; }

    /// <summary>
    /// Magnetic heading in degrees (BDS 5,3, BDS 6,0).
    /// Direction aircraft nose points relative to magnetic north.
    /// Range: 0-360 degrees.
    /// Resolution: 90/512 degrees (~0.176°).
    /// Different from true heading due to magnetic declination.
    /// Null if BDS 5,3 or BDS 6,0 not received.
    /// </summary>
    public double? MagneticHeading { get; init; }

    /// <summary>
    /// Barometric vertical rate in feet per minute (BDS 6,0).
    /// Climb/descent rate derived from barometric altitude changes.
    /// Positive: climbing, Negative: descending.
    /// Range: typically -6000 to +6000 fpm.
    /// Resolution: 32 ft/min.
    /// Null if BDS 6,0 not received or not available.
    /// </summary>
    public int? BarometricVerticalRate { get; init; }

    /// <summary>
    /// Inertial vertical rate in feet per minute (BDS 6,0).
    /// Climb/descent rate from inertial reference system.
    /// More responsive than barometric vertical rate.
    /// Positive: climbing, Negative: descending.
    /// Range: typically -6000 to +6000 fpm.
    /// Resolution: 32 ft/min.
    /// Null if BDS 6,0 not received or not available.
    /// </summary>
    public int? InertialVerticalRate { get; init; }

    /// <summary>
    /// Mach number (BDS 5,3, BDS 6,0).
    /// Aircraft speed as fraction of speed of sound.
    /// Range: 0.0-1.0 (typically 0.3-0.9 for commercial aircraft).
    /// Resolution: 0.008 (1/125).
    /// Null if BDS 5,3 or BDS 6,0 not received.
    /// </summary>
    public double? MachNumber { get; init; }

    /// <summary>
    /// Track angle rate (rate of turn) in degrees per second (BDS 5,0 bits 35-45).
    /// Rate of change of ground track angle.
    /// Positive: turning right, Negative: turning left.
    /// Range: typically -10 to +10 degrees/second for normal flight.
    /// Resolution: 1/4 degree/second (0.25°/s).
    /// Null if BDS 5,0 not received or track rate not available.
    /// </summary>
    public double? TrackRate { get; init; }

    /// <summary>
    /// Timestamp of last flight dynamics update.
    /// Updated when any flight dynamics field changes.
    /// Null if no flight dynamics data received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
