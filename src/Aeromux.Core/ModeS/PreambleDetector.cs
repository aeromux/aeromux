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
/// Detects Mode S preambles in magnitude data and extracts raw frames using phase-correlation techniques.
/// Implements 2.4 MSPS sampling (2.4 million samples per second) with multi-phase detection for sub-sample timing resolution.
/// Conforms to ICAO Annex 10, Volume IV, Chapter 3 (Surveillance Radar and Collision Avoidance Systems).
/// </summary>
/// <remarks>
/// <para><b>2.4 MSPS Sampling Challenge:</b></para>
/// <para>
/// Mode S preambles consist of 4 pulses at precise timing: 0.0µs, 1.0µs, 3.5µs, 4.5µs.
/// At 2.4 MSPS (0.417µs per sample), these pulses align at fractional sample positions:
/// 0, 2.4, 8.4, 10.8. This creates phase ambiguity where the signal can be sampled at
/// 5 different phase offsets (0-4) relative to bit boundaries.
/// </para>
///
/// <para><b>Detection Strategy:</b></para>
/// <list type="bullet">
/// <item><b>Pre-check filter:</b> Fast rejection test using magnitude relationships (reduces false positives by ~90%)</item>
/// <item><b>Multiphase correlation:</b> Tests 5 possible phase alignments (phases 4-8) to find optimal sampling offset</item>
/// <item><b>Weighted correlation functions:</b> Extracts bit values from 3-4 adjacent samples using hand-tuned coefficients</item>
/// <item><b>Noise-adaptive thresholding:</b> Dynamic threshold based on local noise estimation from valley samples</item>
/// <item><b>Linear buffer scanning:</b> Direct array access without modulo operations for maximum performance</item>
/// </list>
/// </remarks>
public sealed class PreambleDetector
{
    private readonly double _preambleThreshold;
    private readonly List<RawFrame> _frameBuffer = [];
    private readonly ValidatedFrameFactory _validatedFrameFactory = new(); // For validating extracted messages
    private readonly IcaoConfidenceTracker? _confidenceTracker; // Optional: for filtering AP mode from unknown aircraft

    /// <summary>
    /// RTL-SDR sample rate in samples per second (2.4 MSPS, industry standard for Mode S/ADS-B).
    /// This value matches the hardcoded sample rate in DeviceWorker and the phase correlation
    /// coefficients tuned throughout this class.
    /// </summary>
    private const int SampleRate = 2_400_000;

    /// <summary>
    /// Pre-computed ticks per sample for sample-offset timestamp calculation.
    /// At 2.4 MSPS: 10,000,000 ticks/second / 2,400,000 samples/second = ~4.1667 ticks/sample.
    /// Stored as double to avoid integer truncation drift across buffer positions.
    /// Maximum rounding error per frame: 0.5 tick = 50 nanoseconds (from double to long conversion).
    /// </summary>
    private const double TicksPerSample = (double)TimeSpan.TicksPerSecond / SampleRate;

    // Statistics
    private long _preambleCandidates;
    private long _framesExtracted;
    private long _framesRejectedDuringExtraction;  // Would have extracted but rejected due to unknown ICAO

    /// <summary>
    /// Creates a new PreambleDetector with specified configuration.
    /// </summary>
    /// <param name="preambleThreshold">Signal-to-noise threshold for preamble detection (1.5-10.0, default 1.8125)</param>
    /// <param name="confidenceTracker">Optional ICAO confidence tracker for filtering unknown aircraft in AP mode</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when preambleThreshold is outside valid range (1.5-10.0)</exception>
    public PreambleDetector(
        double preambleThreshold = 1.8125,
        IcaoConfidenceTracker? confidenceTracker = null)
    {
        if (preambleThreshold is < 1.5 or > 10.0)
        {
            throw new ArgumentOutOfRangeException(nameof(preambleThreshold),
                $"Preamble threshold must be between 1.5 and 10.0 (got {preambleThreshold})");
        }

        _preambleThreshold = preambleThreshold;
        _confidenceTracker = confidenceTracker;
    }

