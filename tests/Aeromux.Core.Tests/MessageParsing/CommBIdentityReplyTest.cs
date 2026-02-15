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
/// Tests for parsing Comm-B Identity Reply messages (DF 21).
/// Covers basic DF 21 fields: squawk code, flight status, downlink request, and utility message.
/// BDS-specific decoding (BdsCode, BdsData) is intentionally out of scope.
/// </summary>
public class CommBIdentityReplyTest
{
    private readonly MessageParser _parser = new();

    // ========================================
    // Basic Message Fields
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_4D2407, "4D2407", DownlinkFormat.CommBIdentityReply)]
    [InlineData(RealFrames.CommB_Identity_49D414, "49D414", DownlinkFormat.CommBIdentityReply)]
    [InlineData(RealFrames.CommB_Identity_3C4AD7, "3C4AD7", DownlinkFormat.CommBIdentityReply)]
    [InlineData(RealFrames.CommB_Identity_3C4AD7_WithCallsign, "3C4AD7", DownlinkFormat.CommBIdentityReply)]
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
    [InlineData(RealFrames.CommB_Identity_3C4AD7, "3205")]
    [InlineData(RealFrames.CommB_Identity_3C4AD7_WithCallsign, "3205")]
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
    [InlineData(RealFrames.CommB_Identity_3C4AD7, FlightStatus.AirborneNormal)]
    [InlineData(RealFrames.CommB_Identity_3C4AD7_WithCallsign, FlightStatus.AirborneNormal)]
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
    [InlineData(RealFrames.CommB_Identity_3C4AD7, 0)]
    [InlineData(RealFrames.CommB_Identity_3C4AD7_WithCallsign, 0)]
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
    [InlineData(RealFrames.CommB_Identity_3C4AD7, 0)]
    [InlineData(RealFrames.CommB_Identity_3C4AD7_WithCallsign, 0)]
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
    // Callsign (BDS 2,0)
    // ========================================

    [Theory]
    [InlineData(RealFrames.CommB_Identity_3C4AD7_WithCallsign, "DLH755")]
    public void ParseMessage_DF21_CommB_Callsign(
        string hexFrame,
        string expectedCallsign)
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
        reply.BdsData.Should().NotBeNull();
        Bds20AircraftIdentification? bds20 = reply.BdsData.Should().BeOfType<Bds20AircraftIdentification>().Subject;
        bds20.Callsign.Should().Be(expectedCallsign, "Callsign is encoded in BDS 2,0 register");
    }

    // ========================================
    // BDS Fields (Not Tested - Out of Scope)
    // ========================================

    // NOTE: BdsCode and BdsData are intentionally not tested here.
    // BDS register decoding is a complex, optional feature that would
    // require dedicated test coverage if needed in the future.
    // These tests focus on core DF 21 message fields only.
}
