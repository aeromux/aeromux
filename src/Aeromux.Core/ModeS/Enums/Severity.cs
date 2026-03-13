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

using System.Text.Json.Serialization;

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Meteorological hazard severity levels from BDS 4,5 (Meteorological Hazard Report).
/// Used for turbulence, wind shear, microburst, icing, and wake vortex severity indicators.
/// </summary>
/// <remarks>
/// Values are transmitted as 2-bit fields in BDS 4,5 messages.
/// These severity levels follow standard aviation meteorological reporting categories
/// as defined in ICAO Annex 3 (Meteorological Service for International Air Navigation).
/// </remarks>
public enum Severity
{
    /// <summary>
    /// NIL - No hazard detected or negligible conditions.
    /// Represents normal flight conditions with no adverse weather impact.
    /// </summary>
    [JsonStringEnumMemberName("Nil")]
    Nil = 0,

    /// <summary>
    /// Light severity - Minor turbulence, wind shear, or icing.
    /// May cause slight, erratic changes in altitude/attitude but minimal passenger discomfort.
    /// Aircraft handling remains normal with no significant performance degradation.
    /// </summary>
    [JsonStringEnumMemberName("Light")]
    Light = 1,

    /// <summary>
    /// Moderate severity - Noticeable turbulence, wind shear, or icing.
    /// Changes in altitude/attitude occur but aircraft remains under positive control.
    /// Passengers feel definite strain against seat belts. Moderate performance impact.
    /// Icing may require activation of anti-ice/de-ice systems.
    /// </summary>
    [JsonStringEnumMemberName("Moderate")]
    Moderate = 2,

    /// <summary>
    /// Severe severity - Intense turbulence, wind shear, or icing.
    /// Aircraft may be momentarily out of control. Large, abrupt altitude/attitude changes.
    /// Occupants thrown violently against seat belts. Significant performance degradation.
    /// Severe icing can cause rapid accumulation requiring immediate action.
    /// May require immediate altitude/route change for safety.
    /// </summary>
    [JsonStringEnumMemberName("Severe")]
    Severe = 3
}