    /// <summary>
    /// Scans magnitude buffer for Mode S preambles and extracts valid frames.
    /// Uses linear buffer scanning with overlapping prefix for seamless boundary detection.
    /// </summary>
    /// <param name="magnitudeBuffer">Magnitude buffer containing [326-sample prefix | new data]</param>
    /// <param name="bufferTimestamp">
    /// Wall-clock timestamp anchoring the start of the current IQ buffer.
    /// Captured once per buffer in DeviceWorker via StopwatchTimeProvider.
    /// Per-frame timestamps are computed as: bufferTimestamp + (samplePosition - prefixLength) x ticksPerSample.
    /// </param>
    /// <returns>List of successfully extracted and validated raw frames.</returns>
    /// <remarks>
    /// <para>
    /// Scanning begins at index 0 (including the prefix region) and continues through
    /// the new data region. This allows preamble detection to span buffer boundaries
    /// without special wraparound handling. The prefix contains a copy of the previous
    /// buffer's trailing 326 samples, providing detection continuity.
    /// </para>
    /// </remarks>
    public List<RawFrame> DetectAndExtract(
        SignalProcessing.IQDemodulator.MagnitudeBuffer magnitudeBuffer,
        DateTime bufferTimestamp)
    {
        ArgumentNullException.ThrowIfNull(magnitudeBuffer);

        _frameBuffer.Clear();

        // Extract magnitude data and length metadata
        ReadOnlySpan<ushort> m = magnitudeBuffer.Data.AsSpan();
        int mlen = magnitudeBuffer.Length;

        // Linear buffer scanning: start at beginning (index 0, includes prefix), scan through new data
        // Buffer structure: [prefix 0-325][new data 326..326+mlen-1]
        // However, to prevent duplicates, we must NOT scan the last 326 samples of new data,
        // as those will be copied to the next buffer's prefix and scanned there.
        // So we scan: [0, mlen) which covers prefix + first part of new data
        // The last 326 samples (suffix) remain unscanned and become the next buffer's prefix
        int pa = 0;      // Current scan position
        int stop = mlen; // Scan boundary: stops BEFORE the suffix region

        while (pa < stop)
        {
            int idx = pa;

            // Pre-check filter: test 10 consecutive positions for preamble-like magnitude pattern
            // This fast rejection filter reduces false positives by ~90% before expensive phase correlation
            // Pattern test: pulse peaks at relative positions 1 and 12 should exceed valleys at 7, 14, 15
            bool foundPattern = false;
            for (int offset = 0; offset < 10 && !foundPattern; offset++)
            {
                int testPos = idx + offset;

                // Pre-check filter: Fast magnitude relationship test rejects 90% of non-preambles
                // before expensive phase correlation. Indices 1,7,12,14,15 correspond to Mode S
                // preamble pulse/valley timing where peaks must exceed valleys for valid signal.
                if (m[testPos + 1] > m[testPos + 7] &&
                    m[testPos + 12] > m[testPos + 14] &&
                    m[testPos + 12] > m[testPos + 15])
                {
                    // Pre-check passed - attempt full phase correlation and frame extraction
                    RawFrame? frame = DetectAndExtractWithPhases(m, testPos, bufferTimestamp, out bool hadPreamble);

                    // Preamble detection: count if ANY phase exceeded noise threshold
                    // (hadPreamble == true means at least one phase alignment was successful)
                    if (hadPreamble)
                    {
                        _preambleCandidates++;

                        if (frame != null)
                        {
                            // Valid frame extracted - add to output and skip past frame data
                            _frameBuffer.Add(frame);
                            _framesExtracted++;

                            // Advance past frame to prevent re-detection
                            // At 2.4 MSPS: preamble (8µs) + data (N µs) = (8 + N) × 2.4 samples
                            // Preamble: 8 µs × 2.4 = 19.2 samples
                            // Data: N bits × 1 µs × 2.4 = N × 2.4 samples
                            // Using integer math: 19 + (N × 12 / 5) ≈ actual frame length
                            // Add safety margin to account for:
                            // 1. Integer rounding (can be short by ~1 sample)
                            // 2. Trailing noise/interference after frame
                            // 3. Adjacent frame preambles that might overlap
                            int preambleSamples = 19;
                            int dataSamples = frame.LengthBits * 12 / 5;  // More accurate: bits × 2.4
                            const int safetyMargin = 10;  // Additional samples to ensure clean separation
                            int skipAmount = offset + preambleSamples + dataSamples + safetyMargin;
                            pa += skipAmount;
                        }
                        else
                        {
                            // Preamble detected but frame rejected (CRC failure, unknown ICAO, etc.)
                            // Advance minimally to search for next preamble
                            pa += offset + 1;
                        }
                        foundPattern = true;
                    }
                    else
                    {
                        // Pre-check passed but no phase exceeded threshold (weak signal or noise)
                        pa += offset + 1;
                        foundPattern = true;
                    }
                }
            }

            if (!foundPattern)
            {
                // No pre-check match in 10-sample window - skip entire window
                pa += 10;
            }
        }

        return _frameBuffer;
    }

