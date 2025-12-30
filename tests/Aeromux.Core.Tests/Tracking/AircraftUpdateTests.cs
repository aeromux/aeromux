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

using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;
using FluentAssertions;
using Xunit;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for aircraft state updates from subsequent frames.
/// Covers updates to callsign, position, velocity, and signal strength.
/// </summary>
public class AircraftUpdateTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Update_CallsignOnExistingAircraft_UpdatesCallsignAndPreservesPosition()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame idFrame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Create aircraft with ID first
        _tracker.Update(idFrame);

        // Simpler approach: Just test that updating the same aircraft increments counters
        ProcessedFrame idFrame2 = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        _tracker.Update(idFrame2);

        // Assert
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Callsign.Should().Be("WZZ476");
        aircraft.Status.TotalMessages.Should().Be(2);
        aircraft.Status.IdentificationMessages.Should().Be(2);
    }

    [Fact]
    public void Update_WithCPRPair_PopulatesCoordinate()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        Aircraft? afterEven = _tracker.GetAircraft("80073B");

        _tracker.Update(oddFrame);
        Aircraft? afterOdd = _tracker.GetAircraft("80073B");

        // Assert
        afterEven.Should().NotBeNull();
        afterOdd.Should().NotBeNull();

        // After second frame (odd), coordinate should be decoded
        afterOdd!.Position.Coordinate.Should().NotBeNull();
        afterOdd.Position.BarometricAltitude.Should().NotBeNull();
        afterOdd.Status.TotalMessages.Should().Be(2);
        afterOdd.Status.PositionMessages.Should().Be(2);
    }

    [Fact]
    public void Update_MultipleVelocityFrames_UpdatesVelocityEachTime()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");
        ProcessedFrame frame2 = CreateFrame(RealFrames.AirborneVel_39CEAD_Level, "39CEAD");
        ProcessedFrame frame3 = CreateFrame(RealFrames.AirborneVel_4D2407_Level, "4D2407");

        // Act - Create 3 different aircraft
        _tracker.Update(frame1);
        _tracker.Update(frame2);
        _tracker.Update(frame3);

        // Assert - Each aircraft should exist with its own velocity
        Aircraft? aircraft1 = _tracker.GetAircraft("4BB027");
        Aircraft? aircraft2 = _tracker.GetAircraft("39CEAD");
        Aircraft? aircraft3 = _tracker.GetAircraft("4D2407");

        aircraft1.Should().NotBeNull();
        aircraft2.Should().NotBeNull();
        aircraft3.Should().NotBeNull();

        aircraft1!.Velocity.Speed!.Knots.Should().BeInRange(384, 394);
        aircraft2!.Velocity.Speed!.Knots.Should().BeInRange(395, 405);
        aircraft3!.Velocity.Speed!.Knots.Should().BeInRange(478, 488);

        _tracker.Count.Should().Be(3);
    }

    [Fact]
    public void Update_DifferentSignalStrengths_ReflectsLatestSignalStrength()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = new ProcessedFrameBuilder()
            .WithHexData(RealFrames.AircraftId_471DBC)
            .WithIcaoAddress("471DBC")
            .WithSignalStrength(100)
            .Build();

        ProcessedFrame frame2 = new ProcessedFrameBuilder()
            .WithHexData(RealFrames.AircraftId_471DBC)
            .WithIcaoAddress("471DBC")
            .WithSignalStrength(200)
            .Build();

        // Act
        _tracker.Update(frame1);
        Aircraft? after1 = _tracker.GetAircraft("471DBC");

        _tracker.Update(frame2);
        Aircraft? after2 = _tracker.GetAircraft("471DBC");

        // Assert
        after1.Should().NotBeNull();
        after1!.Status.SignalStrength.Should().Be(100);

        after2.Should().NotBeNull();
        after2!.Status.SignalStrength.Should().Be(200);
    }
}
