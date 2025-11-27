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
/// Altitude snapshot record for historical tracking.
/// Immutable time-stamped altitude measurement with type indicator.
/// Stored in CircularBuffer for altitude graphs and climb/descent rate analysis.
/// </summary>
/// <param name="Timestamp">UTC timestamp when this altitude was observed</param>
/// <param name="Altitude">Altitude value (feet, barometric or geometric)</param>
/// <param name="AltitudeType">Altitude type: Barometric (pressure), Geometric (GNSS/GPS), or Ground</param>
public sealed record AltitudeSnapshot(
    DateTime Timestamp,
    Altitude Altitude,
    AltitudeType AltitudeType
);
