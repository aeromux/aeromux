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
/// Tests for aircraft creation from first frame received.
/// Covers all message types that can create a new aircraft entry.
/// </summary>
public class AircraftCreationTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Update_WithFirstAircraftIdentificationFrame_CreatesNewAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        Tracker.Update(frame);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();
        aircraft!.Identification.ICAO.Should().Be("471DBC");
        aircraft.Identification.Callsign.Should().Be("WZZ476");
        aircraft.Status.TotalMessages.Should().Be(1);
        aircraft.Status.IdentificationMessages.Should().Be(1);
        aircraft.Status.PositionMessages.Should().Be(0);
        aircraft.Status.VelocityMessages.Should().Be(0);
    }

    [Fact]
    public void Update_WithFirstAirbornePositionFrame_CreatesNewAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B");

        // Act
        Tracker.Update(frame);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("80073B");
        aircraft.Should().NotBeNull();
        aircraft!.Identification.ICAO.Should().Be("80073B");
        aircraft.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.BarometricAltitude!.Feet.Should().BeInRange(39950, 40000);
        aircraft.Status.TotalMessages.Should().Be(1);
        aircraft.Status.PositionMessages.Should().Be(1);
        aircraft.Status.SeenPosSeconds.Should().Be(0);
    }

    [Fact]
    public void Update_WithFirstAirborneVelocityFrame_CreatesNewAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        Tracker.Update(frame);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("4BB027");
        aircraft.Should().NotBeNull();
        aircraft!.Identification.ICAO.Should().Be("4BB027");
        aircraft.Velocity.Speed.Should().NotBeNull();
        aircraft.Velocity.Speed!.Knots.Should().BeInRange(384, 394);
        aircraft.Velocity.Track.Should().BeInRange(292.0, 296.0);
        aircraft.Velocity.VerticalRate.Should().BeInRange(-2034, -1934);
        aircraft.Status.TotalMessages.Should().Be(1);
        aircraft.Status.VelocityMessages.Should().Be(1);
    }

    [Fact]
    public void Update_WithFirstSurveillanceAltitudeFrame_CreatesNewAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.Surveillance_Altitude_49D414, "49D414");

        // Act
        Tracker.Update(frame);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("49D414");
        aircraft.Should().NotBeNull();
        aircraft!.Identification.ICAO.Should().Be("49D414");
        aircraft.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.BarometricAltitude!.Feet.Should().BeInRange(34975, 35025);
        aircraft.Status.TotalMessages.Should().Be(1);
    }

    [Fact]
    public void Update_WithFirstAllCallReplyFrame_CreatesNewAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AllCall_471F87, "471F87");

        // Act
        Tracker.Update(frame);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("471F87");
        aircraft.Should().NotBeNull();
        aircraft!.Identification.ICAO.Should().Be("471F87");
        aircraft.Status.TotalMessages.Should().Be(1);
        Tracker.Count.Should().Be(1);
    }
}
