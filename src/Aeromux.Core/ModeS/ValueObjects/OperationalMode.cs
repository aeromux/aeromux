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

using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents operational mode and status fields from Aircraft Operational Status (BDS 6,5) ADS-B messages.
/// These fields describe the aircraft's current operational state and active systems.
/// </summary>
public record OperationalMode
{
    /// <summary>
    /// Indicates whether a TCAS Resolution Advisory (RA) is currently active.
    /// An RA is issued when TCAS detects a collision threat and provides escape maneuvers.
    /// </summary>
    public bool? TCASRAActive { get; init; }

    /// <summary>
    /// Indicates whether the aircraft's IDENT switch is currently active.
    /// IDENT is used by ATC to positively identify an aircraft on radar.
    /// </summary>
    public bool? IdentSwitchActive { get; init; }

    /// <summary>
    /// Indicates whether the aircraft is receiving ATC (Air Traffic Control) services.
    /// </summary>
    public bool? ATCServices { get; init; }

    /// <summary>
    /// Indicates the antenna configuration (single or dual antenna system).
    /// Affects the accuracy and availability of position reports.
    /// </summary>
    public AntennaFlag? SingleAntenna { get; init; }

    /// <summary>
    /// System Design Assurance (SDA) level.
    /// Indicates the probability of failure conditions that could cause loss of function.
    /// Higher SDA levels indicate more rigorous design assurance.
    /// </summary>
    public SdaSupportedFailureCondition? SDA { get; init; }

    /// <summary>
    /// Lateral offset of the GPS antenna from the aircraft reference point.
    /// Used to accurately determine the aircraft's position based on antenna location.
    /// </summary>
    public LateralGpsAntennaOffset? GPSLatOffset { get; init; }

    /// <summary>
    /// Longitudinal offset of the GPS antenna from the aircraft reference point.
    /// Used to accurately determine the aircraft's position based on antenna location.
    /// </summary>
    public LongitudinalGpsAntennaOffset? GPSLongOffset { get; init; }
}
