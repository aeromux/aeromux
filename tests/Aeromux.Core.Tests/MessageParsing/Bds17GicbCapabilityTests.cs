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
/// Tests for BDS 1,7 - Common Usage GICB Capability Report
/// Based on "The 1090MHz Riddle" Chapter 16.2, Page 120
/// </summary>
public class Bds17GicbCapabilityTests
{
    private readonly MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF20_Bds17_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds17_Gicb_000063)
            .WithIcaoAddress("000063")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds17, "MB field starts with 0x17 indicating BDS 1,7");
    }

    [Fact]
    public void ParseMessage_DF20_Bds17_HasBdsData()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds17_Gicb_000063)
            .WithIcaoAddress("000063")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull("BDS 1,7 should have parsed capability data");
        reply.BdsData.Should().BeOfType<Bds17GicbCapability>("BDS 1,7 data should be GICB capability report");
    }

    [Fact]
    public void ParseMessage_DF20_Bds17_CapabilityMask()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds17_Gicb_000063)
            .WithIcaoAddress("000063")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds17GicbCapability? bds17 = reply.BdsData.Should().BeOfType<Bds17GicbCapability>().Subject;

        // MB field: FA81C100000000
        // Expected capability mask from book:
        // Bits 1,2,3,4,5,7,9,16,17,18,24 are set to 1
        // This corresponds to BDS codes: 0,5/0,6/0,7/0,8/0,9/2,0/4,0/5,0/5,1/5,2/6,0
        bds17.CapabilityMask.Should().NotBe(0, "capability mask should indicate supported BDS codes");
    }
}
