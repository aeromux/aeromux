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

namespace Aeromux.Core.Tracking;

/// <summary>
/// Cached magnetic declination value with calculation timestamp.
/// Immutable value object for WMM-2025 declination caching.
/// </summary>
/// <param name="Declination">Magnetic declination in degrees (positive East, negative West).</param>
/// <param name="CalculatedAt">Timestamp when this declination was calculated.</param>
public sealed record MagneticDeclination(double Declination, DateTime CalculatedAt);
