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
using FluentAssertions;
using System.Linq;
using Xunit;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for tracking multiple aircraft simultaneously.
/// </summary>
public class MultipleAircraftTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Update_FiveAircraftSimultaneously_TracksAllIndependently()
    {
        // Arrange
        _tracker = CreateTracker();

        // Act - Track 5 different aircraft
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        _tracker.Update(CreateFrame(RealFrames.AircraftId_8965F3, "8965F3"));
        _tracker.Update(CreateFrame(RealFrames.AircraftId_8964A0, "8964A0"));
        _tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        _tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        // Assert
        _tracker.Count.Should().Be(5);

        IReadOnlyList<Aircraft> allAircraft = _tracker.GetAllAircraft();
        allAircraft.Should().HaveCount(5);

        // Verify each aircraft is tracked correctly
        allAircraft.Should().Contain(a => a.Identification.Icao == "471DBC");
        allAircraft.Should().Contain(a => a.Identification.Icao == "8965F3");
        allAircraft.Should().Contain(a => a.Identification.Icao == "8964A0");
        allAircraft.Should().Contain(a => a.Identification.Icao == "4D2407");
        allAircraft.Should().Contain(a => a.Identification.Icao == "80073B");
    }

    [Fact]
    public void Update_InterleavedUpdates_EachAircraftTrackedIndependently()
    {
        // Arrange
        _tracker = CreateTracker();

        // Act - Interleaved updates: A1, B1, A2, C1, B2, A3
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")); // A1
        _tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));    // B1
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")); // A2
        _tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));    // C1
        _tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));    // B2
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")); // A3

        // Assert
        _tracker.Count.Should().Be(3);

        Aircraft? aircraftA = _tracker.GetAircraft("471DBC");
        Aircraft? aircraftB = _tracker.GetAircraft("4D2407");
        Aircraft? aircraftC = _tracker.GetAircraft("80073B");

        aircraftA.Should().NotBeNull();
        aircraftB.Should().NotBeNull();
        aircraftC.Should().NotBeNull();

        aircraftA!.Status.TotalMessages.Should().Be(3);
        aircraftB!.Status.TotalMessages.Should().Be(2);
        aircraftC!.Status.TotalMessages.Should().Be(1);
    }
}
