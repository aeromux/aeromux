namespace Aeromux.Core.ModeS;

/// <summary>
/// Constants for Mode S frame lengths.
/// Mode S messages come in two standard lengths: short (56 bits) and long (112 bits).
/// </summary>
/// <remarks>
/// Frame structure:
/// - Short frame: 56 bits = 7 bytes (DF 0, 4, 5, 11, 16, 24)
/// - Long frame: 112 bits = 14 bytes (DF 17, 18, 19, 20, 21)
///
/// At 2 MSPS sampling rate (2 samples per bit):
/// - Short frame: 112 samples
/// - Long frame: 224 samples
/// </remarks>
public static class ModeSFrameLength
{
    /// <summary>
    /// Short frame length in bits.
    /// Used by: DF 0, 4, 5, 11, 16, 24
    /// </summary>
    public const int Short = 56;

    /// <summary>
    /// Long frame length in bits.
    /// Used by: DF 17, 18, 19, 20, 21
    /// </summary>
    public const int Long = 112;

    /// <summary>
    /// Short frame length in bytes.
    /// </summary>
    public const int ShortBytes = Short / 8;  // 7 bytes

    /// <summary>
    /// Long frame length in bytes.
    /// </summary>
    public const int LongBytes = Long / 8;  // 14 bytes
}
