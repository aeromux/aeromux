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
/// Surveillance Integrity Level (SIL) from Operational Status message (TC 31).
/// Indicates the probability of the reported position exceeding the containment radius.
/// </summary>
/// <remarks>
/// Reference: RTCA DO-260B, Table 2-15 (SIL values).
/// SIL field is 2 bits (values 0-3).
/// Higher values indicate better integrity (lower probability of position error).
/// Used with NACp to determine if position can be used for safety-critical applications.
/// </remarks>
public enum SourceIntegrityLevel
{
    /// <summary>
    /// Unknown or unavailable integrity.
    /// Position integrity cannot be determined.
    /// Not suitable for safety-critical applications.
    /// </summary>
    [JsonStringEnumMemberName("Unknown")]
    Unknown = 0,

    /// <summary>
    /// Probability of exceeding NACp (Navigation Accuracy Category - Position) radius ≤ 1×10⁻³ per hour.
    /// Low integrity - suitable for traffic awareness only.
    /// </summary>
    [JsonStringEnumMemberName("10⁻³ per hour")]
    PerHour1E3 = 1,

    /// <summary>
    /// Probability of exceeding NACp radius ≤ 1×10⁻⁵ per hour.
    /// Medium integrity - suitable for most ATC (Air Traffic Control) applications.
    /// </summary>
    [JsonStringEnumMemberName("10⁻⁵ per hour")]
    PerHour1E5 = 2,

    /// <summary>
    /// Probability of exceeding NACp radius ≤ 1×10⁻⁷ per hour.
    /// High integrity - suitable for safety-critical applications (e.g., separation services).
    /// Required for RTCA DO-260B compliance in safety-critical airspace.
    /// </summary>
    [JsonStringEnumMemberName("10⁻⁷ per hour")]
    PerHour1E7 = 3
}
