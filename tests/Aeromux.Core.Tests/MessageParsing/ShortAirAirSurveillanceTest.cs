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
/// Tests for parsing Short Air-Air Surveillance (DF 0 ACAS messages)
/// </summary>
public class ShortAirAirSurveillanceTest
{
    private readonly MessageParser _parser = new();
    private readonly ValidatedFrameFactory _frameFactory = new();

    [Theory]
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", 33000, AltitudeType.Barometric)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", 37850, AltitudeType.Barometric)]
    [InlineData(RealFrames.ShortAirAir_8418B4, "8418B4", 37000, AltitudeType.Barometric)]
    [InlineData(RealFrames.ShortAirAir_3C4AD7, "3C4AD7", 39975, AltitudeType.Barometric)]
    public void ParseMessage_DF0_ShortAirAirSurveillance_Altitude(
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
        ShortAirAirSurveillance shortAirAirMessage = message.Should().BeOfType<ShortAirAirSurveillance>().Subject;
        shortAirAirMessage.Altitude.Should().NotBeNull();
        shortAirAirMessage.Altitude!.Feet.Should().Be(expectedAltitude);
        shortAirAirMessage.Altitude!.Type.Should().Be(expectedAltitudeType);
    }

    [Theory]
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", VerticalStatus.Airborne)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", VerticalStatus.Airborne)]
    [InlineData(RealFrames.ShortAirAir_8418B4, "8418B4", VerticalStatus.Airborne)]
    [InlineData(RealFrames.ShortAirAir_3C4AD7, "3C4AD7", VerticalStatus.Airborne)]
    public void ParseMessage_DF0_ShortAirAirSurveillance_VerticalStatus(
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
        ShortAirAirSurveillance shortAirAirMessage = message.Should().BeOfType<ShortAirAirSurveillance>().Subject;
        shortAirAirMessage.VerticalStatus.Should().Be(expectedVerticalStatus);
    }

    [Theory]
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", true)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", true)]
    [InlineData(RealFrames.ShortAirAir_8418B4, "8418B4", true)]
    [InlineData(RealFrames.ShortAirAir_3C4AD7, "3C4AD7", true)]
    public void ParseMessage_DF0_ShortAirAirSurveillance_CrossLinkCapability(
        string hexFrame,
        string expectedIcao,
        bool expectedCrossLinkCapability)
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
        ShortAirAirSurveillance shortAirAirMessage = message.Should().BeOfType<ShortAirAirSurveillance>().Subject;
        shortAirAirMessage.CrossLinkCapability.Should().Be(expectedCrossLinkCapability);
    }

    [Theory]
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", 7)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", 7)]
    [InlineData(RealFrames.ShortAirAir_8418B4, "8418B4", 7)]
    [InlineData(RealFrames.ShortAirAir_3C4AD7, "3C4AD7", 7)]
    public void ParseMessage_DF0_ShortAirAirSurveillance_SensitivityLevel(
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
        ShortAirAirSurveillance shortAirAirMessage = message.Should().BeOfType<ShortAirAirSurveillance>().Subject;
        shortAirAirMessage.SensitivityLevel.Should().Be(expectedSensitivityLevel);
    }

    [Theory]
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    [InlineData(RealFrames.ShortAirAir_8418B4, "8418B4", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    [InlineData(RealFrames.ShortAirAir_3C4AD7, "3C4AD7", AcasReplyInformation.VerticalOnlyResolutionCapability)]
    public void ParseMessage_DF0_ShortAirAirSurveillance_ReplyInformation(
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
        ShortAirAirSurveillance shortAirAirMessage = message.Should().BeOfType<ShortAirAirSurveillance>().Subject;
        shortAirAirMessage.ReplyInformation.Should().Be(expectedReplyInformation);
    }

    [Fact]
    public void ParseMessage_DF0_ValidRI_MaximumAirspeed()
    {
        // Frame ShortAirAir_4BCE08 has RI value of 12 (Maximum airspeed 300-600 knots)
        // RI field can indicate either ACAS operational status (0,2,3,4) or maximum airspeed (8-14)

        byte[] frameBytes = Convert.FromHexString(RealFrames.ShortAirAir_4BCE08);
        RawFrame rawFrame = new(frameBytes, DateTime.UtcNow);

        // Act - ValidatedFrameFactory accepts the frame (AP mode)
        ValidatedFrame? validatedFrame = _frameFactory.ValidateFrame(rawFrame, 150);

        // Assert - Frame passes validation
        validatedFrame.Should().NotBeNull("DF 0 is AP mode, no CRC validation, factory always accepts");
        validatedFrame!.IcaoAddress.Should().Be("4BCE08");
        validatedFrame.WasCorrected.Should().BeFalse("no bit error correction attempted for AP mode");

        // Act - MessageParser accepts valid RI field
        ModeSMessage? message = _parser.ParseMessage(validatedFrame);

        // Assert - Parser correctly accepts RI=12 as maximum airspeed indicator
        message.Should().NotBeNull("RI value 12 is valid (Maximum airspeed 300-600 knots)");
        ShortAirAirSurveillance shortAirAirMessage = message.Should().BeOfType<ShortAirAirSurveillance>().Subject;
        shortAirAirMessage.ReplyInformation.Should().Be(AcasReplyInformation.MaximumAirspeed300To600Knots);
    }
}
