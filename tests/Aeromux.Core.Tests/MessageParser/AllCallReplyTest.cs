using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing All-Call Reply
/// </summary>
public class AllCallReplyTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AllCall_4D2407, "4D2407")]
    [InlineData(RealFrames.AllCall_471F87, "471F87")]
    [InlineData(RealFrames.AllCall_80073B, "80073B")]
    public void ParseMessage_DF11_AllCallReply_Icao(
        string hexFrame,
        string expectedIcao)
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
        AllCallReply reply = message.Should().BeOfType<AllCallReply>().Subject;
        reply.ExtractedIcao.Should().NotBeNull();
        reply.ExtractedIcao.Should().Be(expectedIcao);
    }

    [Theory]
    [InlineData(RealFrames.AllCall_4D2407, "4D2407", TransponderCapability.Level2PlusAirborne)]
    [InlineData(RealFrames.AllCall_471F87, "471F87", TransponderCapability.Level2PlusAirborne)]
    [InlineData(RealFrames.AllCall_80073B, "80073B", TransponderCapability.Level2PlusAirborne)]
    public void ParseMessage_DF11_AllCallReply_Capability(
        string hexFrame,
        string expectedIcao,
        TransponderCapability expectedCapability)
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
        AllCallReply reply = message.Should().BeOfType<AllCallReply>().Subject;
        reply.Capability.Should().Be(expectedCapability);
    }
}
