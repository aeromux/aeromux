using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParser;

/// <summary>
/// Tests for parsing Target State and Status messages (TC 29).
/// </summary>
public class TargetStateAndStatusTest
{
    private readonly Aeromux.Core.ModeS.MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, "49D414", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.TargetStateStatus_73806C, "73806C", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, "39CEAD", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.TargetStateStatus_71C011, "71C011", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, "86E778", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, "4D2047", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, "8965F3", DownlinkFormat.ExtendedSquitter)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_BasicFields(
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
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.IcaoAddress.Should().Be(expectedIcao);
        targetState.DownlinkFormat.Should().Be(expectedDF);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, TargetStateSubtype.Version2)]
    [InlineData(RealFrames.TargetStateStatus_73806C, TargetStateSubtype.Version2)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, TargetStateSubtype.Version2)]
    [InlineData(RealFrames.TargetStateStatus_71C011, TargetStateSubtype.Version2)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, TargetStateSubtype.Version2)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, TargetStateSubtype.Version2)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, TargetStateSubtype.Version2)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_Subtype(
        string hexFrame,
        TargetStateSubtype expectedSubtype)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.Subtype.Should().Be(expectedSubtype);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, 35008)]
    [InlineData(RealFrames.TargetStateStatus_73806C, 38016)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, 40000)]
    [InlineData(RealFrames.TargetStateStatus_71C011, 31008)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, 32992)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, 36992)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, 36992)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_TargetAltitude(
        string hexFrame,
        int expectedAltitudeFeet)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.TargetAltitude.Should().NotBeNull();
        targetState.TargetAltitude!.Feet.Should().Be(expectedAltitudeFeet);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, AltitudeSource.McpFcu)]
    [InlineData(RealFrames.TargetStateStatus_73806C, AltitudeSource.McpFcu)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, AltitudeSource.McpFcu)]
    [InlineData(RealFrames.TargetStateStatus_71C011, AltitudeSource.McpFcu)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, AltitudeSource.McpFcu)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, AltitudeSource.McpFcu)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, AltitudeSource.McpFcu)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_AltitudeSource(
        string hexFrame,
        AltitudeSource expectedSource)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.AltitudeSource.Should().Be(expectedSource);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, 132.0)]
    [InlineData(RealFrames.TargetStateStatus_73806C, 310.0)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, 270.0)]
    [InlineData(RealFrames.TargetStateStatus_71C011, 120.0)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, 104.0)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, 111.0)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_TargetHeading(
        string hexFrame,
        double expectedHeading)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.TargetHeading.Should().NotBeNull();
        targetState.TargetHeading.Should().BeApproximately(expectedHeading, 1.0);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, 1013.6)]
    [InlineData(RealFrames.TargetStateStatus_73806C, 1013.6)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, 1013.6)]
    [InlineData(RealFrames.TargetStateStatus_71C011, 1012.8)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, 1012.8)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, 1013.6)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, 1012.8)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_BarometricPressure(
        string hexFrame,
        double expectedPressure)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.BarometricPressure.Should().NotBeNull();
        targetState.BarometricPressure.Should().BeApproximately(expectedPressure, 0.1);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, true)]
    [InlineData(RealFrames.TargetStateStatus_73806C, true)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, true)]
    [InlineData(RealFrames.TargetStateStatus_71C011, true)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, true)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, true)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, true)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_TcasOperational(
        string hexFrame,
        bool expectedTcasOperational)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.TcasOperational.Should().Be(expectedTcasOperational);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, NavigationAccuracyCategoryPosition.LessThan3m)]
    [InlineData(RealFrames.TargetStateStatus_73806C, NavigationAccuracyCategoryPosition.LessThan93m)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.TargetStateStatus_71C011, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, NavigationAccuracyCategoryPosition.LessThan30m)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_NACp(
        string hexFrame,
        NavigationAccuracyCategoryPosition expectedNACp)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.NACp.Should().Be(expectedNACp);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.TargetStateStatus_73806C, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.TargetStateStatus_71C011, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_NICBaroAltitudeIntegrity(
        string hexFrame,
        BarometricAltitudeIntegrityCode expectedIntegrity)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.NICBaroIntegrity.Should().Be(expectedIntegrity);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_49D414, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.TargetStateStatus_73806C, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.TargetStateStatus_39CEAD, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.TargetStateStatus_71C011, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.TargetStateStatus_4D2047, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, SourceIntegrityLevel.PerHour1E7)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_SIL(
        string hexFrame,
        SourceIntegrityLevel expectedSIL)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.SIL.Should().Be(expectedSIL);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, true)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, true)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_AutopilotEngaged(
        string hexFrame,
        bool expectedAutopilot)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.AutopilotEngaged.Should().NotBeNull();
        targetState.AutopilotEngaged.Should().Be(expectedAutopilot);
    }

    [Theory]
    [InlineData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV, true)]
    [InlineData(RealFrames.TargetStateStatus_8965F3_AutopilotVNAV, true)]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_VnavMode(
        string hexFrame,
        bool expectedVnav)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.VnavMode.Should().NotBeNull();
        targetState.VnavMode.Should().Be(expectedVnav);
    }

    [Fact]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_HighNACp_ParsesCorrectly()
    {
        // Arrange - Frame with NACp=11 (high accuracy < 3m)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.TargetStateStatus_49D414)
            .WithIcaoAddress("49D414")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.NACp.Should().Be(NavigationAccuracyCategoryPosition.LessThan3m, "Frame has NACp=11 for < 3m accuracy");
    }

    [Fact]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_AutopilotVNAV_ParsesCorrectly()
    {
        // Arrange - Frame with autopilot and VNAV engaged
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.TargetStateStatus_86E778_AutopilotVNAV)
            .WithIcaoAddress("86E778")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.AutopilotEngaged.Should().BeTrue("Frame indicates autopilot engaged");
        targetState.VnavMode.Should().BeTrue("Frame indicates VNAV mode engaged");
        targetState.TargetAltitude.Should().NotBeNull();
        targetState.TargetAltitude!.Feet.Should().Be(32992);
        targetState.TargetHeading.Should().BeApproximately(104.0, 0.5);
    }

    [Fact]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_StandardPressure_ParsesCorrectly()
    {
        // Arrange - Frame with standard pressure setting (1013.6 mb)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.TargetStateStatus_49D414)
            .WithIcaoAddress("49D414")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.BarometricPressure.Should().BeApproximately(1013.6, 0.1, "Frame has standard pressure setting");
    }

    [Fact]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_HighAltitude_ParsesCorrectly()
    {
        // Arrange - Frame with FL400 (40000 ft)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.TargetStateStatus_39CEAD)
            .WithIcaoAddress("39CEAD")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;
        targetState.TargetAltitude.Should().NotBeNull();
        targetState.TargetAltitude!.Feet.Should().Be(40000, "Frame has target altitude of FL400");
        targetState.TargetHeading.Should().BeApproximately(270.0, 1, "Frame has target heading of 270°");
    }

    [Fact]
    public void ParseMessage_DF17_TC29_TargetStateAndStatus_Version2_TargetHeadingType_IsNull()
    {
        // Arrange - Version 2 messages (all current test frames are Version 2)
        // Note: Version 2 specification does not include TargetHeadingType field
        // Version 1 has this field, but we don't currently have Version 1 test frames
        string[] version2Frames =
        [
            RealFrames.TargetStateStatus_49D414,
            RealFrames.TargetStateStatus_73806C,
            RealFrames.TargetStateStatus_39CEAD,
            RealFrames.TargetStateStatus_71C011,
            RealFrames.TargetStateStatus_86E778_AutopilotVNAV,
            RealFrames.TargetStateStatus_4D2047,
            RealFrames.TargetStateStatus_8965F3_AutopilotVNAV
        ];

        foreach (string hexFrame in version2Frames)
        {
            ValidatedFrame frame = new ValidatedFrameBuilder()
                .WithHexData(hexFrame)
                .Build();

            // Act
            ModeSMessage? message = _parser.ParseMessage(frame);

            // Assert
            message.Should().NotBeNull();
            TargetStateAndStatus? targetState = message.Should().BeOfType<TargetStateAndStatus>().Subject;

            // Version 2 messages do not include TargetHeadingType
            // (only Version 1 has the track/heading type indicator)
            targetState.TargetHeadingType.Should().BeNull(
                $"Frame {hexFrame}: Version 2 messages do not include TargetHeadingType field");
        }
    }
}
