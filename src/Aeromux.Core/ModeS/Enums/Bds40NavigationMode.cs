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
/// Navigation modes from BDS 4,0 (Selected Vertical Intention).
/// Indicates which autopilot vertical navigation modes are active.
/// Multiple modes can be active simultaneously.
/// </summary>
/// <remarks>
/// Transmitted as a 3-bit field (MB bits 49-51) in BDS 4,0 messages, valid only when the
/// mode status bit (MB bit 48) is set. Each bit is an independent flag.
/// Values follow the wire layout: MB bit 49 is the most significant bit of the field, so
/// the value read with ExtractBits(mb, 49, 3) casts directly to this enum.
/// </remarks>
[Flags]
public enum Bds40NavigationMode
{
    /// <summary>
    /// No navigation modes active (value 0).
    /// </summary>
    None = 0,

    /// <summary>
    /// VNAV (Vertical Navigation) mode active (MB bit 49, value 4).
    /// Aircraft is following FMS vertical profile.
    /// </summary>
    Vnav = 4,

    /// <summary>
    /// Altitude Hold mode active (MB bit 50, value 2).
    /// Aircraft is maintaining selected altitude.
    /// </summary>
    AltitudeHold = 2,

    /// <summary>
    /// Approach mode active (MB bit 51, value 1).
    /// Aircraft is in approach mode for landing.
    /// </summary>
    Approach = 1
}
