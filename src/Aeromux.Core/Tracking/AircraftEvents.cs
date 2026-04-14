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

namespace Aeromux.Core.Tracking;

/// <summary>
/// Event arguments for aircraft added or expired events.
/// Used by OnAircraftAdded and OnAircraftExpired events.
/// </summary>
public class AircraftEventArgs : EventArgs
{
    /// <summary>
    /// The aircraft that was added to tracking or expired and removed.
    /// Contains complete aircraft state at time of event.
    /// </summary>
    public required Aircraft Aircraft { get; init; }
}
