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

using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for GetAircraft, GetAllAircraft, and Count methods.
/// </summary>
public class GetMethodsTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void GetAircraft_WithUnknownIcao_ReturnsNull()
    {
        // Arrange
        Tracker = CreateTracker();

        // Act
        Aircraft? aircraft = Tracker.GetAircraft("ABCDEF");

        // Assert
        aircraft.Should().BeNull();
    }

    [Fact]
    public void GetAircraft_WithKnownIcao_ReturnsCorrectAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_8965F3, "8965F3"));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_8964A0, "8964A0"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        // Act
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Identification.ICAO.Should().Be("471DBC");
        aircraft.Identification.Callsign.Should().Be("WZZ476");
    }

    [Fact]
    public void GetAllAircraft_SortsAircraftByIcao()
    {
        // Arrange
        Tracker = CreateTracker();
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Act
        IReadOnlyList<Aircraft> aircraft = Tracker.GetAllAircraft();

        // Assert
        aircraft.Should().HaveCount(3);
        aircraft[0].Identification.ICAO.Should().Be("471DBC");
        aircraft[1].Identification.ICAO.Should().Be("4D2407");
        aircraft[2].Identification.ICAO.Should().Be("80073B");
    }

    [Fact]
    public void GetAllAircraft_WithNoAircraft_ReturnsEmptyList()
    {
        // Arrange
        Tracker = CreateTracker();

        // Act
        IReadOnlyList<Aircraft> aircraft = Tracker.GetAllAircraft();

        // Assert
        aircraft.Should().BeEmpty();
    }

    [Fact]
    public void Count_ReflectsCurrentAircraftCount()
    {
        // Arrange
        Tracker = CreateTracker();

        // Act & Assert
        Tracker.Count.Should().Be(0);

        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Count.Should().Be(1);

        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Count.Should().Be(2);

        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));
        Tracker.Count.Should().Be(3);
    }
}
