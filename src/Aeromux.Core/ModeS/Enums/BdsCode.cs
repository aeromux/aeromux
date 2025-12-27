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
/// BDS (Comm-B Data Selector) register codes.
/// Identifies the type of data contained in the 56-bit MB field of Comm-B messages (DF 20/21).
/// </summary>
/// <remarks>
/// BDS registers provide enhanced surveillance (EHS), elementary surveillance (ELS),
/// and meteorological (MRAR) data from aircraft transponders.
/// Reference: ICAO Doc 9871 (Technical Provisions for Mode S Services and Extended Squitter).
/// </remarks>
public enum BdsCode
{
    /// <summary>
    /// Unknown BDS code - unable to infer register type from MB field.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Empty response - all zeros in MB field.
    /// </summary>
    Empty = 1,

    /// <summary>
    /// BDS 1,0: Data link capability report.
    /// Reports aircraft's Mode S data link capabilities (16-bit capability flags).
    /// </summary>
    Bds10 = 10,

    /// <summary>
    /// BDS 1,7: Common usage GICB capability report.
    /// Reports which BDS registers the aircraft supports (56-bit capability mask).
    /// </summary>
    Bds17 = 17,

    /// <summary>
    /// BDS 2,0: Aircraft identification (callsign).
    /// 8-character callsign encoded using AIS charset (6 bits per character).
    /// </summary>
    Bds20 = 20,

    /// <summary>
    /// BDS 3,0: ACAS Resolution Advisory.
    /// ACAS/TCAS active resolution advisory data (simplified VDS validation only).
    /// </summary>
    Bds30 = 30,

    /// <summary>
    /// BDS 4,0: Selected vertical intention (EHS).
    /// MCP/FCU selected altitude, FMS selected altitude, barometric pressure setting.
    /// </summary>
    Bds40 = 40,

    /// <summary>
    /// BDS 4,4: Meteorological routine report (MRAR).
    /// Wind speed/direction, static air temperature, pressure, figure of merit.
    /// </summary>
    Bds44 = 44,

    /// <summary>
    /// BDS 4,5: Meteorological hazard report (MRAR).
    /// Turbulence, wind shear, microburst, icing, wake vortex, temperature, pressure, radio height.
    /// </summary>
    Bds45 = 45,

    /// <summary>
    /// BDS 5,0: Track and turn report (EHS).
    /// Roll angle, track angle, ground speed, true airspeed.
    /// </summary>
    Bds50 = 50,

    /// <summary>
    /// BDS 5,3: Air-referenced state vector (EHS).
    /// Magnetic heading, indicated airspeed, Mach number, true airspeed.
    /// </summary>
    Bds53 = 53,

    /// <summary>
    /// BDS 6,0: Heading and speed report (EHS).
    /// Magnetic heading, IAS, Mach, barometric vertical rate, inertial vertical rate.
    /// </summary>
    Bds60 = 60
}
