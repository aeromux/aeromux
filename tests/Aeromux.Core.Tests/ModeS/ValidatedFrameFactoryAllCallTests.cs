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

using Aeromux.Core.Tests.TestData;
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for ValidatedFrameFactory with DF 11 all-call replies.
/// </summary>
/// <remarks>
/// A DF 11 parity field is overlaid with the interrogator's code, so a correct reply has a
/// CRC remainder equal to its interrogator ID (IID, low 7 bits), not zero.
/// </remarks>
public class ValidatedFrameFactoryAllCallTests
{
    private readonly ValidatedFrameFactory _factory = new();

    [Theory]
    [InlineData(RealFrames.AllCall_503D74, "503D74", 0)]
    [InlineData(RealFrames.AllCall_80073B, "80073B", 33)]
    [InlineData(RealFrames.AllCall_80073B_Iid4, "80073B", 4)]
    [InlineData(RealFrames.AllCall_471F87, "471F87", 39)]
    [InlineData(RealFrames.AllCall_4D2407, "4D2407", 39)]
    public void ValidateFrame_AllCallWithAnyInterrogatorId_IsValidAndNotCorrected(string hex, string icao, int iid)
    {
        // Act
        ValidatedFrame? frame = _factory.ValidateFrame(CreateRawFrame(hex), 0.0);

        // Assert
        frame.Should().NotBeNull(because: $"a remainder of {iid} is the interrogator ID, not an error");
        frame!.IcaoAddress.Should().Be(icao);
        frame.WasCorrected.Should().BeFalse(because: "nothing was wrong with the frame");
    }

    [Fact]
    public void ValidateFrame_AllCallIid0WithOneAddressBitFlipped_IsCorrectedBackToIcao()
    {
        // Arrange - flip bit 20 (inside the AA field, bits 9-32) of a real IID 0 reply
        string corrupted = FlipBit(RealFrames.AllCall_503D74, 20);

        // Act
        ValidatedFrame? frame = _factory.ValidateFrame(CreateRawFrame(corrupted), 0.0);

        // Assert
        frame.Should().NotBeNull();
        frame!.WasCorrected.Should().BeTrue();
        frame.IcaoAddress.Should().Be("503D74");
    }

    [Fact]
    public void ValidateFrame_AllCallIid33WithAnyAddressBitFlipped_IsRejected()
    {
        // Each flip leaves remainder bits above 0x7F, and no single-bit syndrome matches
        for (int bit = 9; bit <= 32; bit++)
        {
            // Arrange
            string corrupted = FlipBit(RealFrames.AllCall_80073B, bit);

            // Act
            ValidatedFrame? frame = _factory.ValidateFrame(CreateRawFrame(corrupted), 0.0);

            // Assert
            frame.Should().BeNull(because: $"flipping address bit {bit} of an IID 33 reply is not correctable");
        }
    }

    private static RawFrame CreateRawFrame(string hex) =>
        new(Convert.FromHexString(hex), DateTime.UtcNow, 0, 0.0);

    /// <summary>
    /// Returns the frame with one bit (1-indexed, MSB first) inverted.
    /// </summary>
    private static string FlipBit(string hex, int bit)
    {
        byte[] data = Convert.FromHexString(hex);
        data[(bit - 1) / 8] ^= (byte)(0x80 >> ((bit - 1) % 8));
        return Convert.ToHexString(data);
    }
}
