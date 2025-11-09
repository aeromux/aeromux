namespace Aeromux.Core.ModeS;

/// <summary>
/// Detects Mode S preambles in magnitude data and extracts raw frames.
/// Implements 2.4 MSPS sampling with readsb's phase correlation approach.
/// </summary>
/// <remarks>
/// 2.4 MSPS Sampling (readsb approach):
/// - 6 samples per 5 symbols creates phase ambiguity
/// - Mode S preamble: 4 pulses at 0.0, 1.0, 3.5, 4.5 µs
/// - At 2.4 MHz: sample positions 0, 2.4, 8.4, 10.8 (fractional)
/// - Phase tracking: 5 possible phases (0-4) for bit alignment
/// - Uses weighted correlation functions to extract bits from 3-4 adjacent samples
///
/// Direct port of readsb's demod_2400.c:
/// - Pre-check filter for fast rejection (lines 333-344)
/// - Phase-specific preamble magnitude calculation (lines 366-400)
/// - 5 correlation functions slice_phase0-4 (lines 74-93)
/// - slice_byte with phase-specific sample offsets (lines 133-213)
/// - score_phase to find best phase alignment (lines 215-254)
///
/// Reference: readsb/demod_2400.c
/// </remarks>
public sealed class PreambleDetector
{
    private readonly double _preambleThreshold;
    private readonly List<RawFrame> _frameBuffer = new();
    private readonly CrcValidator _crcValidator = new(); // For validating extracted messages
    private readonly IcaoConfidenceTracker? _confidenceTracker; // Optional: for filtering AP mode from unknown aircraft

    // Statistics
    private long _preambleCandidates;
    private long _framesExtracted;
    private long _framesRejectedDuringExtraction;  // Would have extracted but rejected due to unknown ICAO

    public PreambleDetector(double preambleThreshold = 3.16, IcaoConfidenceTracker? confidenceTracker = null)
    {
        if (preambleThreshold < 1.5 || preambleThreshold > 10.0)
        {
            throw new ArgumentOutOfRangeException(nameof(preambleThreshold),
                $"Preamble threshold must be between 1.5 and 10.0 (got {preambleThreshold})");
        }

        _preambleThreshold = preambleThreshold;
        _confidenceTracker = confidenceTracker;
    }

    /// <summary>
    /// Scans magnitude buffer for Mode S preambles using readsb's 2.4 MHz approach.
    /// </summary>
    public List<RawFrame> DetectAndExtract(ReadOnlySpan<ushort> magnitudes, int startPosition, int count)
    {
        _frameBuffer.Clear();
        int bufferLength = magnitudes.Length;

        int samplesScanned = 0;
        while (samplesScanned < count)
        {
            int idx = (startPosition + samplesScanned) % bufferLength;

            // readsb's pre-check pattern (lines 333-344) - test 10 consecutive positions
            bool foundPattern = false;
            for (int offset = 0; offset < 10 && !foundPattern; offset++)
            {
                int testPos = (idx + offset) % bufferLength;

                // Pre-check: pa[1] > pa[7] && pa[12] > pa[14] && pa[12] > pa[15]
                if (magnitudes[(testPos + 1) % bufferLength] > magnitudes[(testPos + 7) % bufferLength] &&
                    magnitudes[(testPos + 12) % bufferLength] > magnitudes[(testPos + 14) % bufferLength] &&
                    magnitudes[(testPos + 12) % bufferLength] > magnitudes[(testPos + 15) % bufferLength])
                {
                    // Try phase correlation and frame extraction
                    RawFrame? frame = DetectAndExtractWithPhases(magnitudes, testPos, bufferLength, out bool hadPreamble);

                    // Count as preamble if at least one phase exceeded threshold (matches readsb line 461: bestscore != -42)
                    // This counts based on signal magnitude threshold passage, NOT CRC validity
                    if (hadPreamble)
                    {
                        _preambleCandidates++;

                        if (frame != null)
                        {
                            _frameBuffer.Add(frame);
                            _framesExtracted++;

                            // Skip past most of frame - matches readsb line 511: pa += msglen * 8 / 4
                            // msglen is in bits, formula simplifies to: msglen * 2 samples
                            // This skips ~83% of the message, allowing overlapping preamble detection
                            int skipAmount = offset + (frame.LengthBits * 2);
                            samplesScanned += skipAmount;
                        }
                        else
                        {
                            // Preamble detected but rejected - advance by 1 sample only (matches readsb behavior)
                            samplesScanned += offset + 1;
                        }
                        foundPattern = true;
                    }
                    else
                    {
                        // Pre-check passed but no phase exceeded threshold (bestscore = -42 in readsb)
                        // Advance by 1 sample to match readsb's continue behavior
                        samplesScanned += offset + 1;
                        foundPattern = true;
                    }
                }
            }

            if (!foundPattern)
            {
                // No pattern in 10 positions - skip forward
                samplesScanned += 10;
            }
        }

        return _frameBuffer;
    }

