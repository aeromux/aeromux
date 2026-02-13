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
/// Tests for BDS 6,0 - Heading and Speed Report
/// Based on "The 1090MHz Riddle" Chapter 17.3, Pages 132-134
/// </summary>
public class Bds60HeadingAndSpeedTests
{
    private readonly MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF21_Bds60_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds60, "message structure and validation rules match BDS 6,0");
    }

    [Fact]
    public void ParseMessage_DF21_Bds60_MagneticHeading110_39()
    {
        // Arrange
        // Magnetic heading: 628 × (90/512) = 110.39 degrees
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.MagneticHeading.Should().BeApproximately(110.39, 0.01,
            "magnetic heading = 628 × (90/512) ≈ 110.39 degrees");
    }

    [Fact]
    public void ParseMessage_DF21_Bds60_IndicatedAirspeed259()
    {
        // Arrange
        // Indicated airspeed: 259 × 1 = 259 kt
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.IndicatedAirspeed.Should().Be(259, "indicated airspeed = 259 × 1 = 259 kt");
    }

    [Fact]
    public void ParseMessage_DF21_Bds60_MachNumber0_7()
    {
        // Arrange
        // Mach number: 175 × 0.004 = 0.7
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.MachNumber.Should().BeApproximately(0.7, 0.001,
            "Mach number = 175 × 0.004 = 0.7");
    }

    [Fact]
    public void ParseMessage_DF21_Bds60_BarometricVerticalRateNegative2144()
    {
        // Arrange
        // Barometric altitude rate: -67 × 32 = -2144 ft/min
        // Two's complement: 445 - 512 = -67
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.BarometricVerticalRate.Should().Be(-2144,
            "barometric vertical rate = -67 × 32 = -2144 ft/min");
    }

    [Fact]
    public void ParseMessage_DF21_Bds60_InertialVerticalRateNegative2016()
    {
        // Arrange
        // Inertial vertical velocity: -63 × 32 = -2016 ft/min
        // Two's complement: 449 - 512 = -63
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.InertialVerticalRate.Should().Be(-2016,
            "inertial vertical velocity = -63 × 32 = -2016 ft/min");
    }

    [Fact]
    public void ParseMessage_DF21_Bds60_AllFieldsValid()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds60_HeadingAndSpeed_80004A)
            .WithIcaoAddress("80004A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;

        // All fields should be valid (status bits = 1)
        bds60.MagneticHeading.Should().NotBeNull("magnetic heading status bit is 1");
        bds60.IndicatedAirspeed.Should().NotBeNull("indicated airspeed status bit is 1");
        bds60.MachNumber.Should().NotBeNull("Mach number status bit is 1");
        bds60.BarometricVerticalRate.Should().NotBeNull("barometric vertical rate status bit is 1");
        bds60.InertialVerticalRate.Should().NotBeNull("inertial vertical rate status bit is 1");
    }

    // ========================================
    // Real Capture Tests
    // ========================================

    [Fact]
    public void ParseMessage_DF20_Bds60_RealCapture_760918_Heading99_IAS267_BaroRateNeg160()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_760918_BDS60_A)
            .WithIcaoAddress("760918")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds60, "message structure and validation rules match BDS 6,0");
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.MagneticHeading.Should().BeApproximately(99.0, 0.2, "magnetic heading from real capture");
        bds60.IndicatedAirspeed.Should().Be(267, "IAS from real capture");
        bds60.MachNumber.Should().BeApproximately(0.820, 0.001, "Mach number from real capture");
        bds60.BarometricVerticalRate.Should().Be(-160, "barometric rate from real capture");
        bds60.InertialVerticalRate.Should().Be(-32, "geometric rate from real capture");
    }

    [Fact]
    public void ParseMessage_DF20_Bds60_RealCapture_760918_Heading99_IAS267_BaroRateNeg64()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_760918_BDS60_B)
            .WithIcaoAddress("760918")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds60, "message structure and validation rules match BDS 6,0");
        reply.BdsData.Should().NotBeNull();

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.MagneticHeading.Should().BeApproximately(99.0, 0.2, "magnetic heading from real capture");
        bds60.IndicatedAirspeed.Should().Be(267, "IAS from real capture");
        bds60.MachNumber.Should().BeApproximately(0.820, 0.001, "Mach number from real capture");
        bds60.BarometricVerticalRate.Should().Be(-64, "barometric rate from real capture");
        bds60.InertialVerticalRate.Should().Be(-32, "geometric rate from real capture");
    }
}
