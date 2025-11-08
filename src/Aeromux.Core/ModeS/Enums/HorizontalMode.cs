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
/// Horizontal mode from Target State and Status message (TC 29, Version 1).
/// Indicates the autopilot's lateral navigation mode.
/// </summary>
/// <remarks>
/// Reference: ICAO Annex 10, Volume IV, Table 2-86 (Horizontal Mode).
/// Field is 2 bits (values 0-3), but only 0-2 are currently defined.
/// Value 3 is reserved for future use.
/// </remarks>
public enum HorizontalMode
{
    /// <summary>
    /// No horizontal mode active or unknown (value 0).
    /// Autopilot may be off or lateral mode not engaged.
    /// </summary>
    None = 0,

    /// <summary>
    /// Acquiring horizontal target (value 1).
    /// Aircraft is turning toward the target heading or track.
    /// Autopilot is actively changing heading to reach the target.
    /// </summary>
    Acquiring = 1,

    /// <summary>
    /// Capturing or maintaining horizontal target (value 2).
    /// Aircraft has reached target heading/track and is holding it,
    /// or is in the process of stabilizing on the target.
    /// </summary>
    CapturingOrMaintaining = 2

    // Value 3: Reserved for future use (not yet defined by ICAO)
}
