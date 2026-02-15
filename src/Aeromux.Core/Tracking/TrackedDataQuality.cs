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

namespace Aeromux.Core.Tracking;

/// <summary>
/// Data quality, accuracy, and integrity indicators for aircraft tracking.
/// Contains navigation accuracy categories, integrity levels, and reference system indicators.
/// Sources: TC 31 (Operational Status), TC 29 V2 (Target State and Status Version 2).
/// </summary>
public sealed record TrackedDataQuality
{
    /// <summary>
    /// Geometric (GNSS) vertical accuracy (GVA) from TC 31 Operational Status.
    /// Indicates the accuracy of geometric altitude measurements from GNSS/GPS.
    /// Range: Unknown (0) to LessThan45Meters (4).
    /// - Level 0: Unknown or greater than 150m.
    /// - Level 1: Less than 150 meters.
    /// - Level 2: Less than 45 meters.
    /// - Level 3-4: Reserved for future use.
    /// Null if TC 31 not yet received, or if aircraft is on surface (GVA only provided for airborne).
    /// </summary>
    public GeometricVerticalAccuracy? GeometricVerticalAccuracy { get; init; }

    /// <summary>
    /// Barometric Altitude Integrity Code from TC 29 Version 2 (NICbaro).
    /// Indicates whether barometric altitude has been cross-checked against another source (e.g., GNSS).
    /// - CrossChecked: Barometric altitude has been cross-checked and is consistent.
    /// - NotCrossChecked: Barometric altitude has not been cross-checked or is inconsistent.
    /// Provides integrity information for barometric altitude data.
    /// Null if TC 29 Version 2 not yet received.
    /// </summary>
    public BarometricAltitudeIntegrityCode? NICbaro_TC29 { get; init; }

    /// <summary>
    /// NIC Supplement-A bit from TC 31 Operational Status.
    /// Used in combination with NIC (Navigation Integrity Category) from position messages (TC 9-18)
    /// to determine the horizontal containment radius for position accuracy.
    /// Supplements NIC to provide more granular position integrity information.
    /// Null if TC 31 not yet received.
    /// </summary>
    public bool? NICSupplementA { get; init; }

    /// <summary>
    /// SIL Supplement from TC 31 Operational Status.
    /// Indicates the time basis for the Source Integrity Level (SIL):
    /// - PerHour: SIL probability applies per flight hour.
    /// - PerSample: SIL probability applies per sample (message).
    /// Affects interpretation of SIL probability values.
    /// Null if TC 31 not yet received.
    /// </summary>
    public SilSupplement? SILSupplement { get; init; }

    /// <summary>
    /// Source Integrity Level (SIL) from TC 29 Version 2.
    /// Indicates the probability of position information exceeding the containment radius (Rc):
    /// - Level 0: Unknown or probability greater than 1×10⁻³ per hour/sample.
    /// - Level 1: Probability less than 1×10⁻³ per hour/sample.
    /// - Level 2: Probability less than 1×10⁻⁵ per hour/sample.
    /// - Level 3: Probability less than 1×10⁻⁷ per hour/sample.
    /// Higher SIL levels indicate higher position integrity.
    /// Null if TC 29 Version 2 not yet received.
    /// Note: TC 31 also provides SIL, but this field specifically tracks TC 29 V2 SIL for redundancy/comparison.
    /// </summary>
    public SourceIntegrityLevel? SIL_TC29 { get; init; }

    /// <summary>
    /// Navigation Accuracy Category for Position (NACp) from TC 29 Version 2.
    /// Indicates the horizontal accuracy of position information (95% containment radius):
    /// - Level 0: Unknown or EPU ≥ 18.52 km.
    /// - Level 1-11: Progressively better accuracy (18.52 km down to &lt;3 m).
    /// Higher NACp values indicate better position accuracy.
    /// Null if TC 29 Version 2 not yet received.
    /// Note: TC 31 also provides NACp, but this field specifically tracks TC 29 V2 NACp for redundancy/comparison.
    /// </summary>
    public NavigationAccuracyCategoryPosition? NACp_TC29 { get; init; }

    /// <summary>
    /// Horizontal Reference Direction from TC 31 Operational Status (HRD).
    /// Indicates the reference system for horizontal direction/heading:
    /// - TrueNorth: Headings are referenced to True North (geographic north).
    /// - MagneticNorth: Headings are referenced to Magnetic North.
    /// Critical for correctly interpreting heading and track data.
    /// Null if TC 31 not yet received.
    /// </summary>
    public HorizontalReferenceDirection? HorizontalReference { get; init; }

    /// <summary>
    /// Track/Heading indicator from TC 31 Operational Status (surface operations only).
    /// Indicates whether surface movement direction represents:
    /// - Track: Direction of movement over ground (actual path).
    /// - Heading: Direction the aircraft is pointing (nose direction).
    /// On surface, heading and track can differ during taxiing (e.g., during turns).
    /// Null if TC 31 surface operational status not yet received, or if aircraft is airborne.
    /// Note: Also available in TC 29 Version 2 (TargetHeadingType for autopilot intent).
    /// </summary>
    public TargetHeadingType? HeadingType { get; init; }

    /// <summary>
    /// Timestamp of the most recent data quality update.
    /// Updated whenever any quality indicator field changes.
    /// Null if no data quality information has been received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
