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

using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;
using Aeromux.Core.Tracking.Handlers;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for geometric altitude recalculation logic.
/// Verifies that geometric altitude is recalculated only when barometric altitude updates.
/// Delta values from test data (corrected bit positions):
/// - 73806C: -325 ft
/// - 39CEAD: -375 ft
/// - 3C4AD7: -1300 ft
/// - 4BB027: 25 ft
/// - 4D2407: -75 ft
/// </summary>
public class GeometricAltitudeRecalculationTests : AircraftStateTrackerTestsBase
{
    // ========================================
    // AirborneVelocityHandler Tests
    // ========================================

    [Fact]
    public void NewDelta_RecalculatesGeometricAltitude_WhenBarometricUpdates()
    {
        // Arrange: Create aircraft with barometric altitude and initial delta
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 19: Airborne velocity with delta = -325 ft (73806C)
        ProcessedFrame velFrame1 = CreateFrame(RealFrames.AirborneVel_73806C_Climbing, "73806C", parser);

        // TC 12: Airborne position with barometric altitude (73806C at 37600 ft)
        ProcessedFrame posFrame1 = CreateFrame(RealFrames.AirbornePos_73806C_Even, "73806C", parser);

        // Act: TC 19 arrives first (delta saved, no calculation yet)
        Tracker.Update(velFrame1);
        Aircraft? afterDelta = Tracker.GetAircraft("73806C");

        // TC 9-18 arrives (triggers calculation with delta)
        Tracker.Update(posFrame1);
        Aircraft? afterFirstPosition = Tracker.GetAircraft("73806C");

        // New delta arrives via TC 19
        ProcessedFrame velFrame2 = CreateFrame(RealFrames.AirborneVel_39CEAD_Level, "73806C", parser);
        Tracker.Update(velFrame2);
        Aircraft? afterNewDelta = Tracker.GetAircraft("73806C");

        // Another position update arrives (triggers recalculation with new delta)
        ProcessedFrame posFrame2 = CreateFrame(RealFrames.AirbornePos_73806C_Odd, "73806C", parser);
        Tracker.Update(posFrame2);
        Aircraft? afterSecondPosition = Tracker.GetAircraft("73806C");

        // Assert: Delta saved but geometric not calculated until barometric arrives
        afterDelta.Should().NotBeNull();
        afterDelta!.Position.GeometricBarometricDelta.Should().Be(-325);
        afterDelta.Position.GeometricAltitude.Should().BeNull(); // No calculation yet

        // After first position: geometric calculated
        afterFirstPosition.Should().NotBeNull();
        afterFirstPosition!.Position.BarometricAltitude!.Feet.Should().Be(37600);
        afterFirstPosition.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // 37275

        // After new delta: delta updated but geometric not recalculated yet
        afterNewDelta.Should().NotBeNull();
        afterNewDelta!.Position.GeometricBarometricDelta.Should().Be(-375);
        afterNewDelta.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // Still 37275 (old value)

        // After second position: geometric recalculated with new delta
        afterSecondPosition.Should().NotBeNull();
        afterSecondPosition!.Position.GeometricAltitude!.Feet.Should().Be(37600 - 375); // 37225 (recalculated)
    }

    [Fact]
    public void NewDelta_WithoutBarometric_DoesNotCalculateGeometric()
    {
        // Arrange: Create aircraft with only velocity (no position yet)
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 19: Airborne velocity with delta = -325 ft (73806C)
        ProcessedFrame velFrame = CreateFrame(RealFrames.AirborneVel_73806C_Climbing, "73806C", parser);

        // Act: Update with velocity only (no barometric altitude)
        Tracker.Update(velFrame);
        Aircraft? aircraft = Tracker.GetAircraft("73806C");

        // Assert: Geometric altitude should remain null (no barometric to derive from)
        aircraft.Should().NotBeNull();
        aircraft!.Position.GeometricBarometricDelta.Should().Be(-325);
        aircraft.Position.BarometricAltitude.Should().BeNull();
        aircraft.Position.GeometricAltitude.Should().BeNull();
    }

    // ========================================
    // AirbornePositionHandler Tests
    // ========================================

