// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.

using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParsing;

/// <summary>
/// Tests for BDS 4,0 - Selected Vertical Intention
/// Based on "The 1090MHz Riddle" Chapter 17.1, Pages 128-129
/// </summary>
public class Bds40SelectedVerticalIntentionTests
{
    private readonly MessageParser _parser = new();

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

    // ========================================
    // Real Capture Tests
    // ========================================

    [Fact]
    public void ParseMessage_DF20_Bds40_RealCapture_760918_McpAltitude37008()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_760918_BDS40)
            .WithIcaoAddress("760918")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds40, "message structure and validation rules match BDS 4,0");
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.McpSelectedAltitude.Should().Be(37008, "MCP selected altitude from real capture");
        bds40.BarometricPressureSetting.Should().BeApproximately(1013.0, 0.5, "QNH from real capture");
    }

    [Fact]
    public void ParseMessage_DF20_Bds40_RealCapture_49D421_McpAltitude29008()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_49D421_BDS40)
            .WithIcaoAddress("49D421")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds40, "message structure and validation rules match BDS 4,0");
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.McpSelectedAltitude.Should().Be(29008, "MCP selected altitude from real capture");
        bds40.FmsSelectedAltitude.Should().Be(29008, "FMS selected altitude from real capture");
        bds40.BarometricPressureSetting.Should().BeApproximately(1013.2, 0.5, "QNH from real capture");
    }

    [Fact]
    public void ParseMessage_DF21_Bds40_RealCapture_4C0177_McpAltitude12000()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Identity_4C0177_BDS40)
            .WithIcaoAddress("4C0177")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds40, "message structure and validation rules match BDS 4,0");
        reply.BdsData.Should().NotBeNull();

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.McpSelectedAltitude.Should().Be(12000, "MCP selected altitude from real capture");
        bds40.BarometricPressureSetting.Should().BeApproximately(1013.2, 0.5, "QNH from real capture");
    }

    // ========================================
    // Navigation Mode Tests (MB bits 48-51)
    // ========================================

    [Fact]
    public void ParseMessage_DF20_Bds40_RealCapture_80073B_VnavOnly()
    {
        // Arrange
        // Mode status valid (bit 48), only bit 49 set. TC 29 from the same aircraft reports
        // VNAV engaged and approach off.
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.CommB_Altitude_80073B)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds40, "message structure and validation rules match BDS 4,0");

        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.McpSelectedAltitude.Should().Be(40000, "MCP selected altitude matches the TC 29 target altitude");
        bds40.NavigationModes.Should().Be(Bds40NavigationMode.Vnav, "MB bit 49 is the VNAV flag");
    }

    /// <summary>
    /// Each mode flag decodes from its own MB bit.
    /// </summary>
    /// <remarks>
    /// No capture holds a real frame with bit 50 or 51 set, so these are
    /// <see cref="RealFrames.CommB_Altitude_80073B"/> with only the last MB byte changed
    /// (bits 49-56). The parity is not recomputed, which does not matter here because the
    /// frame builder takes the ICAO address directly.
    /// </remarks>
    [Theory]
    [InlineData("A0001997CE200030A40140DD14CF", Bds40NavigationMode.AltitudeHold)]
    [InlineData("A0001997CE200030A40120DD14CF", Bds40NavigationMode.Approach)]
    [InlineData("A0001997CE200030A401E0DD14CF",
        Bds40NavigationMode.Vnav | Bds40NavigationMode.AltitudeHold | Bds40NavigationMode.Approach)]
    public void ParseMessage_DF20_Bds40_ModeBits_DecodeToMatchingFlags(string hex, Bds40NavigationMode expected)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hex)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        CommBAltitudeReply? reply = message.Should().BeOfType<CommBAltitudeReply>().Subject;
        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.NavigationModes.Should().Be(expected,
            "bit 49 is VNAV, bit 50 is altitude hold and bit 51 is approach");
    }

    [Fact]
    public void ParseMessage_DF21_Bds40_ModeStatusValidNoModes_DecodesNone()
    {
        // Arrange
        // Mode status valid (bit 48), bits 49-51 clear: a definite "no mode active",
        // which differs from mode status not valid (null)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds40_VerticalIntention_8001EB)
            .WithIcaoAddress("8001EB")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        Bds40SelectedVerticalIntention? bds40 = reply.BdsData.Should().BeOfType<Bds40SelectedVerticalIntention>().Subject;
        bds40.NavigationModes.Should().Be(Bds40NavigationMode.None, "mode status is valid with no mode bit set");
    }
}
