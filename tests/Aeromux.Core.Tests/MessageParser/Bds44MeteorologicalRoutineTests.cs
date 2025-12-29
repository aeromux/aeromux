using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for BDS 4,4 - Meteorological Routine Air Report
/// Based on "The 1090MHz Riddle" Chapter 18.1, Pages 136-137
/// </summary>
public class Bds44MeteorologicalRoutineTests
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF20_Bds44_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds44, "message structure and validation rules match BDS 4,4");
    }

    [Fact]
    public void ParseMessage_DF20_Bds44_FomIsIns()
    {
        // Arrange
        // FOM: 1 = INS (Inertial Navigation System)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds44MeteorologicalRoutine? bds44 = reply.BdsData.Should().BeOfType<Bds44MeteorologicalRoutine>().Subject;
        bds44.FigureOfMerit.Should().Be(1, "FOM = 1 indicates INS data source");
    }

    [Fact]
    public void ParseMessage_DF20_Bds44_WindSpeed22()
    {
        // Arrange
        // Wind speed: 22 × 1 = 22 kt
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds44MeteorologicalRoutine? bds44 = reply.BdsData.Should().BeOfType<Bds44MeteorologicalRoutine>().Subject;
        bds44.WindSpeed.Should().Be(22, "wind speed = 22 × 1 = 22 kt");
    }

    [Fact]
    public void ParseMessage_DF20_Bds44_WindDirection344_5()
    {
        // Arrange
        // Wind direction: 490 × (180/256) = 344.53 degrees
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds44MeteorologicalRoutine? bds44 = reply.BdsData.Should().BeOfType<Bds44MeteorologicalRoutine>().Subject;
        bds44.WindDirection.Should().BeApproximately(344.5, 0.1,
            "wind direction = 490 × (180/256) ≈ 344.53 degrees");
    }

    [Fact]
    public void ParseMessage_DF20_Bds44_TemperatureNegative48_75()
    {
        // Arrange
        // Static air temperature: -195 × 0.25 = -48.75°C
        // Two's complement: 829 - 1024 = -195
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds44MeteorologicalRoutine? bds44 = reply.BdsData.Should().BeOfType<Bds44MeteorologicalRoutine>().Subject;
        bds44.StaticAirTemperature.Should().BeApproximately(-48.75, 0.01,
            "temperature = -195 × 0.25 = -48.75°C");
    }

    [Fact]
    public void ParseMessage_DF20_Bds44_PressureIsNull()
    {
        // Arrange
        // Pressure status bit is 0 (not available)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds44MeteorologicalRoutine? bds44 = reply.BdsData.Should().BeOfType<Bds44MeteorologicalRoutine>().Subject;
        bds44.Pressure.Should().BeNull("pressure status bit is 0 (not available)");
    }

    [Fact]
    public void ParseMessage_DF20_Bds44_ValidAndInvalidFields()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds44_Meteorological_000169)
            .WithIcaoAddress("000169")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds44MeteorologicalRoutine? bds44 = reply.BdsData.Should().BeOfType<Bds44MeteorologicalRoutine>().Subject;

        // Valid fields (status = 1)
        bds44.FigureOfMerit.Should().NotBeNull("FOM is valid");
        bds44.WindSpeed.Should().NotBeNull("wind speed status bit is 1");
        bds44.WindDirection.Should().NotBeNull("wind direction status bit is 1");
        bds44.StaticAirTemperature.Should().NotBeNull("temperature status bit is 1");

        // Invalid fields (status = 0)
        bds44.Pressure.Should().BeNull("pressure status bit is 0");
    }
}
