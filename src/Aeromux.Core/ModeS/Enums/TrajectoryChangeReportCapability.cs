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
/// Indicates an aircraft's capability to send Trajectory Change Reports.
/// This field is part of the Capability Class codes in TC 31 (Operational Status) messages.
/// </summary>
/// <remarks>
/// Trajectory Change Reports provide advance notice of planned flight path changes,
/// enabling improved conflict detection and resolution in air traffic management.
/// </remarks>
public enum TrajectoryChangeReportCapability
{
    /// <summary>
    /// No capability for sending messages to support Trajectory Change Reports.
    /// Aircraft does not transmit trajectory change information.
    /// </summary>
    NoCapability = 0,

    /// <summary>
    /// Capability of sending messages to support TC+0 Report only.
    /// Aircraft can report trajectory changes at the current time (TC+0).
    /// </summary>
    Tc0ReportOnly = 1,

    /// <summary>
    /// Capability of sending information for multiple TC reports.
    /// Aircraft can report trajectory changes at multiple future time points.
    /// </summary>
    MultipleTcReports = 2,

    /// <summary>
    /// Reserved for future use.
    /// This value is not currently defined in the specification.
    /// </summary>
    Reserved = 3
}
