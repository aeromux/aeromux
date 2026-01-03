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

namespace Aeromux.Infrastructure.Tests.Network.Protocols;

/// <summary>
/// Roundtrip tests for BeastEncoder/BeastParser.
/// NOTE: Timestamps do NOT roundtrip perfectly - this is expected behavior!
/// - BeastEncoder sends relative timestamps (12MHz counter from reference time)
/// - BeastParser uses reception time (DateTime.UtcNow) - parser cannot know sender's reference
/// - Beast protocol limitation: timestamps are sender-relative, not absolute
/// - For absolute timestamp preservation, use JSON or SBS formats
/// </summary>
public class BeastRoundtripTests
{
    private readonly DateTime _referenceTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly BeastEncoder _encoder;
    private readonly BeastParser _parser;
    private readonly ValidatedFrameBuilder _frameBuilder = new();

    // Tolerance values for floating-point conversions
    private const int SignalStrengthTolerance = 2;  // ±2 due to sqrt transform rounding

    public BeastRoundtripTests()
    {
        _encoder = new BeastEncoder(_referenceTime);
        _parser = new BeastParser();  // No reference time needed
    }

    // ======================================================================================
    // PRIORITY 1: Basic Roundtrip
    // ======================================================================================

    [Fact]
    public async Task Roundtrip_ShortFrame_DataPreserved()
    {
        // Arrange: Create original frame
        var originalTimestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        byte originalSignal = 150;

        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.ShortFrame_DF0)
            .WithTimestamp(originalTimestamp)
            .WithSignalStrength(originalSignal)
            .WithIcaoAddress("4BCE08")
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(1, "one frame should be roundtripped");
        ValidatedFrame parsedFrame = parsedFrames[0];

        // Frame data should match exactly
        byte[] expectedData = BeastTestData.HexToBytes(BeastTestData.ShortFrame_DF0);
        parsedFrame.Data.Should().BeEquivalentTo(expectedData,
            "frame data should be preserved byte-perfect");

