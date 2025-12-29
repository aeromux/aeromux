using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for BDS 4,0 - Selected Vertical Intention
/// Based on "The 1090MHz Riddle" Chapter 17.1, Pages 128-129
/// </summary>
public class Bds40SelectedVerticalIntentionTests
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF21_Bds40_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds40_VerticalIntention_8001EB)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds40, "message structure and validation rules match BDS 4,0");
    }

    [Fact]
    public void ParseMessage_DF21_Bds40_McpSelectedAltitude24000()
    {
        // Arrange
        // MCP/FCU selected altitude: 1500 × 16 ft = 24000 ft
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds40_VerticalIntention_8001EB)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.McpSelectedAltitude.Should().Be(24000, "MCP/FCU altitude = 1500 × 16 ft = 24000 ft");
    }

    [Fact]
    public void ParseMessage_DF21_Bds40_FmsSelectedAltitude24000()
    {
        // Arrange
        // FMS selected altitude: 1500 × 16 ft = 24000 ft
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds40_VerticalIntention_8001EB)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.FmsSelectedAltitude.Should().Be(24000, "FMS altitude = 1500 × 16 ft = 24000 ft");
    }

    [Fact]
    public void ParseMessage_DF21_Bds40_BarometricPressure1013_2()
    {
        // Arrange
        // Barometric pressure: (2132 × 0.1) + 800 = 1013.2 mb
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds40_VerticalIntention_8001EB)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.BarometricPressureSetting.Should().BeApproximately(1013.2, 0.5,
            "barometric pressure = (2132 × 0.1) + 800 = 1013.2 mb");
    }

    [Fact]
    public void ParseMessage_DF21_Bds40_AllFieldsValid()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds40_VerticalIntention_8001EB)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;

        // All three fields should be valid (status bits = 1)
        bds40.McpSelectedAltitude.Should().NotBeNull("MCP/FCU status bit is 1");
        bds40.FmsSelectedAltitude.Should().NotBeNull("FMS status bit is 1");
        bds40.BarometricPressureSetting.Should().NotBeNull("barometric pressure status bit is 1");
    }
}
