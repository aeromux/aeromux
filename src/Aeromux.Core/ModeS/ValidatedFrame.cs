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

namespace Aeromux.Core.ModeS;

/// <summary>
/// Represents a Mode S frame that has passed CRC validation.
/// Ready for message parsing.
/// </summary>
/// <param name="Data">Validated frame bytes (7 or 14 bytes)</param>
/// <param name="Timestamp">UTC timestamp when frame was received</param>
/// <param name="IcaoRaw">24-bit ICAO aircraft address as uint (for internal lookups without string overhead)</param>
/// <param name="IcaoAddress">24-bit ICAO aircraft address (hex string, 6 chars)</param>
/// <param name="SignalStrength">Signal strength as power value (0.0-255.0, higher = stronger)</param>
/// <param name="WasCorrected">True if frame had single-bit error that was corrected</param>
/// <remarks>
/// ICAO address format: "A1B2C3" (6 hex characters, uppercase)
/// IcaoRaw: Same 24-bit value as IcaoAddress but as uint — used by IcaoConfidenceTracker for
/// dictionary lookups to avoid string hashing/comparison overhead.
/// Signal strength: Power value with full double precision for accurate weak signal representation
/// CRC validation ensures frame integrity before message parsing
/// </remarks>
public sealed record ValidatedFrame(
    byte[] Data,
    DateTime Timestamp,
    uint IcaoRaw,
    string IcaoAddress,
    double SignalStrength,
    bool WasCorrected)
{
    /// <summary>
    /// Gets the frame length in bits (56 or 112).
    /// </summary>
    public int LengthBits => Data.Length * 8;

    /// <summary>
    /// Gets the Downlink Format (DF) field from the first 5 bits.
    /// </summary>
    public DownlinkFormat DownlinkFormat => (DownlinkFormat)(Data[0] >> 3);

    /// <summary>
    /// Gets whether this frame uses PI mode (ICAO in AA field) or AP mode (ICAO in CRC).
    /// </summary>
    public bool UsesPIMode => DownlinkFormat is
        DownlinkFormat.AllCallReply or
        DownlinkFormat.ExtendedSquitter or
        DownlinkFormat.ExtendedSquitterNonTransponder or
        DownlinkFormat.MilitaryExtendedSquitter;
}
