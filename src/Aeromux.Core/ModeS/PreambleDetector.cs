namespace Aeromux.Core.ModeS;

/// <summary>
/// Detects Mode S preambles in magnitude data and extracts raw frames.
/// Uses local noise estimation following readsb's architecture.
/// </summary>
/// <remarks>
/// Mode S preamble structure (at 2 MSPS):
/// - 4 pulses at: 0.0µs (samples 0-1), 1.0µs (samples 2-3), 3.5µs (samples 7-8), 4.5µs (samples 9-10)
/// - Total duration: 8µs = 16 samples
/// - Data starts at sample 16
///
/// Local noise estimation (readsb approach):
/// - Uses 5 samples from LOW regions: 5, 8 (preamble valleys), 16-18 (early data PPM valleys)
/// - Threshold validation: peaks must exceed (average noise × threshold²)
/// - Threshold squared because Phase 2 stores I²+Q² (power), not amplitude
///
/// Reference: readsb/demod_2400.c
/// </remarks>
public sealed class PreambleDetector
{
    /// <summary>
    /// Maximum samples needed to read a complete frame (for documentation/reference).
    /// Preamble (16) + Long frame data (112 bits × 2 samples/bit) = 240 samples
    /// Note: Not used for bounds checking with circular buffer (modulo handles wrap-around).
    /// </summary>
    private const int MaxFrameReadAhead = 16 + (ModeSFrameLength.Long * 2);

    private readonly double _preambleThresholdSquared;  // Squared for power comparison
    private readonly List<RawFrame> _frameBuffer = new();  // Reusable buffer

    // Statistics (exposed as properties for DeviceWorker to log)
    private long _preambleCandidates;
    private long _validPreambles;
    private long _framesExtracted;

    /// <summary>
    /// Initializes a new preamble detector with configurable threshold.
    /// </summary>
    /// <param name="preambleThreshold">
    /// Signal-to-noise ratio threshold for preamble validation (amplitude ratio).
    /// Default 3.16 ≈ √10 = 10 dB SNR (user-friendly amplitude ratio).
    /// Valid range: 1.5 (3.5 dB, sensitive) to 10.0 (20 dB, restrictive).
    /// Lower values = more sensitive (more false positives).
    /// Higher values = less sensitive (may miss weak signals).
    ///
    /// Note: Internally squared (3.16² ≈ 10) because Phase 2 stores I²+Q² (power),
    /// not sqrt(I²+Q²) (amplitude). This allows correct power-to-power comparison.
    /// </param>
    public PreambleDetector(double preambleThreshold = 3.16)
    {
        if (preambleThreshold < 1.5 || preambleThreshold > 10.0)
        {
            throw new ArgumentOutOfRangeException(nameof(preambleThreshold),
                $"Preamble threshold must be between 1.5 and 10.0 (got {preambleThreshold})");
        }

        // Square the amplitude threshold for power comparison
        // User specifies 3.16 (amplitude ratio), we store 10 (power ratio)
        _preambleThresholdSquared = preambleThreshold * preambleThreshold;
    }

