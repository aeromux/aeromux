using RtlSdrManager;

namespace Aeromux.Core.SignalProcessing;

/// <summary>
/// Converts IQ samples from RTL-SDR into magnitude values for Mode S signal processing.
/// Implements magnitude calculation with pre-computed lookup table for performance.
/// </summary>
/// <remarks>
/// Mode S uses Pulse Position Modulation (PPM) at 1090 MHz with 1 Mbit/s data rate.
/// At 2 MSPS sample rate, we get 2 samples per bit.
///
/// Phase 2 Process (following readsb architecture):
/// - Convert IQ samples to squared magnitude: I² + Q²
/// - Store in circular buffer for Phase 3 (preamble detection)
/// - No pulse detection here - Phase 3 will use local noise estimation
///
/// Reference: readsb/demod_2400.c and convert.c
/// </remarks>
public sealed class IQDemodulator : IDisposable
{
    // Magnitude lookup table (pre-computed for performance, following readsb approach)
    // 256×256 table = 128 KB memory, eliminates per-sample multiplication
    private static readonly ushort[,] MagnitudeLookup = InitializeMagnitudeLookup();

    // Magnitude buffer (circular buffer for efficient processing)
    // At 2.4 MSPS: ~1 second = 2,400,000 samples (rounded to power of 2: 2,097,152 = 2^21)
    private readonly ushort[] _magnitudeBuffer = new ushort[2_097_152];

    // Statistics (exposed as properties for DeviceWorker to log)

    /// <summary>
    /// Initializes the magnitude lookup table (called once at static initialization).
    /// Pre-computes squared magnitude for all possible I/Q byte pairs (0-255).
    /// This eliminates per-sample multiplication, following readsb's approach.
    /// </summary>
    /// <remarks>
    /// Memory usage: 256×256×2 bytes = 128 KB
    /// One-time cost: 65,536 calculations at startup
    /// Per-sample benefit: Eliminates 2 multiplications per sample (4M multiplications/sec at 2 MSPS)
    /// </remarks>
    private static ushort[,] InitializeMagnitudeLookup()
    {
        ushort[,] lookup = new ushort[256, 256];

        for (int i = 0; i < 256; i++)
        {
            for (int q = 0; q < 256; q++)
            {
                // Center IQ values (0-255 → -128 to +127)
                int iCentered = i - 128;
                int qCentered = q - 128;

                // Calculate squared magnitude: I² + Q²
                int magnitudeSquared = (iCentered * iCentered) + (qCentered * qCentered);

                // Store as ushort (max value is 128² + 128² = 32,768, fits in ushort)
                lookup[i, q] = (ushort)magnitudeSquared;
            }
        }

        return lookup;
    }

    /// <summary>
    /// Processes a batch of IQ samples and converts them to magnitude values.
    /// </summary>
    /// <param name="samples">IQ samples from RTL-SDR device.</param>
    /// <remarks>
    /// Converts each IQ sample to squared magnitude (I² + Q²) using pre-computed lookup table.
    /// Stores magnitudes in circular buffer for Phase 3 (preamble detection).
    /// No pulse detection or noise floor tracking - Phase 3 will use local noise estimation per preamble (readsb approach).
    /// </remarks>
    public void ProcessSamples(IReadOnlyList<IQData> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        // Convert IQ samples to magnitude using pre-computed lookup table
        foreach (IQData sample in samples)
        {
            // Look up pre-computed squared magnitude: I² + Q²
            // This eliminates per-sample multiplication (readsb approach)
            ushort magnitude = MagnitudeLookup[sample.I, sample.Q];

            // Store in circular buffer for Phase 3 preamble detection
            _magnitudeBuffer[BufferPosition] = magnitude;
            BufferPosition = (BufferPosition + 1) % _magnitudeBuffer.Length;

            TotalSamplesProcessed++;
        }
    }

    /// <summary>
    /// Gets the current magnitude buffer for preamble detection (Phase 3).
    /// </summary>
    /// <returns>Read-only view of the magnitude buffer.</returns>
    public ReadOnlySpan<ushort> GetMagnitudeBuffer() => _magnitudeBuffer.AsSpan();

    /// <summary>
    /// Gets the current buffer position (for Phase 3 preamble detector).
    /// Indicates where the next sample will be written in the circular buffer.
    /// </summary>
    public int BufferPosition { get; private set; }

    /// <summary>
    /// Gets the total number of samples processed (converted to magnitude) by this demodulator.
    /// Used by DeviceWorker for statistics logging (ADR-009: Coordinator Pattern).
    /// </summary>
    public long TotalSamplesProcessed { get; private set; }

    public void Dispose()
    {
        // No unmanaged resources to clean up
        // Magnitude buffer is managed array
    }
}
