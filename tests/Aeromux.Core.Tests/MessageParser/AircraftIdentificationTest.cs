using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Aircraft Identification
/// </summary>
public class AircraftIdentificationTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

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
