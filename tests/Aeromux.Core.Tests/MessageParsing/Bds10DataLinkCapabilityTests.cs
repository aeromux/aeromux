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
/// Tests for BDS 1,0 - Data Link Capability Report
/// Real captures from dump1090 with BDS 1,0 identified Comm-B messages.
/// </summary>
public class Bds10DataLinkCapabilityTests
{
    private readonly MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF20_Bds10_RealCapture_407D44_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_407D44_BDS10)
            .WithIcaoAddress("407D44")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds10, "MB field starts with 0x10 indicating BDS 1,0");
    }

    [Fact]
    public void ParseMessage_DF20_Bds10_RealCapture_407D44_HasBdsData()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_407D44_BDS10)
            .WithIcaoAddress("407D44")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull("BDS 1,0 should have parsed capability data");
        reply.BdsData.Should().BeOfType<Bds10DataLinkCapability>("BDS 1,0 data should be data link capability report");
    }

    [Fact]
    public void ParseMessage_DF20_Bds10_RealCapture_4C0177_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_4C0177_BDS10)
            .WithIcaoAddress("4C0177")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds10, "MB field starts with 0x10 indicating BDS 1,0");
    }

    [Fact]
    public void ParseMessage_DF20_Bds10_RealCapture_4C0177_HasBdsData()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_4C0177_BDS10)
            .WithIcaoAddress("4C0177")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull("BDS 1,0 should have parsed capability data");
        reply.BdsData.Should().BeOfType<Bds10DataLinkCapability>("BDS 1,0 data should be data link capability report");
    }

    [Fact]
    public void ParseMessage_DF20_Bds10_RealCapture_CapabilityBitsNonZero()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_407D44_BDS10)
            .WithIcaoAddress("407D44")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds10DataLinkCapability? bds10 = reply.BdsData.Should().BeOfType<Bds10DataLinkCapability>().Subject;
        bds10.CapabilityBits.Should().NotBe(0, "capability bits should indicate supported features");
    }
}
