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
/// Represents the Lateral Axis GPS Antenna Offset from Aircraft Operational Status Messages.
/// Encodes the lateral distance of the GPS antenna from the longitudinal (roll) axis.
/// Uses 3 bits: bit 33 (direction), bits 34-35 (distance magnitude).
/// Reference: DO-282B §2.2.3.2.7.2.4.7
/// </summary>
public record LateralGpsAntennaOffset
{
    // Stores bits 33-35 (message bits 65-67)
    // Bit 33: 0=left, 1=right
    // Bits 34-35: distance encoding (00=no data/0m, 01=2m, 10=4m, 11=6m)

    /// <summary>
    /// Creates a new LateralGpsAntennaOffset from the encoded 3-bit value.
    /// </summary>
    /// <param name="bits">Bits 33-35 from the message (only lower 3 bits are used)</param>
    public LateralGpsAntennaOffset(int bits)
    {
        EncodedValue = (byte)(bits & 0x7); // Mask to 3 bits
    }

    /// <summary>
    /// Gets whether the GPS antenna is positioned left of the longitudinal axis.
    /// </summary>
    public bool IsLeft => (EncodedValue & 0x4) == 0;

    /// <summary>
    /// Gets whether the GPS antenna is positioned right of the longitudinal axis.
    /// </summary>
    public bool IsRight => (EncodedValue & 0x4) != 0;

    /// <summary>
    /// Gets whether the offset data is unavailable (bits 34-35 are 00).
    /// </summary>
    public bool IsNoData => (EncodedValue & 0x3) == 0;

    /// <summary>
    /// Gets the lateral distance in meters from the longitudinal axis.
    /// Negative values indicate LEFT, positive values indicate RIGHT.
    /// Returns 0 if no data is available.
    /// </summary>
    public int DistanceMeters
    {
        get
        {
            if (IsNoData)
            {
                return 0;
            }

            // Decode bits 34-35 to get magnitude in meters
            int magnitude = (EncodedValue & 0x3) switch
            {
                0b01 => 2,  // 2 meters
                0b10 => 4,  // 4 meters
                0b11 => 6,  // 6 meters (max per spec)
                _ => 0
            };

            // Apply sign based on direction: left is negative, right is positive
            return IsLeft ? -magnitude : magnitude;
        }
    }

    /// <summary>
    /// Gets the lateral distance in feet from the longitudinal axis.
    /// Negative values indicate LEFT, positive values indicate RIGHT.
    /// Returns 0 if no data is available.
    /// Conversion: 1 meter = 3.28084 feet
    /// </summary>
    public double DistanceFeet => DistanceMeters * 3.28084;

    /// <summary>
    /// Gets the raw 3-bit encoded value.
    /// </summary>
    public byte EncodedValue { get; }

    /// <summary>
    /// Returns a human-readable string representation of the offset.
    /// </summary>
    public override string ToString() =>
        IsNoData ? "NO DATA" : $"{Math.Abs(DistanceMeters)}m {(IsLeft ? "LEFT" : "RIGHT")}";
}
