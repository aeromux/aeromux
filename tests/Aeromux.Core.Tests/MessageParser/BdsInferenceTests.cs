using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for BDS Inference Logic - Distinguishing between similar BDS codes
/// Based on "The 1090MHz Riddle" Chapter 19.3, Pages 143-144
/// </summary>
public class BdsInferenceTests
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF20_BdsInference_InfersAsBds60()
    {
        // Arrange
        // This message has ambiguous structure (could be BDS 5,0 or 6,0)
        // BDS 5,0 fails: GS-TAS difference is 392 kt (exceeds 200 kt threshold)
        // BDS 6,0 passes: IAS-CAS difference is 0 kt (within 15 kt threshold)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.BdsInference_000183_Is60)
            .WithIcaoAddress("000183")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds60,
            "should infer BDS 6,0 because BDS 5,0 has unreasonable GS-TAS difference (392 kt > 200 kt threshold)");
    }

    [Fact]
    public void ParseMessage_DF20_BdsInference_Bds60HasValidData()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.BdsInference_000183_Is60)
            .WithIcaoAddress("000183")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull("BDS 6,0 should have valid decoded data");

        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;

        // From book: IAS = 249 kt, Mach = 0.788
        bds60.IndicatedAirspeed.Should().Be(249, "IAS from BDS 6,0 decoding");
        bds60.MachNumber.Should().BeApproximately(0.788, 0.001, "Mach number from BDS 6,0 decoding");
    }

    [Fact]
    public void ParseMessage_DF21_BdsInference_InfersAsBds50()
    {
        // Arrange
        // This message has ambiguous structure (could be BDS 5,0 or 6,0)
        // DF 21 does not include altitude, so Mach-to-CAS validation cannot be used
        // Inference requires ADS-B reference data (GS ≈ 320 kt, Track ≈ 250°)
        // BDS 5,0 matches ADS-B reference
        // BDS 6,0 does not match ADS-B reference
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.BdsInference_8001EB_Is50)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds50,
            "should infer BDS 5,0 based on match with ADS-B reference data (GS ≈ 320 kt, Track ≈ 250°)");
    }

    [Fact]
    public void ParseMessage_DF21_BdsInference_Bds50HasValidData()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.BdsInference_8001EB_Is50)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull("BDS 5,0 should have valid decoded data");

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;

        // From book: GS = 322 kt (close to ADS-B 320 kt), Track = 250° (matches ADS-B)
        bds50.GroundSpeed.Should().Be(322,
            "ground speed should be 322 kt, which is close to ADS-B reference of 320 kt");
        bds50.TrackAngle.Should().BeApproximately(250, 1,
            "track angle should be close to ADS-B reference of 250°");
    }

    [Fact]
    public void ParseMessage_BdsInference_ExplainsAmbiguity()
    {
        // Arrange
        // Both test messages have structures that match multiple BDS codes
        ValidatedFrame frame60 = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.BdsInference_000183_Is60)
            .WithIcaoAddress("000183")
            .Build();

        ValidatedFrame frame50 = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.BdsInference_8001EB_Is50)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message60 = _parser.ParseMessage(frame60);
        ModeSMessage? message50 = _parser.ParseMessage(frame50);

        // Assert
        message60.Should().NotBeNull();
        message50.Should().NotBeNull();

        // Both messages are successfully decoded despite structural ambiguity
        ((CommBAltitudeReply)message60!).BdsCode.Should().Be(BdsCode.Bds60);
        ((CommBIdentityReply)message50!).BdsCode.Should().Be(BdsCode.Bds50);

        // The inference logic successfully distinguishes between BDS 5,0 and 6,0
        // using different validation strategies:
        // - DF 20 (with altitude): Use Mach-to-CAS conversion validation
        // - DF 21 (without altitude): Use ADS-B reference data validation
    }
}
