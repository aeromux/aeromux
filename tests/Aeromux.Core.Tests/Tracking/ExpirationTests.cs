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

        // Act - Wait for expiration
        Thread.Sleep(TimeSpan.FromSeconds(4));

        // Assert
        Aircraft? afterExpiry = Tracker.GetAircraft("471DBC");
        afterExpiry.Should().BeNull("aircraft should expire after timeout");
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

        // Act - Wait for all to expire
        // Note: CleanupInterval defaults to 10 seconds (set in constructor before we can change it)
        // So we need to wait at least 12 seconds (10s for first cleanup + 2s timeout)
        Thread.Sleep(TimeSpan.FromSeconds(13));

        // Assert - All should be expired
        Tracker.Count.Should().Be(0);
        expiredIcaos.Should().HaveCount(5);
        Tracker.GetAllAircraft().Should().BeEmpty();
    }

    [Fact]
    public void Expiration_RecentAircraftNotExpired()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 10);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Act - Wait 3 seconds, send update, wait another 3 seconds
        Thread.Sleep(TimeSpan.FromSeconds(3));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Thread.Sleep(TimeSpan.FromSeconds(3));

        // Assert - Should still be tracked (last update was 3 seconds ago, timeout is 10)
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull("aircraft should still be active after recent update");

        // SeenSeconds is calculated at update time, not read time
        // After the update at T+3, SeenSeconds should be ~3 seconds (from T0 to T3)
        aircraft!.Status.SeenSeconds.Should().BeGreaterOrEqualTo(3.0);

        // Verify aircraft is not expired (timeout is 10 seconds, last update was 3 seconds ago)
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

        // Wait for cleanup to run
        Thread.Sleep(TimeSpan.FromMilliseconds(500));

        // Assert - A expired, B still active
        Aircraft? aircraftA = Tracker.GetAircraft("471DBC");
        Aircraft? aircraftB = Tracker.GetAircraft("4D2407");

        aircraftA.Should().BeNull("Aircraft A should have expired");
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

        // Let first aircraft expire
        Thread.Sleep(TimeSpan.FromSeconds(2.5));

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
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 5);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Wait almost to expiration
        Thread.Sleep(TimeSpan.FromMilliseconds(4900));

        // Update just before expiry
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Assert - Should NOT be expired (LastSeen updated)
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull("aircraft updated just before expiry");

        // Now wait for full timeout
        Thread.Sleep(TimeSpan.FromSeconds(6));

        // Assert - Now it should be expired
        Aircraft? afterTimeout = Tracker.GetAircraft("471DBC");
        afterTimeout.Should().BeNull("aircraft expired after full timeout from last update");
    }
}
