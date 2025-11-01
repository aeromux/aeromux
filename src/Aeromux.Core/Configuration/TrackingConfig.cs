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

namespace Aeromux.Core.Configuration;

/// <summary>
/// Aircraft tracking and state management configuration.
/// </summary>
public class TrackingConfig
{
    /// <summary>
    /// Gets or sets the aircraft timeout in minutes.
    /// Aircraft that haven't sent messages within this time are removed from tracking.
    /// Default: 60 minutes
    /// </summary>
    public int AircraftTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of position reports to store per aircraft.
    /// Used for track history and velocity calculations.
    /// Default: 1000 positions
    /// </summary>
    public int PositionHistorySize { get; set; } = 1000;
}
