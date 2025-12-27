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
/// Specifies the source of selected altitude information in aircraft autopilot/FMS systems.
/// Used in ADS-B Target State and Status messages (TC 29) and Comm-B Selected Vertical Intention (BDS 4,0).
/// </summary>
public enum AltitudeSource
{
    /// <summary>
    /// Mode Control Panel / Flight Control Unit.
    /// Altitude manually selected by the pilot on the autopilot control panel.
    /// Used for tactical altitude changes and direct pilot intervention.
    /// Sources: TC 29 (both versions), BDS 4,0.
    /// </summary>
    McpFcu,

    /// <summary>
    /// Flight Management System.
    /// Altitude programmed into the FMS as part of the flight plan.
    /// Used for strategic navigation following the planned vertical profile.
    /// Sources: TC 29 Version 2 only.
    /// </summary>
    Fms,

    /// <summary>
    /// Flight Management System / Area Navigation.
    /// Altitude from FMS/RNAV system, includes both flight plan and RNAV procedures.
    /// Sources: TC 29 Version 1, BDS 4,0.
    /// </summary>
    FmsRnav,

    /// <summary>
    /// Holding mode altitude.
    /// Altitude maintained during holding pattern operations.
    /// Sources: TC 29 Version 1 only.
    /// </summary>
    HoldingMode,

    /// <summary>
    /// Unknown or unavailable altitude source.
    /// Used when altitude source information is not available or cannot be determined.
    /// </summary>
    Unknown
}
