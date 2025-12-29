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

namespace Aeromux.Core.Tracking;

/// <summary>
/// Aircraft meteorological information group.
/// Contains wind, temperature, pressure, and hazard data from MRAR (Meteorological Routine Air Report).
/// Sources: BDS 4,4 (Meteorological Routine), BDS 4,5 (Meteorological Hazard).
/// </summary>
public sealed record TrackedMeteo
{
    /// <summary>
    /// Wind speed in knots (BDS 4,4).
    /// Meteorological wind speed measured by aircraft.
    /// Range: 0-250 knots.
    /// Null if BDS 4,4 not received or wind data unavailable.
    /// </summary>
    public int? WindSpeed { get; init; }

    /// <summary>
    /// Wind direction in degrees (BDS 4,4).
    /// Direction wind is coming FROM (meteorological convention).
    /// Range: 0-360 degrees.
    /// Resolution: 180/256 degrees (~0.703°).
    /// Null if BDS 4,4 not received or wind data unavailable.
    /// </summary>
    public double? WindDirection { get; init; }

    /// <summary>
    /// Static air temperature in °C (BDS 4,4, BDS 4,5).
    /// Outside air temperature (OAT) measured by aircraft.
    /// Range: -80 to +60 °C.
    /// Resolution: 0.25 °C.
    /// Null if BDS 4,4/4,5 not received or temperature unavailable.
    /// </summary>
    public double? StaticAirTemperature { get; init; }

    /// <summary>
    /// Atmospheric pressure in hPa (BDS 4,4, BDS 4,5).
    /// Static pressure at aircraft altitude.
    /// Range: 100-1200 hPa.
    /// Null if BDS 4,4/4,5 not received or pressure unavailable.
    /// </summary>
    public double? Pressure { get; init; }

    /// <summary>
    /// Turbulence severity (BDS 4,5).
    /// Values: Nil, Light, Moderate, Severe.
    /// Null if BDS 4,5 not received or turbulence data unavailable.
    /// </summary>
    public Severity? Turbulence { get; init; }

    /// <summary>
    /// Wind shear severity (BDS 4,5).
    /// Values: Nil, Light, Moderate, Severe.
    /// Null if BDS 4,5 not received or wind shear data unavailable.
    /// </summary>
    public Severity? WindShear { get; init; }

    /// <summary>
    /// Microburst severity (BDS 4,5).
    /// Values: Nil, Light, Moderate, Severe.
    /// Null if BDS 4,5 not received or microburst data unavailable.
    /// </summary>
    public Severity? Microburst { get; init; }

    /// <summary>
    /// Icing severity (BDS 4,5).
    /// Values: Nil, Light, Moderate, Severe.
    /// Null if BDS 4,5 not received or icing data unavailable.
    /// </summary>
    public Severity? Icing { get; init; }

    /// <summary>
    /// Wake vortex severity (BDS 4,5).
    /// Values: Nil, Light, Moderate, Severe.
    /// Null if BDS 4,5 not received or wake vortex data unavailable.
    /// </summary>
    public Severity? WakeVortex { get; init; }

    /// <summary>
    /// Radio (radar) altitude in feet (BDS 4,5).
    /// Height above ground measured by radar altimeter.
    /// Range: 0-65520 feet.
    /// Resolution: 16 feet.
    /// Typically only available below 2500 feet AGL.
    /// Null if BDS 4,5 not received or radio height unavailable.
    /// </summary>
    public int? RadioHeight { get; init; }

    /// <summary>
    /// Figure of Merit (BDS 4,4).
    /// Quality indicator for meteorological data (0-7 scale).
    /// Higher values indicate better data quality.
    /// Null if BDS 4,4 not received.
    /// </summary>
    public int? FigureOfMerit { get; init; }

    /// <summary>
    /// Relative humidity in percentage (BDS 4,4).
    /// Atmospheric humidity measured by aircraft.
    /// Range: 0-100%.
    /// Resolution: 100/64 (~1.5625%).
    /// Null if BDS 4,4 not received or humidity data unavailable.
    /// </summary>
    public double? Humidity { get; init; }

    /// <summary>
    /// Timestamp of last meteorological data update.
    /// Updated when any meteo field changes.
    /// Null if no meteorological data received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
