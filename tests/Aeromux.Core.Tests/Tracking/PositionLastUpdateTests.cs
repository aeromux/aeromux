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

using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;
using Aeromux.Core.Tracking.Handlers;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for when <see cref="TrackedPosition.LastUpdate"/> moves: the time a position was last
/// received and accepted, whether or not the aircraft moved.
/// </summary>
/// <remarks>
/// Most tests drive the position handlers directly with hand-built messages, because the
/// recorded test frames hold a single CPR pair and no surface positions, which cannot produce
/// a second fix, a rejected jump or a surface coordinate.
/// </remarks>
public class PositionLastUpdateTests : AircraftStateTrackerTestsBase
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly GeographicCoordinate Budapest = new(47.5, 19.0);
    private static readonly GeographicCoordinate NearBudapest = new(47.51, 19.0);   // about 0.6 nm away
    private static readonly GeographicCoordinate FarAway = new(49.5, 19.0);         // about 120 nm away

    // Default 60 s aircraft timeout, so the speed check is bypassed after 30 s
    private readonly AirbornePositionHandler _airborne = new(new TrackingConfigBuilder().Build());
    private readonly SurfacePositionHandler _surface = new();
    private readonly ProcessedFrame _frame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B");

    // --- Airborne ---

    [Fact]
    public void Airborne_FirstFix_SetsLastUpdate()
    {
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0);

        aircraft.Position.Coordinate.Should().Be(Budapest);
        aircraft.Position.LastUpdate.Should().Be(T0);
    }

    [Fact]
    public void Airborne_RepeatedIdenticalPosition_RefreshesLastUpdate()
    {
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0);

        aircraft = Airborne(aircraft, Budapest, T0.AddSeconds(5));

        aircraft.Position.LastUpdate.Should().Be(T0.AddSeconds(5), "the position was received again, even if unchanged");
    }

    [Fact]
    public void Airborne_PlausibleMove_RefreshesLastUpdate()
    {
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0);

        aircraft = Airborne(aircraft, NearBudapest, T0.AddSeconds(5));

        aircraft.Position.Coordinate.Should().Be(NearBudapest);
        aircraft.Position.LastUpdate.Should().Be(T0.AddSeconds(5));
    }

    [Fact]
    public void Airborne_NoCoordinateDecoded_LeavesLastUpdate()
    {
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0, feet: 35000);

        // A position message whose CPR decode produced nothing, carrying a new altitude
        aircraft = Airborne(aircraft, position: null, T0.AddSeconds(5), feet: 34000);

        aircraft.Position.BarometricAltitude!.Feet.Should().Be(34000);
        aircraft.Position.LastUpdate.Should().Be(T0, "no position was received, and altitude does not count");
    }

    [Fact]
    public void Airborne_ImplausibleJump_IsRejectedAndLeavesLastUpdate()
    {
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0);

        aircraft = Airborne(aircraft, FarAway, T0.AddSeconds(5));

        aircraft.Position.Coordinate.Should().Be(Budapest);
        aircraft.Position.ImplausibleCount.Should().Be(1);
        aircraft.Position.LastUpdate.Should().Be(T0, "a rejected position is not a received position");
    }

    [Fact]
    public void Airborne_InvalidationAfterRepeatedJumps_KeepsTheLastAcceptedFixTime()
    {
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0);

        // Four rejections, then the fifth implausible position invalidates the coordinate
        for (int i = 1; i <= 5; i++)
        {
            aircraft = Airborne(aircraft, FarAway, T0.AddSeconds(i));
        }

        aircraft.Position.Coordinate.Should().BeNull("repeated implausible positions invalidate the old one");
        aircraft.Position.LastUpdate.Should().Be(T0, "nothing was accepted after the last fix");

        // The next position is taken as a fresh first fix
        aircraft = Airborne(aircraft, FarAway, T0.AddSeconds(6));
        aircraft.Position.Coordinate.Should().Be(FarAway);
        aircraft.Position.LastUpdate.Should().Be(T0.AddSeconds(6));
    }

    [Fact]
    public void Airborne_StationaryAircraft_KeepsTheSpeedCheckActive()
    {
        // Positions every 5 s for 40 s without moving: well past the 30 s after which a fix
        // counts as stale and the speed check is skipped
        Aircraft aircraft = Airborne(NewAircraft(), Budapest, T0);
        for (int s = 5; s <= 40; s += 5)
        {
            aircraft = Airborne(aircraft, Budapest, T0.AddSeconds(s));
        }

        // A decode error one second later must still be caught
        aircraft = Airborne(aircraft, FarAway, T0.AddSeconds(41));

        aircraft.Position.Coordinate.Should().Be(Budapest, "the last fix is 1 s old, not 41 s");
        aircraft.Position.ImplausibleCount.Should().Be(1);
    }

    // --- Surface ---

    [Fact]
    public void Surface_DecodedCoordinate_SetsLastUpdate()
    {
        Aircraft aircraft = Surface(NewAircraft(), Budapest, T0);

        aircraft.Position.Coordinate.Should().Be(Budapest);
        aircraft.Position.LastUpdate.Should().Be(T0);
    }

    [Fact]
    public void Surface_NoCoordinateDecoded_LeavesLastUpdate()
    {
        // Before any fix: no position, so no position update time either
        Aircraft aircraft = Surface(NewAircraft(), position: null, T0);
        aircraft.Position.Coordinate.Should().BeNull();
        aircraft.Position.LastUpdate.Should().BeNull();

        // After a fix: an undecoded message leaves its time alone
        aircraft = Surface(aircraft, Budapest, T0.AddSeconds(1));
        aircraft = Surface(aircraft, position: null, T0.AddSeconds(6));
        aircraft.Position.Coordinate.Should().Be(Budapest);
        aircraft.Position.LastUpdate.Should().Be(T0.AddSeconds(1));
    }

    // --- Through the tracker, with real frames ---

    [Fact]
    public void Tracker_RepeatedRealPosition_AdvancesLastUpdate()
    {
        // Arrange - decode the 80073B pair, then hear the same position again 3 s later
        var clock = new ManualClock();
        Tracker = CreateTrackerWithClock(timeoutSeconds: 60, clock);
        var parser = new MessageParser();
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser));
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser));
        Aircraft first = Tracker.GetAircraft("80073B")!;
        first.Position.Coordinate.Should().NotBeNull("the pair decodes a position");

        // Act
        clock.Advance(TimeSpan.FromSeconds(3));
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser));
        Aircraft again = Tracker.GetAircraft("80073B")!;

        // Assert
        again.Position.Coordinate.Should().Be(first.Position.Coordinate);
        again.Position.LastUpdate.Should().Be(first.Position.LastUpdate!.Value.AddSeconds(3));
    }

    // --- Helpers ---

    private static Aircraft NewAircraft() => new()
    {
        Identification = new TrackedIdentification { ICAO = "80073B" },
        Status = new TrackedStatus()
    };

    private Aircraft Airborne(Aircraft aircraft, GeographicCoordinate? position, DateTime time, int feet = 35000) =>
        _airborne.Apply(
            aircraft,
            new AirbornePosition("80073B", time, DownlinkFormat.ExtendedSquitter, 0, false,
                position, Altitude.FromFeet(feet, AltitudeType.Barometric), null, SurveillanceStatus.NoAlertNoSPI),
            _frame,
            time);

    private Aircraft Surface(Aircraft aircraft, GeographicCoordinate? position, DateTime time) =>
        _surface.Apply(
            aircraft,
            new SurfacePosition("80073B", time, DownlinkFormat.ExtendedSquitter, 0, false,
                position, null, null, SurfaceMovement.Stopped),
            _frame,
            time);
}
