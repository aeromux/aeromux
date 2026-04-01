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
/// Tests for aircraft timeout and expiration cleanup.
/// </summary>
public class ExpirationTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Expiration_AircraftExpiresAfterTimeout()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 3);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        Aircraft? beforeExpiry = Tracker.GetAircraft("471DBC");
        beforeExpiry.Should().NotBeNull();

        // Act & Assert - Wait for GetAircraft to return null (IsExpired check is synchronous)
        WaitForCondition(
            () => Tracker.GetAircraft("471DBC") == null,
            TimeSpan.FromSeconds(5),
            "aircraft should expire after timeout");
        Tracker.Count.Should().Be(0, "expired aircraft should not be counted");
    }

    [Fact]
    public void Expiration_CleanupRemovesMultipleExpiredAircraft()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 2);
        var expiredIcaos = new List<string>();
        Tracker.OnAircraftExpired += (sender, args) => expiredIcaos.Add(args.Aircraft.Identification.ICAO);

        // Create 5 aircraft at different times
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Thread.Sleep(TimeSpan.FromMilliseconds(200));

        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Thread.Sleep(TimeSpan.FromMilliseconds(200));

        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));
        Thread.Sleep(TimeSpan.FromMilliseconds(200));

        Tracker.Update(CreateFrame(RealFrames.AircraftId_8965F3, "8965F3"));
        Thread.Sleep(TimeSpan.FromMilliseconds(200));

        Tracker.Update(CreateFrame(RealFrames.AircraftId_8964A0, "8964A0"));

        Tracker.Count.Should().Be(5);

        // Act & Assert - Wait for cleanup timer to fire and remove all expired aircraft
        WaitForCondition(
            () => expiredIcaos.Count == 5,
            TimeSpan.FromSeconds(5),
            "all 5 aircraft should have been expired by cleanup timer");
        Tracker.Count.Should().Be(0);
        Tracker.GetAllAircraft().Should().BeEmpty();
    }

    [Fact]
    public void Expiration_RecentAircraftNotExpired()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 5);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Act - Wait 1 second, send update, wait another 1 second
        Thread.Sleep(TimeSpan.FromSeconds(1));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Thread.Sleep(TimeSpan.FromSeconds(1));

        // Assert - Should still be tracked (last update was 1 second ago, timeout is 5)
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull("aircraft should still be active after recent update");

        // SeenSeconds is calculated at update time, not read time
        // After the update at T+1, SeenSeconds should be ~1 second (from T0 to T1)
        aircraft!.Status.SeenSeconds.Should().BeGreaterOrEqualTo(1.0);

        // Verify aircraft is not expired (timeout is 5 seconds, last update was 1 second ago)
        Tracker.Count.Should().Be(1, "aircraft should not have been cleaned up yet");
    }

    [Fact]
    public void Expiration_MixedExpiredAndActiveAircraft()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 2);

        // Create aircraft A (will expire)
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Wait 1 second
        Thread.Sleep(TimeSpan.FromSeconds(1));

        // Create aircraft B (will be kept alive)
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Wait another 1.5 seconds (A expires, B does not)
        Thread.Sleep(TimeSpan.FromMilliseconds(1500));

        // Keep B alive
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Assert - Wait for A to expire (IsExpired check is synchronous in GetAircraft)
        WaitForCondition(
            () => Tracker.GetAircraft("471DBC") == null,
            TimeSpan.FromSeconds(5),
            "Aircraft A should have expired");

        Aircraft? aircraftB = Tracker.GetAircraft("4D2407");
        aircraftB.Should().NotBeNull("Aircraft B should still be active");
        Tracker.Count.Should().Be(1);
    }

    [Fact]
    public void Expiration_GetAllAircraftFiltersExpired()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 2);

        // Create 3 aircraft
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        Tracker.Count.Should().Be(3);

        // Wait for first aircraft to expire, then keep the other two alive
        WaitForCondition(
            () => Tracker.GetAircraft("471DBC") == null,
            TimeSpan.FromSeconds(5),
            "first aircraft should have expired");

        // Keep other two alive
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        // Act
        IReadOnlyList<Aircraft> allAircraft = Tracker.GetAllAircraft();

        // Assert
        allAircraft.Should().HaveCount(2, "one aircraft expired");
        Tracker.Count.Should().Be(2);
    }

    [Fact]
    public void Expiration_EdgeCaseJustBeforeExpiry()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 2);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Wait almost to expiration
        Thread.Sleep(TimeSpan.FromMilliseconds(1800));

        // Update just before expiry
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Assert - Should NOT be expired (LastSeen updated)
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull("aircraft updated just before expiry");

        // Assert - Wait for full timeout from last update
        WaitForCondition(
            () => Tracker.GetAircraft("471DBC") == null,
            TimeSpan.FromSeconds(5),
            "aircraft should expire after full timeout from last update");
    }
}
