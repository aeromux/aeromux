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
/// ADS-B version number from Operational Status message (TC 31).
/// Indicates the ADS-B protocol version and capabilities supported by the aircraft.
/// </summary>
/// <remarks>
/// Reference: RTCA DO-260, DO-260A, DO-260B, DO-260C standards.
/// Version field is 3 bits (values 0-7).
/// Higher versions include all capabilities of lower versions plus new features.
/// </remarks>
public enum AdsbVersion
{
    /// <summary>
    /// DO-260 (original ADS-B standard, version 0).
    /// Basic ADS-B functionality without version reporting.
    /// Legacy aircraft or transponders that don't report version number.
    /// </summary>
    DO260 = 0,

    /// <summary>
    /// DO-260A (ADS-B version 1).
    /// Adds: NACp (Navigation Accuracy Category - Position),
    /// NACv (Navigation Accuracy Category - Velocity),
    /// SIL (Source Integrity Level), operational status messages.
    /// First version to include accuracy and integrity reporting.
    /// </summary>
    DO260A = 1,

    /// <summary>
    /// DO-260B (ADS-B version 2).
    /// Adds: NIC (Navigation Integrity Category) supplements,
    /// SDA (System Design Assurance), GVA (Geometric Vertical Accuracy),
    /// target state and status. Mandatory for 2020+ aircraft (FAA - Federal Aviation
    /// Administration and EASA - European Union Aviation Safety Agency mandates).
    /// Enhanced position accuracy and integrity parameters.
    /// </summary>
    DO260B = 2,

    /// <summary>
    /// DO-260C (future ADS-B version 3, not yet deployed).
    /// Reserved for future enhancements to ADS-B protocol.
    /// Specifications not finalized as of 2025.
    /// </summary>
    Reserved3 = 3,

    /// <summary>
    /// Reserved for future use (version 4).
    /// </summary>
    Reserved4 = 4,

    /// <summary>
    /// Reserved for future use (version 5).
    /// </summary>
    Reserved5 = 5,

    /// <summary>
    /// Reserved for future use (version 6).
    /// </summary>
    Reserved6 = 6,

    /// <summary>
    /// Reserved for future use (version 7).
    /// </summary>
    Reserved7 = 7
}
