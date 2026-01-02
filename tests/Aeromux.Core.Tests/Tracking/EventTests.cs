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
/// Tests for OnAircraftAdded, OnAircraftUpdated, and OnAircraftExpired events.
/// </summary>
public class EventTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Update_FirstFrame_FiresOnAircraftAddedEvent()
    {
        // Arrange
        Tracker = CreateTracker();
        Aircraft? addedAircraft = null;
        Tracker.OnAircraftAdded += (sender, args) => addedAircraft = args.Aircraft;

        // Act
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Assert
        addedAircraft.Should().NotBeNull();
        addedAircraft!.Identification.ICAO.Should().Be("471DBC");
        addedAircraft.Identification.Callsign.Should().Be("WZZ476");
    }

    [Fact]
    public void Update_ThreeDifferentAircraft_FiresOnAircraftAddedThreeTimes()
    {
        // Arrange
        Tracker = CreateTracker();
        var addedIcaos = new List<string>();
        Tracker.OnAircraftAdded += (sender, args) => addedIcaos.Add(args.Aircraft.Identification.ICAO);

        // Act
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        // Assert
        addedIcaos.Should().HaveCount(3);
        addedIcaos.Should().Contain("471DBC");
        addedIcaos.Should().Contain("4D2407");
        addedIcaos.Should().Contain("80073B");
    }

    [Fact]
    public void Update_ExistingAircraft_FiresOnAircraftUpdatedEvent()
    {
        // Arrange
        Tracker = CreateTracker();
        int updateCount = 0;
        Aircraft? previousState = null;
        Aircraft? updatedState = null;

        Tracker.OnAircraftUpdated += (sender, args) =>
        {
            updateCount++;
            previousState = args.Previous;
            updatedState = args.Updated;
        };

        // Act - First frame creates aircraft (no update event)
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Second frame updates aircraft (fires update event)
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Assert
        updateCount.Should().Be(1); // Only the second update fires the event
        previousState.Should().NotBeNull();
        updatedState.Should().NotBeNull();
        previousState!.Status.TotalMessages.Should().Be(1);
        updatedState!.Status.TotalMessages.Should().Be(2);
    }

    [Fact]
    public void Update_FiveUpdates_FiresOnAircraftUpdatedFiveTimes()
    {
        // Arrange
        Tracker = CreateTracker();
        int updateCount = 0;
        Tracker.OnAircraftUpdated += (sender, args) => updateCount++;

        // Act
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        for (int i = 0; i < 6; i++) // First creates, next 5 update
        {
            Tracker.Update(frame);
        }

        // Assert
        updateCount.Should().Be(5); // 5 updates after creation
    }
}
