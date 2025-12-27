using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Comm-B Identity Reply messages (DF 21).
/// Covers basic DF 21 fields: squawk code, flight status, downlink request, and utility message.
/// BDS-specific decoding (BdsCode, BdsData) is intentionally out of scope.
/// </summary>
public class CommBIdentityReplyTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    // ========================================
    // Basic Message Fields
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_4D2407, "4D2407", DownlinkFormat.CommBIdentityReply)]
    [InlineData(RealFrames.CommB_Identity_49D414, "49D414", DownlinkFormat.CommBIdentityReply)]
    public void ParseMessage_DF21_CommB_BasicFields(
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
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.IcaoAddress.Should().Be(expectedIcao);
        reply.DownlinkFormat.Should().Be(expectedDF);
    }

    // ========================================
    // Squawk Code
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_4D2407, "6415")]
    [InlineData(RealFrames.CommB_Identity_49D414, "1420")]
    public void ParseMessage_DF21_CommB_SquawkCode(
        string hexFrame,
        string expectedSquawk)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.SquawkCode.Should().NotBeNull();
        reply.SquawkCode.Should().Be(expectedSquawk, "Squawk code is a 4-digit octal identifier");
    }

    // ========================================
    // Flight Status
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_4D2407, FlightStatus.AirborneNormal)]
    [InlineData(RealFrames.CommB_Identity_49D414, FlightStatus.AirborneNormal)]
    public void ParseMessage_DF21_CommB_FlightStatus(
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
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.FlightStatus.Should().Be(expectedFlightStatus, "Both test frames are airborne with no alert or SPI");
    }

    // ========================================
    // Downlink Request
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_4D2407, 0)]
    [InlineData(RealFrames.CommB_Identity_49D414, 0)]
    public void ParseMessage_DF21_CommB_DownlinkRequest(
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
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.DownlinkRequest.Should().Be(expectedDownlinkRequest, "No downlink request in test frames");
    }

    // ========================================
    // Utility Message
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_4D2407, 0)]
    [InlineData(RealFrames.CommB_Identity_49D414, 0)]
    public void ParseMessage_DF21_CommB_UtilityMessage(
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
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.UtilityMessage.Should().Be(expectedUtilityMessage, "No utility message in test frames");
    }

    // ========================================
    // BDS Fields (Not Tested - Out of Scope)
    // ========================================

    // NOTE: BdsCode and BdsData are intentionally not tested here.
    // BDS register decoding is a complex, optional feature that would
    // require dedicated test coverage if needed in the future.
    // These tests focus on core DF 21 message fields only.
}
