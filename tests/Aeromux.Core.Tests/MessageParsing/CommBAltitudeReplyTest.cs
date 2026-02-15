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
/// Tests for parsing Comm-B Altitude Reply messages (DF 20).
/// Covers basic DF 20 fields: altitude, flight status, downlink request, and utility message.
/// BDS-specific decoding (BdsCode, BdsData) is intentionally out of scope.
/// </summary>
public class CommBAltitudeReplyTest
{
    private readonly MessageParser _parser = new();

    // ========================================
    // Basic Message Fields
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Altitude_4D2407, "4D2407", DownlinkFormat.CommBAltitudeReply)]
    [InlineData(RealFrames.CommB_Altitude_80073B, "80073B", DownlinkFormat.CommBAltitudeReply)]
    [InlineData(RealFrames.CommB_Altitude_3C4AD7, "3C4AD7", DownlinkFormat.CommBAltitudeReply)]
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
    [InlineData(RealFrames.CommB_Altitude_3C4AD7, 40000)]
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
    [InlineData(RealFrames.CommB_Altitude_3C4AD7, FlightStatus.AirborneNormal)]
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
    [InlineData(RealFrames.CommB_Altitude_3C4AD7, 0)]
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
    [InlineData(RealFrames.CommB_Altitude_3C4AD7, 0)]
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
