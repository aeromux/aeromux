using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Comm-B Altitude Reply
/// </summary>
public class SurveillanceAltitudeReplyTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.Surveillance_Altitude_4BA913, "4BA913", 36000, AltitudeType.Barometric)]
    [InlineData(RealFrames.Surveillance_Altitude_49D414, "49D414", 35000, AltitudeType.Barometric)]
    public void ParseMessage_DF4_Surveillance_AltitudeReply_Altitude(
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
        SurveillanceAltitudeReply reply = message.Should().BeOfType<SurveillanceAltitudeReply>().Subject;
        reply.Altitude.Should().NotBeNull();
        reply.Altitude.Feet.Should().Be(expectedAltitude);
        reply.Altitude.Type.Should().Be(expectedAltitudeType);
    }
}
