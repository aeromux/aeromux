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
/// Emergency state codes from TC 28 aircraft status messages.
/// Indicates the type of emergency or priority condition reported by the aircraft.
/// </summary>
public enum EmergencyState
{
    /// <summary>No emergency (0)</summary>
    NoEmergency = 0,

    /// <summary>General emergency - squawk 7700 (1)</summary>
    GeneralEmergency = 1,

    /// <summary>Lifeguard/medical emergency (2)</summary>
    LifeguardMedical = 2,

    /// <summary>Minimum fuel (3)</summary>
    MinimumFuel = 3,

    /// <summary>No communications - squawk 7600 (4)</summary>
    NoCommunications = 4,

    /// <summary>Unlawful interference (hijack) - squawk 7500 (5)</summary>
    UnlawfulInterference = 5,

    /// <summary>Downed aircraft (6)</summary>
    DownedAircraft = 6,

    /// <summary>Reserved (7)</summary>
    Reserved = 7
}
