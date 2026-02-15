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

namespace Aeromux.Core.ModeS.ValueObjects;

/// <summary>
/// Represents the Longitudinal Axis GPS Antenna Offset from Aircraft Operational Status Messages.
/// Encodes the longitudinal distance of the GPS antenna from the NOSE of the aircraft.
/// Uses 5 bits: bits 36-40 (message bits 68-72).
/// Reference: DO-282B §2.2.3.2.7.2.4.7B
/// </summary>
public record LongitudinalGpsAntennaOffset
{
    // Stores bits 36-40 (message bits 68-72)
    // 5-bit encoding for distance aft from aircraft nose
    // Range: 0m (or NO DATA), 2m, 4m, 6m, 8m, 10m, 12m, ... up to 62m
    // Maximum distance aft from aircraft nose is 62 meters or 203.412 feet

    /// <summary>
    /// Creates a new LongitudinalGpsAntennaOffset from the encoded 5-bit value.
    /// </summary>
    /// <param name="bits">Bits 36-40 from the message (only lower 5 bits are used)</param>
    public LongitudinalGpsAntennaOffset(int bits)
    {
        EncodedValue = (byte)(bits & 0x1F); // Mask to 5 bits
    }

    /// <summary>
    /// Gets whether the offset data is unavailable (all bits are 0).
    /// </summary>
    public bool IsNoData => EncodedValue == 0;

    /// <summary>
    /// Gets the longitudinal distance in meters aft from the aircraft nose.
    /// Returns 0 if no data is available.
    /// Valid range: 0, 2, 4, 6, 8, 10, 12, ..., 62 meters (2-meter increments).
    /// </summary>
    public int DistanceMeters
    {
        get
        {
            if (IsNoData)
            {
                return 0;
            }

            // Each increment represents 2 meters
            // 0b00000 (0) = 0 or NO DATA
            // 0b00001 (1) = 2m
            // 0b00010 (2) = 4m
            // 0b00011 (3) = 6m
            // 0b00100 (4) = 8m
            // 0b00101 (5) = 10m
            // ...
            // 0b11111 (31) = 62m
            return EncodedValue * 2;
        }
    }

    /// <summary>
    /// Gets the longitudinal distance in feet aft from the aircraft nose.
    /// Returns 0 if no data is available.
    /// Conversion: 1 meter = 3.28084 feet
    /// Maximum distance: 203.412 feet
    /// </summary>
    public double DistanceFeet => DistanceMeters * 3.28084;

    /// <summary>
    /// Gets the raw 5-bit encoded value.
    /// </summary>
    public byte EncodedValue { get; }

    /// <summary>
    /// Returns a human-readable string representation of the offset.
    /// </summary>
    public override string ToString() =>
        IsNoData ? "NO DATA" : $"{DistanceMeters}m AFT";
}
