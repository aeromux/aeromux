using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.Extensions;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Airborne Position messages (TC 9-18, 20-22).
/// Note: These tests focus on message parsing, not CPR decoding.
/// Position will be null unless CPR decoder has paired even/odd frames.
/// </summary>
public class AirbornePositionTests
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AirbornePos_73806C_Even, "73806C", 37600, AltitudeType.Barometric)]
    [InlineData(RealFrames.AirbornePos_73806C_Odd, "73806C",37600, AltitudeType.Barometric)]
    [InlineData(RealFrames.AirbornePos_80073B_Even, "80073B",39975, AltitudeType.Barometric)]
    [InlineData(RealFrames.AirbornePos_80073B_Odd, "80073B",39975, AltitudeType.Barometric)]
    public void ParseMessage_DF17_TC12_AirbornePosition_Altitude(
        string hexFrame,
        string expectedIcao,
        int expectedAltitudeFeet,
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
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.Altitude.Should().NotBeNull();
        position.Altitude!.Feet.Should().Be(expectedAltitudeFeet);
        position.Altitude!.Type.Should().Be(expectedAltitudeType);
    }

    [Theory]
    [InlineData(RealFrames.AirbornePos_73806C_Even, "73806C")]
    [InlineData(RealFrames.AirbornePos_73806C_Odd, "73806C")]
    [InlineData(RealFrames.AirbornePos_80073B_Even, "80073B")]
    [InlineData(RealFrames.AirbornePos_80073B_Odd, "80073B")]
    public void ParseMessage_DF17_AirbornePosition_Position_NullIfSingleFrame(
        string hexFrame,
        string expectedIcao)
    {
        // Arrange - Single frame without paired frame
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress(expectedIcao)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert - Position should be null (CPR decoding needs paired frames)
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.Position.Should().BeNull("CPR decoding requires both even and odd frames");
    }

    [Theory]
    [InlineData(
        RealFrames.AirbornePos_73806C_Even,
        RealFrames.AirbornePos_73806C_Odd,
        "73806C",
        47.0, 48.0,   // Latitude range
        20.0, 21.0)]  // Longitude range
    [InlineData(
        RealFrames.AirbornePos_80073B_Even,
        RealFrames.AirbornePos_80073B_Odd,
        "80073B",
        46.0, 47.0,   // Latitude range
        19.0, 20.0)]  // Longitude range
    public void ParseMessage_DF17_AirbornePosition_BothFrames_DecodesPosition(
        string evenFrameHex,
        string oddFrameHex,
        string expectedIcao,
        double expectedLatMin,
        double expectedLatMax,
        double expectedLonMin,
        double expectedLonMax)
    {
        // Arrange - Parse both even and odd frames
        ValidatedFrame evenFrame = new ValidatedFrameBuilder()
            .WithHexData(evenFrameHex)
            .WithIcaoAddress(expectedIcao)
            .Build();

        ValidatedFrame oddFrame = new ValidatedFrameBuilder()
            .WithHexData(oddFrameHex)
            .WithIcaoAddress(expectedIcao)
            .Build();

        // Act
        ModeSMessage? evenMessage = _parser.ParseMessage(evenFrame);
        ModeSMessage? oddMessage = _parser.ParseMessage(oddFrame);

        // Assert - At least one should have position decoded
        evenMessage.Should().NotBeNull();
        oddMessage.Should().NotBeNull();

        AirbornePosition? evenPos = evenMessage.Should().BeOfType<AirbornePosition>().Subject;
        AirbornePosition? oddPos = oddMessage.Should().BeOfType<AirbornePosition>().Subject;

        // After parsing both, at least the second one should have position
        (evenPos.Position is not null || oddPos.Position is not null).Should().BeTrue(
            "After parsing both even and odd frames, CPR decoder should provide position");

        // Verify position if available
        AirbornePosition positionMessage = oddPos.Position is not null ? oddPos : evenPos;
        if (positionMessage.Position is not null)
        {
            positionMessage.Position.Latitude.Should().BeInRange(expectedLatMin, expectedLatMax);
            positionMessage.Position.Longitude.Should().BeInRange(expectedLonMin, expectedLonMax);
        }
    }

    [Theory]
    [InlineData(RealFrames.AirbornePos_80073B_Even, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.AirbornePos_80073B_Odd, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.AirbornePos_73806C_Even, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.AirbornePos_73806C_Odd, AntennaFlag.DiversityAntenna)]
    public void ParseMessage_DF17_AirbornePosition_AntennaField(
        string hexFrame,
        AntennaFlag expectedAntennaFlag)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.Antenna.Should().Be(expectedAntennaFlag);
    }

    [Fact]
    public void ParseMessage_DF17_AirbornePosition_SingleAntenna_ParsesCorrectly()
    {
        // Arrange - Manually construct a frame with SA flag = 1 (SingleAntenna)
        // Based on AirbornePos_80073B_Even but with bit 40 set to 1
        // Original: 8D80073B58CD7331C27497A8A51D
        // Byte 4 (0x58 = 01011000) -> change bit 40 (bit 0 of this byte after TC/SS) to 1
        // New byte 4: 0x59 = 01011001
        string hexFrame = "8D80073B59CD7331C27497";

        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.Antenna.Should().Be(AntennaFlag.SingleAntenna, "SA flag bit 40 is set to 1");
    }

    [Theory]
    [InlineData(RealFrames.AirbornePos_80073B_Even, SurveillanceStatus.NoAlertNoSPI)]
    [InlineData(RealFrames.AirbornePos_80073B_Odd, SurveillanceStatus.NoAlertNoSPI)]
    [InlineData(RealFrames.AirbornePos_73806C_Even, SurveillanceStatus.NoAlertNoSPI)]
    [InlineData(RealFrames.AirbornePos_73806C_Odd, SurveillanceStatus.NoAlertNoSPI)]
    public void ParseMessage_DF17_AirbornePosition_SurveillanceStatus(
        string hexFrame,
        SurveillanceStatus expectedStatus)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.SurveillanceStatus.Should().Be(expectedStatus);
    }

    [Fact]
    public void ParseMessage_DF17_AirbornePosition_PermanentAlert_ParsesCorrectly()
    {
        // Arrange - Manually construct a frame with SS = 1 (PermanentAlert)
        // Based on AirbornePos_80073B_Even but with SS bits 38-39 set to 01
        // Original: 8D80073B58CD7331C27497A8A51D
        // Byte 4 layout: [TC4 TC3 TC2 TC1 TC0 SS1 SS0 SA]
        // Original byte 4: 0x58 = 01011000 (TC=11, SS=00, SA=0)
        // New byte 4:     0x5A = 01011010 (TC=11, SS=01, SA=0)
        string hexFrame = "8D80073B5ACD7331C27497";

        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.SurveillanceStatus.Should().Be(SurveillanceStatus.PermanentAlert, "SS bits 38-39 are set to 01");
    }

    [Fact]
    public void ParseMessage_DF17_AirbornePosition_TemporaryAlert_ParsesCorrectly()
    {
        // Arrange - Manually construct a frame with SS = 2 (TemporaryAlert)
        // Based on AirbornePos_80073B_Even but with SS bits 38-39 set to 10
        // Original: 8D80073B58CD7331C27497A8A51D
        // Byte 4 layout: [TC4 TC3 TC2 TC1 TC0 SS1 SS0 SA]
        // Original byte 4: 0x58 = 01011000 (TC=11, SS=00, SA=0)
        // New byte 4:     0x5C = 01011100 (TC=11, SS=10, SA=0)
        string hexFrame = "8D80073B5CCD7331C27497";

        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.SurveillanceStatus.Should().Be(SurveillanceStatus.TemporaryAlert, "SS bits 38-39 are set to 10");
    }

    [Fact]
    public void ParseMessage_DF17_AirbornePosition_SPI_ParsesCorrectly()
    {
        // Arrange - Manually construct a frame with SS = 3 (SPI)
        // Based on AirbornePos_80073B_Even but with SS bits 38-39 set to 11
        // Original: 8D80073B58CD7331C27497A8A51D
        // Byte 4 layout: [TC4 TC3 TC2 TC1 TC0 SS1 SS0 SA]
        // Original byte 4: 0x58 = 01011000 (TC=11, SS=00, SA=0)
        // New byte 4:     0x5E = 01011110 (TC=11, SS=11, SA=0)
        string hexFrame = "8D80073B5ECD7331C27497";

        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        AirbornePosition? position = message.Should().BeOfType<AirbornePosition>().Subject;
        position.SurveillanceStatus.Should().Be(SurveillanceStatus.SPI, "SS bits 38-39 are set to 11");
    }
}
