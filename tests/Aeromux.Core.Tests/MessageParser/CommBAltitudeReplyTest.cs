using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Comm-B Altitude Reply messages (DF 20).
/// Covers basic DF 20 fields: altitude, flight status, downlink request, and utility message.
/// BDS-specific decoding (BdsCode, BdsData) is intentionally out of scope.
/// </summary>
public class CommBAltitudeReplyTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    // ========================================
    // Basic Message Fields
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Altitude_4D2407, "4D2407", DownlinkFormat.CommBAltitudeReply)]
    [InlineData(RealFrames.CommB_Altitude_80073B, "80073B", DownlinkFormat.CommBAltitudeReply)]
    public void ParseMessage_DF20_CommB_BasicFields(
        string hexFrame,
        string expectedIcao,
        DownlinkFormat expectedDF)
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
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.IcaoAddress.Should().Be(expectedIcao);
        reply.DownlinkFormat.Should().Be(expectedDF);
    }

    // ========================================
    // Altitude
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Altitude_4D2407, 33000)]
    [InlineData(RealFrames.CommB_Altitude_80073B, 39975)]
    public void ParseMessage_DF20_CommB_Altitude(
        string hexFrame,
        int expectedAltitude)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.Altitude.Should().NotBeNull();
        reply.Altitude!.Feet.Should().Be(expectedAltitude);
        reply.Altitude!.Type.Should().Be(AltitudeType.Barometric, "DF 20 altitude is always barometric");
    }

    // ========================================
    // Flight Status
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Altitude_4D2407, FlightStatus.AirborneNormal)]
    [InlineData(RealFrames.CommB_Altitude_80073B, FlightStatus.AirborneNormal)]
    public void ParseMessage_DF20_CommB_FlightStatus(
        string hexFrame,
        FlightStatus expectedFlightStatus)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.FlightStatus.Should().Be(expectedFlightStatus, "Both test frames are airborne with no alert or SPI");
    }

    // ========================================
    // Downlink Request
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Altitude_4D2407, 0)]
    [InlineData(RealFrames.CommB_Altitude_80073B, 0)]
    public void ParseMessage_DF20_CommB_DownlinkRequest(
        string hexFrame,
        int expectedDownlinkRequest)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.DownlinkRequest.Should().Be(expectedDownlinkRequest, "No downlink request in test frames");
    }

    // ========================================
    // Utility Message
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Altitude_4D2407, 0)]
    [InlineData(RealFrames.CommB_Altitude_80073B, 0)]
    public void ParseMessage_DF20_CommB_UtilityMessage(
        string hexFrame,
        int expectedUtilityMessage)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.UtilityMessage.Should().Be(expectedUtilityMessage, "No utility message in test frames");
    }

    // ========================================
    // BDS Fields (Not Tested - Out of Scope)
    // ========================================

    // NOTE: BdsCode and BdsData are intentionally not tested here.
    // BDS register decoding is a complex, optional feature that would
    // require dedicated test coverage if needed in the future.
    // These tests focus on core DF 20 message fields only.
}