        // Parser uses reception time (DateTime.UtcNow), not reconstructed from Beast timestamp
        // Beast timestamps are sender-relative; absolute time requires JSON/SBS formats
        parsedFrame.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1),
            "timestamp should be reception time");

        // Signal strength should match within sqrt transform tolerance
        parsedFrame.SignalStrength.Should().BeInRange(
            (byte)(originalSignal - SignalStrengthTolerance),
            (byte)(originalSignal + SignalStrengthTolerance),
            "signal strength should be preserved within sqrt transform rounding");

        // ICAO should match
        parsedFrame.IcaoAddress.Should().Be("4BCE08", "ICAO address should be preserved");
    }

    [Fact]
    public async Task Roundtrip_LongFrame_DataPreserved()
    {
        // Arrange: Create original long frame (14 bytes)
        var originalTimestamp = new DateTime(2024, 6, 15, 18, 30, 45, DateTimeKind.Utc);
        byte originalSignal = 200;

        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.LongFrame_DF17)
            .WithTimestamp(originalTimestamp)
            .WithSignalStrength(originalSignal)
            .WithIcaoAddress("471DBC")
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        parsedFrame.Data.Length.Should().Be(14, "long frame should have 14 bytes");
        byte[] expectedData = BeastTestData.HexToBytes(BeastTestData.LongFrame_DF17);
        parsedFrame.Data.Should().BeEquivalentTo(expectedData, "frame data should match exactly");

        // Parser uses reception time - Beast timestamps are sender-relative
        parsedFrame.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        parsedFrame.SignalStrength.Should().BeInRange(
            (byte)(originalSignal - SignalStrengthTolerance),
            (byte)(originalSignal + SignalStrengthTolerance));
    }

    // ======================================================================================
    // PRIORITY 2: Escape Byte Roundtrip
    // ======================================================================================

    [Fact]
    public async Task Roundtrip_FrameWithEscapeBytes_CompleteRestoration()
    {
        // Arrange: Frame data with multiple 0x1A bytes
        DateTime originalTimestamp = DateTime.UtcNow;
        byte originalSignal = 128;

        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.MultipleEscapeBytes)
            .WithTimestamp(originalTimestamp)
            .WithSignalStrength(originalSignal)
            .WithIcaoAddress("000000")
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert: Verify byte-perfect restoration despite escaping
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        byte[] expectedData = BeastTestData.HexToBytes(BeastTestData.MultipleEscapeBytes);
        parsedFrame.Data.Should().BeEquivalentTo(expectedData,
            "frame with ESC bytes should be restored exactly");

        // Verify all 0x1A bytes are present
        int expectedEscapeCount = expectedData.Count(b => b == BeastTestData.ESC);
        int parsedEscapeCount = parsedFrame.Data.Count(b => b == BeastTestData.ESC);
        parsedEscapeCount.Should().Be(expectedEscapeCount,
            "all ESC bytes should be preserved");
    }

    [Fact]
    public async Task Roundtrip_AllEscapeBytes_ExactMatch()
    {
        // Arrange: Pathological case - all data bytes are 0x1A
        DateTime originalTimestamp = DateTime.UtcNow;
        byte originalSignal = 100;

        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.AllEscapeBytes)
            .WithTimestamp(originalTimestamp)
            .WithSignalStrength(originalSignal)
            .WithIcaoAddress("1A1A1A")
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);

        // Encoded should be significantly larger due to doubling
        byte[] expectedData = BeastTestData.HexToBytes(BeastTestData.AllEscapeBytes);
        encoded.Length.Should().BeGreaterThan(expectedData.Length * 2,
            "all ESC bytes should be doubled");

        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert: Verify complete fidelity
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        parsedFrame.Data.Should().BeEquivalentTo(expectedData,
            "all-ESC-byte frame should roundtrip perfectly");
        parsedFrame.Data.Should().OnlyContain(b => b == BeastTestData.ESC,
            "all data bytes should be ESC");
    }

    // ======================================================================================
    // PRIORITY 3: Signal Strength Fidelity
    // ======================================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(255)]
    public async Task Roundtrip_SignalStrengthRange_WithinTolerance(byte originalSignal)
    {
        // Arrange
        DateTime originalTimestamp = DateTime.UtcNow;

        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.ShortFrame_DF0)
            .WithTimestamp(originalTimestamp)
            .WithSignalStrength(originalSignal)
            .WithIcaoAddress("4BCE08")
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert: Verify signal within acceptable error
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        parsedFrame.SignalStrength.Should().BeInRange(
            (byte)Math.Max(0, originalSignal - SignalStrengthTolerance),
            (byte)Math.Min(255, originalSignal + SignalStrengthTolerance),
            $"signal {originalSignal} should roundtrip within ±{SignalStrengthTolerance}");
    }

    // ======================================================================================
    // PRIORITY 4: Various Downlink Formats
    // ======================================================================================

    [Fact]
    public async Task Roundtrip_DF0_APMode_CorrectICAO()
    {
        // Arrange: DF 0 (Short Air-Air Surveillance) - AP mode, ICAO extracted from CRC
        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.ShortFrame_DF0)
            .WithTimestamp(DateTime.UtcNow)
            .WithSignalStrength(150)
            .WithIcaoAddress("4BCE08")  // AP mode ICAO (extracted from CRC)
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        parsedFrame.IcaoAddress.Should().NotBeNullOrEmpty("ICAO should be extracted from CRC");
        parsedFrame.DownlinkFormat.Should().Be(DownlinkFormat.ShortAirAirSurveillance);
        parsedFrame.UsesPIMode.Should().BeFalse("DF 0 uses AP mode");
    }

    [Fact]
    public async Task Roundtrip_DF17_PIMode_CorrectICAO()
    {
        // Arrange: DF 17 (Extended Squitter / ADS-B) - PI mode
        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.LongFrame_DF17)
            .WithTimestamp(DateTime.UtcNow)
            .WithSignalStrength(180)
            .WithIcaoAddress("471DBC")  // PI mode ICAO
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        parsedFrame.IcaoAddress.Should().Be("471DBC", "PI mode ICAO should be preserved");
        parsedFrame.DownlinkFormat.Should().Be(DownlinkFormat.ExtendedSquitter);
    }

    [Fact]
    public async Task Roundtrip_DF4_APMode_CorrectICAO()
    {
        // Arrange: DF 4 (Surveillance Altitude Reply) - AP mode, ICAO in CRC
        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.ShortFrame_DF4)
            .WithTimestamp(DateTime.UtcNow)
            .WithSignalStrength(120)
            .WithIcaoAddress("49D414")  // AP mode - ICAO extracted from CRC by factory
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        // ICAO depends on ValidatedFrameFactory extraction from CRC
        parsedFrame.IcaoAddress.Should().NotBeNullOrEmpty();
        parsedFrame.DownlinkFormat.Should().Be(DownlinkFormat.SurveillanceAltitudeReply);
        parsedFrame.UsesPIMode.Should().BeFalse("DF 4 uses AP mode");
    }

    [Fact]
    public async Task Roundtrip_DF20_APMode_CorrectICAO()
    {
        // Arrange: DF 20 (Comm-B Altitude Reply) - AP mode, long frame
        ValidatedFrame originalFrame = _frameBuilder
            .WithHexData(BeastTestData.LongFrame_DF20)
            .WithTimestamp(DateTime.UtcNow)
            .WithSignalStrength(140)
            .WithIcaoAddress("4D2407")  // AP mode
            .Build();

        // Act: Encode → Parse
        byte[] encoded = _encoder.Encode(originalFrame);
        MemoryStream stream = BeastTestData.CreateStreamFromBytes(encoded);

        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert
        parsedFrames.Should().HaveCount(1);
        ValidatedFrame parsedFrame = parsedFrames[0];

        parsedFrame.Data.Length.Should().Be(14, "DF 20 is long frame");
        parsedFrame.DownlinkFormat.Should().Be(DownlinkFormat.CommBAltitudeReply);
        parsedFrame.UsesPIMode.Should().BeFalse("DF 20 uses AP mode");
    }

    // ======================================================================================
    // PRIORITY 5: Multiple Frame Sequence
    // ======================================================================================

    [Fact]
    public async Task Roundtrip_MultipleFramesInSequence_AllPreserved()
    {
        // Arrange: Create 5 frames (mixed short/long)
        var frames = new List<ValidatedFrame>
        {
            _frameBuilder.WithHexData(BeastTestData.ShortFrame_DF0)
                .WithTimestamp(DateTime.UtcNow).WithSignalStrength(100).WithIcaoAddress("4BCE08").Build(),

            _frameBuilder.WithHexData(BeastTestData.LongFrame_DF17)
                .WithTimestamp(DateTime.UtcNow.AddSeconds(1)).WithSignalStrength(120).WithIcaoAddress("471DBC").Build(),

            _frameBuilder.WithHexData(BeastTestData.ShortFrame_DF4)
                .WithTimestamp(DateTime.UtcNow.AddSeconds(2)).WithSignalStrength(90).WithIcaoAddress("49D414").Build(),

            _frameBuilder.WithHexData(BeastTestData.LongFrame_DF20)
                .WithTimestamp(DateTime.UtcNow.AddSeconds(3)).WithSignalStrength(150).WithIcaoAddress("4D2407").Build(),

            _frameBuilder.WithHexData(BeastTestData.ShortFrame_DF0)
                .WithTimestamp(DateTime.UtcNow.AddSeconds(4)).WithSignalStrength(110).WithIcaoAddress("4BCE08").Build()
        };

        // Act: Encode all frames to single stream
        var stream = new MemoryStream();
        foreach (ValidatedFrame frame in frames)
        {
            byte[] encoded = _encoder.Encode(frame);
            await stream.WriteAsync(encoded);
        }
        stream.Position = 0;

        // Parse all frames
        var parsedFrames = new List<ValidatedFrame>();
        await foreach (ValidatedFrame frame in _parser.ParseStreamAsync(stream))
        {
            parsedFrames.Add(frame);
        }

        // Assert: Verify all frames preserved in order
        parsedFrames.Should().HaveCount(5, "all 5 frames should roundtrip");

        parsedFrames[0].Data.Length.Should().Be(7, "frame 1 is short");
        parsedFrames[1].Data.Length.Should().Be(14, "frame 2 is long");
        parsedFrames[2].Data.Length.Should().Be(7, "frame 3 is short");
        parsedFrames[3].Data.Length.Should().Be(14, "frame 4 is long");
        parsedFrames[4].Data.Length.Should().Be(7, "frame 5 is short");

        // Verify data matches originals
        for (int i = 0; i < 5; i++)
        {
            parsedFrames[i].Data.Should().BeEquivalentTo(frames[i].Data,
                $"frame {i + 1} data should match original");
        }
    }
}
