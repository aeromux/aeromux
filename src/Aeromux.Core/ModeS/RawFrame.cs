namespace Aeromux.Core.ModeS;

/// <summary>
/// Represents a raw Mode S frame extracted from magnitude data.
/// Ready for CRC validation in Phase 4.
/// </summary>
/// <param name="Data">Raw frame bytes (7 bytes for short, 14 bytes for long frames)</param>
/// <param name="Timestamp">UTC timestamp when frame was detected</param>
/// <remarks>
/// The frame data includes:
/// - DF field (first 5 bits of byte 0)
/// - Message payload
/// - CRC/Parity field (last 24 bits)
///
/// CRC validation (Phase 4) will verify frame integrity and extract ICAO address.
/// </remarks>
public sealed record RawFrame(byte[] Data, DateTime Timestamp)
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
