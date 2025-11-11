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
/// At 2.4 MSPS sampling rate (standard):
/// - Mode S data rate: 1 Mbit/s
/// - Samples per bit: 2.4 samples (requiring phase tracking for bit detection)
/// - Short frame: ~134 samples
/// - Long frame: ~269 samples
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