    [Fact]
    public void NewBarometricAltitude_RecalculatesGeometricAltitude_AlwaysUsesLatest()
    {
        // Arrange: Create aircraft with delta and initial barometric altitude
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 19: Airborne velocity with delta = -325 ft (73806C)
        ProcessedFrame velFrame = CreateFrame(RealFrames.AirborneVel_73806C_Climbing, "73806C", parser);

        // TC 12: First position with barometric altitude = 37600 ft
        ProcessedFrame posFrame1 = CreateFrame(RealFrames.AirbornePos_73806C_Even, "73806C", parser);

        // TC 12: Second position with barometric altitude = 37600 ft (same altitude in test data)
        ProcessedFrame posFrame2 = CreateFrame(RealFrames.AirbornePos_73806C_Odd, "73806C", parser);

        // Act: Update with delta, then first position, then second position
        Tracker.Update(velFrame);
        Tracker.Update(posFrame1);
        Aircraft? afterFirstPosition = Tracker.GetAircraft("73806C");

        Tracker.Update(posFrame2);
        Aircraft? afterSecondPosition = Tracker.GetAircraft("73806C");

        // Assert: Geometric altitude should be recalculated with each new barometric altitude
        afterFirstPosition.Should().NotBeNull();
        afterFirstPosition!.Position.BarometricAltitude.Should().NotBeNull();
        afterFirstPosition.Position.BarometricAltitude!.Feet.Should().Be(37600);
        afterFirstPosition.Position.GeometricBarometricDelta.Should().Be(-325);
        afterFirstPosition.Position.GeometricAltitude.Should().NotBeNull();
        afterFirstPosition.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // 37275

        afterSecondPosition.Should().NotBeNull();
        afterSecondPosition!.Position.BarometricAltitude.Should().NotBeNull();
        afterSecondPosition.Position.BarometricAltitude!.Feet.Should().Be(37600); // Same altitude in test data
        afterSecondPosition.Position.GeometricAltitude.Should().NotBeNull();
        afterSecondPosition.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // Recalculated with latest barometric
    }

    [Fact]
    public void NewBarometricAltitude_WithoutDelta_DoesNotCalculateGeometric()
    {
        // Arrange: Create aircraft with only position (no delta yet)
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 12: Airborne position with barometric altitude = 37600 ft (73806C)
        ProcessedFrame posFrame = CreateFrame(RealFrames.AirbornePos_73806C_Even, "73806C", parser);

        // Act: Update with position only (no delta)
        Tracker.Update(posFrame);
        Aircraft? aircraft = Tracker.GetAircraft("73806C");

        // Assert: Geometric altitude should remain null (no delta to derive from)
        aircraft.Should().NotBeNull();
        aircraft!.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.BarometricAltitude!.Feet.Should().Be(37600);
        aircraft.Position.GeometricBarometricDelta.Should().BeNull();
        aircraft.Position.GeometricAltitude.Should().BeNull();
    }

    // ========================================
    // Integration Tests
    // ========================================

    [Fact]
    public void Sequence_TC19_TC918_TC918_ClimbingAircraft_RecalculatesGeometric()
    {
        // Test Scenario 1 from plan: TC 19 → TC 9-18 → TC 9-18 (altitude change)
        // Arrange
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 19: Delta = 725 ft
        ProcessedFrame velFrame = CreateFrame(RealFrames.AirborneVel_73806C_Climbing, "73806C", parser);

        // TC 12: Barometric = 37600 ft
        ProcessedFrame posFrame1 = CreateFrame(RealFrames.AirbornePos_73806C_Even, "73806C", parser);
        ProcessedFrame posFrame2 = CreateFrame(RealFrames.AirbornePos_73806C_Odd, "73806C", parser);

        // Act & Assert: Step-by-step verification

        // Step 1: TC 19 arrives
        Tracker.Update(velFrame);
        Aircraft? after1 = Tracker.GetAircraft("73806C");
        after1.Should().NotBeNull();
        after1!.Position.GeometricBarometricDelta.Should().Be(-325);
        after1.Position.GeometricAltitude.Should().BeNull(); // No barometric yet

        // Step 2: TC 9-18 arrives with barometric = 37600 ft
        Tracker.Update(posFrame1);
        Aircraft? after2 = Tracker.GetAircraft("73806C");
        after2.Should().NotBeNull();
        after2!.Position.BarometricAltitude!.Feet.Should().Be(37600);
        after2.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // 37275 (derived)

        // Step 3: TC 9-18 arrives with same altitude (in test data both frames have same alt)
        Tracker.Update(posFrame2);
        Aircraft? after3 = Tracker.GetAircraft("73806C");
        after3.Should().NotBeNull();
        after3!.Position.BarometricAltitude!.Feet.Should().Be(37600);
        after3.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // Recalculated
    }

