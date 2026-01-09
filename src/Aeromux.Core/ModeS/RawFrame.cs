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

namespace Aeromux.Core.ModeS;

/// <summary>
/// Represents a raw Mode S frame extracted from magnitude data.
/// Ready for CRC validation.
/// </summary>
/// <param name="Data">Raw frame bytes (7 bytes for short, 14 bytes for long frames)</param>
/// <param name="Timestamp">UTC timestamp when frame was detected</param>
/// <param name="SignalStrength">Signal strength as POWER value (0.0-255.0, higher = stronger signal)</param>
/// <remarks>
/// The frame data includes:
/// - DF field (first 5 bits of byte 0)
/// - Message payload
/// - CRC/Parity field (last 24 bits)
///
/// CRC validation will verify frame integrity and extract ICAO address.
///
/// Signal strength stores power (not amplitude) with full double precision to accurately
/// represent very weak signals. Only quantized to byte when encoding to Beast format.
/// </remarks>
public sealed record RawFrame(byte[] Data, DateTime Timestamp, double SignalStrength)
{
    /// <summary>
    /// Gets the frame length in bits (56 or 112).
    /// </summary>
    public int LengthBits => Data.Length * 8;

    /// <summary>
    /// Gets the frame length in bytes (7 or 14).
    /// </summary>
    public int LengthBytes => Data.Length;

    /// <summary>
    /// Gets the Downlink Format (DF) field from the first 5 bits.
    /// Determines message type and expected frame length.
    /// </summary>
    public DownlinkFormat DownlinkFormat => (DownlinkFormat)(Data[0] >> 3);
}
