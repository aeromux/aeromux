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

using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParsing;

/// <summary>
/// Tests for parsing Surveillance Identity Reply
/// </summary>
public class SurveillanceIdentityReplyTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.Surveillance_Identity_49D414, "4D2407", "1420")]
    [InlineData(RealFrames.Surveillance_Identity_80073B, "80073B", "3205")]
    [InlineData(RealFrames.Surveillance_Identity_3C4AD7, "3C4AD7", "3205")]
    public void ParseMessage_DF5_Surveillance_IdentityReply_Squawk(
        string hexFrame,
        string expectedIcao,
        string expectedSquawk)
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
        SurveillanceIdentityReply reply = message.Should().BeOfType<SurveillanceIdentityReply>().Subject;
        reply.SquawkCode.Should().NotBeNull();
        reply.SquawkCode.Should().Be(expectedSquawk);
    }
}
