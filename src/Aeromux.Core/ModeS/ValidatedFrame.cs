namespace Aeromux.Core.ModeS;

/// <summary>
/// Represents a Mode S frame that has passed CRC validation.
/// Ready for message parsing in Phase 5.
/// </summary>
/// <param name="Data">Validated frame bytes (7 or 14 bytes)</param>
/// <param name="Timestamp">UTC timestamp when frame was received</param>
/// <param name="IcaoAddress">24-bit ICAO aircraft address (hex string, 6 chars)</param>
/// <param name="SignalStrength">Signal strength indicator (0-255, higher = stronger)</param>
/// <param name="WasCorrected">True if frame had single-bit error that was corrected</param>
/// <remarks>
/// ICAO address format: "A1B2C3" (6 hex characters, uppercase)
/// Signal strength: Relative magnitude value from Phase 2 demodulator
/// CRC validation ensures frame integrity before message parsing (Phase 5)
/// </remarks>
public sealed record ValidatedFrame(
    byte[] Data,
    DateTime Timestamp,
    string IcaoAddress,
    byte SignalStrength,
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
