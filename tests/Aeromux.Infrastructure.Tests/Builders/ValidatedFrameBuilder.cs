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

namespace Aeromux.Infrastructure.Tests.Builders;

/// <summary>
/// Fluent builder for constructing ValidatedFrame instances for testing.
/// Allows bypassing CRC validation for direct parser testing.
/// </summary>
public class ValidatedFrameBuilder
{
    private byte[] _data = [];
    private DateTime _timestamp = DateTime.UtcNow;
    private string _icaoAddress = "000000";
    private double _signalStrength = 255.0;
    private bool _wasCorrected;

    /// <summary>
    /// Sets the frame data from a hex string.
    /// </summary>
    /// <param name="hexString">Hex string (e.g., "8D4840D6202CC371C32CE0576098")</param>
    public ValidatedFrameBuilder WithHexData(string hexString)
    {
        _data = ConvertHexStringToBytes(hexString);
        return this;
    }

    /// <summary>
    /// Sets the frame data from a byte array.
    /// </summary>
    public ValidatedFrameBuilder WithData(byte[] data)
    {
        _data = data;
        return this;
    }

    /// <summary>
    /// Sets the ICAO aircraft address.
    /// </summary>
    /// <param name="icao">24-bit ICAO address as hex string (e.g., "4840D6")</param>
    public ValidatedFrameBuilder WithIcaoAddress(string icao)
    {
        ArgumentNullException.ThrowIfNull(icao);
        _icaoAddress = icao.ToUpperInvariant();
        return this;
    }

    /// <summary>
    /// Sets the signal strength.
    /// </summary>
    /// <param name="strength">Signal strength (0.0-255.0)</param>
    public ValidatedFrameBuilder WithSignalStrength(double strength)
    {
        _signalStrength = strength;
        return this;
    }

    /// <summary>
    /// Sets whether the frame was error-corrected.
    /// </summary>
    public ValidatedFrameBuilder WithCorrectionFlag(bool corrected)
    {
        _wasCorrected = corrected;
        return this;
    }

    /// <summary>
    /// Sets the timestamp.
    /// </summary>
    public ValidatedFrameBuilder WithTimestamp(DateTime timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    /// <summary>
    /// Builds the ValidatedFrame instance.
    /// </summary>
    public ValidatedFrame Build()
    {
        return new ValidatedFrame(_data, _timestamp, _icaoAddress, _signalStrength, _wasCorrected);
    }

    /// <summary>
    /// Converts a hex string to a byte array.
    /// </summary>
    private static byte[] ConvertHexStringToBytes(string hex)
    {
        // Remove spaces and common separators
        hex = hex.Replace(" ", "").Replace("-", "").Replace("*", "");

        // Convert pairs of hex characters to bytes
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }
}
