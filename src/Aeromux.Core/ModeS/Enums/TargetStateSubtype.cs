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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Target State and Status message subtype (TC 29).
/// Indicates the version/format of the target state information.
/// </summary>
/// <remarks>
/// Reference: ICAO Annex 10, Volume IV, 3.1.2.8.10 (Target State and Status).
/// Subtype field is 2 bits (values 0-3), but only 0 and 1 are currently defined.
/// Values 2-3 are reserved for future use.
/// Version 1 and Version 2 have different field layouts and capabilities.
/// </remarks>
public enum TargetStateSubtype
{
    /// <summary>
    /// Version 1 (subtype 0).
    /// Contains: target altitude, target heading/track, vertical/horizontal modes,
    /// TCAS status, and emergency state.
    /// Used by earlier DO-260A/B implementations.
    /// </summary>
    Version1 = 0,

    /// <summary>
    /// Version 2 (subtype 1).
    /// Contains: selected altitude, selected heading, barometric pressure,
    /// autopilot modes (A/P, VNAV, ALT HOLD, LNAV, APPROACH),
    /// accuracy parameters (SIL, NACp, NICbaro), and TCAS status.
    /// Used by newer DO-260B implementations (2020+ mandate).
    /// </summary>
    Version2 = 1

    // Values 2-3: Reserved for future use (not yet defined by ICAO)
}
