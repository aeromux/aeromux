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
/// Tests for parsing Aircraft Identification
/// </summary>
public class AircraftIdentificationTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AircraftId_471DBC, "471DBC", "WZZ476")]
    [InlineData(RealFrames.AircraftId_8964A0, "8964A0", "UAE182")]
    [InlineData(RealFrames.AircraftId_8965F3, "8965F3", "ETD128")]
    public void ParseMessage_DF17_TC4_AircraftIdentification_Callsign(
        string hexFrame,
        string expectedIcao,
        string expectedCallsign)
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
        AircraftIdentification identification = message.Should().BeOfType<AircraftIdentification>().Subject;
        identification.Callsign.Should().NotBeNull();
        identification.Callsign.Should().Be(expectedCallsign);
    }

    [Theory]
    [InlineData(RealFrames.AircraftId_471DBC, "471DBC", AircraftCategory.Large)]
    [InlineData(RealFrames.AircraftId_8964A0, "8964A0", AircraftCategory.Heavy)]
    [InlineData(RealFrames.AircraftId_8965F3, "8965F3", AircraftCategory.Heavy)]
    public void ParseMessage_DF17_TC4_AircraftIdentification_Category(
        string hexFrame,
        string expectedIcao,
        AircraftCategory expectedCategory)
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
        AircraftIdentification identification = message.Should().BeOfType<AircraftIdentification>().Subject;
        identification.Category.Should().Be(expectedCategory);
    }
}