    /// <summary>
    /// Performs multiphase preamble detection with noise-adaptive thresholding.
    /// Tests 5 possible phase alignments (4-8) and extracts the best-scoring frame.
    /// </summary>
    /// <param name="m">Magnitude sample buffer</param>
    /// <param name="pos">Suspected preamble start position in magnitude buffer</param>
    /// <param name="bufferTimestamp">Wall-clock anchor for the current IQ buffer</param>
    /// <param name="hadPreamble">Output: true if at least one phase exceeded threshold (indicates valid preamble signal)</param>
    /// <returns>Best extracted frame, or null if all phases failed or were rejected</returns>
    private RawFrame? DetectAndExtractWithPhases(
        ReadOnlySpan<ushort> m, int pos, DateTime bufferTimestamp, out bool hadPreamble)
    {
        // Local noise estimation from 5 valley samples (preamble low-points)
        // Samples at offsets 5, 8, 16, 17, 18 represent expected low-magnitude regions in preamble
        int baseNoise = m[pos + 5] +
                        m[pos + 8] +
                        m[pos + 16] +
                        m[pos + 17] +
                        m[pos + 18];

        // Calculate detection threshold as multiple of base noise
        // Threshold ratio (default 1.8125) provides balance between sensitivity and false-positive rate
        // Higher values = less sensitive (fewer false positives, may miss weak signals)
        // Lower values = more sensitive (more detections, higher false-positive rate)
        int refLevel = (int)(baseNoise * _preambleThreshold);

        // Phase-specific magnitude calculations for candidate phase selection
        // These calculations determine which phases (4-8) are worth testing based on preamble pulse magnitudes
        // Optimized formulae combine multiple sample comparisons to quickly identify promising phase alignments
        int diff2And3 = m[pos + 2] - m[pos + 3];
        int sum1And4 = m[pos + 1] + m[pos + 4];
        int diff10And11 = m[pos + 10] - m[pos + 11];
        int common3456 = sum1And4 - diff2And3 + m[pos + 9] + m[pos + 12];

        // Score meanings: no phase tested yet: -42 (initial state), -2 (bad CRC), -1 (valid CRC but unknown ICAO), >0 (valid message with known ICAO)
        const int noPhaseTestedYet = -42;
        int bestScore = noPhaseTestedYet;
        byte[]? bestMessage = null;
        double bestSignalStrength = 0.0;  // Track signal strength for best phase

        // Test phase groups based on magnitude thresholds
        // Each group tests 1-2 phases that share similar preamble characteristics

        // Group 1: Phases 4 and 5
        int paMag1 = common3456 - diff10And11;
        if (paMag1 >= refLevel)
        {
            TryPhase(m, pos, 4, ref bestScore, ref bestMessage, ref bestSignalStrength);
            TryPhase(m, pos, 5, ref bestScore, ref bestMessage, ref bestSignalStrength);
        }

        // Group 2: Phases 6 and 7
        int paMag2 = common3456 + diff10And11;
        if (paMag2 >= refLevel)
        {
            TryPhase(m, pos, 6, ref bestScore, ref bestMessage, ref bestSignalStrength);
            TryPhase(m, pos, 7, ref bestScore, ref bestMessage, ref bestSignalStrength);
        }

        // Group 3: Phase 8
        int paMag3 = sum1And4 + (2 * diff2And3) + diff10And11 + m[pos + 12];
        if (paMag3 >= refLevel)
        {
            TryPhase(m, pos, 8, ref bestScore, ref bestMessage, ref bestSignalStrength);
        }

        // Preamble detection outcome: successful if ANY phase was tested (bestScore changed from initial state)
        // This indicates the signal exceeded noise threshold, regardless of CRC/ICAO validation results
        hadPreamble = bestScore != noPhaseTestedYet;

        // Return best message if we found a valid one
        if (bestMessage != null && bestScore > 0)
        {
            // Compute frame timestamp from sample position in magnitude buffer.
            // sampleOffset = pos - PrefixSamples: offset from start of current IQ buffer.
            // Positive for new data region, negative for prefix (previous buffer's trailing samples).
            // Both cases produce correct timestamps relative to bufferTimestamp.
            int sampleOffset = pos - SignalProcessing.IQDemodulator.PrefixSamples;
            DateTime frameTimestamp = bufferTimestamp.AddTicks((long)(sampleOffset * TicksPerSample));
            return new RawFrame(bestMessage, frameTimestamp, bestSignalStrength);
        }

        // Count rejection if bestScore is -1 (valid CRC but unknown ICAO)
        // This tracks frames with valid CRC from unconfident aircraft (noise filtering)
        if (bestScore == -1)
        {
            _framesRejectedDuringExtraction++;
        }

        return null;
    }

