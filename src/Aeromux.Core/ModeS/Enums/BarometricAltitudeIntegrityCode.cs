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
/// Represents the Barometric Altitude Integrity Code (NIC_BARO) from Aircraft Operational Status Messages.
/// Indicates whether the barometric pressure altitude has been cross-checked against another source.
/// This is a 1-bit subfield (bit 53, message bit 85) in Subtype 0 ADS-B messages.
/// Reference: DO-282B §2.2.3.2.7.2.10, Table 2-73
/// </summary>
public enum BarometricAltitudeIntegrityCode
{
    /// <summary>
    /// The barometric altitude is based on Gilham coded input that has NOT been
    /// cross-checked against another source of pressure altitude.
    /// If no update received within past 5 seconds, this value shall be used.
    /// </summary>
    [JsonStringEnumMemberName("Not Cross-Checked")]
    NotCrossChecked = 0,

    /// <summary>
    /// The barometric altitude is either:
    /// - Based on Gilham code input that HAS been cross-checked against another source
    ///   of pressure altitude and verified as consistent, OR
    /// - Based on a non-Gilham coded source (e.g., Synchro or DADS).
    /// For non-Gilham sources, this value is used whenever the barometric altitude is valid.
    /// </summary>
    [JsonStringEnumMemberName("Cross-Checked")]
    CrossCheckedOrNonGilham = 1
}