    /// <summary>
    /// Scans magnitude buffer for Mode S preambles and extracts frames.
    /// </summary>
    /// <param name="magnitudes">Magnitude buffer from IQDemodulator (circular)</param>
    /// <param name="startPosition">Starting position in circular buffer (0 to bufferLength-1)</param>
    /// <param name="count">Number of samples to scan</param>
    /// <returns>List of raw frames ready for CRC validation (Phase 4)</returns>
    /// <remarks>
    /// Performance: Reuses internal buffer to avoid allocations on hot path.
    /// Frame skipping: After detecting a frame, skips ahead to avoid re-detection.
    ///   Note: Frame skip may exceed count boundary (e.g., detect frame at position 19,950
    ///   in 20,000 sample scan, skip 240 samples = 20,190). The excess samples remain
    ///   in the circular buffer for the next scan batch - no data loss occurs.
    /// Circular buffer: Correctly handles wrap-around using modulo arithmetic.
    /// </remarks>
    public List<RawFrame> DetectAndExtract(ReadOnlySpan<ushort> magnitudes, int startPosition, int count)
    {
        _frameBuffer.Clear();  // Reuse existing list
        int bufferLength = magnitudes.Length;

        // Scan specified number of samples (handles circular buffer wrap-around)
        int samplesScanned = 0;
        while (samplesScanned < count)
        {
            // Calculate circular buffer position
            int idx = (startPosition + samplesScanned) % bufferLength;

            // Quick check: must be local maximum
            if (magnitudes[idx] < magnitudes[(idx + 1) % bufferLength])
            {
                samplesScanned++;
                continue;
            }

            _preambleCandidates++;

            // Check preamble pattern and validate with local noise
            if (CheckPreamble(magnitudes, idx, bufferLength))
            {
                _validPreambles++;

                // Extract frame
                RawFrame? frame = ExtractFrame(magnitudes, idx, bufferLength);
                if (frame != null)
                {
                    _frameBuffer.Add(frame);
                    _framesExtracted++;

                    // Skip ahead to avoid re-detecting the same frame
                    // Preamble (16 samples) + frame data (bits × 2 samples/bit)
                    int skipAmount = 16 + (frame.LengthBits * 2);
                    samplesScanned += skipAmount;
                }
                else
                {
                    samplesScanned++;
                }
            }
            else
            {
                samplesScanned++;
            }
        }

        return _frameBuffer;
    }

    /// <summary>
    /// Checks if a preamble pattern exists at the given position using local noise estimation.
    /// Follows readsb's algorithm with 5 noise samples and configurable threshold.
    /// </summary>
    /// <remarks>
    /// CRITICAL: Uses squared threshold because Phase 2 stores I²+Q² (power), not amplitude.
    /// Averages 5 noise samples to get per-sample noise for correct peak comparison.
    /// </remarks>
    private bool CheckPreamble(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        // Local noise estimation using 5 samples from LOW regions:
        // - Sample 5: Preamble valley (between pulse 2 and pulse 3)
        // - Sample 8: Preamble valley (after pulse 3, before pulse 4)
        // - Samples 16-18: Early data samples (PPM valleys at noise level)
        // PPM encoding ensures ~2-3 of data samples are LOW (noise level)
        // This gives robust noise estimate from actual signal environment
        uint baseNoise = (uint)(m[(pos + 5) % bufferLength] + m[(pos + 8) % bufferLength] +
                                m[(pos + 16) % bufferLength] + m[(pos + 17) % bufferLength] + m[(pos + 18) % bufferLength]);

        // CRITICAL FIX 1: Average the 5 noise samples to get per-sample noise level
        // We compare individual peak samples to threshold, not sum-of-peaks to sum-of-noise
        uint averageNoise = baseNoise / 5;

        // CRITICAL FIX 2: Use squared threshold for power-to-power comparison
        // Phase 2 stores I²+Q² (power), not sqrt(I²+Q²) (amplitude)
        // User specifies 3.16 (amplitude), we squared it to ~10 (power) in constructor
        uint threshold = (uint)(averageNoise * _preambleThresholdSquared);

        // Validate peaks (4 samples that should be HIGH)
        // Pattern: HI LO HI LO (silence) HI LO HI
        if (m[(pos + 0) % bufferLength] < threshold)
        {
            return false;  // First peak
        }

        if (m[(pos + 2) % bufferLength] < threshold)
        {
            return false;  // Second peak
        }

        if (m[(pos + 7) % bufferLength] < threshold)
        {
            return false;  // Third peak
        }

        if (m[(pos + 9) % bufferLength] < threshold)
        {
            return false;  // Fourth peak
        }

        // Validate valleys are lower than peaks (pattern check)
        ushort m0 = m[(pos + 0) % bufferLength];
        ushort m2 = m[(pos + 2) % bufferLength];
        ushort m7 = m[(pos + 7) % bufferLength];
        ushort m9 = m[(pos + 9) % bufferLength];

        if (m[(pos + 1) % bufferLength] >= m0)
        {
            return false;  // Valley 1
        }

        if (m[(pos + 3) % bufferLength] >= m2)
        {
            return false;  // Valley 2
        }

        if (m[(pos + 8) % bufferLength] >= m7)
        {
            return false;  // Valley 3
        }

        if (m[(pos + 10) % bufferLength] >= m9)
        {
            return false; // Valley 4
        }

        // Check silence period (samples 4-6 should be low)
        if (m[(pos + 4) % bufferLength] > threshold / 2)
        {
            return false;
        }

        if (m[(pos + 5) % bufferLength] > threshold / 2)
        {
            return false;
        }

        if (m[(pos + 6) % bufferLength] > threshold / 2)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts frame bits using Pulse Position Modulation (PPM) decoding.
    /// Compares magnitudes at pulse positions to determine bit values.
    /// </summary>
    /// <remarks>
    /// Uses circular buffer arithmetic (modulo) to handle wrap-around.
    /// Data starts at position (pos + 16) in the circular buffer.
    /// </remarks>
    private RawFrame? ExtractFrame(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        // Preamble is 16 samples (8 µs), data starts 16 samples after preamble start
        // Use modulo arithmetic for all buffer accesses to handle circular wrap-around

        // Read first byte to determine message length from DF field
        byte[] firstByte = new byte[1];
        for (int bit = 0; bit < 8; bit++)
        {
            // Calculate position: preamble (16) + bit offset (bit * 2 samples per bit)
            int sampleIdx = (pos + 16 + bit * 2) % bufferLength;
            int nextIdx = (sampleIdx + 1) % bufferLength;

            // PPM: compare first half vs second half of bit period
            if (m[sampleIdx] > m[nextIdx])
            {
                firstByte[0] |= (byte)(1 << (7 - bit));  // Bit 0 (pulse in first half)
            }
            // else: Bit 1 (pulse in second half), already 0
        }

        // Determine message length from DF (first 5 bits)
        var df = (DownlinkFormat)(firstByte[0] >> 3);
        int messageLengthBits = GetMessageLength(df);
        if (messageLengthBits == 0)
        {
            return null;  // Invalid DF
        }

        // Extract full message
        int messageLengthBytes = messageLengthBits / 8;
        byte[] message = new byte[messageLengthBytes];
        message[0] = firstByte[0];

        // Extract remaining bytes
        for (int byteIdx = 1; byteIdx < messageLengthBytes; byteIdx++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                int totalBit = byteIdx * 8 + bit;
                // Calculate position with modulo for circular buffer
                int sampleIdx = (pos + 16 + totalBit * 2) % bufferLength;
                int nextIdx = (sampleIdx + 1) % bufferLength;

                if (m[sampleIdx] > m[nextIdx])
                {
                    message[byteIdx] |= (byte)(1 << (7 - bit));
                }
            }
        }

        return new RawFrame(message, DateTime.UtcNow);
    }

