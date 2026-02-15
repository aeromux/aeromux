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
/// Provides value-based equality comparison for byte arrays.
/// Used by FrameDeduplicator to enable Dictionary lookups based on frame content.
/// </summary>
/// <remarks>
/// <para>
/// C# Dictionary uses reference equality by default for byte arrays, which would treat
/// identical content in different array instances as distinct keys. This comparer enables
/// content-based comparison using Span&lt;byte&gt;.SequenceEqual for efficient value comparison.
/// </para>
///
/// <para><b>Hash Code Strategy:</b></para>
/// <list type="bullet">
/// <item>Mode S frames are 7 or 14 bytes with high entropy in first 4 bytes (DF + payload start)</item>
/// <item>XOR-based hash over first 4 bytes provides good distribution with minimal collisions</item>
/// <item>Actual equality verification uses full content comparison via SequenceEqual</item>
/// </list>
/// </remarks>
public sealed class ByteArrayComparer : IEqualityComparer<byte[]>
{
    /// <summary>
    /// Singleton instance for reuse across FrameDeduplicator instances.
    /// </summary>
    public static readonly ByteArrayComparer Instance = new();

    /// <summary>
    /// Determines whether two byte arrays have identical content.
    /// </summary>
    /// <param name="x">First byte array to compare.</param>
    /// <param name="y">Second byte array to compare.</param>
    /// <returns>true if both arrays have identical length and content; otherwise false.</returns>
    public bool Equals(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        // Span<byte>.SequenceEqual performs optimized SIMD comparison when available
        return x.AsSpan().SequenceEqual(y);
    }

    /// <summary>
    /// Generates a hash code based on the first 4 bytes of the array.
    /// </summary>
    /// <param name="obj">Byte array to hash.</param>
    /// <returns>Hash code derived from XOR of first 4 bytes.</returns>
    /// <remarks>
    /// Mode S frames have high entropy in the first 4 bytes:
    /// - Byte 0: DF (5 bits) + CA/VS/CC field (3 bits)
    /// - Bytes 1-3: Payload start (ICAO address for many DFs, or other message data)
    ///
    /// XOR-based hashing provides good distribution while maintaining fast computation.
    /// </remarks>
    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (obj.Length == 0)
        {
            return 0;
        }

        // XOR first 4 bytes (or fewer if array is shorter)
        int hash = 0;
        int bytesToHash = Math.Min(4, obj.Length);

        for (int i = 0; i < bytesToHash; i++)
        {
            hash ^= obj[i] << (i * 8);
        }

        return hash;
    }
}
