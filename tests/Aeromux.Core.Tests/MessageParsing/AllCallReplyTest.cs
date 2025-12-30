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
/// Tests for parsing All-Call Reply
/// </summary>
public class AllCallReplyTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AllCall_4D2407, "4D2407")]
    [InlineData(RealFrames.AllCall_471F87, "471F87")]
    [InlineData(RealFrames.AllCall_80073B, "80073B")]
    public void ParseMessage_DF11_AllCallReply_Icao(
        string hexFrame,
        string expectedIcao)
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
        AllCallReply reply = message.Should().BeOfType<AllCallReply>().Subject;
        reply.ExtractedIcao.Should().NotBeNull();
        reply.ExtractedIcao.Should().Be(expectedIcao);
    }

    [Theory]
    [InlineData(RealFrames.AllCall_4D2407, "4D2407", TransponderCapability.Level2PlusAirborne)]
    [InlineData(RealFrames.AllCall_471F87, "471F87", TransponderCapability.Level2PlusAirborne)]
    [InlineData(RealFrames.AllCall_80073B, "80073B", TransponderCapability.Level2PlusAirborne)]
    public void ParseMessage_DF11_AllCallReply_Capability(
        string hexFrame,
        string expectedIcao,
        TransponderCapability expectedCapability)
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
        AllCallReply reply = message.Should().BeOfType<AllCallReply>().Subject;
        reply.Capability.Should().Be(expectedCapability);
    }
}
