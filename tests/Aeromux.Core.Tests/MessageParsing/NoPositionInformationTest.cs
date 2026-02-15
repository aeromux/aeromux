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

using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParsing;

/// <summary>
/// Tests for parsing NoPositionInformation messages (TC 0).
/// </summary>
public class NoPositionInformationTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.NoPosition_89642D_36000, "89642D", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.NoPosition_89642D_36025, "89642D", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.NoPosition_89642D_35975, "89642D", DownlinkFormat.ExtendedSquitter)]
    public void ParseMessage_DF17_TC0_NoPositionInformation_BasicFields(
        string hexFrame,
        string expectedIcao,
        DownlinkFormat expectedDF)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress(expectedIcao)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        NoPositionInformation noPosition = message.Should().BeOfType<NoPositionInformation>().Subject;
        noPosition.IcaoAddress.Should().Be(expectedIcao);
        noPosition.DownlinkFormat.Should().Be(expectedDF);
    }

    [Fact]
    public void ParseMessage_DF17_TC0_NoPositionInformation_TrackedInTCStatistics()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.NoPosition_89642D_36000)
            .WithIcaoAddress("89642D")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert - Verify TC 0 is counted in statistics (not as unsupported)
        message.Should().NotBeNull();
        message.Should().BeOfType<NoPositionInformation>();
        _parser.MessagesByTC.Should().ContainKey(0);
        _parser.MessagesByTC[0].Should().Be(1);
    }

    [Fact]
    public void ParseMessage_DF17_TC0_NoPositionInformation_NotCountedAsUnsupported()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.NoPosition_89642D_36000)
            .WithIcaoAddress("89642D")
            .Build();

        long unsupportedBefore = _parser.UnsupportedMessages;

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert - TC 0 should NOT increment unsupported counter
        message.Should().NotBeNull();
        message.Should().BeOfType<NoPositionInformation>();
        _parser.UnsupportedMessages.Should().Be(unsupportedBefore);
    }

    [Theory]
    [InlineData(RealFrames.NoPosition_89642D_36000, "89642D")]
    [InlineData(RealFrames.NoPosition_89642D_36025, "89642D")]
    [InlineData(RealFrames.NoPosition_89642D_35975, "89642D")]
    public void ParseMessage_DF17_TC0_NoPositionInformation_PreservesBaseFields(
        string hexFrame,
        string expectedIcao)
    {
        // Arrange
        byte expectedSignalStrength = 128;
        bool expectedWasCorrected = false;
        DateTime expectedTimestamp = DateTime.UtcNow;

        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress(expectedIcao)
            .WithSignalStrength(expectedSignalStrength)
            .WithCorrectionFlag(expectedWasCorrected)
            .WithTimestamp(expectedTimestamp)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        NoPositionInformation noPosition = message.Should().BeOfType<NoPositionInformation>().Subject;
        noPosition.IcaoAddress.Should().Be(expectedIcao);
        noPosition.SignalStrength.Should().Be(expectedSignalStrength);
        noPosition.WasCorrected.Should().Be(expectedWasCorrected);
        noPosition.Timestamp.Should().Be(expectedTimestamp);
    }
}