    /// <summary>
    /// Detects preamble using phase-specific magnitude calculation and extracts frame with best phase.
    /// Implements readsb's approach (lines 346-428).
    /// </summary>
    /// <param name="hadPreamble">True if at least one phase exceeded the magnitude threshold (bestscore != -42 in readsb)</param>
    private RawFrame? DetectAndExtractWithPhases(ReadOnlySpan<ushort> m, int pos, int bufferLength, out bool hadPreamble)
    {
        // Noise estimation using 5 samples from valleys (readsb line 352)
        int baseNoise = m[(pos + 5) % bufferLength] +
                        m[(pos + 8) % bufferLength] +
                        m[(pos + 16) % bufferLength] +
                        m[(pos + 17) % bufferLength] +
                        m[(pos + 18) % bufferLength];

        // Reference level: base_noise * threshold / 32 (readsb lines 358-362)
        // NOTE: _preambleThreshold is a ratio (e.g., 3.16), but readsb expects it pre-multiplied by 32
        // So we need: refLevel = baseNoise * (threshold * 32) / 32 = baseNoise * threshold
        int refLevel = (int)(baseNoise * _preambleThreshold);

        // Phase-specific preamble magnitude calculation (readsb lines 366-400)
        int diff_2_3 = m[(pos + 2) % bufferLength] - m[(pos + 3) % bufferLength];
        int sum_1_4 = m[(pos + 1) % bufferLength] + m[(pos + 4) % bufferLength];
        int diff_10_11 = m[(pos + 10) % bufferLength] - m[(pos + 11) % bufferLength];
        int common3456 = sum_1_4 - diff_2_3 + m[(pos + 9) % bufferLength] + m[(pos + 12) % bufferLength];

        int bestScore = -42;
        byte[]? bestMessage = null;

        // Try phases based on preamble magnitude thresholds
        int paMag1 = common3456 - diff_10_11;
        if (paMag1 >= refLevel)
        {
            TryPhase(m, pos, 4, bufferLength, ref bestScore, ref bestMessage);
            TryPhase(m, pos, 5, bufferLength, ref bestScore, ref bestMessage);
        }

        int paMag2 = common3456 + diff_10_11;
        if (paMag2 >= refLevel)
        {
            TryPhase(m, pos, 6, bufferLength, ref bestScore, ref bestMessage);
            TryPhase(m, pos, 7, bufferLength, ref bestScore, ref bestMessage);
        }

        int paMag3 = sum_1_4 + (2 * diff_2_3) + diff_10_11 + m[(pos + 12) % bufferLength];
        if (paMag3 >= refLevel)
        {
            TryPhase(m, pos, 8, bufferLength, ref bestScore, ref bestMessage);
        }

        // Determine if we had a preamble: at least one phase exceeded the magnitude threshold
        // This matches readsb line 461: count when bestscore != -42
        // bestScore != -42 means at least one phase was scored (positive or negative)
        hadPreamble = bestScore != -42;

        // Return best message if we found a valid one
        if (bestMessage != null && bestScore > 0)
        {
            return new RawFrame(bestMessage, DateTime.UtcNow);
        }

        // Count rejection if bestScore is -1 (valid CRC but unknown ICAO)
        // This matches readsb's "unrecognized ICAO address" rejection
        if (bestScore == -1)
        {
            _framesRejectedDuringExtraction++;
        }

        return null;
    }