    /// <summary>
    /// Returns message length in bits based on Downlink Format (DF).
    /// </summary>
    private static int GetMessageLength(DownlinkFormat df)
    {
        return df switch
        {
            // Short frames (56 bits = 7 bytes)
            DownlinkFormat.ShortAirAirSurveillance or      // DF 0
            DownlinkFormat.SurveillanceAltitudeReply or    // DF 4
            DownlinkFormat.SurveillanceIdentityReply or    // DF 5
            DownlinkFormat.AllCallReply or                 // DF 11
            DownlinkFormat.LongAirAirSurveillance or       // DF 16
            DownlinkFormat.CommDExtendedLength             // DF 24
                => ModeSFrameLength.Short,

            // Long frames (112 bits = 14 bytes)
            // ADS-B Extended Squitter messages
            DownlinkFormat.ExtendedSquitter or                     // DF 17
            DownlinkFormat.ExtendedSquitterNonTransponder or       // DF 18
            DownlinkFormat.MilitaryExtendedSquitter or             // DF 19
            // Comm-B messages (NOT short frames)
            DownlinkFormat.CommBAltitudeReply or                   // DF 20
            DownlinkFormat.CommBIdentityReply                      // DF 21
                => ModeSFrameLength.Long,

            // Unknown/invalid DF
            _ => 0
        };
    }

    // Statistics properties for Coordinator Pattern (ADR-009)
    public long PreambleCandidates => _preambleCandidates;
    public long ValidPreambles => _validPreambles;
    public long FramesExtracted => _framesExtracted;
}
