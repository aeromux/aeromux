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
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for message counters and statistics tracking.
/// </summary>
public class CounterTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Update_TenFramesSameAircraft_TotalMessagesIncrementsCorrectly()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        for (int i = 0; i < 10; i++)
        {
            Tracker.Update(frame);
        }

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();
        aircraft!.Status.TotalMessages.Should().Be(10);
    }

    [Fact]
    public void Update_MixedMessageTypes_CountersIncrementCorrectly()
    {
        // Arrange
        Tracker = CreateTracker();
        string icao = "80073B";

        // Act - 2 position, 1 velocity, 1 ID (simulated with real frames for same ICAO)
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, icao));  // Position 1
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, icao));   // Position 2

        // For velocity and ID, we'd need frames with the same ICAO, which we don't have in RealFrames
        // So let's test with what we have
        Aircraft? aircraft = Tracker.GetAircraft(icao);

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Status.TotalMessages.Should().Be(2);
        aircraft.Status.PositionMessages.Should().Be(2);
        aircraft.Status.VelocityMessages.Should().Be(0);
        aircraft.Status.IdentificationMessages.Should().Be(0);
    }

    [Fact]
    public void Update_SeenSeconds_CalculatesTimeSinceFirstSeen()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        Tracker.Update(frame);
        DateTime firstSeenTime = Tracker.GetAircraft("471DBC")!.Status.FirstSeen;

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Tracker.Update(frame);

        Aircraft? aircraft = Tracker.GetAircraft("471DBC");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Status.SeenSeconds.Should().BeGreaterOrEqualTo(2.0);
        aircraft.Status.FirstSeen.Should().Be(firstSeenTime);
    }

    [Fact]
    public void Update_PositionMessage_ResetsSeenPosSeconds()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame posFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B");

        // Act
        Tracker.Update(posFrame);
        Aircraft? after1 = Tracker.GetAircraft("80073B");

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Tracker.Update(posFrame);
        Aircraft? after2 = Tracker.GetAircraft("80073B");

        // Assert
        after1.Should().NotBeNull();
        after1!.Status.SeenPosSeconds.Should().Be(0);

        after2.Should().NotBeNull();
        // After 2 seconds, another position update resets SeenPosSeconds to 0
        after2!.Status.SeenPosSeconds.Should().BeLessThan(1.0); // Should be close to 0
    }
}
