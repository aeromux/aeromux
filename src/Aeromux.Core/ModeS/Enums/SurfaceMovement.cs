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
/// Surface movement categories (TC 5-8).
/// 7-bit field with non-linear quantization (0-199 knots).
/// Reference: ICAO Annex 10, Volume IV, 3.1.2.8.2.1, Table 2-14.
/// </summary>
public enum SurfaceMovement
{
    /// <summary>No information available or aircraft stopped.</summary>
    NoInformation = 0,

    /// <summary>Aircraft stopped (ground speed = 0 kt).</summary>
    Stopped = 1,

    /// <summary>Ground speed: 0-0.125 kt (0.0-0.2 km/h).</summary>
    LessThan0_125Kt = 2,

    /// <summary>Ground speed: 0.125-1 kt (0.2-1.9 km/h).</summary>
    From0_125To1Kt = 3,

    /// <summary>Ground speed: 1-2 kt (1.9-3.7 km/h).</summary>
    From1To2Kt = 4,

    /// <summary>Ground speed: 2-5 kt (3.7-9.3 km/h).</summary>
    From2To5Kt = 5,

    /// <summary>Ground speed: 5-10 kt (9.3-18.5 km/h).</summary>
    From5To10Kt = 6,

    /// <summary>Ground speed: 10-15 kt (18.5-27.8 km/h).</summary>
    From10To15Kt = 7,

    /// <summary>Ground speed: 15-20 kt (27.8-37.0 km/h).</summary>
    From15To20Kt = 8,

    /// <summary>Ground speed: 20-30 kt (37.0-55.6 km/h).</summary>
    From20To30Kt = 9,

    /// <summary>Ground speed: 30-40 kt (55.6-74.1 km/h).</summary>
    From30To40Kt = 10,

    /// <summary>Ground speed: 40-50 kt (74.1-92.6 km/h).</summary>
    From40To50Kt = 11,

    /// <summary>Ground speed: 50-60 kt (92.6-111.1 km/h).</summary>
    From50To60Kt = 12,

    /// <summary>Ground speed: 60-70 kt (111.1-129.6 km/h).</summary>
    From60To70Kt = 13,

    /// <summary>Ground speed: 70-80 kt (129.6-148.2 km/h).</summary>
    From70To80Kt = 14,

    /// <summary>Ground speed: 80-90 kt (148.2-166.7 km/h).</summary>
    From80To90Kt = 15,

    /// <summary>Ground speed: 90-100 kt (166.7-185.2 km/h).</summary>
    From90To100Kt = 16,

    /// <summary>Ground speed: 100-110 kt (185.2-203.7 km/h).</summary>
    From100To110Kt = 17,

    /// <summary>Ground speed: 110-120 kt (203.7-222.2 km/h).</summary>
    From110To120Kt = 18,

    /// <summary>Ground speed: 120-130 kt (222.2-240.7 km/h).</summary>
    From120To130Kt = 19,

    /// <summary>Ground speed: 130-140 kt (240.7-259.3 km/h).</summary>
    From130To140Kt = 20,

    /// <summary>Ground speed: 140-150 kt (259.3-277.8 km/h).</summary>
    From140To150Kt = 21,

    /// <summary>Ground speed: 150-160 kt (277.8-296.3 km/h).</summary>
    From150To160Kt = 22,

    /// <summary>Ground speed: 160-175 kt (296.3-324.1 km/h).</summary>
    From160To175Kt = 23,

    /// <summary>Ground speed: 175-199 kt (324.1-368.5 km/h) - maximum for surface movement.</summary>
    From175To199Kt = 24,

    /// <summary>Ground speed ≥ 199 kt (≥ 368.5 km/h) - rare for taxiing aircraft.</summary>
    GreaterOrEqual199Kt = 124,

    /// <summary>Reserved for future use (values 25-123, 125-127).</summary>
    Reserved = 127
}
