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
/// CPR (Compact Position Reporting) format field.
/// Indicates whether the CPR-encoded position is an even or odd frame.
/// </summary>
/// <remarks>
/// CPR encoding requires to be paired even and odd frames for global position decoding.
/// The format bit (F) determines which frame type:
/// - Even frames (F=0): Use NZ = 60 for latitude zones
/// - Odd frames (F=1): Use NZ = 59 for latitude zones
///
/// Both frame types are needed for unambiguous position calculation.
/// The most recent frame determines which latitude/longitude values to use.
///
/// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.8.
/// </remarks>
public enum CprFormat
{
    /// <summary>
    /// Even frame (F=0).
    /// Uses 60 latitude zones for CPR decoding.
    /// </summary>
    Even = 0,

    /// <summary>
    /// Odd frame (F=1).
    /// Uses 59 latitude zones for CPR decoding.
    /// </summary>
    Odd = 1
}
