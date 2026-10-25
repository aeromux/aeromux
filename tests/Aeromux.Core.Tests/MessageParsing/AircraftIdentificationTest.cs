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

using System.Text.Json;
using System.Text.Json.Serialization;
using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParsing;

/// <summary>
/// Tests for parsing Aircraft Identification
/// </summary>
public class AircraftIdentificationTest
{
    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(RealFrames.AircraftId_471DBC, "471DBC", "WZZ476")]
    [InlineData(RealFrames.AircraftId_8964A0, "8964A0", "UAE182")]
    [InlineData(RealFrames.AircraftId_8965F3, "8965F3", "ETD128")]
    public void ParseMessage_DF17_TC4_AircraftIdentification_Callsign(
        string hexFrame,
        string expectedIcao,
        string expectedCallsign)
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
        AircraftIdentification identification = message.Should().BeOfType<AircraftIdentification>().Subject;
        identification.Callsign.Should().NotBeNull();
        identification.Callsign.Should().Be(expectedCallsign);
    }

    [Theory]
    [InlineData(RealFrames.AircraftId_471DBC, "471DBC", AircraftCategory.Large)]
    [InlineData(RealFrames.AircraftId_8964A0, "8964A0", AircraftCategory.Heavy)]
    [InlineData(RealFrames.AircraftId_8965F3, "8965F3", AircraftCategory.Heavy)]
    public void ParseMessage_DF17_TC4_AircraftIdentification_Category(
        string hexFrame,
        string expectedIcao,
        AircraftCategory expectedCategory)
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
        AircraftIdentification identification = message.Should().BeOfType<AircraftIdentification>().Subject;
        identification.Category.Should().Be(expectedCategory);
    }

    [Theory]
    [InlineData(1, 0, AircraftCategory.NoInformation)]
    [InlineData(1, 1, AircraftCategory.Reserved)]
    [InlineData(1, 2, AircraftCategory.Reserved)]
    [InlineData(1, 3, AircraftCategory.Reserved)]
    [InlineData(1, 4, AircraftCategory.Reserved)]
    [InlineData(1, 5, AircraftCategory.Reserved)]
    [InlineData(1, 6, AircraftCategory.Reserved)]
    [InlineData(1, 7, AircraftCategory.Reserved)]
    [InlineData(2, 0, AircraftCategory.NoInformation)]
    [InlineData(2, 1, AircraftCategory.SurfaceEmergencyVehicle)]
    [InlineData(2, 2, AircraftCategory.SurfaceServiceVehicle)]
    [InlineData(2, 3, AircraftCategory.PointObstacle)]
    [InlineData(2, 4, AircraftCategory.ClusterObstacle)]
    [InlineData(2, 5, AircraftCategory.LineObstacle)]
    [InlineData(2, 6, AircraftCategory.Reserved)]
    [InlineData(2, 7, AircraftCategory.Reserved)]
    [InlineData(3, 0, AircraftCategory.NoInformation)]
    [InlineData(3, 1, AircraftCategory.Glider)]
    [InlineData(3, 2, AircraftCategory.LighterThanAir)]
    [InlineData(3, 3, AircraftCategory.Parachutist)]
    [InlineData(3, 4, AircraftCategory.Ultralight)]
    [InlineData(3, 5, AircraftCategory.Reserved)]
    [InlineData(3, 6, AircraftCategory.UnmannedAerialVehicle)]
    [InlineData(3, 7, AircraftCategory.SpaceVehicle)]
    [InlineData(4, 0, AircraftCategory.NoInformation)]
    [InlineData(4, 1, AircraftCategory.Light)]
    [InlineData(4, 2, AircraftCategory.Small)]
    [InlineData(4, 3, AircraftCategory.Large)]
    [InlineData(4, 4, AircraftCategory.HighVortexLarge)]
    [InlineData(4, 5, AircraftCategory.Heavy)]
    [InlineData(4, 6, AircraftCategory.HighPerformance)]
    [InlineData(4, 7, AircraftCategory.Rotorcraft)]
    public void GetAircraftCategory_FollowsDo260bEmitterCategorySets(int tc, int ca, AircraftCategory expected)
    {
        // Act
        AircraftCategory category = MessageParser.GetAircraftCategory(tc, ca);

        // Assert
        category.Should().Be(expected);
    }

    [Fact]
    public void AircraftCategory_SerializesToLabelsTheWebMapDependsOn()
    {
        // Arrange - the Web Map picks icons by these strings, so a change must be deliberate
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        string[] expected =
        [
            "\"No Information\"", "\"Surface Emergency Vehicle\"", "\"Surface Service Vehicle\"",
            "\"Point Obstacle\"", "\"Cluster Obstacle\"", "\"Line Obstacle\"",
            "\"Glider / Sailplane\"", "\"Lighter-than-air\"", "\"Parachutist / Skydiver\"",
            "\"Ultralight / Hang-glider / Paraglider\"", "\"Unmanned Aerial Vehicle\"",
            "\"Space / Trans-atmospheric Vehicle\"", "\"Light\"", "\"Small\"", "\"Large\"",
            "\"High Vortex Large\"", "\"Heavy\"", "\"High Performance\"", "\"Rotorcraft\"", "\"Reserved\""
        ];

        // Act
        string[] labels = Enum.GetValues<AircraftCategory>()
            .Select(c => JsonSerializer.Serialize(c, options))
            .ToArray();

        // Assert
        labels.Should().BeEquivalentTo(expected);
    }
}
