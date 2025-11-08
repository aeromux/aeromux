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
/// Operational Status message subtype (TC 31).
/// Indicates whether the aircraft is airborne or on the surface.
/// </summary>
/// <remarks>
/// Reference: ICAO Annex 10, Volume IV, 3.1.2.8.9 (Aircraft Operational Status).
/// Subtype field is 3 bits, but only 0 and 1 are currently defined.
/// Values 2-7 are reserved for future use.
/// </remarks>
public enum OperationalStatusSubtype
{
    /// <summary>
    /// Airborne operational status (subtype 0).
    /// Aircraft is in flight - provides airborne-specific capabilities and accuracies.
    /// </summary>
    Airborne = 0,

    /// <summary>
    /// Surface operational status (subtype 1).
    /// Aircraft is on ground - provides surface-specific capabilities and accuracies.
    /// </summary>
    Surface = 1

    // Values 2-7: Reserved for future use (not yet defined by ICAO)
}