    [Fact]
    public void Sequence_TC19_TC918_TC19_TC918_DeltaChange_RecalculatesGeometric()
    {
        // Test Scenario: TC 19 → TC 9-18 → TC 19 → TC 9-18 (delta change, then recalculation on next position)
        // Arrange
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 19: First delta = -325 ft (73806C)
        ProcessedFrame velFrame1 = CreateFrame(RealFrames.AirborneVel_73806C_Climbing, "73806C", parser);

        // TC 12: Barometric = 37600 ft
        ProcessedFrame posFrame1 = CreateFrame(RealFrames.AirbornePos_73806C_Even, "73806C", parser);

        // TC 19: Second delta = -375 ft (using different frame data)
        ProcessedFrame velFrame2 = CreateFrame(RealFrames.AirborneVel_39CEAD_Level, "73806C", parser);

        // TC 12: Another position update (triggers recalculation with new delta)
        ProcessedFrame posFrame2 = CreateFrame(RealFrames.AirbornePos_73806C_Odd, "73806C", parser);

        // Act & Assert: Step-by-step verification

        // Step 1: TC 19 arrives with delta = 725 ft (delta saved, no calculation)
        Tracker.Update(velFrame1);
        Aircraft? after1 = Tracker.GetAircraft("73806C");
        after1.Should().NotBeNull();
        after1!.Position.GeometricBarometricDelta.Should().Be(-325);
        after1.Position.GeometricAltitude.Should().BeNull(); // Not calculated yet

        // Step 2: TC 9-18 arrives with barometric = 37600 ft (triggers calculation)
        Tracker.Update(posFrame1);
        Aircraft? after2 = Tracker.GetAircraft("73806C");
        after2.Should().NotBeNull();
        after2!.Position.BarometricAltitude!.Feet.Should().Be(37600);
        after2.Position.GeometricAltitude!.Feet.Should().Be(37600 - 325); // 37275

        // Step 3: TC 19 arrives with new delta = -375 ft (delta updated, NO recalculation)
        Tracker.Update(velFrame2);
        Aircraft? after3 = Tracker.GetAircraft("73806C");
        after3.Should().NotBeNull();
        after3!.Position.GeometricBarometricDelta.Should().Be(-375);
        after3.Position.GeometricAltitude!.Feet.Should().Be(37275); // Still old value

        // Step 4: TC 9-18 arrives (triggers recalculation with new delta)
        Tracker.Update(posFrame2);
        Aircraft? after4 = Tracker.GetAircraft("73806C");
        after4.Should().NotBeNull();
        after4!.Position.GeometricAltitude!.Feet.Should().Be(37600 - 375); // 37225 (recalculated)
    }

    [Fact]
    public void Sequence_TC19_TC918_TC918_DifferentAltitudes_RecalculatesEachTime()
    {
        // Test with aircraft using different altitudes to verify recalculation
        // Arrange
        Tracker = CreateTracker();
        var parser = new MessageParser();

        // TC 19: Delta = -1300 ft (3C4AD7)
        ProcessedFrame velFrame = CreateFrame(RealFrames.AirborneVel_3C4AD7_Level, "3C4AD7", parser);

        // TC 11: Barometric = 40000 ft (3C4AD7 Even)
        ProcessedFrame posFrame1 = CreateFrame(RealFrames.AirbornePos_3C4AD7_Even, "3C4AD7", parser);

        // TC 11: Barometric = 40000 ft (3C4AD7 Odd)
        ProcessedFrame posFrame2 = CreateFrame(RealFrames.AirbornePos_3C4AD7_Odd, "3C4AD7", parser);

        // Act & Assert
        Tracker.Update(velFrame);
        Tracker.Update(posFrame1);
        Aircraft? after1 = Tracker.GetAircraft("3C4AD7");

        after1.Should().NotBeNull();
        after1!.Position.BarometricAltitude!.Feet.Should().Be(40000);
        after1.Position.GeometricBarometricDelta.Should().Be(-1300);
        after1.Position.GeometricAltitude!.Feet.Should().Be(40000 - 1300); // 38700

        Tracker.Update(posFrame2);
        Aircraft? after2 = Tracker.GetAircraft("3C4AD7");

        after2.Should().NotBeNull();
        after2!.Position.BarometricAltitude!.Feet.Should().Be(40000);
        after2.Position.GeometricAltitude!.Feet.Should().Be(40000 - 1300); // 38700 (recalculated)
    }

    // ========================================
    // Out-of-Range Derived Altitude Tests
    // ========================================

