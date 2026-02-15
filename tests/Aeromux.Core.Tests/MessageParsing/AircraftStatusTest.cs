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
/// Tests for parsing AircraftStatus
/// </summary>
public class AircraftStatusTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.EmergencyStatus_4D2407, "4D2407", "6415")]
    [InlineData(RealFrames.EmergencyStatus_503D74, "503D74", "3254")]
    [InlineData(RealFrames.EmergencyStatus_80073B, "80073B", "3205")]
    public void ParseMessage_DF17_TC28_AircraftEmergencyStatus_Squawk(
        string hexFrame,
        string expectedIcao,
        string expectedSquawk)
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
        AircraftStatus aircraftStatus = message.Should().BeOfType<AircraftStatus>().Subject;
        aircraftStatus.SquawkCode.Should().NotBeNull();
        aircraftStatus.SquawkCode.Should().Be(expectedSquawk);
    }

    [Theory]
    [InlineData(RealFrames.EmergencyStatus_4D2407, "4D2407", EmergencyState.NoEmergency)]
    [InlineData(RealFrames.EmergencyStatus_503D74, "503D74", EmergencyState.NoEmergency)]
    [InlineData(RealFrames.EmergencyStatus_80073B, "80073B", EmergencyState.NoEmergency)]
    public void ParseMessage_DF17_TC28_AircraftEmergencyStatus_EmergencyState(
        string hexFrame,
        string expectedIcao,
        EmergencyState expectedState)
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
        AircraftStatus aircraftStatus = message.Should().BeOfType<AircraftStatus>().Subject;
        aircraftStatus.EmergencyState.Should().NotBeNull();
        aircraftStatus.EmergencyState.Should().Be(expectedState);
    }
}
