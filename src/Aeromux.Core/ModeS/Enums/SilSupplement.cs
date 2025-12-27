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
/// Represents the SIL Supplement (Source Integrity Level Supplement) from Target State and Status Messages.
/// Defines whether the reported SIL probability is based on "per hour" or "per sample" probability.
/// This is a 1-bit subfield (bit 8, message bit 40).
/// Reference: DO-282B §2.2.3.2.7.1.3.1, Table 2-46
/// </summary>
public enum SilSupplement
{
    /// <summary>
    /// Probability of exceeding NIC radius of containment is based on "per hour".
    /// The probability of the reported geometric position laying outside the NIC containment
    /// radius in any given hour without an alert or an alert longer than the allowable time-to-alert.
    /// Typically used when the probability of exceeding the NIC is greater for the faulted
    /// versus fault-free Signal-in-Space case (when SIS fault rate is defined as hourly).
    /// For GNSS position sources, NIC is derived from GNSS Horizontal Protection Level (HPL)
    /// which is based on a probability of 1×10⁻⁷ per hour.
    /// </summary>
    PerHour = 0,

    /// <summary>
    /// Probability of exceeding NIC radius of containment is based on "per sample".
    /// The probability of a reported geometric position laying outside the NIC containment radius.
    /// Typically used when the probability of exceeding the NIC is greater for the fault-free
    /// Signal-in-Space case, or when the position source does not depend on a Signal-in-Space.
    /// For IRU, DME/DME and DME/DME/LOC position sources, probability may be based on
    /// a per sample basis.
    /// </summary>
    PerSample = 1
}