    /// <summary>
    /// Verifies that derived geometric altitude is skipped when the sum of
    /// barometric altitude and delta exceeds the valid Altitude range [-2000, 126700].
    /// This prevents ArgumentOutOfRangeException from crashing the consumer task.
    /// </summary>
    [Fact]
    public void DerivedGeometricAltitude_OutOfRange_SkipsDerivation()
    {
        // Arrange — handler with default config
        TrackingConfig config = new TrackingConfigBuilder().Build();
        var handler = new AirbornePositionHandler(config);
        DateTime now = DateTime.UtcNow;

        // Aircraft with cached delta of -3150 ft (maximum negative from TC 19)
        // and existing barometric altitude of -1000 ft
        // Derived: -1000 + (-3150) = -4150, which is below -2000
        var aircraft = new Aircraft
        {
            Identification = new TrackedIdentification
            {
                ICAO = "AABBCC",
                EmergencyState = EmergencyState.NoEmergency
            },
            Position = new TrackedPosition
            {
                BarometricAltitude = Altitude.FromFeet(-1000, AltitudeType.Barometric),
                GeometricBarometricDelta = -3150,
                GeometricAltitude = null
            },
            Status = new TrackedStatus
            {
                FirstSeen = now,
                LastSeen = now,
                TotalMessages = 1
            }
        };

        // AirbornePosition message (TC 9-18) with barometric altitude = -1000 ft
        var message = new AirbornePosition(
            IcaoAddress: "AABBCC",
            Timestamp: now,
            DownlinkFormat: DownlinkFormat.ExtendedSquitter,
            SignalStrength: 128.0,
            WasCorrected: false,
            Position: null,
            Altitude: Altitude.FromFeet(-1000, AltitudeType.Barometric),
            Antenna: null,
            SurveillanceStatus: SurveillanceStatus.NoAlertNoSPI);

        // Minimal ValidatedFrame and ProcessedFrame for the handler
        var validatedFrame = new ValidatedFrame(
            Data: new byte[14],
            Timestamp: now,
            Timestamp12MHz: 0,
            IcaoRaw: 0xAABBCC,
            IcaoAddress: "AABBCC",
            SignalStrength: 128.0,
            WasCorrected: false);
        var frame = new ProcessedFrame(validatedFrame, message, now);

        // Act — should NOT throw ArgumentOutOfRangeException
        Aircraft result = handler.Apply(aircraft, message, frame, now);

        // Assert — geometric altitude should remain null (derivation skipped)
        result.Position.BarometricAltitude!.Feet.Should().Be(-1000);
        result.Position.GeometricBarometricDelta.Should().Be(-3150);
        result.Position.GeometricAltitude.Should().BeNull(
            "derived geometric altitude (-4150 ft) exceeds valid range and should be skipped");
    }

    /// <summary>
    /// Verifies that derived geometric altitude is skipped when the sum exceeds
    /// the upper bound (126700 ft) of the valid Altitude range.
    /// </summary>
    [Fact]
    public void DerivedGeometricAltitude_ExceedsUpperBound_SkipsDerivation()
    {
        // Arrange
        TrackingConfig config = new TrackingConfigBuilder().Build();
        var handler = new AirbornePositionHandler(config);
        DateTime now = DateTime.UtcNow;

        // Barometric = 126000 ft + delta = +3150 ft = 129150, exceeds 126700
        var aircraft = new Aircraft
        {
            Identification = new TrackedIdentification
            {
                ICAO = "AABBCC",
                EmergencyState = EmergencyState.NoEmergency
            },
            Position = new TrackedPosition
            {
                BarometricAltitude = Altitude.FromFeet(126000, AltitudeType.Barometric),
                GeometricBarometricDelta = 3150,
                GeometricAltitude = null
            },
            Status = new TrackedStatus
            {
                FirstSeen = now,
                LastSeen = now,
                TotalMessages = 1
            }
        };

        var message = new AirbornePosition(
            IcaoAddress: "AABBCC",
            Timestamp: now,
            DownlinkFormat: DownlinkFormat.ExtendedSquitter,
            SignalStrength: 128.0,
            WasCorrected: false,
            Position: null,
            Altitude: Altitude.FromFeet(126000, AltitudeType.Barometric),
            Antenna: null,
            SurveillanceStatus: SurveillanceStatus.NoAlertNoSPI);

        var validatedFrame = new ValidatedFrame(
            Data: new byte[14],
            Timestamp: now,
            Timestamp12MHz: 0,
            IcaoRaw: 0xAABBCC,
            IcaoAddress: "AABBCC",
            SignalStrength: 128.0,
            WasCorrected: false);
        var frame = new ProcessedFrame(validatedFrame, message, now);

        // Act
        Aircraft result = handler.Apply(aircraft, message, frame, now);

        // Assert
        result.Position.BarometricAltitude!.Feet.Should().Be(126000);
        result.Position.GeometricAltitude.Should().BeNull(
            "derived geometric altitude (129150 ft) exceeds valid range and should be skipped");
    }
}
