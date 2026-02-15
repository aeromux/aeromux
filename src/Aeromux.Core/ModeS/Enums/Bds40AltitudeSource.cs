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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Altitude source from BDS 4,0 (Selected Vertical Intention).
/// Indicates which system is providing the selected altitude target.
/// </summary>
/// <remarks>
/// Transmitted as 2-bit field (bits 55-56) in BDS 4,0 messages.
/// </remarks>
public enum Bds40AltitudeSource
{
    /// <summary>
    /// Unknown altitude source (value 0).
    /// Source of altitude selection cannot be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Aircraft altitude source (value 1).
    /// Altitude from aircraft's own systems.
    /// </summary>
    Aircraft = 1,

    /// <summary>
    /// Mode Control Panel (MCP) altitude source (value 2).
    /// Altitude manually selected by pilot on autopilot MCP.
    /// </summary>
    McpFcu = 2,

    /// <summary>
    /// Flight Management System (FMS) altitude source (value 3).
    /// Altitude programmed in FMS flight plan.
    /// </summary>
    Fms = 3
}
