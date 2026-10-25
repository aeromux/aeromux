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
/// Decides whether a frame's address field holds a 24-bit ICAO aircraft address.
/// </summary>
/// <remarks>
/// <para>
/// DF 18 is sent by devices that are not Mode S transponders (non-transponder ADS-B, TIS-B
/// ground stations, ADS-R rebroadcasts). Its control field (CF) says what the address field
/// holds. TIS-B and ADS-R frames also carry an IMF bit that switches the address to a Mode A
/// code with a track number, or to an anonymous address. Such an address can match a real
/// aircraft's 24 bits by chance, so it must not create or update an aircraft.
/// </para>
/// <para>
/// DF 19 is military; its body is not standardized, so its address can't be trusted either.
/// </para>
/// </remarks>
internal static class IcaoAddressClassifier
{
    /// <summary>
    /// Returns whether the frame's address is an ICAO aircraft address.
    /// </summary>
    /// <param name="data">CRC-valid frame bytes (7 or 14).</param>
    /// <returns>
    /// False for DF 19, for DF 18 with CF 1, 4, 5 or 7, and for DF 18 TIS-B/ADS-R frames whose
    /// IMF bit is set; true otherwise.
    /// </returns>
    internal static bool HasIcaoAddress(ReadOnlySpan<byte> data)
    {
        int df = data[0] >> 3;
        if (df == 19)
        {
            return false;
        }

        if (df != 18 || data.Length < 14)
        {
            return true;
        }

        // Control field (bits 6-8, 3 bits)
        int cf = data[0] & 0x07;
        return cf switch
        {
            0 => true,                              // ADS-B from a non-transponder device, ICAO address
            2 or 6 => !GetBit(data, ImfBit(data)),  // Fine TIS-B or ADS-R: ICAO address when IMF is 0
            3 => !GetBit(data, 33),              // Coarse TIS-B: IMF is the first ME bit
            _ => false                              // CF 1 and 5 non-ICAO address, CF 4 management, CF 7 reserved
        };
    }

    /// <summary>
    /// Frame bit (1-based) holding the IMF flag for the type code in a CF 2 or CF 6 frame,
    /// or 0 when that message has no IMF flag.
    /// </summary>
    /// <remarks>
    /// Each message type puts the flag in a bit that is spare in its DF 17 layout.
    /// </remarks>
    private static int ImfBit(ReadOnlySpan<byte> data)
    {
        // Type code (bits 33-37, 5 bits)
        int tc = data[4] >> 3;
        return tc switch
        {
            0 or (>= 9 and <= 18) or (>= 20 and <= 22) => 40,   // Airborne position: the single-antenna bit
            19 => 41,                                           // Airborne velocity: ME bit 9
            >= 5 and <= 8 => 53,                                // Surface position: ME bit 21
            28 => (data[4] & 0x07) == 1 ? 88 : 0,               // Emergency status (subtype 1) only: ME bit 56
            29 => 83,                                           // Target state and status: ME bit 51
            31 => 88,                                           // Operational status: ME bit 56
            _ => 0                                              // Identification and others have no IMF flag
        };
    }

    private static bool GetBit(ReadOnlySpan<byte> data, int bit) =>
        bit != 0 && ((data[(bit - 1) >> 3] >> (7 - ((bit - 1) & 7))) & 1) == 1;
}
