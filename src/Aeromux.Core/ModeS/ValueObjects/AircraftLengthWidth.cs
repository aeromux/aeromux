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

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents the Aircraft/Vehicle Length and Width Code from Aircraft Operational Status Messages.
/// Encodes the dimensions of the transmitting aircraft or ground vehicle.
/// Uses 4 bits: bits 21-24 (message bits 53-56).
/// Reference: DO-282B §2.2.3.2.7.2.11, Table 2-74
/// </summary>
public record AircraftLengthAndWidth
{
    // Stores bits 21-24 (message bits 53-56)
    // 4-bit encoding for length and width categories
    // Code 0 = No Data or Unknown
    // Code 1-14 = Various length/width combinations
    // Code 15 = Length > 85m or Width > 90m

    /// <summary>
    /// Creates a new AircraftLengthAndWidthCode from the encoded 4-bit value.
    /// </summary>
    /// <param name="code">Bits 21-24 from the message (only lower 4 bits are used)</param>
    public AircraftLengthAndWidth(int code)
    {
        Code = (byte)(code & 0x0F); // Mask to 4 bits
    }

    /// <summary>
    /// Gets whether the length and width data is unavailable or unknown.
    /// </summary>
    public bool IsNoData => Code == 0;

    /// <summary>
    /// Gets the upper bound length in meters for this code.
    /// Returns 0 if no data is available.
    /// </summary>
    public int UpperBoundLengthMeters => Code switch
    {
        0 => 0,
        1 => 15,
        2 => 25,
        3 => 25,
        4 => 35,
        5 => 35,
        6 => 45,
        7 => 45,
        8 => 55,
        9 => 55,
        10 => 65,
        11 => 65,
        12 => 75,
        13 => 75,
        14 => 85,
        15 => 85,
        _ => 0
    };

    /// <summary>
    /// Gets the upper bound width in meters for this code.
    /// Returns 0 if no data is available.
    /// </summary>
    public double UpperBoundWidthMeters => Code switch
    {
        0 => 0.0,
        1 => 23.0,
        2 => 28.5,
        3 => 34.0,
        4 => 33.0,
        5 => 38.0,
        6 => 39.5,
        7 => 45.0,
        8 => 45.0,
        9 => 52.0,
        10 => 59.5,
        11 => 67.0,
        12 => 72.5,
        13 => 80.0,
        14 => 80.0,
        15 => 90.0,
        _ => 0.0
    };

    /// <summary>
    /// Gets the upper bound length in feet for this code.
    /// Returns 0 if no data is available.
    /// Conversion: 1 meter = 3.28084 feet
    /// </summary>
    public double UpperBoundLengthFeet => UpperBoundLengthMeters * 3.28084;

    /// <summary>
    /// Gets the upper bound width in feet for this code.
    /// Returns 0 if no data is available.
    /// Conversion: 1 meter = 3.28084 feet
    /// </summary>
    public double UpperBoundWidthFeet => UpperBoundWidthMeters * 3.28084;

    /// <summary>
    /// Gets the raw 4-bit encoded value (0-15).
    /// </summary>
    public byte Code { get; }

    /// <summary>
    /// Returns a human-readable string representation of the length and width code.
    /// </summary>
    public override string ToString()
    {
        if (IsNoData)
        {
            return "NO DATA";
        }

        if (Code == 15)
        {
            return "Length > 85m, Width > 90m";
        }

        return $"Length ≤ {UpperBoundLengthMeters}m, Width ≤ {UpperBoundWidthMeters}m";
    }
}
