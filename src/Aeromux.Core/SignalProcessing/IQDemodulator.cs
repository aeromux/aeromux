// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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

using RtlSdrManager;

namespace Aeromux.Core.SignalProcessing;

/// <summary>
/// Converts IQ samples from RTL-SDR into magnitude values for Mode S signal processing.
/// Implements magnitude calculation with pre-computed lookup table for performance.
/// </summary>
/// <remarks>
/// <para>
/// Mode S signals use Pulse Position Modulation (PPM) at 1090 MHz with 1 Mbit/s data rate.
/// At the industry-standard 2.4 MSPS sample rate, this yields 2.4 samples per bit, requiring
/// phase-correlation techniques for accurate bit extraction.
/// </para>
///
/// <para><b>Demodulation Strategy:</b></para>
/// <list type="bullet">
/// <item>Convert IQ samples to magnitude using Euclidean distance: √(I² + Q²)</item>
/// <item>Store magnitude samples in linear buffers with overlapping prefix regions</item>
/// <item>Prefix overlap ensures preamble detection continuity across buffer boundaries</item>
/// <item>Linear buffer architecture enables direct array access without modulo arithmetic</item>
/// <item>Preamble detection and bit extraction occur in subsequent processing stages</item>
/// </list>
/// </remarks>
public sealed class IQDemodulator : IDisposable
{
    // Magnitude lookup table: pre-computed for performance optimization
    // 256×256 table = 128 KB memory footprint, eliminates ~4.8M sqrt operations per second at 2.4 MSPS
    private static readonly ushort[,] MagnitudeLookup = InitializeMagnitudeLookup();

    // Multi-buffer architecture for continuous sample processing
    // 12 buffers provide adequate buffering depth for sustained processing at 2.4 MSPS
    // Each buffer layout: [326-sample prefix region][variable-length new data region (up to 262,144 samples)]
    // Prefix region contains copy of previous buffer's trailing samples for preamble detection continuity
    private const int NumBuffers = 12;
    private const int PrefixSamples = 326;  // Calculated: (8µs preamble + 112-bit max message + 16-bit safety margin) × 2.4 samples/µs = 326
    private const int MaxBufferSamples = 262144;  // Maximum samples per buffer (actual length varies per callback)

    /// <summary>
    /// Represents a magnitude buffer containing converted IQ samples.
    /// Each buffer maintains a prefix region copied from the previous buffer to ensure
    /// preamble detection can span buffer boundaries without loss of signal continuity.
    /// </summary>
    public class MagnitudeBuffer
    {
        /// <summary>
        /// Buffer data: [prefix region | new data region]
        /// Allocate total buffer: prefix region (326) + maximum new data region (262,144)
        /// </summary>
        ///
        public ushort[] Data = new ushort[PrefixSamples + MaxBufferSamples];

        /// <summary>Length of new data region only (excludes the 326-sample prefix)</summary>
        public int Length;

        /// <summary>Number of samples dropped due to buffer overflow (for gap detection)</summary>
        public int Dropped;
    }

    private readonly MagnitudeBuffer[] _buffers = new MagnitudeBuffer[NumBuffers];
    private int _currentBufferIndex;  // Which buffer we're filling next

    // Statistics (exposed as properties for DeviceWorker to log)

    public IQDemodulator()
    {
        // Initialize all buffers
        for (int i = 0; i < NumBuffers; i++)
        {
            _buffers[i] = new MagnitudeBuffer();
        }
    }

    /// <summary>
    /// Initializes the magnitude lookup table at static initialization.
    /// Pre-computes Euclidean magnitude √(I² + Q²) for all 65,536 possible IQ byte combinations.
    /// </summary>
    /// <remarks>
    /// <para><b>Performance Trade-off:</b></para>
    /// <list type="bullet">
    /// <item>Memory cost: 256×256×2 bytes = 128 KB (static allocation)</item>
    /// <item>Initialization cost: 65,536 magnitude calculations at startup (one-time)</item>
    /// <item>Runtime benefit: Eliminates ~4.8M sqrt operations per second at 2.4 MSPS</item>
    /// </list>
    ///
    /// <para><b>Algorithm:</b></para>
    /// <list type="number">
    /// <item>Center and normalize IQ values from [0, 255] to [-1.0, +1.0] range</item>
    /// <item>Calculate squared magnitude: I² + Q²</item>
    /// <item>Clamp to 1.0 to prevent numeric overflow in edge cases</item>
    /// <item>Take square root to obtain true Euclidean magnitude</item>
    /// <item>Scale result to uint16 range [0, 65535] with rounding</item>
    /// </list>
    /// </remarks>
    private static ushort[,] InitializeMagnitudeLookup()
    {
        ushort[,] lookup = new ushort[256, 256];

        for (int i = 0; i < 256; i++)
        {
            for (int q = 0; q < 256; q++)
            {
                // Step 1: Center and normalize unsigned byte values to signed unit range
                // Transform: [0, 255] → [-127.5, +127.5] → [-1.0, +1.0]
                double fI = (i - 127.5) / 127.5;
                double fQ = (q - 127.5) / 127.5;

                // Step 2: Calculate squared magnitude
                double magSquared = (fI * fI) + (fQ * fQ);

                // Step 3: Clamp to prevent overflow (handles edge cases where I² + Q² > 1.0)
                if (magSquared > 1.0)
                {
                    magSquared = 1.0;
                }

                // Step 4: Calculate true Euclidean magnitude (not squared magnitude)
                double mag = Math.Sqrt(magSquared);

                // Step 5: Scale to uint16 range [0, 65535] with banker's rounding (+ 0.5 for rounding)
                lookup[i, q] = (ushort)((mag * 65535.0) + 0.5);
            }
        }

        return lookup;
    }

