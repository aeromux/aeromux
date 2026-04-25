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
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft velocity information group.
/// Contains speed, heading/track, vertical rate, and velocity type.
/// Sources: TC 19 (ADS-B Airborne Velocity), TC 5-8 (Surface Position with ground movement).
/// </summary>
public sealed record TrackedVelocity
{
    /// <summary>
    /// Airborne velocity from TC 19 (ADS-B Airborne Velocity) messages.
    /// Type depends on VelocitySubtype: ground speed (subtype 1-2), true airspeed (subtype 3), or indicated airspeed (subtype 4).
    /// Null if no TC 19 velocity message received yet.
    /// For surface movement speed, see GroundSpeed property.
    /// </summary>
    public Velocity? Speed { get; init; }

    /// <summary>
    /// True heading - direction aircraft nose is pointing (TC 19, subtype 3-4 airspeed messages).
    /// Range: 0-359.9 degrees (0 = North, 90 = East, 180 = South, 270 = West).
    /// Null if unavailable or using ground speed message (subtype 1-2).
    /// Different from Track which is actual direction of movement over ground (Track accounts for wind).
    /// </summary>
    public double? Heading { get; init; }

    /// <summary>
    /// Ground track angle - actual direction of movement over ground from TC 19 airborne velocity (subtype 1-2).
    /// Range: 0-359.9 degrees (0 = North, 90 = East, 180 = South, 270 = West).
    /// Null if unavailable or using airspeed message (subtype 3-4).
    /// Different from Heading due to wind effect (Track = Heading + wind correction).
    /// For surface ground track, see GroundTrack property.
    /// </summary>
    public double? Track { get; init; }

    /// <summary>
    /// Ground speed from TC 5-8 (Surface Position) messages for aircraft on the ground.
    /// Range: 0-199 knots with non-linear quantization (higher precision at lower speeds).
    /// Separate from Speed field which comes from TC 19 airborne velocity messages.
    /// Used for airport surface movement tracking, taxiing, and ground operations.
    /// Null if no surface position message with ground speed received.
    /// </summary>
    public Velocity? GroundSpeed { get; init; }

    /// <summary>
    /// Ground track angle from TC 5-8 (Surface Position) messages for aircraft on the ground.
    /// Range: 0-360 degrees with 2.8125° resolution (360/128 quantization steps).
    /// Direction of movement on the airport surface during taxi operations.
    /// Separate from Track field which comes from TC 19 airborne velocity messages.
    /// Null if no surface position message with ground track received.
    /// </summary>
    public double? GroundTrack { get; init; }

    /// <summary>
    /// Indicated Airspeed (IAS) from Comm-B BDS registers (BDS 5,3 or BDS 6,0).
    /// Airspeed as measured by the aircraft's pitot-static system (uncorrected for altitude/temperature).
    /// Range: 0-500 knots.
    /// IAS is the speed displayed to pilots and used for aircraft performance (stall speed, V-speeds).
    /// Null if no BDS 5,3 or BDS 6,0 message received (requires ground interrogation for Comm-B).
    /// </summary>
    public Velocity? CommBIndicatedAirspeed { get; init; }

    /// <summary>
    /// True Airspeed (TAS) from Comm-B BDS registers (BDS 5,0 or BDS 5,3).
    /// Indicated airspeed corrected for altitude and temperature (actual speed through air mass).
    /// Range: 0-2046 knots.
    /// TAS is used for navigation calculations and differs from ground speed due to wind.
    /// Null if no BDS 5,0 or BDS 5,3 message received (requires ground interrogation for Comm-B).
    /// </summary>
    public Velocity? CommBTrueAirspeed { get; init; }

    /// <summary>
    /// Ground Speed from Comm-B BDS 5,0 (Track and Turn) register.
    /// Aircraft's speed over the ground while airborne (from ground interrogation).
    /// Range: 0-2046 knots.
    /// Provides redundancy to TC 19 ground speed and allows cross-validation.
    /// Different from GroundSpeed field which comes from TC 5-8 surface movement messages.
    /// This field is for airborne ground speed from Comm-B interrogations.
    /// Null if no BDS 5,0 message received (requires ground interrogation for Comm-B).
    /// </summary>
    public Velocity? CommBGroundSpeed { get; init; }

    /// <summary>
    /// True track angle from Comm-B BDS 5,0 (Track and Turn) register.
    /// Direction of movement over ground in degrees (0-360, 0 = North).
    /// Provides redundancy to TC 19 Track field and allows cross-validation.
    /// Range: 0-359.9 degrees.
    /// Null if no BDS 5,0 message received (requires ground interrogation for Comm-B).
    /// </summary>
    public double? TrackAngle { get; init; }

    /// <summary>
    /// Climb/descent rate in feet per minute (TC 19, all subtypes).
    /// Positive: climbing, Negative: descending, Zero/null: level flight or unavailable.
    /// Range: typically -6000 to +6000 fpm for normal flight.
    /// Null if not provided in message.
    /// </summary>
    public int? VerticalRate { get; init; }

    /// <summary>
    /// Velocity subtype from TC 19 (Airborne Velocity) message.
    /// Indicates velocity source and speed range (subsonic vs supersonic).
    /// Values: GroundSpeedSubsonic, GroundSpeedSupersonic, AirspeedSubsonic, AirspeedSupersonic.
    /// This preserves all information from the Mode S message without lossy mapping.
    /// Null if no velocity message received yet.
    /// </summary>
    public VelocitySubtype? VelocitySubtype { get; init; }

    /// <summary>
    /// NACv (Navigation Accuracy Category for Velocity) from TC 19 Airborne Velocity.
    /// Indicates the 95% accuracy of horizontal and vertical velocity measurements.
    /// Scale: 0-4 (0 = unknown, 1 = &lt;10 m/s, 2 = &lt;3 m/s, 3 = &lt;1 m/s, 4 = &lt;0.3 m/s).
    /// Extracted from TC 19 (Airborne Velocity) messages and preserved across velocity updates.
    /// Higher values indicate better velocity measurement accuracy.
    /// Used by ATC for determining separation minima and velocity-based conflict detection.
    /// Null if no TC 19 message received yet.
    /// </summary>
    public NavigationAccuracyCategoryVelocity? NACv { get; init; }

    /// <summary>
    /// Timestamp of last velocity update (when Speed, GroundSpeed, or related fields were last set).
    /// Updated by both TC 19 (airborne velocity) and TC 5-8 (surface position) messages.
    /// Null if no velocity data received yet.
    /// Used for calculating staleness of velocity information.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
