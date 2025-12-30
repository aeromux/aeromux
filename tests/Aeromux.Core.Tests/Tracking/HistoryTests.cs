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

using Aeromux.Core.Configuration;
using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for history buffer updates (position, altitude, velocity).
/// </summary>
public class HistoryTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public void Update_PositionHistory_FillsCircularBufferCorrectly()
    {
        // Arrange
        TrackingConfig config = new TrackingConfigBuilder()
            .WithMaxHistorySize(100)
            .Build();
        Tracker = new AircraftStateTracker(config);
        Disposables.Add(Tracker);
        var parser = new Aeromux.Core.ModeS.MessageParser();

        // Act - Send 150 position frame pairs (exceeds buffer size of 100)
        // Need both even and odd frames to decode position coordinates
        for (int i = 0; i < 150; i++)
        {
            Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser));
            Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser));
        }

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("80073B");
        aircraft.Should().NotBeNull();
        aircraft!.History.PositionHistory.Should().NotBeNull();
        aircraft.History.PositionHistory!.Count.Should().Be(100); // Circular buffer max size
    }

    [Fact]
    public void Update_AltitudeHistory_TracksBarometricAltitude()
    {
        // Arrange
        Tracker = CreateTracker();

        // Act
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B"));
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_73806C_Even, "73806C"));

        // Assert
        Aircraft? aircraft1 = Tracker.GetAircraft("80073B");
        Aircraft? aircraft2 = Tracker.GetAircraft("73806C");

        aircraft1.Should().NotBeNull();
        aircraft1!.History.AltitudeHistory.Should().NotBeNull();
        aircraft1.History.AltitudeHistory!.Count.Should().BeGreaterThan(0);

        AltitudeSnapshot[] altitudes = aircraft1.History.AltitudeHistory!.GetAll();
        altitudes.Should().NotBeEmpty();
        altitudes[0].Altitude.Feet.Should().BeInRange(39950, 40000); // 39975 ft +/- 25
        altitudes[0].AltitudeType.Should().Be(AltitudeType.Barometric);

        aircraft2.Should().NotBeNull();
        aircraft2!.History.AltitudeHistory.Should().NotBeNull();
        aircraft2.History.AltitudeHistory!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Update_VelocityHistory_CapturesAllFields()
    {
        // Arrange
        Tracker = CreateTracker();

        // Act
        Tracker.Update(CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027"));

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("4BB027");
        aircraft.Should().NotBeNull();
        aircraft!.History.VelocityHistory.Should().NotBeNull();
        aircraft.History.VelocityHistory!.Count.Should().BeGreaterThan(0);

        VelocitySnapshot[] snapshots = aircraft.History.VelocityHistory.GetAll();
        snapshots.Should().NotBeEmpty();
        snapshots[0].Velocity.Should().NotBeNull();
        snapshots[0].VerticalRate.Should().NotBeNull();
    }

    [Fact]
    public void Update_HistoryDisabled_DoesNotCreateBuffers()
    {
        // Arrange
        TrackingConfig config = new TrackingConfigBuilder()
            .WithHistoryDisabled()
            .Build();
        Tracker = new AircraftStateTracker(config);
        Disposables.Add(Tracker);

        // Act
        Tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B"));

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("80073B");
        aircraft.Should().NotBeNull();
        aircraft!.History.PositionHistory.Should().BeNull();
        aircraft.History.AltitudeHistory.Should().BeNull();
        aircraft.History.VelocityHistory.Should().BeNull();
    }

    [Fact]
    public void Update_IdentificationOnly_NoHistorySnapshotsAdded()
    {
        // Arrange
        Tracker = CreateTracker();

        // Act
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();

        // Position/Velocity/Altitude history should be empty since ID frame has no such data
        if (aircraft!.History.PositionHistory != null)
        {
            aircraft.History.PositionHistory.Count.Should().Be(0);
        }
        if (aircraft.History.VelocityHistory != null)
        {
            aircraft.History.VelocityHistory.Count.Should().Be(0);
        }
    }
}
