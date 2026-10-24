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

using System.Collections.Concurrent;
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for aircraft timeout and expiration cleanup.
/// </summary>
/// <remarks>
/// The tracker runs on a manual clock with its cleanup timer off, so every test is exact and
/// instant. An aircraft is expired only once it is older than the timeout, never at exactly
/// the timeout. Only <see cref="Expiration_CleanupTimer_RemovesExpiredAircraft"/> uses the real
/// timer, to cover its wiring.
/// </remarks>
public class ExpirationTests : AircraftStateTrackerTestsBase
{
    private readonly ManualClock _clock = new();

    [Fact]
    public void Expiration_AircraftExpiresAfterTimeout()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 3, _clock);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Act & Assert - still tracked at exactly the timeout, gone just after it
        _clock.Advance(TimeSpan.FromSeconds(3));
        Tracker.GetAircraft("471DBC").Should().NotBeNull("an aircraft is expired only once older than the timeout");

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Tracker.GetAircraft("471DBC").Should().BeNull("aircraft should expire after timeout");
        Tracker.Count.Should().Be(0, "expired aircraft should not be counted");
    }

    [Fact]
    public void Expiration_CleanupRemovesMultipleExpiredAircraft()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);
        var expiredIcaos = new List<string>();
        Tracker.OnAircraftExpired += (sender, args) => expiredIcaos.Add(args.Aircraft.Identification.ICAO);

        // Create 5 aircraft 200 ms apart
        string[] icaos = ["471DBC", "4D2407", "80073B", "8965F3", "8964A0"];
        string[] frames = [RealFrames.AircraftId_471DBC, RealFrames.AllCall_4D2407, RealFrames.AllCall_80073B,
            RealFrames.AircraftId_8965F3, RealFrames.AircraftId_8964A0];
        for (int i = 0; i < icaos.Length; i++)
        {
            if (i > 0)
            {
                _clock.Advance(TimeSpan.FromMilliseconds(200));
            }
            Tracker.Update(CreateFrame(frames[i], icaos[i]));
        }

        Tracker.Count.Should().Be(5);

        // Act - past the timeout of the newest aircraft, then clean up
        _clock.Advance(TimeSpan.FromSeconds(2) + TimeSpan.FromMilliseconds(1));
        Tracker.CleanupExpiredAircraft();

        // Assert
        expiredIcaos.Should().BeEquivalentTo(icaos);
        Tracker.Count.Should().Be(0);
        Tracker.GetAllAircraft().Should().BeEmpty();
    }

    [Fact]
    public void Expiration_CleanupRemovesOnlyExpiredAircraft()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);
        var expiredIcaos = new List<string>();
        Tracker.OnAircraftExpired += (sender, args) => expiredIcaos.Add(args.Aircraft.Identification.ICAO);

        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Act - the first is 2.5 s old, the second 1.5 s
        _clock.Advance(TimeSpan.FromMilliseconds(1500));
        Tracker.CleanupExpiredAircraft();

        // Assert - a second cleanup with nothing new to expire raises no further events
        Tracker.CleanupExpiredAircraft();
        expiredIcaos.Should().Equal("471DBC");
        Tracker.GetAircraft("4D2407").Should().NotBeNull();
    }

    [Fact]
    public void Expiration_RecentAircraftNotExpired()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 5, _clock);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Act - 1 second, update, another 1 second
        _clock.Advance(TimeSpan.FromSeconds(1));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Assert - Should still be tracked (last update was 1 second ago, timeout is 5)
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull("aircraft should still be active after recent update");

        // SeenSeconds is calculated at update time (from first seen), not read time
        aircraft!.Status.SeenSeconds.Should().Be(1.0);
        Tracker.Count.Should().Be(1, "aircraft should not have been cleaned up yet");
    }

    [Fact]
    public void Expiration_MixedExpiredAndActiveAircraft()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);

        // Create aircraft A (will expire)
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // 1 second later, create aircraft B (will be kept alive)
        _clock.Advance(TimeSpan.FromSeconds(1));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Another 1.5 seconds: A is 2.5 s old and expired, B is 1.5 s old; keep B alive
        _clock.Advance(TimeSpan.FromMilliseconds(1500));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Assert
        Tracker.GetAircraft("471DBC").Should().BeNull("Aircraft A should have expired");
        Tracker.GetAircraft("4D2407").Should().NotBeNull("Aircraft B should still be active");
        Tracker.Count.Should().Be(1);
    }

    [Fact]
    public void Expiration_GetAllAircraftFiltersExpired()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);

        // Create 3 aircraft
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        Tracker.Count.Should().Be(3);

        // Keep two alive a second later, then let the first pass its timeout
        _clock.Advance(TimeSpan.FromSeconds(1));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));
        _clock.Advance(TimeSpan.FromMilliseconds(1500));

        // Act - no cleanup has run, so the expired aircraft is still stored but filtered out
        IReadOnlyList<Aircraft> allAircraft = Tracker.GetAllAircraft();

        // Assert
        allAircraft.Select(a => a.Identification.ICAO).Should().BeEquivalentTo("4D2407", "80073B");
        Tracker.Count.Should().Be(2);
    }

    [Fact]
    public void Expiration_EdgeCaseJustBeforeExpiry()
    {
        // Arrange
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Almost to expiration, then an update just before expiry
        _clock.Advance(TimeSpan.FromMilliseconds(1800));
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Assert - Should NOT be expired (LastSeen updated)
        Tracker.GetAircraft("471DBC").Should().NotBeNull("aircraft updated just before expiry");

        // The full timeout now counts from the last update
        _clock.Advance(TimeSpan.FromSeconds(2));
        Tracker.GetAircraft("471DBC").Should().NotBeNull("exactly at the timeout from the last update");

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Tracker.GetAircraft("471DBC").Should().BeNull("aircraft should expire after full timeout from last update");
    }

    [Fact]
    public void Expiration_CleanupKeepsAnAircraftRefreshedWhileCleanupRuns()
    {
        // Arrange - two expired aircraft. Cleanup lists both, then removes them one by one;
        // the expiry handler for whichever goes first refreshes the other with a new frame,
        // which is what a frame arriving mid-cleanup does.
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);
        var frames = new Dictionary<string, ProcessedFrame>
        {
            ["471DBC"] = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"),
            ["4D2407"] = CreateFrame(RealFrames.AllCall_4D2407, "4D2407")
        };
        foreach (ProcessedFrame frame in frames.Values)
        {
            Tracker.Update(frame);
        }

        var expiredIcaos = new List<string>();
        string? refreshed = null;
        Tracker.OnAircraftExpired += (sender, args) =>
        {
            expiredIcaos.Add(args.Aircraft.Identification.ICAO);
            if (refreshed is null)
            {
                refreshed = frames.Keys.Single(icao => icao != args.Aircraft.Identification.ICAO);
                Tracker.Update(frames[refreshed]);
            }
        };

        _clock.Advance(TimeSpan.FromSeconds(3));

        // Act
        Tracker.CleanupExpiredAircraft();

        // Assert - the refreshed aircraft keeps its track: it was not expired and was not reset
        expiredIcaos.Should().ContainSingle();
        Aircraft? survivor = Tracker.GetAircraft(refreshed!);
        survivor.Should().NotBeNull("a frame arrived for it before cleanup reached it");
        survivor!.Status.TotalMessages.Should().Be(2, "its state should continue, not start again");
    }

    [Fact]
    public void Expiration_CleanupTimer_RemovesExpiredAircraft()
    {
        // Arrange - the clock is already past the timeout, so the only thing left to wait for is
        // the real timer reaching its first callback. Events go into a concurrent collection
        // because delayed timer callbacks may run on more than one thread at once.
        Tracker = CreateTrackerWithClock(timeoutSeconds: 2, _clock);
        var expiredIcaos = new ConcurrentQueue<string>();
        Tracker.OnAircraftExpired += (sender, args) => expiredIcaos.Enqueue(args.Aircraft.Identification.ICAO);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        _clock.Advance(TimeSpan.FromSeconds(3));

        // Act - switch the timer on
        Tracker.CleanupInterval = TimeSpan.FromMilliseconds(100);

        // Assert - normally done within one interval; the bound only matters when the
        // thread pool is starved by a parallel test run
        WaitForCondition(
            () => expiredIcaos.Contains("471DBC"),
            TimeSpan.FromSeconds(30),
            "the cleanup timer should remove the expired aircraft");
        expiredIcaos.Should().ContainSingle();
    }
}
