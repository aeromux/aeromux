using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for BDS 2,0 - Aircraft Identification
/// Based on "The 1090MHz Riddle" Chapter 16.3, Pages 121-122
/// </summary>
public class Bds20AircraftIdentificationTests
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF20_Bds20_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds20_AircraftId_000083_KLM1017)
            .WithIcaoAddress("000083")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds20, "MB field starts with 0x20 indicating BDS 2,0");
    }

    [Fact]
    public void ParseMessage_DF20_Bds20_CallsignKLM1017()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds20_AircraftId_000083_KLM1017)
            .WithIcaoAddress("000083")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds20AircraftIdentification? bds20 = reply.BdsData.Should().BeOfType<Bds20AircraftIdentification>().Subject;
        bds20.Callsign.Should().Be("KLM1017", "decoded callsign from 8 x 6-bit characters should be 'KLM1017'");
    }

    [Fact]
    public void ParseMessage_DF20_Bds20_CallsignDecoding()
    {
        // Arrange
        // MB Binary: 0010 0000 001011 001100 001101 110001 110000 110001 110111 100000
        // Expected: BDS=0x20, K, L, M, 1, 0, 1, 7, [space]
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds20_AircraftId_000083_KLM1017)
            .WithIcaoAddress("000083")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds20AircraftIdentification? bds20 = reply.BdsData.Should().BeOfType<Bds20AircraftIdentification>().Subject;

        // Callsign should be trimmed (trailing space removed)
        bds20.Callsign.Should().NotContain(" ", "callsign should be trimmed");
        bds20.Callsign.Length.Should().Be(7, "KLM1017 has 7 characters");
        bds20.Callsign.Should().StartWith("KLM", "airline code");
        bds20.Callsign.Should().EndWith("1017", "flight number");
    }
}