    /// <summary>
    /// Processes a batch of IQ samples and converts them to magnitude values.
    /// Implements rolling buffer strategy with overlapping prefix regions for continuity.
    /// </summary>
    /// <param name="samples">IQ samples from RTL-SDR device.</param>
    /// <returns>The filled magnitude buffer ready for preamble scanning, or null if buffer unavailable or sample count invalid.</returns>
    /// <remarks>
    /// <para><b>Processing Steps:</b></para>
    /// <list type="number">
    /// <item>Select next available buffer from the 12-buffer pool</item>
    /// <item>Copy trailing 326 samples from previous buffer into current buffer's prefix region</item>
    /// <item>Convert new IQ samples to magnitude values starting at index 326 (after prefix)</item>
    /// <item>Advance to next buffer in rotation for subsequent call</item>
    /// </list>
    ///
    /// <para>
    /// The prefix overlap ensures that preamble detection algorithms can examine samples
    /// spanning buffer boundaries without special handling for wraparound conditions.
    /// </para>
    /// </remarks>
    public MagnitudeBuffer? ProcessSamples(IReadOnlyList<IQData> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0 || samples.Count > MaxBufferSamples)
        {
            return null;
        }

        // Select current buffer to fill and identify previous buffer for prefix source
        MagnitudeBuffer currentBuffer = _buffers[_currentBufferIndex];
        MagnitudeBuffer previousBuffer = _buffers[(_currentBufferIndex + NumBuffers - 1) % NumBuffers];

        // Copy trailing samples from previous buffer to current buffer's prefix region
        // This creates overlapping coverage: last 326 samples of previous buffer become
        // first 326 samples of current buffer, ensuring seamless preamble detection
        // across buffer boundaries.
        //
        // Buffer geometry:
        //   Previous: [326 prefix | ... | last 326 samples of new data]
        //   Current:  [326 prefix (copied from previous trailing) | new data]
        //
        // Source location calculation:
        //   previousBuffer.Data[0..325] = prefix region (326 samples)
        //   previousBuffer.Data[326..326+Length-1] = new data region (Length samples)
        //   We want the last 326 samples of the new data region
        //   Start of last 326: (326 + Length) - 326 = Length
        //   Therefore sourceStart = previousBuffer.Length points to the beginning of
        //   the last 326 samples in the previous buffer's total data array
        if (previousBuffer.Length >= PrefixSamples)
        {
            int sourceStart = previousBuffer.Length;  // Start of trailing 326 samples in previous buffer
            Array.Copy(
                sourceArray: previousBuffer.Data,
                sourceIndex: sourceStart,
                destinationArray: currentBuffer.Data,
                destinationIndex: 0,
                length: PrefixSamples);
        }
        else
        {
            // First buffer of session or previous buffer had insufficient data - zero out prefix
            Array.Clear(currentBuffer.Data, 0, PrefixSamples);
        }

        // Convert IQ samples to magnitude, placing results after the prefix region (index 326 onwards)
        for (int i = 0; i < samples.Count; i++)
        {
            IQData sample = samples[i];
            // Lookup pre-computed Euclidean magnitude: √(I² + Q²)
            ushort magnitude = MagnitudeLookup[sample.I, sample.Q];
            currentBuffer.Data[PrefixSamples + i] = magnitude;
        }

        // Update buffer metadata
        currentBuffer.Length = samples.Count;
        currentBuffer.Dropped = 0;  // TODO: Implement dropped sample tracking for gap detection

        TotalSamplesProcessed += samples.Count;

        // Advance to next buffer in circular rotation
        _currentBufferIndex = (_currentBufferIndex + 1) % NumBuffers;

        return currentBuffer;
    }

    /// <summary>
    /// Gets the total number of samples processed (converted to magnitude) by this demodulator.
    /// Used by DeviceWorker for statistics logging.
    /// </summary>
    public long TotalSamplesProcessed { get; private set; }

    public void Dispose()
    {
        // No unmanaged resources to clean up
        // Magnitude buffer is managed array
    }
}