    /// <summary>
    /// Tries to extract a message at a specific phase (readsb lines 215-254).
    /// Uses CrcValidator to verify the extracted message is valid.
    /// Updates bestScore/bestMessage if this phase produces a better result.
    /// Score meanings (matching readsb):
    ///   > 0: Valid message with known/accepted ICAO
    ///   -1: Valid CRC but unknown/rejected ICAO
    ///   -2: Bad message or invalid CRC
    /// </summary>
    private void TryPhase(ReadOnlySpan<ushort> m, int pos, int tryPhase, int bufferLength,
                          ref int bestScore, ref byte[]? bestMessage)
    {
        // Calculate data start position and initial phase (readsb lines 220-221)
        int pPtr = (pos + 19 + (tryPhase / 5)) % bufferLength;
        int phase = tryPhase % 5;

        // Extract first byte to determine message length
        byte firstByte = SliceByte(m, ref pPtr, ref phase, bufferLength);

        // Validate DF early (readsb lines 227-239)
        var df = (DownlinkFormat)(firstByte >> 3);
        int messageLengthBits = GetMessageLength(df);
        if (messageLengthBits == 0)
        {
            // Invalid DF - score -2 and update best (matching readsb behavior)
            if (-2 > bestScore)
            {
                bestScore = -2;
            }
            return;
        }

        // Extract full message
        int messageLengthBytes = messageLengthBits / 8;
        byte[] message = new byte[messageLengthBytes];
        message[0] = firstByte;

        for (int i = 1; i < messageLengthBytes; i++)
        {
            message[i] = SliceByte(m, ref pPtr, ref phase, bufferLength);
        }

        // Validate message with CRC (like readsb's scoreModesMessage)
        var rawFrame = new RawFrame(message, DateTime.UtcNow);
        ValidatedFrame? validated = _crcValidator.ValidateFrame(rawFrame, 128);

        int score;
        if (validated == null)
        {
            // Invalid CRC - score -2 (bad message)
            score = -2;
        }
        else
        {
            // Valid CRC! Check ICAO filter for AP mode messages
            // PI mode messages (DF 11,17,18,19) establish ICAO addresses with real CRC validation
            // AP mode messages (DF 0,4,5,16,20,21) only accepted if ICAO already confident
            if (!validated.UsesPIMode && _confidenceTracker != null)
            {
                if (!_confidenceTracker.IsConfident(validated.IcaoAddress))
                {
                    // AP mode with unknown ICAO - score -1 (valid CRC, unknown ICAO)
                    score = -1;
                }
                else
                {
                    // AP mode with known ICAO - assign positive score
                    score = 1000;
                }
            }
            else
            {
                // PI mode or no confidence tracker - assign positive score based on DF type
                score = df switch
                {
                    DownlinkFormat.ExtendedSquitter => 1800,
                    DownlinkFormat.ExtendedSquitterNonTransponder => 1400,
                    DownlinkFormat.AllCallReply => 700,
                    _ => 1000 // Other PI mode messages
                };
            }
        }

        // Update best score if this is better (higher score wins, including negative scores)
        if (score > bestScore)
        {
            bestScore = score;
            // Only save message if score is positive (accepted)
            if (score > 0)
            {
                bestMessage = message;
            }
            else
            {
                bestMessage = null;
            }
        }
    }

