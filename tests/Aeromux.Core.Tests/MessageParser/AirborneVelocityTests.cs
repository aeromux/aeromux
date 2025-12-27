using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.Extensions;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Airborne Velocity messages (TC 19).
/// </summary>
public class AirborneVelocityTests
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AirborneVel_4BB027_Descending, "4BB027", 389,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_73806C_Climbing, "73806C", 413,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_4D2407_Level, "4D2407", 483,
        VelocityType.GroundSpeed, VelocitySubtype.GroundSpeedSubsonic)]
    [InlineData(RealFrames.AirborneVel_39CEAD_Level, "39CEAD", 400,
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
        velocity.Velocity.Knots.Should().Be(expectedVelocity);
        velocity.Velocity.Type.Should().Be(expectedVelocityType);
        velocity.Subtype.Should().Be(expectedVelocitySubtype);
    }

    [Theory]
    [InlineData(RealFrames.AirborneVel_4BB027_Descending, "4BB027", -1984)]
    [InlineData(RealFrames.AirborneVel_73806C_Climbing, "73806C", 896)]
    [InlineData(RealFrames.AirborneVel_4D2407_Level, "4D2407", 0)]
    [InlineData(RealFrames.AirborneVel_39CEAD_Level, "39CEAD", 0)]
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
}
