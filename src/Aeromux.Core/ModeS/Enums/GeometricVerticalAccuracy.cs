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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Represents the Geometric Vertical Accuracy (GVA) from Aircraft Operational Status Messages.
/// Indicates the vertical position accuracy of the aircraft.
/// Reference: DO-282B §2.2.3.2.7.2.8, Table 2.2.3.2.7.2.8
/// </summary>
public enum GeometricVerticalAccuracy
{
    /// <summary>
    /// Unknown vertical accuracy or greater than 45 meters.
    /// </summary>
    UnknownOrGreaterThan45Meters = 0,

    /// <summary>
    /// Vertical accuracy is less than or equal to 45 meters.
    /// </summary>
    LessThanOrEqualTo45Meters = 1,

    /// <summary>
    /// Reserved value. Per DO-260B, should be treated as less than 45 meters
    /// until future versions of MOPS redefine this value.
    /// </summary>
    Reserved2 = 2,

    /// <summary>
    /// Reserved value. Per DO-260B, should be treated as less than 45 meters
    /// until future versions of MOPS redefine this value.
    /// </summary>
    Reserved3 = 3
}
