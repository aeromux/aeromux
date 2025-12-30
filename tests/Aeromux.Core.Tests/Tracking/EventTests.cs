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
using Xunit;
using System.Collections.Generic;

namespace Aeromux.Core.Tests.Tracking
{
    /// <summary>
    /// Tests for OnAircraftAdded, OnAircraftUpdated, and OnAircraftExpired events.
    /// </summary>
    public class EventTests : AircraftStateTrackerTestsBase
    {
        [Fact]
        public void Update_FirstFrame_FiresOnAircraftAddedEvent()
        {
            // Arrange
            _tracker = CreateTracker();
            Aircraft? addedAircraft = null;
            _tracker.OnAircraftAdded += (sender, args) => addedAircraft = args.Aircraft;

            // Act
            _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

            // Assert
            addedAircraft.Should().NotBeNull();
            addedAircraft!.Identification.Icao.Should().Be("471DBC");
            addedAircraft.Identification.Callsign.Should().Be("WZZ476");
        }

        [Fact]
        public void Update_ThreeDifferentAircraft_FiresOnAircraftAddedThreeTimes()
        {
            // Arrange
            _tracker = CreateTracker();
            var addedIcaos = new List<string>();
            _tracker.OnAircraftAdded += (sender, args) => addedIcaos.Add(args.Aircraft.Identification.Icao);

            // Act
            _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
            _tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
            _tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

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
            _tracker = CreateTracker();
            int updateCount = 0;
            Aircraft? previousState = null;
            Aircraft? updatedState = null;

            _tracker.OnAircraftUpdated += (sender, args) =>
            {
                updateCount++;
                previousState = args.Previous;
                updatedState = args.Updated;
            };

            // Act - First frame creates aircraft (no update event)
            _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

            // Second frame updates aircraft (fires update event)
            _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

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
            _tracker = CreateTracker();
            int updateCount = 0;
            _tracker.OnAircraftUpdated += (sender, args) => updateCount++;

            // Act
            ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
            for (int i = 0; i < 6; i++) // First creates, next 5 update
            {
                _tracker.Update(frame);
            }

            // Assert
            updateCount.Should().Be(5); // 5 updates after creation
        }
    }
}