    /// <summary>
    /// Tries to extract a message at a specific phase alignment.
    /// Uses ValidatedFrameFactory to verify the extracted message is valid.
    /// Updates bestScore/bestMessage/bestSignalStrength if this phase produces a better result.
    /// Uses direct array access - NO MODULO.
    /// Score meanings:
    ///   > 0: Valid message with known/accepted ICAO
    ///   -1: Valid CRC but unknown/rejected ICAO
    ///   -2: Bad message or invalid CRC
    /// </summary>
    private void TryPhase(ReadOnlySpan<ushort> m, int pos, int tryPhase,
                          ref int bestScore, ref byte[]? bestMessage, ref double bestSignalStrength)
    {
        // Calculate data start position and initial phase
        // Direct calculation - NO MODULO (performance optimization)
        int pPtr = pos + 19 + (tryPhase / 5);
        int phase = tryPhase % 5;

        // Extract first byte to determine message length
        byte firstByte = SliceByte(m, ref pPtr, ref phase);

        // Validate DF early (reject unsupported formats immediately)
        var df = (DownlinkFormat)(firstByte >> 3);
        int messageLengthBits = GetMessageLength(df);
        if (messageLengthBits == 0)
        {
            // Invalid DF - score -2 and update best if better than previous attempts
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
            message[i] = SliceByte(m, ref pPtr, ref phase);
        }

        // Validate message with CRC (signal strength deferred until best phase selected)
        var rawFrame = new RawFrame(message, default, 0.0);
        ValidatedFrame? validated = _validatedFrameFactory.ValidateFrame(rawFrame, 0.0);

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
            // Only save message and compute signal strength if score is positive (accepted)
            if (score > 0)
            {
                bestMessage = message;
                bestSignalStrength = CalculateSignalStrength(m, pos, messageLengthBits);
            }
            else
            {
                bestMessage = null;
            }
        }
    }

    /// <summary>
    /// Extracts one byte using phase-specific correlation functions.
    /// Uses direct array access - NO MODULO (performance optimization).
    /// </summary>
    private byte SliceByte(ReadOnlySpan<ushort> m, ref int pPtr, ref int phase)
    {
        byte theByte = 0;

        switch (phase)
        {
            case 0:
                theByte = (byte)(
                    (SlicePhase0(m, pPtr) > 0 ? 0x80 : 0) |
                    (SlicePhase2(m, pPtr + 2) > 0 ? 0x40 : 0) |
                    (SlicePhase4(m, pPtr + 4) > 0 ? 0x20 : 0) |
                    (SlicePhase1(m, pPtr + 7) > 0 ? 0x10 : 0) |
                    (SlicePhase3(m, pPtr + 9) > 0 ? 0x08 : 0) |
                    (SlicePhase0(m, pPtr + 12) > 0 ? 0x04 : 0) |
                    (SlicePhase2(m, pPtr + 14) > 0 ? 0x02 : 0) |
                    (SlicePhase4(m, pPtr + 16) > 0 ? 0x01 : 0));
                phase = 1;
                pPtr += 19;
                break;

            case 1:
                theByte = (byte)(
                    (SlicePhase1(m, pPtr) > 0 ? 0x80 : 0) |
                    (SlicePhase3(m, pPtr + 2) > 0 ? 0x40 : 0) |
                    (SlicePhase0(m, pPtr + 5) > 0 ? 0x20 : 0) |
                    (SlicePhase2(m, pPtr + 7) > 0 ? 0x10 : 0) |
                    (SlicePhase4(m, pPtr + 9) > 0 ? 0x08 : 0) |
                    (SlicePhase1(m, pPtr + 12) > 0 ? 0x04 : 0) |
                    (SlicePhase3(m, pPtr + 14) > 0 ? 0x02 : 0) |
                    (SlicePhase0(m, pPtr + 17) > 0 ? 0x01 : 0));
                phase = 2;
                pPtr += 19;
                break;

            case 2:
                theByte = (byte)(
                    (SlicePhase2(m, pPtr) > 0 ? 0x80 : 0) |
                    (SlicePhase4(m, pPtr + 2) > 0 ? 0x40 : 0) |
                    (SlicePhase1(m, pPtr + 5) > 0 ? 0x20 : 0) |
                    (SlicePhase3(m, pPtr + 7) > 0 ? 0x10 : 0) |
                    (SlicePhase0(m, pPtr + 10) > 0 ? 0x08 : 0) |
                    (SlicePhase2(m, pPtr + 12) > 0 ? 0x04 : 0) |
                    (SlicePhase4(m, pPtr + 14) > 0 ? 0x02 : 0) |
                    (SlicePhase1(m, pPtr + 17) > 0 ? 0x01 : 0));
                phase = 3;
                pPtr += 19;
                break;

            case 3:
                theByte = (byte)(
                    (SlicePhase3(m, pPtr) > 0 ? 0x80 : 0) |
                    (SlicePhase0(m, pPtr + 3) > 0 ? 0x40 : 0) |
                    (SlicePhase2(m, pPtr + 5) > 0 ? 0x20 : 0) |
                    (SlicePhase4(m, pPtr + 7) > 0 ? 0x10 : 0) |
                    (SlicePhase1(m, pPtr + 10) > 0 ? 0x08 : 0) |
                    (SlicePhase3(m, pPtr + 12) > 0 ? 0x04 : 0) |
                    (SlicePhase0(m, pPtr + 15) > 0 ? 0x02 : 0) |
                    (SlicePhase2(m, pPtr + 17) > 0 ? 0x01 : 0));
                phase = 4;
                pPtr += 19;
                break;

            case 4:
                theByte = (byte)(
                    (SlicePhase4(m, pPtr) > 0 ? 0x80 : 0) |
                    (SlicePhase1(m, pPtr + 3) > 0 ? 0x40 : 0) |
                    (SlicePhase3(m, pPtr + 5) > 0 ? 0x20 : 0) |
                    (SlicePhase0(m, pPtr + 8) > 0 ? 0x10 : 0) |
                    (SlicePhase2(m, pPtr + 10) > 0 ? 0x08 : 0) |
                    (SlicePhase4(m, pPtr + 12) > 0 ? 0x04 : 0) |
                    (SlicePhase1(m, pPtr + 15) > 0 ? 0x02 : 0) |
                    (SlicePhase3(m, pPtr + 17) > 0 ? 0x01 : 0));
                phase = 0;
                pPtr += 20;
                break;
        }

        return theByte;
    }

    // ============================================================================
    // Phase-Correlation Functions
    //
    // These weighted correlation functions extract bit values from fractional sample positions.
    // At 2.4 MSPS with 1 MHz bit rate, each bit spans 2.4 samples, creating phase ambiguity.
    // Each function uses hand-tuned coefficients to optimally extract signal from 3-4 adjacent samples.
    //
    // Coefficient Selection Criteria:
    // - DC balance: Sum of coefficients should be close to zero for noise rejection
    // - Temporal alignment: Weight distribution matches expected pulse position within bit period
    // - Empirical validation: Tested against real Mode S signals to minimize bit error rate
    // - Phase 2 exception: Slightly DC-unbalanced (sum = +1) but yields better practical results
    //
    // Coefficients were empirically optimized for Mode S signal characteristics and should NOT be modified.
    // Changing these values will degrade the bit extraction accuracy and increase error rates.
    // ============================================================================

    /// <summary>
    /// Phase 0 correlation function: 18*m[0] - 15*m[1] - 3*m[2]
    /// </summary>
    private int SlicePhase0(ReadOnlySpan<ushort> m, int pos)
        => (18 * m[pos]) - (15 * m[pos + 1]) - (3 * m[pos + 2]);

    /// <summary>
    /// Phase 1 correlation function: 14*m[0] - 5*m[1] - 9*m[2]
    /// </summary>
    private int SlicePhase1(ReadOnlySpan<ushort> m, int pos)
        => (14 * m[pos]) - (5 * m[pos + 1]) - (9 * m[pos + 2]);

    /// <summary>
    /// Phase 2 correlation function: 16*m[0] + 5*m[1] - 20*m[2]
    /// Note: Slightly DC-unbalanced but provides better practical results than balanced alternatives.
    /// </summary>
    private int SlicePhase2(ReadOnlySpan<ushort> m, int pos)
        => (16 * m[pos]) + (5 * m[pos + 1]) - (20 * m[pos + 2]);

    /// <summary>
    /// Phase 3 correlation function: 7*m[0] + 11*m[1] - 18*m[2]
    /// </summary>
    private int SlicePhase3(ReadOnlySpan<ushort> m, int pos)
        => (7 * m[pos]) + (11 * m[pos + 1]) - (18 * m[pos + 2]);

    /// <summary>
    /// Phase 4 correlation function: 4*m[0] + 15*m[1] - 20*m[2] + 1*m[3]
    /// Uses 4 samples instead of 3 for improved accuracy at this phase offset.
    /// </summary>
    private int SlicePhase4(ReadOnlySpan<ushort> m, int pos)
        => (4 * m[pos]) + (15 * m[pos + 1]) - (20 * m[pos + 2]) + (1 * m[pos + 3]);

    /// <summary>
    /// Calculates signal strength (RSSI) as average power over message duration.
    /// Returns normalized power scaled to 0-255 range for consistent signal quality assessment.
    /// </summary>
    /// <param name="m">Magnitude sample buffer</param>
    /// <param name="pos">Preamble start position</param>
    /// <param name="messageLengthBits">Message length in bits (56 or 112)</param>
    /// <returns>Signal strength as POWER value (0.0-255.0, higher = stronger signal)</returns>
    /// <remarks>
    /// Uses average power calculation to provide stable signal strength measurement that's
    /// less sensitive to noise spikes than peak magnitude. Power is stored (not amplitude)
    /// because BeastEncoder applies sqrt transform for transmission - this maintains the
    /// full dynamic range when encoding to 8-bit Beast format.
    ///
    /// Returns double precision to accurately represent very weak signals that would otherwise
    /// be lost when quantizing to byte. Only quantized to byte when encoding to Beast format.
    /// </remarks>
    private static double CalculateSignalStrength(ReadOnlySpan<ushort> m, int pos, int messageLengthBits)
    {
        // Calculate how many samples span the message at 2.4 MSPS sample rate
        int signalLengthSamples = (messageLengthBits * 12) / 5;

        // Skip preamble and measure only the data portion for cleaner signal assessment
        int signalStart = pos + 19;

        // Use squared magnitudes to calculate power (not amplitude) for better SNR representation
        ulong sumSquared = 0;
        for (int i = 0; i < signalLengthSamples; i++)
        {
            ushort mag = m[signalStart + i];
            sumSquared += (ulong)mag * (ulong)mag;
        }

        // Normalize by theoretical maximum to get power ratio (0.0 to 1.0)
        const double maxMagnitude = 65535.0;
        double normalizedPower = sumSquared / (maxMagnitude * maxMagnitude * signalLengthSamples);

        // Scale to 0-255 range while preserving full precision as double
        double signalStrength = normalizedPower * 255.0;

        return signalStrength;  // Returns double, no rounding or clamping
    }

    /// <summary>Returns message length in bits based on Downlink Format.</summary>
    private static int GetMessageLength(DownlinkFormat df)
    {
        return df switch
        {
            DownlinkFormat.ShortAirAirSurveillance or
            DownlinkFormat.SurveillanceAltitudeReply or
            DownlinkFormat.SurveillanceIdentityReply or
            DownlinkFormat.AllCallReply
                => ModeSFrameLength.Short,

            DownlinkFormat.LongAirAirSurveillance or
            DownlinkFormat.CommDExtendedLength or
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
