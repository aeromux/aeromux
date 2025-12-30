// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
/// Tests for BDS 5,0 - Track and Turn Report
/// Based on "The 1090MHz Riddle" Chapter 17.2, Pages 130-131
/// </summary>
public class Bds50TrackAndTurnTests
{
    private readonly MessageParser _parser = new();

    [Fact]
    public void ParseMessage_DF21_Bds50_CorrectBdsCode()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsCode.Should().Be(BdsCode.Bds50, "message structure and validation rules match BDS 5,0");
    }

    [Fact]
    public void ParseMessage_DF21_Bds50_RollAngleNegative9_7()
    {
        // Arrange
        // Roll angle: -55 × (45/256) = -9.70 degrees
        // Two's complement: 457 - 512 = -55
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;
        bds50.RollAngle.Should().BeApproximately(-9.7, 0.1,
            "roll angle = -55 × (45/256) ≈ -9.7 degrees");
    }

    [Fact]
    public void ParseMessage_DF21_Bds50_TrackAngle140_27()
    {
        // Arrange
        // True track angle: 798 × (90/512) = 140.27 degrees
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;
        bds50.TrackAngle.Should().BeApproximately(140.27, 0.01,
            "track angle = 798 × (90/512) ≈ 140.27 degrees");
    }

    [Fact]
    public void ParseMessage_DF21_Bds50_GroundSpeed476()
    {
        // Arrange
        // Ground speed: 238 × 2 = 476 kt
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;
        bds50.GroundSpeed.Should().Be(476, "ground speed = 238 × 2 = 476 kt");
    }

    [Fact]
    public void ParseMessage_DF21_Bds50_TrackAngleRateNegative0_406()
    {
        // Arrange
        // Track angle rate: -13 × (8/256) = -0.40625 ≈ -0.406 deg/s
        // Two's complement: 499 - 512 = -13
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;
        bds50.TrackRate.Should().BeApproximately(-0.406, 0.001,
            "track angle rate = -13 × (8/256) ≈ -0.406 deg/s");
    }

    [Fact]
    public void ParseMessage_DF21_Bds50_TrueAirspeed466()
    {
        // Arrange
        // True airspeed: 233 × 2 = 466 kt
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;
        bds50.TrueAirspeed.Should().Be(466, "true airspeed = 233 × 2 = 466 kt");
    }

    [Fact]
    public void ParseMessage_DF21_Bds50_AllFieldsValid()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(BdsFrames.Bds50_TrackAndTurn_80006A)
            .WithIcaoAddress("80006A")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        CommBIdentityReply? reply = message.Should().BeOfType<CommBIdentityReply>().Subject;
        reply.BdsData.Should().NotBeNull();

        Bds50TrackAndTurn? bds50 = reply.BdsData.Should().BeOfType<Bds50TrackAndTurn>().Subject;

        // All fields should be valid (status bits = 1)
        bds50.RollAngle.Should().NotBeNull("roll angle status bit is 1");
        bds50.TrackAngle.Should().NotBeNull("track angle status bit is 1");
        bds50.GroundSpeed.Should().NotBeNull("ground speed status bit is 1");
        bds50.TrackRate.Should().NotBeNull("track angle rate status bit is 1");
        bds50.TrueAirspeed.Should().NotBeNull("true airspeed status bit is 1");
    }
}
