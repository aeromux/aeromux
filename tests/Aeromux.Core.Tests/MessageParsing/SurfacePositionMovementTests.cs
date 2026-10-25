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
/// Tests for the movement (ground speed) field of surface position messages (TC 5-8).
/// </summary>
/// <remarks>
/// Movement bands:
/// 0 no information, 1 stopped, 2-8 below 1 kt, 9-12 1-2 kt, 13-38 2-15 kt, 39-93 15-70 kt,
/// 94-108 70-100 kt, 109-123 100-175 kt, 124 175 kt or more, 125-127 reserved.
/// The speed is the middle of the step, rounded half away from zero.
/// </remarks>
public class SurfacePositionMovementTests
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.SurfacePos_3944F8_Mov0, "3944F8", SurfaceMovement.NoInformation, null)]
    [InlineData(RealFrames.SurfacePos_394C0F_Mov9, "394C0F", SurfaceMovement.From1To2Kt, 1)]
    [InlineData(RealFrames.SurfacePos_3461CF_Mov24, "3461CF", SurfaceMovement.From2To15Kt, 8)]
    [InlineData(RealFrames.SurfacePos_3461CF_Mov25, "3461CF", SurfaceMovement.From2To15Kt, 8)]
    [InlineData(RealFrames.SurfacePos_3461CF_Mov39, "3461CF", SurfaceMovement.From15To70Kt, 16)]
    [InlineData(RealFrames.SurfacePos_3950CF_Mov94, "3950CF", SurfaceMovement.From70To100Kt, 71)]
    [InlineData(RealFrames.SurfacePos_393320_Mov109, "393320", SurfaceMovement.From100To175Kt, 103)]
    [InlineData(RealFrames.SurfacePos_484175_Riddle, "484175", SurfaceMovement.From15To70Kt, 18)]
    public void ParseMessage_RealSurfaceFrame_DecodesMovement(
        string hex, string icao, SurfaceMovement expectedCategory, int? expectedKnots)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hex)
            .WithIcaoAddress(icao)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        SurfacePosition? surface = message.Should().BeOfType<SurfacePosition>().Subject;
        surface.Movement.Should().Be(expectedCategory);
        surface.GroundSpeed.Should().Be(expectedKnots);
    }

    /// <summary>
    /// Every band edge maps to its band and to the middle of its step. Raw 5 pins the
    /// 0.125 kt steps below 1 kt (0.5625 kt rounds to 1); raw 109 and 123 pin rounding half
    /// away from zero (102.5 and 172.5 kt).
    /// </summary>
    [Theory]
    [InlineData(0, SurfaceMovement.NoInformation, null)]
    [InlineData(1, SurfaceMovement.Stopped, 0)]
    [InlineData(2, SurfaceMovement.Below1Kt, 0)]
    [InlineData(5, SurfaceMovement.Below1Kt, 1)]
    [InlineData(8, SurfaceMovement.Below1Kt, 1)]
    [InlineData(9, SurfaceMovement.From1To2Kt, 1)]
    [InlineData(12, SurfaceMovement.From1To2Kt, 2)]
    [InlineData(13, SurfaceMovement.From2To15Kt, 2)]
    [InlineData(38, SurfaceMovement.From2To15Kt, 15)]
    [InlineData(39, SurfaceMovement.From15To70Kt, 16)]
    [InlineData(93, SurfaceMovement.From15To70Kt, 70)]
    [InlineData(94, SurfaceMovement.From70To100Kt, 71)]
    [InlineData(108, SurfaceMovement.From70To100Kt, 99)]
    [InlineData(109, SurfaceMovement.From100To175Kt, 103)]
    [InlineData(123, SurfaceMovement.From100To175Kt, 173)]
    [InlineData(124, SurfaceMovement.AtLeast175Kt, 175)]
    [InlineData(125, SurfaceMovement.Reserved, null)]
    [InlineData(127, SurfaceMovement.Reserved, null)]
    public void DecodeMovement_BandEdges_ReturnBandAndStepMiddle(
        int raw, SurfaceMovement expectedCategory, int? expectedKnots)
    {
        // Act
        (SurfaceMovement category, int? knots) = MessageParser.DecodeMovement(raw);

        // Assert
        category.Should().Be(expectedCategory);
        knots.Should().Be(expectedKnots);
    }
}
