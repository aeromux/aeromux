namespace Aeromux.Core.Tests.Builders;

/// <summary>
/// Fluent builder for constructing ValidatedFrame instances for testing.
/// Allows bypassing CRC validation for direct parser testing.
/// </summary>
public class ValidatedFrameBuilder
{
    private byte[] _data = Array.Empty<byte>();
    private DateTime _timestamp = DateTime.UtcNow;
    private string _icaoAddress = "000000";
    private byte _signalStrength = 255;
    private bool _wasCorrected = false;

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
    /// <param name="strength">Signal strength (0-255)</param>
    public ValidatedFrameBuilder WithSignalStrength(byte strength)
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
