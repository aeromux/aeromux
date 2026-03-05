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
/// Provides aircraft enrichment data from the aeromux-db database.
/// Interface lives in Core to avoid Infrastructure dependency in the tracker.
/// </summary>
public interface IAircraftDatabaseLookup
{
    /// <summary>
    /// Looks up static aircraft data by ICAO 24-bit hex address.
    /// </summary>
    /// <param name="icaoAddress">ICAO address as 6-character uppercase hex string (e.g., "3C6753").</param>
    /// <returns>
    /// An <see cref="AircraftDatabaseRecord"/> with populated fields if found,
    /// or <see cref="AircraftDatabaseRecord.Empty"/> if the ICAO address is not in the database.
    /// </returns>
    AircraftDatabaseRecord LookupAircraft(string icaoAddress);
}
