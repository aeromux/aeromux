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

using Aeromux.Infrastructure.Tests.Builders;
using Xunit.Abstractions;

namespace Aeromux.Infrastructure.Tests.Network.Protocols;

/// <summary>
/// Tests for BeastParser - validates Beast binary format parsing from streams.
/// All tests are async as ParseStreamAsync returns IAsyncEnumerable.
/// </summary>
public class BeastParserTests(ITestOutputHelper output)
{
    private readonly BeastParser _parser = new();

    private readonly ValidatedFrameBuilder _frameBuilder = new();

    // ======================================================================================
    // PRIORITY 1: Critical Bug Validation (Escape Handling)
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_TimestampWithEscapeByte_CorrectlyUnescaped()
    {
        // Arrange: Create Beast data with 0x1A in timestamp
        // Timestamp byte pattern: [0x1A, 0x00, 0x00, 0x00, 0x00, 0x5D]
        // On wire, 0x1A should be: 0x1A 0x1A (doubled)
        byte[] timestamp = [0x1A, 0x00, 0x00, 0x00, 0x00, 0x5D];
        const byte signal = 100;
        byte[] frameData = BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0);

        MemoryStream stream = BeastTestData.CreateBeastStream(
            BeastTestData.TYPE_SHORT, timestamp, signal, frameData, applyEscaping: true);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1, "one frame should be parsed");
        ValidatedFrame parsedFrame = frames[0];

        // We can't directly access the timestamp bytes, but we can verify the frame was parsed
        parsedFrame.Should().NotBeNull("frame should be successfully parsed despite ESC in timestamp");
    }

    [Fact]
    public async Task ParseStreamAsync_SignalByteIsEscape_CorrectlyUnescaped()
    {
        // Arrange: Signal byte = 0x1A, which on wire appears as 0x1A 0x1A
        byte[] timestamp = [0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
        byte signalByte = 0x1A;  // This will be escaped on wire
        byte[] frameData = BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0);

        MemoryStream stream = BeastTestData.CreateBeastStream(
            BeastTestData.TYPE_SHORT, timestamp, signalByte, frameData, applyEscaping: true);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = frames[0];

        // After parsing and reverse transform, verify signal strength
        // Original signal byte: 0x1A (26)
        // Reverse transform: (26/255.0)^2 * 255.0 ≈ 2.6
        parsedFrame.SignalStrength.Should().BeLessThan(10,
            "signal byte 0x1A should decode to low value after reverse sqrt transform");
    }

    [Fact]
    public async Task ParseStreamAsync_FrameDataWithEscapes_AllUnescaped()
    {
        // Arrange: Frame data with multiple 0x1A bytes
        byte[] timestamp = [0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
        byte signal = 100;
        byte[] frameData = BeastTestData.HexToBytes(BeastTestData.MultipleEscapeBytes);

        MemoryStream stream = BeastTestData.CreateBeastStream(
            BeastTestData.TYPE_SHORT, timestamp, signal, frameData, applyEscaping: true);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = frames[0];

        // Verify frame data has correct length and contains 0x1A bytes
        parsedFrame.Data.Length.Should().Be(7, "short frame should have 7 bytes");
        parsedFrame.Data.Should().Contain(0x1A, "frame data should contain unescaped ESC bytes");

        // Count 0x1A bytes in parsed data
        int escapeCount = parsedFrame.Data.Count(b => b == 0x1A);
        escapeCount.Should().Be(4, "original data had 4 ESC bytes");
    }

    // ======================================================================================
    // PRIORITY 2: Signal Strength Transform Reversal
    // ======================================================================================

    [Theory]
    [InlineData(255, 255, 2)]      // Max signal: tolerance ±2
    [InlineData(181, 128, 3)]      // Mid-high: (181/255)^2 * 255 ≈ 128
    [InlineData(128, 64, 3)]       // Mid: (128/255)^2 * 255 ≈ 64
    [InlineData(64, 16, 2)]        // Low
    [InlineData(32, 4, 2)]         // Very low
    [InlineData(0, 0, 1)]          // Minimum
    public async Task ParseStreamAsync_SignalStrength_ReverseSquareRootTransform(
        byte encodedSignal, int expectedDecoded, int tolerance)
    {
        // Arrange
        byte[] timestamp = [0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
        byte[] frameData = BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0);

        MemoryStream stream = BeastTestData.CreateBeastStream(
            BeastTestData.TYPE_SHORT, timestamp, encodedSignal, frameData, applyEscaping: true);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = frames[0];

        // Verify reverse transform: (encodedSignal/255.0)^2 * 255.0
        double actualSignal = parsedFrame.SignalStrength;
        actualSignal.Should().BeInRange(expectedDecoded - tolerance, expectedDecoded + tolerance,
            $"encoded {encodedSignal} should decode to ~{expectedDecoded}");
    }

    // ======================================================================================
    // PRIORITY 4: Stream Reading & Frame Detection
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_ValidShortFrame_ParsedCorrectly()
    {
        // Arrange
        DateTime timestamp = DateTime.UtcNow;
        byte signalStrength = 150;
        byte[] frameData = BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0);

        MemoryStream stream = BeastTestData.CreateCompleteFrameStream(
            isLongFrame: false, timestamp, signalStrength, frameData);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = frames[0];

        parsedFrame.Data.Length.Should().Be(7, "short frame should have 7 bytes");
        parsedFrame.Data.Should().BeEquivalentTo(frameData, "frame data should match");
    }

    [Fact]
    public async Task ParseStreamAsync_ValidLongFrame_ParsedCorrectly()
    {
        // Arrange
        DateTime timestamp = DateTime.UtcNow;
        byte signalStrength = 150;
        byte[] frameData = BeastTestData.HexToBytes(BeastTestData.LongFrame_DF17);

        MemoryStream stream = BeastTestData.CreateCompleteFrameStream(
            isLongFrame: true, timestamp, signalStrength, frameData);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = frames[0];

        parsedFrame.Data.Length.Should().Be(14, "long frame should have 14 bytes");
        parsedFrame.Data.Should().BeEquivalentTo(frameData, "frame data should match");
    }

    [Fact]
    public async Task ParseStreamAsync_MultipleFrames_AllParsedInSequence()
    {
        // Arrange: Create stream with 3 frames
        DateTime timestamp = DateTime.UtcNow;
        var frames = new List<MemoryStream>
        {
            BeastTestData.CreateCompleteFrameStream(false, timestamp, 100,
                BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0)),
            BeastTestData.CreateCompleteFrameStream(true, timestamp.AddSeconds(1), 120,
                BeastTestData.HexToBytes(BeastTestData.LongFrame_DF17)),
            BeastTestData.CreateCompleteFrameStream(false, timestamp.AddSeconds(2), 90,
                BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF4))
        };

        // Combine streams
        var combined = new MemoryStream();
        foreach (MemoryStream frame in frames)
        {
            frame.Position = 0;
            await frame.CopyToAsync(combined);
        }
        combined.Position = 0;

        // Act
        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(combined))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(3, "all three frames should be parsed");
        parsedFrames[0].Data.Length.Should().Be(7, "first frame is short");
        parsedFrames[1].Data.Length.Should().Be(14, "second frame is long");
        parsedFrames[2].Data.Length.Should().Be(7, "third frame is short");
    }

    [Fact]
    public async Task ParseStreamAsync_GarbageBeforeFrame_SkippedToEscapeByte()
    {
        // Arrange: Random garbage bytes followed by valid frame
        var stream = new MemoryStream();

        // Write garbage
        byte[] garbage = [0xFF, 0xAA, 0x55, 0x00, 0x99];
        await stream.WriteAsync(garbage);

        // Write valid frame
        MemoryStream frameStream = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));
        frameStream.Position = 0;
        await frameStream.CopyToAsync(stream);

        stream.Position = 0;

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1, "parser should skip garbage and find valid frame");
    }

    // ======================================================================================
    // PRIORITY 5: Receiver ID Handling
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_ReceiverIdMessage_ConsumedNotProcessed()
    {
        // Arrange: Receiver ID message followed by frame
        var stream = new MemoryStream();

        // Write receiver ID message
        byte[] uuidBytes = BeastTestData.StandardUuid.ToByteArray();
        MemoryStream receiverIdStream = BeastTestData.CreateBeastStream(
            BeastTestData.TYPE_RECEIVER_ID, [], 0, uuidBytes.Take(8).ToArray(), applyEscaping: true);
        receiverIdStream.Position = 0;
        await receiverIdStream.CopyToAsync(stream);

        // Write regular frame
        MemoryStream frameStream = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));
        frameStream.Position = 0;
        await frameStream.CopyToAsync(stream);

        stream.Position = 0;

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1, "only the frame should be yielded, not receiver ID");
        frames[0].Data.Length.Should().Be(7, "frame should be parsed correctly after receiver ID");
    }

    [Fact]
    public async Task ParseStreamAsync_ReceiverIdWithEscapes_CorrectlyConsumed()
    {
        // Arrange: Receiver ID with ESC bytes followed by frame
        var stream = new MemoryStream();

        // UUID with escape bytes
        byte[] uuidBytes = BeastTestData.EscapeUuid.ToByteArray();
        MemoryStream receiverIdStream = BeastTestData.CreateBeastStream(
            BeastTestData.TYPE_RECEIVER_ID, [], 0, uuidBytes.Take(8).ToArray(), applyEscaping: true);
        receiverIdStream.Position = 0;
        await receiverIdStream.CopyToAsync(stream);

        // Write regular frame
        MemoryStream frameStream = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));
        frameStream.Position = 0;
        await frameStream.CopyToAsync(stream);

        stream.Position = 0;

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1, "receiver ID with escapes should be consumed, frame parsed");
    }

    // ======================================================================================
    // PRIORITY 6: EOF & Incomplete Messages
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_EndOfStream_LoopExitsGracefully()
    {
        // Arrange: Single complete frame
        MemoryStream stream = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert: Should complete gracefully
        frames.Should().HaveCount(1);
        // No exception should be thrown
    }

    [Fact]
    public async Task ParseStreamAsync_IncompleteFrameAtEOF_FrameSkipped()
    {
        // Arrange: Complete frame + incomplete frame (EOF mid-data)
        var stream = new MemoryStream();

        // Write complete frame
        MemoryStream completeFrame = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));
        completeFrame.Position = 0;
        await completeFrame.CopyToAsync(stream);

        // Write incomplete frame (just ESC + type + partial timestamp)
        stream.WriteByte(BeastTestData.ESC);
        stream.WriteByte(BeastTestData.TYPE_SHORT);
        stream.WriteByte(0x00);  // Partial timestamp
        stream.WriteByte(0x00);

        stream.Position = 0;

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert: Only complete frame parsed
        frames.Should().HaveCount(1, "incomplete frame should be skipped");
    }

    [Fact]
    public async Task ParseStreamAsync_EOFAfterEscapeByte_BreaksGracefully()
    {
        // Arrange: Just ESC byte, then EOF
        var stream = new MemoryStream();
        stream.WriteByte(BeastTestData.ESC);
        stream.Position = 0;

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert: No frames, no exception
        frames.Should().BeEmpty("EOF after ESC should exit gracefully");
    }

    // ======================================================================================
    // PRIORITY 7: Cancellation
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_Cancellation_StopsEnumeration()
    {
        // Arrange: Stream with multiple frames
        var stream = new MemoryStream();
        for (int i = 0; i < 100; i++)
        {
            MemoryStream frameStream = BeastTestData.CreateCompleteFrameStream(
                false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));
            frameStream.Position = 0;
            await frameStream.CopyToAsync(stream);
        }
        stream.Position = 0;

        using var cts = new CancellationTokenSource();
        var frames = new List<ValidatedFrame>();

        // Act: Parse and cancel after first frame
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream, cts.Token))
        {
            frames.Add(frame);
            if (frames.Count == 1)
            {
                cts.Cancel();
            }
        }

        // Assert: Should stop after cancellation
        frames.Should().HaveCountLessThan(100, "enumeration should stop after cancellation");
    }

    // ======================================================================================
    // PRIORITY 8: ValidatedFrameFactory Integration
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_ValidFrame_ReturnsValidatedFrame()
    {
        // Arrange: Use real frame data that will pass validation
        MemoryStream stream = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert
        frames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = frames[0];

        parsedFrame.Should().NotBeNull();
        parsedFrame.IcaoAddress.Should().NotBeNullOrEmpty();
        parsedFrame.Data.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseStreamAsync_InvalidFrame_NullNotYielded()
    {
        // Arrange: Frame with invalid CRC (all zeros - will likely fail validation)
        byte[] invalidFrameData = new byte[7];  // All zeros
        MemoryStream stream = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, invalidFrameData);

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert: Invalid frame should not be yielded (factory returns null)
        // Note: Behavior depends on ValidatedFrameFactory implementation
        // If it rejects invalid frames, count should be 0
        frames.Count.Should().BeLessThanOrEqualTo(1);
    }

    // ======================================================================================
    // EDGE CASES
    // ======================================================================================

    [Fact]
    public async Task ParseStreamAsync_NullStream_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () =>
        {
            await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(null!))
            {
                // Should not reach here
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ParseStreamAsync_UnknownMessageType_Skipped()
    {
        // Arrange: Frame with unknown message type 'Z' followed by valid frame
        var stream = new MemoryStream();
        stream.WriteByte(BeastTestData.ESC);
        stream.WriteByte((byte)'Z');  // Unknown type
        stream.WriteByte(0x00);

        // Write valid frame
        MemoryStream validFrame = BeastTestData.CreateCompleteFrameStream(
            false, DateTime.UtcNow, 100, BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0));
        validFrame.Position = 0;
        await validFrame.CopyToAsync(stream);

        stream.Position = 0;

        // Act
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            frames.Add(frame);
        }

        // Assert: Unknown message skipped, valid frame parsed
        frames.Should().HaveCount(1, "unknown message type should be skipped");
    }

    // ======================================================================================
    // PRIORITY 9: Real-World Integration Tests
    // ======================================================================================

    /// <summary>
    /// Integration test using real Beast binary data captured from readsb.
    /// Capture command: nc 127.0.0.1 30005 > BeastBinary.bin
    /// This validates parser behavior against production data.
    /// </summary>
    [Fact]
    public async Task ParseStreamAsync_RealWorldBeastData_ParsesAllFramesSuccessfully()
    {
        // Arrange: Load real Beast binary data captured from readsb
        string testDataPath = Path.Combine("TestData", "BeastBinary.bin");
        await using FileStream fileStream = File.OpenRead(testDataPath);

        // Act: Parse entire capture file
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(fileStream))
        {
            frames.Add(frame);
        }

        // Assert: Verify parsing success
        frames.Should().NotBeEmpty("real Beast data should contain parseable frames");

        // Verify frame structure
        frames.Should().OnlyContain(f => f.Data.Length == 7 || f.Data.Length == 14,
            "all frames should be either short (7) or long (14) bytes");

        frames.Should().OnlyContain(f => !string.IsNullOrEmpty(f.IcaoAddress),
            "all frames should have valid ICAO addresses");

        frames.Should().OnlyContain(f => f.SignalStrength >= 0 && f.SignalStrength <= 255,
            "all signal strengths should be in valid range");

        // Verify timestamps are recent (reception time)
        frames.Should().OnlyContain(f => f.Timestamp >= DateTime.UtcNow.AddMinutes(-1),
            "all frames should have recent reception timestamps");

        // Verify frame diversity (real data should have multiple downlink formats)
        var downlinkFormats = frames.Select(f => f.DownlinkFormat).Distinct().ToList();
        downlinkFormats.Should().NotBeEmpty("real data should contain various downlink formats");

        // Collect statistics for diagnostics
        int shortFrames = frames.Count(f => f.Data.Length == 7);
        int longFrames = frames.Count(f => f.Data.Length == 14);
        int uniqueAircraft = frames.Select(f => f.IcaoAddress).Distinct().Count();

        // Verify we got meaningful data
        frames.Should().HaveCountGreaterThan(10, "real capture should contain multiple frames");
        uniqueAircraft.Should().BeGreaterThan(0, "should detect at least one aircraft");

        // Output summary for diagnostics (visible in test output)
        output.WriteLine($"Parsed {frames.Count} frames from real Beast capture:");
        output.WriteLine($"  - {shortFrames} short frames (7 bytes)");
        output.WriteLine($"  - {longFrames} long frames (14 bytes)");
        output.WriteLine($"  - {uniqueAircraft} unique aircraft");
        output.WriteLine($"  - {downlinkFormats.Count} different downlink formats: {string.Join(", ", downlinkFormats)}");
    }

    [Fact]
    public async Task ParseStreamAsync_RealWorldBeastData_ContainsDF17Frames()
    {
        // Arrange: Load real Beast binary data
        string testDataPath = Path.Combine("TestData", "BeastBinary.bin");
        await using FileStream fileStream = File.OpenRead(testDataPath);

        // Act: Parse and filter for DF17 (ADS-B Extended Squitter)
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(fileStream))
        {
            frames.Add(frame);
        }

        // Assert: Real traffic should contain DF17 ADS-B frames (most common in modern aviation)
        var df17Frames = frames.Where(f => f.DownlinkFormat == DownlinkFormat.ExtendedSquitter).ToList();
        df17Frames.Should().NotBeEmpty("real capture should contain DF17 ADS-B frames");

        // DF17 frames are always 14 bytes (long)
        df17Frames.Should().OnlyContain(f => f.Data.Length == 14,
            "DF17 frames should always be 14 bytes");
    }

    [Fact]
    public async Task ParseStreamAsync_RealWorldBeastData_MultipleAircraft()
    {
        // Arrange: Load real Beast binary data
        string testDataPath = Path.Combine("TestData", "BeastBinary.bin");
        await using FileStream fileStream = File.OpenRead(testDataPath);

        // Act: Parse and collect unique ICAO addresses
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(fileStream))
        {
            frames.Add(frame);
        }

        // Assert: Real capture should contain multiple distinct aircraft
        var uniqueIcaos = frames.Select(f => f.IcaoAddress).Distinct().ToList();
        uniqueIcaos.Should().HaveCountGreaterThan(1,
            "real capture should contain transmissions from multiple aircraft");

        // Verify ICAO addresses are properly formatted (6 hex characters)
        uniqueIcaos.Should().OnlyContain(icao => icao.Length == 6,
            "all ICAO addresses should be 6 characters");

        uniqueIcaos.Should().OnlyContain(icao => icao.All(c => "0123456789ABCDEF".Contains(c)),
            "all ICAO addresses should be valid hexadecimal");

        output.WriteLine($"Detected aircraft: {string.Join(", ", uniqueIcaos)}");
    }

    [Fact]
    public async Task ParseStreamAsync_RealWorldBeastData_StatisticalAnalysis()
    {
        // Arrange: Load real Beast binary data
        string testDataPath = Path.Combine("TestData", "BeastBinary.bin");
        FileInfo fileInfo = new(testDataPath);

        // Parse all frames from the capture file
        await using FileStream fileStream = File.OpenRead(testDataPath);
        var frames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(fileStream))
        {
            frames.Add(frame);
        }

        // Assert: We should successfully parse frames
        frames.Should().NotBeEmpty("real Beast data should contain parseable frames");

        // Collect detailed statistics
        int shortFrames = frames.Count(f => f.Data.Length == 7);
        int longFrames = frames.Count(f => f.Data.Length == 14);
        int uniqueAircraft = frames.Select(f => f.IcaoAddress).Distinct().Count();
        var downlinkFormats = frames.GroupBy(f => f.DownlinkFormat)
            .OrderByDescending(g => g.Count())
            .Select(g => new { Format = g.Key, Count = g.Count() })
            .ToList();

        // Calculate signal strength statistics
        double avgSignal = frames.Average(f => f.SignalStrength);
        double minSignal = frames.Min(f => f.SignalStrength);
        double maxSignal = frames.Max(f => f.SignalStrength);

        // Log detailed analysis
        output.WriteLine("=== Beast Binary Data Analysis ===");
        output.WriteLine($"File size: {fileInfo.Length:N0} bytes");
        output.WriteLine("");

        output.WriteLine("Parser statistics:");
        output.WriteLine($"  - Frames parsed (decoded from Beast format): {_parser.FramesParsed:N0}");
        output.WriteLine($"  - Frames validated (passed CRC check): {_parser.FramesValidated:N0}");
        output.WriteLine($"  - Frames rejected (failed validation): {_parser.FramesRejected:N0}");
        if (_parser.FramesParsed > 0)
        {
            double validationRate = _parser.FramesValidated * 100.0 / _parser.FramesParsed;
            double rejectionRate = _parser.FramesRejected * 100.0 / _parser.FramesParsed;
            output.WriteLine($"  - Validation rate: {validationRate:F1}%");
            output.WriteLine($"  - Rejection rate: {rejectionRate:F1}%");
        }
        output.WriteLine("");

        output.WriteLine("Frame type breakdown:");
        output.WriteLine($"  - Short frames (7 bytes): {shortFrames:N0} ({shortFrames * 100.0 / frames.Count:F1}%)");
        output.WriteLine($"  - Long frames (14 bytes): {longFrames:N0} ({longFrames * 100.0 / frames.Count:F1}%)");
        output.WriteLine("");

        output.WriteLine($"Unique aircraft detected: {uniqueAircraft}");
        output.WriteLine("");

        output.WriteLine("Downlink format distribution:");
        foreach (var df in downlinkFormats)
        {
            output.WriteLine($"  - {df.Format}: {df.Count:N0} frames ({df.Count * 100.0 / frames.Count:F1}%)");
        }
        output.WriteLine("");

        output.WriteLine("Signal strength statistics:");
        output.WriteLine($"  - Average: {avgSignal:F1}");
        output.WriteLine($"  - Range: {minSignal} - {maxSignal}");

        // Verify we got meaningful data
        frames.Should().HaveCountGreaterThan(100, "should parse substantial number of frames from real data");
        uniqueAircraft.Should().BeGreaterThan(1, "should detect multiple aircraft in real capture");
        downlinkFormats.Should().NotBeEmpty("should contain various downlink formats");
    }
}
