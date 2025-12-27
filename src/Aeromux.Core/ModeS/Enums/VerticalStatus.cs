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
/// Vertical status from ACAS messages (DF 0, DF 16).
/// Indicates whether the aircraft is airborne or on the ground.
/// </summary>
public enum VerticalStatus
{
    /// <summary>
    /// Aircraft is airborne (value 0).
    /// </summary>
    Airborne = 0,

    /// <summary>
    /// Aircraft is on the ground (value 1).
    /// </summary>
    Ground = 1
}
