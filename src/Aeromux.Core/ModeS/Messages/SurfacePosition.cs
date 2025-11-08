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

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Surface position message (TC 5-8) with movement and ground track.
/// Reports position on the ground using CPR encoding, along with ground speed and track angle.
/// Requires receiver location for CPR decoding.
/// </summary>
/// <remarks>
/// TC 5-8 differences from TC 9-18:
/// - Movement field instead of altitude (non-linear quantization, 0-199 kt)
/// - Ground track instead of surveillance status (0-360°, 360/128 = 2.8125° resolution)
/// - Modified CPR encoding (NL functions differ from airborne CPR)
/// </remarks>
public sealed record SurfacePosition(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    GeographicCoordinate? Position,
    int? GroundSpeed,
    double? GroundTrack,
    int CprLatitude,
    int CprLongitude,
    CprFormat CprFormat,
    SurfaceMovement Movement) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
