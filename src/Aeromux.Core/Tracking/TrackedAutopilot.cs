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
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft autopilot/FMS intent information group.
/// Contains target altitude, heading, and barometric settings from autopilot/FMS.
/// Sources: TC 29 (Target State and Status), BDS 4,0 (Comm-B Selected Vertical Intention).
/// </summary>
/// <remarks>
/// TCAS fields (TcasOperational, TcasRaActive) have been moved to TrackedAcas.
/// This record now focuses exclusively on autopilot/FMS intent and configuration.
/// </remarks>
public sealed record TrackedAutopilot
{
    /// <summary>
    /// Selected altitude from MCP/FCU or FMS (TC 29, BDS 4,0).
    /// Target altitude set by pilot in autopilot or FMS.
    /// Range: -1000 to +100,000 feet.
    /// Null if not available.
    /// Source: TC 29 (both versions) or BDS 4,0 (MCP/FMS altitude).
    /// </summary>
    public Altitude? SelectedAltitude { get; init; }

    /// <summary>
    /// Altitude source indicator (TC 29, BDS 4,0).
    /// Values: "MCP/FCU", "FMS/RNAV", "Holding mode", "FMS/RNAV (FL)", "MCP/FCU (MSL)".
    /// Indicates whether altitude came from pilot-set MCP or FMS flight plan.
    /// Null if SelectedAltitude is null.
    /// </summary>
    public AltitudeSource? AltitudeSource { get; init; }

    /// <summary>
    /// Selected heading from autopilot (TC 29 V1/V2).
    /// Target heading set by pilot in autopilot.
    /// Range: 0-360 degrees (true north reference).
    /// Null if not available or using track mode.
    /// Source: TC 29 only (both Version 1 and Version 2).
    /// </summary>
    public double? SelectedHeading { get; init; }

    /// <summary>
    /// Barometric pressure setting from autopilot (TC 29 V2, BDS 4,0).
    /// QNH setting in millibars (hPa).
    /// Range: 800-1200 mbar.
    /// Null if not available.
    /// Source: TC 29 Version 2 or BDS 4,0.
    /// </summary>
    public double? BarometricPressureSetting { get; init; }

    /// <summary>
    /// Vertical mode from TC 29 Version 1.
    /// Values: Acquiring (1), Capturing/Maintaining (2), Reserved (3).
    /// Indicates autopilot vertical mode (climbing to altitude vs maintaining).
    /// Null if TC 29 V1 not received or mode not available.
    /// </summary>
    public VerticalMode? VerticalMode { get; init; }

    /// <summary>
    /// Horizontal mode from TC 29 Version 1.
    /// Values: Acquiring (1), Capturing/Maintaining (2), Reserved (3).
    /// Indicates autopilot horizontal mode (turning to heading vs maintaining).
    /// Null if TC 29 V1 not received or mode not available.
    /// </summary>
    public HorizontalMode? HorizontalMode { get; init; }

    /// <summary>
    /// Autopilot engaged flag (TC 29 V2).
    /// True if autopilot is engaged, False if disengaged/manual flight.
    /// Null if TC 29 V2 not received.
    /// </summary>
    public bool? AutopilotEngaged { get; init; }

    /// <summary>
    /// VNAV (Vertical Navigation) mode engaged (TC 29 V2).
    /// True if VNAV active (FMS vertical guidance).
    /// Null if TC 29 V2 not received.
    /// </summary>
    public bool? VnavMode { get; init; }

    /// <summary>
    /// LNAV (Lateral Navigation) mode engaged (TC 29 V2).
    /// True if LNAV active (FMS lateral guidance).
    /// Null if TC 29 V2 not received.
    /// </summary>
    public bool? LnavMode { get; init; }

    /// <summary>
    /// Altitude hold mode engaged (TC 29 V2).
    /// True if altitude hold active.
    /// Null if TC 29 V2 not received.
    /// </summary>
    public bool? AltitudeHoldMode { get; init; }

    /// <summary>
    /// Approach mode engaged (TC 29 V2).
    /// True if approach mode active (ILS/RNAV approach).
    /// Null if TC 29 V2 not received.
    /// </summary>
    public bool? ApproachMode { get; init; }

    /// <summary>
    /// Timestamp of last autopilot data update.
    /// Updated when any autopilot field changes.
    /// Null if no autopilot data received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
