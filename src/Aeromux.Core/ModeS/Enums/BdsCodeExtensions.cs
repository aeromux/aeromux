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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Extension methods for <see cref="BdsCode"/> enum.
/// </summary>
public static class BdsCodeExtensions
{
    /// <summary>
    /// Formats a BDS code as a human-readable string (e.g., "BDS 6,0").
    /// </summary>
    /// <param name="bdsCode">BDS code enum value.</param>
    /// <returns>Formatted string (e.g., "BDS 6,0", "Unknown", "Empty").</returns>
    public static string ToFormattedString(this BdsCode bdsCode)
    {
        return bdsCode switch
        {
            BdsCode.Unknown => "Unknown",
            BdsCode.Empty => "Empty",
            // Format numeric BDS codes as "BDS X,Y" (e.g., 60 -> "BDS 6,0")
            _ => $"BDS {(int)bdsCode / 10},{(int)bdsCode % 10}"
        };
    }
}