    /// <summary>
    /// Extracts one byte using phase-specific correlation functions (readsb lines 133-213).
    /// </summary>
    private byte SliceByte(ReadOnlySpan<ushort> m, ref int pPtr, ref int phase, int bufferLength)
    {
        byte theByte = 0;

        switch (phase)
        {
            case 0:
                theByte = (byte)(
                    (SlicePhase0(m, pPtr, bufferLength) > 0 ? 0x80 : 0) |
                    (SlicePhase2(m, (pPtr + 2) % bufferLength, bufferLength) > 0 ? 0x40 : 0) |
                    (SlicePhase4(m, (pPtr + 4) % bufferLength, bufferLength) > 0 ? 0x20 : 0) |
                    (SlicePhase1(m, (pPtr + 7) % bufferLength, bufferLength) > 0 ? 0x10 : 0) |
                    (SlicePhase3(m, (pPtr + 9) % bufferLength, bufferLength) > 0 ? 0x08 : 0) |
                    (SlicePhase0(m, (pPtr + 12) % bufferLength, bufferLength) > 0 ? 0x04 : 0) |
                    (SlicePhase2(m, (pPtr + 14) % bufferLength, bufferLength) > 0 ? 0x02 : 0) |
                    (SlicePhase4(m, (pPtr + 16) % bufferLength, bufferLength) > 0 ? 0x01 : 0));
                phase = 1;
                pPtr = (pPtr + 19) % bufferLength;
                break;

            case 1:
                theByte = (byte)(
                    (SlicePhase1(m, pPtr, bufferLength) > 0 ? 0x80 : 0) |
                    (SlicePhase3(m, (pPtr + 2) % bufferLength, bufferLength) > 0 ? 0x40 : 0) |
                    (SlicePhase0(m, (pPtr + 5) % bufferLength, bufferLength) > 0 ? 0x20 : 0) |
                    (SlicePhase2(m, (pPtr + 7) % bufferLength, bufferLength) > 0 ? 0x10 : 0) |
                    (SlicePhase4(m, (pPtr + 9) % bufferLength, bufferLength) > 0 ? 0x08 : 0) |
                    (SlicePhase1(m, (pPtr + 12) % bufferLength, bufferLength) > 0 ? 0x04 : 0) |
                    (SlicePhase3(m, (pPtr + 14) % bufferLength, bufferLength) > 0 ? 0x02 : 0) |
                    (SlicePhase0(m, (pPtr + 17) % bufferLength, bufferLength) > 0 ? 0x01 : 0));
                phase = 2;
                pPtr = (pPtr + 19) % bufferLength;
                break;

            case 2:
                theByte = (byte)(
                    (SlicePhase2(m, pPtr, bufferLength) > 0 ? 0x80 : 0) |
                    (SlicePhase4(m, (pPtr + 2) % bufferLength, bufferLength) > 0 ? 0x40 : 0) |
                    (SlicePhase1(m, (pPtr + 5) % bufferLength, bufferLength) > 0 ? 0x20 : 0) |
                    (SlicePhase3(m, (pPtr + 7) % bufferLength, bufferLength) > 0 ? 0x10 : 0) |
                    (SlicePhase0(m, (pPtr + 10) % bufferLength, bufferLength) > 0 ? 0x08 : 0) |
                    (SlicePhase2(m, (pPtr + 12) % bufferLength, bufferLength) > 0 ? 0x04 : 0) |
                    (SlicePhase4(m, (pPtr + 14) % bufferLength, bufferLength) > 0 ? 0x02 : 0) |
                    (SlicePhase1(m, (pPtr + 17) % bufferLength, bufferLength) > 0 ? 0x01 : 0));
                phase = 3;
                pPtr = (pPtr + 19) % bufferLength;
                break;

            case 3:
                theByte = (byte)(
                    (SlicePhase3(m, pPtr, bufferLength) > 0 ? 0x80 : 0) |
                    (SlicePhase0(m, (pPtr + 3) % bufferLength, bufferLength) > 0 ? 0x40 : 0) |
                    (SlicePhase2(m, (pPtr + 5) % bufferLength, bufferLength) > 0 ? 0x20 : 0) |
                    (SlicePhase4(m, (pPtr + 7) % bufferLength, bufferLength) > 0 ? 0x10 : 0) |
                    (SlicePhase1(m, (pPtr + 10) % bufferLength, bufferLength) > 0 ? 0x08 : 0) |
                    (SlicePhase3(m, (pPtr + 12) % bufferLength, bufferLength) > 0 ? 0x04 : 0) |
                    (SlicePhase0(m, (pPtr + 15) % bufferLength, bufferLength) > 0 ? 0x02 : 0) |
                    (SlicePhase2(m, (pPtr + 17) % bufferLength, bufferLength) > 0 ? 0x01 : 0));
                phase = 4;
                pPtr = (pPtr + 19) % bufferLength;
                break;

            case 4:
                theByte = (byte)(
                    (SlicePhase4(m, pPtr, bufferLength) > 0 ? 0x80 : 0) |
                    (SlicePhase1(m, (pPtr + 3) % bufferLength, bufferLength) > 0 ? 0x40 : 0) |
                    (SlicePhase3(m, (pPtr + 5) % bufferLength, bufferLength) > 0 ? 0x20 : 0) |
                    (SlicePhase0(m, (pPtr + 8) % bufferLength, bufferLength) > 0 ? 0x10 : 0) |
                    (SlicePhase2(m, (pPtr + 10) % bufferLength, bufferLength) > 0 ? 0x08 : 0) |
                    (SlicePhase4(m, (pPtr + 12) % bufferLength, bufferLength) > 0 ? 0x04 : 0) |
                    (SlicePhase1(m, (pPtr + 15) % bufferLength, bufferLength) > 0 ? 0x02 : 0) |
                    (SlicePhase3(m, (pPtr + 17) % bufferLength, bufferLength) > 0 ? 0x01 : 0));
                phase = 0;
                pPtr = (pPtr + 20) % bufferLength;
                break;
        }

        return theByte;
    }

