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
/// Specifies the type of target heading information in ADS-B Target State and Status messages (TC=29, Version 1).
/// Indicates whether the reported angle represents the aircraft's ground track or true heading.
/// </summary>
public enum TargetHeadingType
{
    /// <summary>
    /// Target angle represents the aircraft's track over the ground.
    /// Track is the actual path of the aircraft relative to the ground, affected by wind.
    /// </summary>
    Track = 0,

    /// <summary>
    /// Target angle represents the aircraft's true heading.
    /// Heading is the direction the aircraft's nose is pointing, regardless of ground track.
    /// </summary>
    Heading = 1
}
