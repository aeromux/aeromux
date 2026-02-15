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
/// Tests for parsing Comm-B Altitude Reply
/// </summary>
public class SurveillanceAltitudeReplyTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.Surveillance_Altitude_4BA913, "4BA913", 36000, AltitudeType.Barometric)]
    [InlineData(RealFrames.Surveillance_Altitude_49D414, "49D414", 35000, AltitudeType.Barometric)]
    public void ParseMessage_DF4_Surveillance_AltitudeReply_Altitude(
        string hexFrame,
        string expectedIcao,
        int expectedAltitude,
        AltitudeType expectedAltitudeType)
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
        SurveillanceAltitudeReply reply = message.Should().BeOfType<SurveillanceAltitudeReply>().Subject;
        reply.Altitude.Should().NotBeNull();
        reply.Altitude!.Feet.Should().Be(expectedAltitude);
        reply.Altitude!.Type.Should().Be(expectedAltitudeType);
    }
}