    // ============================================================================
    // Correlation functions (readsb lines 74-93)
    // Hand-tuned coefficients - do NOT modify!
    // ============================================================================

    /// <summary>Phase 0: 18*m[0] - 15*m[1] - 3*m[2]</summary>
    private int SlicePhase0(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        return (18 * m[pos % bufferLength]) -
               (15 * m[(pos + 1) % bufferLength]) -
               (3 * m[(pos + 2) % bufferLength]);
    }

    /// <summary>Phase 1: 14*m[0] - 5*m[1] - 9*m[2]</summary>
    private int SlicePhase1(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        return (14 * m[pos % bufferLength]) -
               (5 * m[(pos + 1) % bufferLength]) -
               (9 * m[(pos + 2) % bufferLength]);
    }

    /// <summary>Phase 2: 16*m[0] + 5*m[1] - 20*m[2] (slightly DC unbalanced but better results)</summary>
    private int SlicePhase2(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        return (16 * m[pos % bufferLength]) +
               (5 * m[(pos + 1) % bufferLength]) -
               (20 * m[(pos + 2) % bufferLength]);
    }

    /// <summary>Phase 3: 7*m[0] + 11*m[1] - 18*m[2]</summary>
    private int SlicePhase3(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        return (7 * m[pos % bufferLength]) +
               (11 * m[(pos + 1) % bufferLength]) -
               (18 * m[(pos + 2) % bufferLength]);
    }

    /// <summary>Phase 4: 4*m[0] + 15*m[1] - 20*m[2] + 1*m[3]</summary>
    private int SlicePhase4(ReadOnlySpan<ushort> m, int pos, int bufferLength)
    {
        return (4 * m[pos % bufferLength]) +
               (15 * m[(pos + 1) % bufferLength]) -
               (20 * m[(pos + 2) % bufferLength]) +
               (1 * m[(pos + 3) % bufferLength]);
    }

    /// <summary>Returns message length in bits based on Downlink Format.</summary>
    private static int GetMessageLength(DownlinkFormat df)
    {
        return df switch
        {
            DownlinkFormat.ShortAirAirSurveillance or
            DownlinkFormat.SurveillanceAltitudeReply or
            DownlinkFormat.SurveillanceIdentityReply or
            DownlinkFormat.AllCallReply or
            DownlinkFormat.LongAirAirSurveillance or
            DownlinkFormat.CommDExtendedLength
                => ModeSFrameLength.Short,

            DownlinkFormat.ExtendedSquitter or
            DownlinkFormat.ExtendedSquitterNonTransponder or
            DownlinkFormat.MilitaryExtendedSquitter or
            DownlinkFormat.CommBAltitudeReply or
            DownlinkFormat.CommBIdentityReply
                => ModeSFrameLength.Long,

            _ => 0
        };
    }

    // Statistics properties
    public long PreambleCandidates => _preambleCandidates;
    public long FramesExtracted => _framesExtracted;
    public long FramesRejectedDuringExtraction => _framesRejectedDuringExtraction;
}
