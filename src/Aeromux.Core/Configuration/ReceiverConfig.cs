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
/// Receiver station location and metadata configuration.
/// Used for surface position CPR decoding (TC 5-8) and distance/bearing calculations.
/// All properties are optional - if not configured, TC 5-8 decoding will be skipped.
/// </summary>
public class ReceiverConfig
{
    /// <summary>
    /// Gets or sets the receiver latitude in decimal degrees.
    /// Valid range: -90 (South Pole) to +90 (North Pole).
    /// Required for TC 5-8 surface position decoding.
    /// Example: 37.7749 (San Francisco), 51.5074 (London)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the receiver longitude in decimal degrees.
    /// Valid range: -180 (West) to +180 (East).
    /// Required for TC 5-8 surface position decoding.
    /// Example: -122.4194 (San Francisco), -0.1278 (London)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the receiver altitude in meters above sea level (optional).
    /// Used for elevation-corrected range calculations (future enhancement).
    /// Example: 15 (San Francisco), 11 (London)
    /// </summary>
    public int? Altitude { get; set; }

    /// <summary>
    /// Gets or sets a friendly name for this receiver station (optional).
    /// Used for logging and display purposes.
    /// Example: "San Francisco Bay Area", "London Heathrow"
    /// </summary>
    public string? Name { get; set; }
}
