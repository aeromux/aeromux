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
/// Static aircraft data from the aeromux-db database.
/// Contains registration, type, manufacturer, operator, and special flags.
/// All properties are nullable — null means the field is not available in the database.
/// </summary>
public sealed record AircraftDatabaseRecord
{
    /// <summary>
    /// An empty record with all-null fields, used when the ICAO address is not found in the database.
    /// </summary>
    public static readonly AircraftDatabaseRecord Empty = new();

    /// <summary>
    /// Aircraft registration / tail number (e.g., "D-AIZZ").
    /// </summary>
    public string? Registration { get; init; }

    /// <summary>
    /// Country of registration (e.g., "Germany").
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// ICAO type designator (e.g., "A320", "B738").
    /// </summary>
    public string? TypeCode { get; init; }

    /// <summary>
    /// Human-readable aircraft type description (e.g., "Airbus A320").
    /// </summary>
    public string? TypeDescription { get; init; }

    /// <summary>
    /// ICAO type classification code (e.g., "L2J" = land, 2 engines, jet).
    /// </summary>
    public string? TypeIcaoClass { get; init; }

    /// <summary>
    /// Full aircraft model (e.g., "Boeing 777-36N").
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Manufacturer ICAO code (e.g., "AIRBUS").
    /// </summary>
    public string? ManufacturerIcao { get; init; }

    /// <summary>
    /// Manufacturer name (e.g., "Airbus").
    /// </summary>
    public string? ManufacturerName { get; init; }

    /// <summary>
    /// Operator name (e.g., "Lufthansa").
    /// </summary>
    public string? OperatorName { get; init; }

    /// <summary>
    /// FAA Privacy ICAO Address flag. True if the aircraft uses a temporary ICAO address for privacy.
    /// </summary>
    public bool? Pia { get; init; }

    /// <summary>
    /// FAA Limiting Aircraft Data Displayed flag. True if the aircraft owner has opted out of public tracking.
    /// </summary>
    public bool? Ladd { get; init; }

    /// <summary>
    /// Military aircraft flag. True if the aircraft is registered as military.
    /// </summary>
    public bool? Military { get; init; }
}
