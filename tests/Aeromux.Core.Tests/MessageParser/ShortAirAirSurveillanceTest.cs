using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Short Air-Air Surveillance (DF 0 ACAS messages)
/// </summary>
public class ShortAirAirSurveillanceTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();
    private readonly Aeromux.Core.ModeS.ValidatedFrameFactory _frameFactory = new();

    [Theory]
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", 33000, AltitudeType.Barometric)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", 37850, AltitudeType.Barometric)]
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
    [InlineData(RealFrames.ShortAirAir_4D2407, "4D2407", AcasReplyInformation.VerticalOnlyRA)]
    [InlineData(RealFrames.ShortAirAir_73806C, "73806C", AcasReplyInformation.VerticalOnlyRA)]
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
    public void ParseMessage_DF0_InvalidRI_FactoryAccepts_ParserRejects()
    {
        // Frame ShortAirAir_4BCE08 has invalid RI value of 12 (only 0, 2, 3, 4 are valid per ICAO spec)
        // DF 0 is AP mode, so ValidatedFrameFactory accepts it (no CRC validation possible)
        // However, MessageParser should reject it due to semantic validation (invalid RI)

        byte[] frameBytes = Convert.FromHexString(RealFrames.ShortAirAir_4BCE08);
        RawFrame rawFrame = new(frameBytes, DateTime.UtcNow);

        // Act - ValidatedFrameFactory accepts the frame (AP mode)
        ValidatedFrame? validatedFrame = _frameFactory.ValidateFrame(rawFrame, 150);

        // Assert - Frame passes CRC (AP mode always passes)
        validatedFrame.Should().NotBeNull("DF 0 is AP mode, no CRC validation, factory always accepts");
        validatedFrame!.IcaoAddress.Should().Be("4BCE08");
        validatedFrame.WasCorrected.Should().BeFalse("no bit error correction attempted for AP mode");

        // Act - MessageParser rejects due to invalid RI field
        ModeSMessage? message = _parser.ParseMessage(validatedFrame);

        // Assert - Parser correctly rejects invalid ACAS field
        message.Should().BeNull("RI value 12 is invalid per AcasReplyInformation enum");
    }
}
