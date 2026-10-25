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

using Aeromux.Core.Configuration;
using Aeromux.Core.SignalProcessing;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for <see cref="PreambleDetector.FramesCorrected"/>: the detector checks the CRC
/// itself, correcting a single-bit error in place, so it is the only place that can count
/// corrections. Frames are rendered as an ideal pulse position modulated signal straight
/// into a magnitude buffer, as the demodulator would deliver it.
/// </summary>
public class PreambleDetectorCorrectedTests
{
    // DF 17 airborne position, ICAO 80073B (TC 11), with a valid CRC
    private const string Df17Position = "8D80073B58CD7331C27497A8A51D";

    [Fact]
    public void CleanFrame_IsExtractedWithoutCorrection()
    {
        byte[] frame = Convert.FromHexString(Df17Position);

        (PreambleDetector detector, List<RawFrame> frames) = Detect(frame);

        frames.Should().ContainSingle().Which.Data.Should().Equal(frame);
        detector.FramesExtracted.Should().Be(1);
        detector.FramesCorrected.Should().Be(0);
    }

    [Theory]
    [InlineData(40)]   // first bit of the ME field
    [InlineData(60)]
    [InlineData(87)]   // last bit of the ME field
    public void OneBitWrong_IsExtractedCorrectedOnce(int bit)
    {
        byte[] frame = Convert.FromHexString(Df17Position);

        (PreambleDetector detector, List<RawFrame> frames) = Detect(FlipBits(frame, bit));

        frames.Should().ContainSingle().Which.Data.Should().Equal(frame, "the detector repairs the frame it passes on");
        detector.FramesExtracted.Should().Be(1);
        detector.FramesCorrected.Should().Be(1, "only the phase that won is counted, however many were tried");
    }

    [Fact]
    public void ManyBitsWrong_IsNotExtracted()
    {
        byte[] frame = Convert.FromHexString(Df17Position);

        (PreambleDetector detector, List<RawFrame> frames) = Detect(FlipBits(frame, 40, 45, 52, 61, 70, 79));

        frames.Should().BeEmpty();
        detector.FramesExtracted.Should().Be(0);
        detector.FramesCorrected.Should().Be(0);
    }

    [Fact]
    public void SeveralFrames_EachCorrectionCountedOnce()
    {
        byte[] clean = Convert.FromHexString(Df17Position);
        byte[][] frames = [clean, FlipBits(clean, 50), clean, FlipBits(clean, 75)];

        (PreambleDetector detector, List<RawFrame> extracted) = Detect(frames);

        extracted.Should().HaveCount(4);
        detector.FramesCorrected.Should().Be(2);
    }

    [Fact]
    public void FrameAtTheEndOfTheScan_IsCountedByTheScalarPath()
    {
        // The vectorized scan checks blocks of eight positions starting at multiples of 8,
        // while a whole block fits before the end of the scan. The frame is placed so that
        // the detector finds its preamble at position 200 (one sample before the first
        // pulse) and the scan ends at 204: the last whole block is 192 to 199, so position
        // 200 is reached only by the scalar loop that finishes the buffer.
        byte[] frame = FlipBits(Convert.FromHexString(Df17Position), 60);

        (PreambleDetector detector, List<RawFrame> extracted) =
            Detect([frame], firstFrameStart: 201, scanEndsAfterLastFrameStart: 3);

        extracted.Should().ContainSingle();
        detector.FramesCorrected.Should().Be(1);
    }

    // === Helpers ===

    private static byte[] FlipBits(byte[] frame, params int[] bits)
    {
        byte[] copy = [.. frame];
        foreach (int bit in bits)
        {
            copy[bit / 8] ^= (byte)(1 << (7 - (bit % 8)));
        }
        return copy;
    }

    private static (PreambleDetector, List<RawFrame>) Detect(byte[] frame) => Detect([frame]);

    /// <summary>
    /// Renders the frames one after another into a magnitude buffer and runs the detector
    /// over it once.
    /// </summary>
    /// <param name="frames">The frames to render.</param>
    /// <param name="firstFrameStart">The sample where the first frame's first pulse starts.</param>
    /// <param name="scanEndsAfterLastFrameStart">
    /// When set, the scan ends this many samples after the last frame starts, so that its
    /// preamble lies in the last positions scanned.
    /// </param>
    private static (PreambleDetector, List<RawFrame>) Detect(
        byte[][] frames, int firstFrameStart = 200, int? scanEndsAfterLastFrameStart = null)
    {
        var buffer = new IQDemodulator.MagnitudeBuffer();
        Array.Fill(buffer.Data, Floor);

        int position = firstFrameStart;
        int lastFrameStart = position;
        foreach (byte[] frame in frames)
        {
            lastFrameStart = position;
            int samples = Render(buffer.Data, position, frame);
            position += samples + 200;
        }

        buffer.Length = scanEndsAfterLastFrameStart is { } end ? lastFrameStart + end : position;

        // An empty confidence tracker, as on a device: without one, any address/parity reply
        // (DF 4, 5, 20, 21) passes, because its CRC is its address, and the bits inside a
        // rendered frame can look like such a reply
        using var tracker = new IcaoConfidenceTracker(ConfidenceLevel.Medium, 30);
        var detector = new PreambleDetector(confidenceTracker: tracker);
        List<RawFrame> extracted = [.. detector.DetectAndExtract(buffer, DateTime.UtcNow, 0)];
        return (detector, extracted);
    }

    private const ushort Floor = 100;
    private const ushort Pulse = 5000;

    // One sample at 2.4 MSPS spans five ticks of 12 MHz, and one microsecond twelve ticks
    private const int TicksPerSample = 5;
    private const int TicksPerMicrosecond = 12;

    /// <summary>
    /// Renders a Mode S transmission at 12 MHz and averages it down to 2.4 MSPS magnitudes:
    /// the preamble's four half-microsecond pulses at 0, 1, 3.5 and 4.5 µs, then from 8 µs
    /// one bit per microsecond, a pulse in the first half for 1 and in the second for 0.
    /// </summary>
    /// <returns>The number of samples written.</returns>
    private static int Render(ushort[] magnitudes, int start, byte[] frame)
    {
        int bits = frame.Length * 8;
        int totalTicks = (8 + bits) * TicksPerMicrosecond;
        bool[] high = new bool[totalTicks];

        void PulseAt(int tick)
        {
            for (int t = tick; t < tick + TicksPerMicrosecond / 2; t++)
            {
                high[t] = true;
            }
        }

        PulseAt(0);
        PulseAt(12);
        PulseAt(42);
        PulseAt(54);

        for (int i = 0; i < bits; i++)
        {
            bool one = (frame[i / 8] & (1 << (7 - (i % 8)))) != 0;
            PulseAt((8 * TicksPerMicrosecond) + (i * TicksPerMicrosecond) + (one ? 0 : TicksPerMicrosecond / 2));
        }

        int samples = totalTicks / TicksPerSample;
        for (int k = 0; k < samples; k++)
        {
            int highTicks = 0;
            for (int t = k * TicksPerSample; t < (k + 1) * TicksPerSample; t++)
            {
                highTicks += high[t] ? 1 : 0;
            }

            magnitudes[start + k] = (ushort)(Floor + ((Pulse - Floor) * highTicks / TicksPerSample));
        }

        return samples;
    }
}
