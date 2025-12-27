using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Surveillance Identity Reply
/// </summary>
public class SurveillanceIdentityReplyTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.Surveillance_Identity_49D414, "4D2407", "1420")]
    [InlineData(RealFrames.Surveillance_Identity_80073B, "80073B", "3205")]
    public void ParseMessage_DF5_Surveillance_IdentityReply_Squawk(
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
        SurveillanceIdentityReply reply = message.Should().BeOfType<SurveillanceIdentityReply>().Subject;
        reply.SquawkCode.Should().NotBeNull();
        reply.SquawkCode.Should().Be(expectedSquawk);
    }
}
