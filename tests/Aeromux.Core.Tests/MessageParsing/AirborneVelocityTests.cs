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
/// Tests for parsing Airborne Velocity messages (TC 19).
/// </summary>
public class AirborneVelocityTests
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AirborneVel_4BB027_Descending, "4BB027", 389,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_73806C_Climbing, "73806C", 413,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_4D2407_Level, "4D2407", 483,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_39CEAD_Level, "39CEAD", 400,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_3C4AD7_Level, "3C4AD7", 404,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    public void ParseMessage_DF17_TC19_AirborneVelocity_Velocity(
        string hexFrame,
        string expectedIcao,
        int expectedVelocity,
        VelocityType expectedVelocityType,
        VelocitySubtype expectedVelocitySubtype)
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
        AirborneVelocity? velocity = message.Should().BeOfType<AirborneVelocity>().Subject;
        velocity.Velocity.Should().NotBeNull();
        velocity.Velocity!.Knots.Should().Be(expectedVelocity);
        velocity.Velocity!.Type.Should().Be(expectedVelocityType);
        velocity.Subtype.Should().Be(expectedVelocitySubtype);
    }

    [Theory]
    [InlineData(RealFrames.AirborneVel_4BB027_Descending, "4BB027", -1984)]
    [InlineData(RealFrames.AirborneVel_73806C_Climbing, "73806C", 896)]
    [InlineData(RealFrames.AirborneVel_4D2407_Level, "4D2407", 0)]
    [InlineData(RealFrames.AirborneVel_39CEAD_Level, "39CEAD", 0)]
    [InlineData(RealFrames.AirborneVel_3C4AD7_Level, "3C4AD7", 0)]
    public void ParseMessage_DF17_TC19_AirborneVelocity_VerticalRate(
        string hexFrame,
        string expectedIcao,
        int expectedVerticalRate)
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
        AirborneVelocity? velocity = message.Should().BeOfType<AirborneVelocity>().Subject;
        velocity.Velocity.Should().NotBeNull();
        velocity.VerticalRate.Should().Be(expectedVerticalRate);
    }

    [Theory]
    [InlineData(RealFrames.AirborneVel_4BB027_Descending, "4BB027", 294)]
    [InlineData(RealFrames.AirborneVel_73806C_Climbing, "73806C", 317)]
    [InlineData(RealFrames.AirborneVel_4D2407_Level, "4D2407", 117)]
    [InlineData(RealFrames.AirborneVel_39CEAD_Level, "39CEAD", 276)]
    [InlineData(RealFrames.AirborneVel_3C4AD7_Level, "3C4AD7", 299)]
    public void ParseMessage_DF17_TC19_AirborneVelocity_Heading(
        string hexFrame,
        string expectedIcao,
        double expectedHeading)
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
        AirborneVelocity? velocity = message.Should().BeOfType<AirborneVelocity>().Subject;
        velocity.Velocity.Should().NotBeNull();
        velocity.Heading.Should().BeInRange(expectedHeading - 1, expectedHeading + 1);
    }

    [Theory]
    [InlineData(RealFrames.AirborneVel_4BB027_Descending, NavigationAccuracyCategoryVelocity.LessThan3MetersPerSecond)]
    [InlineData(RealFrames.AirborneVel_73806C_Climbing, NavigationAccuracyCategoryVelocity.LessThan10MetersPerSecond)]
    [InlineData(RealFrames.AirborneVel_4D2407_Level, NavigationAccuracyCategoryVelocity.LessThan10MetersPerSecond)]
    [InlineData(RealFrames.AirborneVel_39CEAD_Level, NavigationAccuracyCategoryVelocity.LessThan10MetersPerSecond)]
    public void ParseMessage_DF17_TC19_AirborneVelocity_NACv(
        string hexFrame,
        NavigationAccuracyCategoryVelocity expectedNACv)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirborneVelocity? velocity = message.Should().BeOfType<AirborneVelocity>().Subject;
        velocity.NACv.Should().Be(expectedNACv);
    }

    /// <summary>
    /// TC 19 subtype 2 (supersonic ground speed) with moderate velocity components.
    /// Vew=200, Vns=150 → with ×4 multiplier: vx=796, vy=596 → magnitude ≈ 994 knots.
    /// Frame: 8D AAAAAA 9A 08 C8 12 C0 00 00 000000
    /// </summary>
    [Fact]
    public void ParseMessage_DF17_TC19_SupersonicGroundSpeed_ModerateVelocity_Parses()
    {
        // Arrange — TC 19 subtype 2 frame with Vew=200, Vns=150 (supersonic multiplier ×4)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData("8DAAAAAA9A08C812C0000000000000")
            .WithIcaoAddress("AAAAAA")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert — magnitude ~994 knots, well within 4096 cap
        message.Should().NotBeNull();
        AirborneVelocity? velocity = message.Should().BeOfType<AirborneVelocity>().Subject;
        velocity.Velocity.Should().NotBeNull();
        velocity.Velocity!.Knots.Should().BeInRange(990, 1000);
        velocity.Subtype.Should().Be(VelocitySubtype.GroundSpeedSupersonic);
    }

    /// <summary>
    /// TC 19 subtype 2 (supersonic ground speed) with both axes maxed.
    /// Vew=1023, Vns=1023 → with ×4 multiplier: magnitude ≈ 5781 knots → exceeds 4096, rejected as corrupt.
    /// Frame: 8D AAAAAA 9A 0B FF 7F E0 00 00 000000
    /// </summary>
    [Fact]
    public void ParseMessage_DF17_TC19_SupersonicGroundSpeed_BothAxesMaxed_ReturnsNull()
    {
        // Arrange — TC 19 subtype 2 frame with Vew=1023, Vns=1023 (both maxed)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData("8DAAAAAA9A0BFF7FE0000000000000")
            .WithIcaoAddress("AAAAAA")
            .Build();

        // Act — magnitude ~5781 knots exceeds 4096, rejected as corrupt data
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().BeNull();
    }
}
