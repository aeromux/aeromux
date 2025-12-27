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
/// Represents the Horizontal Reference Direction (HRD) from Aircraft Operational Status Messages.
/// Indicates the reference direction (true north or magnetic north) for horizontal directions
/// such as heading and track angle.
/// This is a 1-bit subfield (bit 54, message bit 86).
/// Reference: DO-282B §2.2.3.2.7.2.13, Table 2-76
/// </summary>
public enum HorizontalReferenceDirection
{
    /// <summary>
    /// Horizontal directions (heading, track angle) are referenced to True North.
    /// </summary>
    TrueNorth = 0,

    /// <summary>
    /// Horizontal directions (heading, track angle) are referenced to Magnetic North.
    /// </summary>
    MagneticNorth = 1
}
