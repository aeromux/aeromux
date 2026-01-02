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
/// Tests for parsing Long Air-Air Surveillance (DF 16 ACAS coordination messages)
/// </summary>
public class LongAirAirSurveillanceTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.LongAirAir_71C011, "71C011", 31000, AltitudeType.Barometric)]
    [InlineData(RealFrames.LongAirAir_440C8E, "440C8E", 35000, AltitudeType.Barometric)]
    [InlineData(RealFrames.LongAirAir_80073B, "80073B", 39975, AltitudeType.Barometric)]
    [InlineData(RealFrames.LongAirAir_3C4AD7, "3C4AD7", 40000, AltitudeType.Barometric)]
    public void ParseMessage_DF16_LongAirAirSurveillance_Altitude(
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
        LongAirAirSurveillance longAirAirSurveillance = message.Should().BeOfType<LongAirAirSurveillance>().Subject;
        longAirAirSurveillance.Altitude.Should().NotBeNull();
        longAirAirSurveillance.Altitude!.Feet.Should().Be(expectedAltitude);
        longAirAirSurveillance.Altitude!.Type.Should().Be(expectedAltitudeType);
    }

    [Theory]
    [InlineData(RealFrames.LongAirAir_71C011, "71C011", VerticalStatus.Airborne)]
    [InlineData(RealFrames.LongAirAir_440C8E, "440C8E", VerticalStatus.Airborne)]
    [InlineData(RealFrames.LongAirAir_80073B, "80073B", VerticalStatus.Airborne)]
    [InlineData(RealFrames.LongAirAir_3C4AD7, "3C4AD7", VerticalStatus.Airborne)]
    public void ParseMessage_DF16_LongAirAirSurveillance_VerticalStatus(
        string hexFrame,
        string expectedIcao,
        VerticalStatus expectedVerticalStatus)
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
        LongAirAirSurveillance longAirAirSurveillance = message.Should().BeOfType<LongAirAirSurveillance>().Subject;
        longAirAirSurveillance.VerticalStatus.Should().Be(expectedVerticalStatus);
    }

    [Theory]
    [InlineData(RealFrames.LongAirAir_71C011, "71C011", 7)]
    [InlineData(RealFrames.LongAirAir_440C8E, "440C8E", 7)]
    [InlineData(RealFrames.LongAirAir_80073B, "80073B", 7)]
    [InlineData(RealFrames.LongAirAir_3C4AD7, "3C4AD7", 7)]
    public void ParseMessage_DF16_LongAirAirSurveillance_SensitivityLevel(
        string hexFrame,
        string expectedIcao,
        int expectedSensitivityLevel)
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
        LongAirAirSurveillance longAirAirSurveillance = message.Should().BeOfType<LongAirAirSurveillance>().Subject;
        longAirAirSurveillance.SensitivityLevel.Should().Be(expectedSensitivityLevel);
    }

    [Theory]
    [InlineData(RealFrames.LongAirAir_71C011, "71C011", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    [InlineData(RealFrames.LongAirAir_440C8E, "440C8E", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    [InlineData(RealFrames.LongAirAir_80073B, "80073B", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    [InlineData(RealFrames.LongAirAir_3C4AD7, "3C4AD7", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    public void ParseMessage_DF16_LongAirAirSurveillance_ReplyInformation(
        string hexFrame,
        string expectedIcao,
        AcasReplyInformation expectedReplyInformation)
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
        LongAirAirSurveillance longAirAirSurveillance = message.Should().BeOfType<LongAirAirSurveillance>().Subject;
        longAirAirSurveillance.ReplyInformation.Should().Be(expectedReplyInformation);
    }

    [Theory]
    [InlineData(RealFrames.LongAirAir_71C011, "71C011")]
    [InlineData(RealFrames.LongAirAir_440C8E, "440C8E")]
    [InlineData(RealFrames.LongAirAir_80073B, "80073B")]
    [InlineData(RealFrames.LongAirAir_3C4AD7, "3C4AD7")]
    public void ParseMessage_DF16_LongAirAirSurveillance_AcasInvalid_WhenVdsIsNot0x30(
        string hexFrame,
        string expectedIcao)
    {
        // These frames have VDS=0x58 (ground-initiated Comm-B data), not 0x30 (ACAS coordination)
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress(expectedIcao)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        LongAirAirSurveillance longAirAirSurveillance = message.Should().BeOfType<LongAirAirSurveillance>().Subject;
        longAirAirSurveillance.AcasValid.Should().BeFalse("VDS = 0x58 indicates non-ACAS data (ground-initiated Comm-B)");

        // When ACAS is invalid, MV subfields should be null
        longAirAirSurveillance.ResolutionAdvisoryTerminated.Should().BeNull();
        longAirAirSurveillance.MultipleThreatEncounter.Should().BeNull();
        longAirAirSurveillance.RacNotBelow.Should().BeNull();
        longAirAirSurveillance.RacNotAbove.Should().BeNull();
        longAirAirSurveillance.RacNotLeft.Should().BeNull();
        longAirAirSurveillance.RacNotRight.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_DF16_LongAirAirSurveillance_AllFields()
    {
        // Test all fields together with one comprehensive frame test
        // Frame: 80073B at 39975 ft (VDS=0x58, not ACAS coordination data)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.LongAirAir_80073B)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        LongAirAirSurveillance msg = message.Should().BeOfType<LongAirAirSurveillance>().Subject;

        // Basic message fields
        msg.IcaoAddress.Should().Be("80073B");
        msg.DownlinkFormat.Should().Be(DownlinkFormat.LongAirAirSurveillance);

        // ACAS fields (always present in DF 16)
        msg.Altitude.Should().NotBeNull();
        msg.Altitude!.Feet.Should().Be(39975);
        msg.Altitude.Type.Should().Be(AltitudeType.Barometric);
        msg.VerticalStatus.Should().Be(VerticalStatus.Airborne);
        msg.SensitivityLevel.Should().Be(7, "maximum ACAS sensitivity");
        msg.ReplyInformation.Should().Be(AcasReplyInformation.VerticalOnlyResolutionCapability);

        // MV field - Not valid ACAS coordination data (VDS=0x58, not 0x30)
        msg.AcasValid.Should().BeFalse("VDS != 0x30 indicates non-ACAS MV data");
        msg.ResolutionAdvisoryTerminated.Should().BeNull("MV subfields only populated when AcasValid=true");
        msg.MultipleThreatEncounter.Should().BeNull("MV subfields only populated when AcasValid=true");
        msg.RacNotBelow.Should().BeNull("MV subfields only populated when AcasValid=true");
        msg.RacNotAbove.Should().BeNull("MV subfields only populated when AcasValid=true");
        msg.RacNotLeft.Should().BeNull("MV subfields only populated when AcasValid=true");
        msg.RacNotRight.Should().BeNull("MV subfields only populated when AcasValid=true");
    }
}
