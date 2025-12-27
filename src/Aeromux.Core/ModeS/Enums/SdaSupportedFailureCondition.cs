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
/// System Design Assurance (SDA) code from DO-282B Table 2.2.3.2.7.2.4.6.
/// Defines the failure condition that the ADS-B system is designed to support.
/// 2-bit field (ME bits 31-32, Message bits 63-64) in Aircraft Operational Status Messages.
/// Indicates the probability of an ADS-B system fault causing false or misleading information to be transmitted.
/// </summary>
/// <remarks>
/// The ADS-B system includes the ADS-B transmission equipment, ADS-B processing equipment,
/// position source, and any other equipment that processes the position data transmitted by the ADS-B system.
/// Definitions and probabilities are defined in AC 25.1309-1A, AC 23-1309-1C, and AC 29-2C.
/// Design assurance levels follow RTCA DO-178B (EUROCAE ED-12B) or RTCA DO-254 (EUROCAE ED-80).
/// </remarks>
public enum SdaSupportedFailureCondition
{
    /// <summary>
    /// Unknown or No safety effect.
    /// Probability of undetected fault: > 1×10⁻³ per flight hour or Unknown.
    /// Software and Hardware Design Assurance Level: N/A.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Minor failure condition.
    /// Probability of undetected fault: ≤ 1×10⁻³ per flight hour.
    /// Software and Hardware Design Assurance Level: D.
    /// </summary>
    Minor = 1,

    /// <summary>
    /// Major failure condition.
    /// Probability of undetected fault: ≤ 1×10⁻⁵ per flight hour.
    /// Software and Hardware Design Assurance Level: C.
    /// </summary>
    Major = 2,

    /// <summary>
    /// Hazardous failure condition.
    /// Probability of undetected fault: ≤ 1×10⁻⁷ per flight hour.
    /// Software and Hardware Design Assurance Level: B.
    /// </summary>
    Hazardous = 3
}
