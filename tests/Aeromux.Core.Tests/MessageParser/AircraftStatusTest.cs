using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing AircraftStatus
/// </summary>
public class AircraftStatusTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.EmergencyStatus_4D2407, "4D2407", "6415")]
    [InlineData(RealFrames.EmergencyStatus_503D74, "503D74", "3254")]
    [InlineData(RealFrames.EmergencyStatus_80073B, "80073B", "3205")]
    public void ParseMessage_DF17_TC28_AircraftEmergencyStatus_Squawk(
        string hexFrame,
        string expectedIcao,
        string expectedSquawk)
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
        AircraftStatus aircraftStatus = message.Should().BeOfType<AircraftStatus>().Subject;
        aircraftStatus.SquawkCode.Should().NotBeNull();
        aircraftStatus.SquawkCode.Should().Be(expectedSquawk);
    }

    [Theory]
    [InlineData(RealFrames.EmergencyStatus_4D2407, "4D2407", EmergencyState.NoEmergency)]
    [InlineData(RealFrames.EmergencyStatus_503D74, "503D74", EmergencyState.NoEmergency)]
    [InlineData(RealFrames.EmergencyStatus_80073B, "80073B", EmergencyState.NoEmergency)]
    public void ParseMessage_DF17_TC28_AircraftEmergencyStatus_EmergencyState(
        string hexFrame,
        string expectedIcao,
        EmergencyState expectedState)
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
        AircraftStatus aircraftStatus = message.Should().BeOfType<AircraftStatus>().Subject;
        aircraftStatus.EmergencyState.Should().NotBeNull();
        aircraftStatus.EmergencyState.Should().Be(expectedState);
    }
}
