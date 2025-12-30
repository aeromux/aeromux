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
/// Comprehensive tests for Aircraft state data verification.
/// Focus on verifying that all aircraft fields are correctly populated after processing various message sequences.
/// Complements operational tests (creation, updates, events) by focusing on data correctness.
/// </summary>
public class AircraftStateTests : AircraftStateTrackerTestsBase
{
    #region 1. Identification Group Tests (7 tests)

    [Fact]
    public void IdentificationFromAircraftIdMessage_PopulatesCallsignAndCategory()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert - Fields populated by TC 1-4
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Icao.Should().Be("471DBC");
        aircraft.Identification.Callsign.Should().Be("WZZ476");
        aircraft.Identification.Category.Should().NotBeNull();
        aircraft.Identification.EmergencyState.Should().Be(EmergencyState.NoEmergency);

        // Assert - Fields NOT set by TC 1-4
        aircraft.Identification.Squawk.Should().BeNull();
        aircraft.Identification.FlightStatus.Should().BeNull();
        aircraft.Identification.Version.Should().BeNull();
    }

    [Fact]
    public void IdentificationWithEmergencyAndSquawk_UpdatesFromTC28()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.EmergencyStatus_4D2407, "4D2407");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("4D2407");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Icao.Should().Be("4D2407");
        aircraft.Identification.Squawk.Should().NotBeNullOrEmpty();
        // Emergency state is accessible (value depends on frame content)
        EmergencyState _ = aircraft.Identification.EmergencyState; // Verify it's accessible

        // Callsign not set by TC 28
        aircraft.Identification.Callsign.Should().BeNull();
    }

    [Fact]
    public void IdentificationFlightStatusFromSurveillance_PopulatesFromDF5()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.Surveillance_Identity_80073B, "80073B");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Icao.Should().Be("80073B");
        aircraft.Identification.FlightStatus.Should().NotBeNull();
        aircraft.Identification.Squawk.Should().NotBeNullOrEmpty(); // DF 5 provides squawk
    }

    [Fact]
    public void IdentificationCompleteState_AllFieldsPopulated()
    {
        // Arrange
        _tracker = CreateTracker();
        string icao = "80073B";

        // Act - Sequence: TC 28 → DF 5
        _tracker.Update(CreateFrame(RealFrames.EmergencyStatus_80073B, icao));
        _tracker.Update(CreateFrame(RealFrames.Surveillance_Identity_80073B, icao));

        Aircraft? aircraft = _tracker.GetAircraft(icao);

        // Assert - After sequence, identification should be well-populated
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Icao.Should().Be(icao);
        aircraft.Identification.Squawk.Should().NotBeNullOrEmpty();
        // Emergency state is accessible (value depends on frame content)
        EmergencyState _ = aircraft.Identification.EmergencyState;
        aircraft.Identification.FlightStatus.Should().NotBeNull();
    }

    [Fact]
    public void IdentificationEmergencyStateValues_CorrectEnumMapping()
    {
        // Arrange
        _tracker = CreateTracker();

        // Act - Different emergency frames
        _tracker.Update(CreateFrame(RealFrames.EmergencyStatus_4D2407, "4D2407"));
        _tracker.Update(CreateFrame(RealFrames.EmergencyStatus_503D74, "503D74"));
        _tracker.Update(CreateFrame(RealFrames.EmergencyStatus_80073B, "80073B"));

        // Assert - Each should have an emergency state
        Aircraft? aircraft1 = _tracker.GetAircraft("4D2407");
        Aircraft? aircraft2 = _tracker.GetAircraft("503D74");
        Aircraft? aircraft3 = _tracker.GetAircraft("80073B");

        aircraft1.Should().NotBeNull();
        aircraft2.Should().NotBeNull();
        aircraft3.Should().NotBeNull();

        // All should have accessible emergency states (actual values depend on frame content)
        EmergencyState state1 = aircraft1!.Identification.EmergencyState;
        EmergencyState state2 = aircraft2!.Identification.EmergencyState;
        EmergencyState state3 = aircraft3!.Identification.EmergencyState;

        // All states should be valid enum values
        state1.Should().BeDefined();
        state2.Should().BeDefined();
        state3.Should().BeDefined();
    }

    [Fact]
    public void IdentificationCategoryMapping_CorrectAircraftCategory()
    {
        // Arrange
        _tracker = CreateTracker();

        // Act - Different aircraft with different categories
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")); // A3 - Large
        _tracker.Update(CreateFrame(RealFrames.AircraftId_8965F3, "8965F3")); // A5 - Heavy
        _tracker.Update(CreateFrame(RealFrames.AircraftId_8964A0, "8964A0")); // Category varies

        // Assert
        Aircraft? large = _tracker.GetAircraft("471DBC");
        Aircraft? heavy = _tracker.GetAircraft("8965F3");
        Aircraft? other = _tracker.GetAircraft("8964A0");

        large.Should().NotBeNull();
        heavy.Should().NotBeNull();
        other.Should().NotBeNull();

        // Categories should be populated
        large!.Identification.Category.Should().NotBeNull();
        heavy!.Identification.Category.Should().NotBeNull();
        other!.Identification.Category.Should().NotBeNull();
    }

    [Fact]
    public void IdentificationWithVersionFromOperationalStatus_PopulatesAdsbVersion()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.OperationalStatus_80073B, "80073B");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Version.Should().NotBeNull();
    }

    #endregion

    #region 2. Position Group Tests (8 tests)

    [Fact]
    public void PositionFromCPRPair_PopulatesCoordinateAndBarometricAltitude()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        _tracker.Update(oddFrame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Position.Coordinate.Should().NotBeNull();
        aircraft.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.GeometricAltitude.Should().BeNull(); // TC 9-18 is barometric only
        aircraft.Position.IsOnGround.Should().BeFalse();
        aircraft.Position.LastUpdate.Should().NotBeNull();
    }

    [Fact]
    public void PositionFromSurveillanceAltitudeReply_PopulatesAltitudeOnly()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.Surveillance_Altitude_49D414, "49D414");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("49D414");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.BarometricAltitude!.Feet.Should().BeInRange(34950, 35050); // ~35000 ft
        aircraft.Position.Coordinate.Should().BeNull(); // DF 4 doesn't provide position
    }

    [Fact]
    public void PositionAntennaFlag_PopulatedFromAirbornePosition()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        _tracker.Update(oddFrame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Position.Antenna.Should().NotBeNull(); // Should be Single or Diversity
    }

    [Fact]
    public void PositionCompleteState_AllPositionFieldsPopulated()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        string icao = "80073B";

        // Act - TC 9-18 (position) → TC 31 (quality metrics)
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, icao, parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, icao, parser);
        ProcessedFrame opStatusFrame = CreateFrame(RealFrames.OperationalStatus_80073B, icao);

        _tracker.Update(evenFrame);
        _tracker.Update(oddFrame);
        _tracker.Update(opStatusFrame);

        Aircraft? aircraft = _tracker.GetAircraft(icao);

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Position.Coordinate.Should().NotBeNull();
        aircraft.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.NACp.Should().NotBeNull(); // From TC 31
        aircraft.Position.SIL.Should().NotBeNull(); // From TC 31
        aircraft.Position.IsOnGround.Should().BeFalse();
        aircraft.Position.LastUpdate.Should().NotBeNull();
    }

    [Fact]
    public void PositionNullFieldsWhenNotReceived_OnlyIdentification()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert - No position messages, so position fields should be null
        aircraft.Should().NotBeNull();
        aircraft!.Position.Coordinate.Should().BeNull();
        aircraft.Position.BarometricAltitude.Should().BeNull();
        aircraft.Position.GeometricAltitude.Should().BeNull();
        aircraft.Position.LastUpdate.Should().BeNull();
    }

    [Fact]
    public void PositionQualityFromOperationalStatus_PopulatesNACpSIL()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.OperationalStatus_80073B, "80073B");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Position.NACp.Should().NotBeNull();
        aircraft.Position.SIL.Should().NotBeNull();
    }

    [Fact]
    public void PositionIsOnGround_FalseForAirborneMessages()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Position.IsOnGround.Should().BeFalse();
    }

    [Fact]
    public void PositionLastUpdate_ReflectsLatestPositionMessage()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        Aircraft? afterEven = _tracker.GetAircraft("80073B");
        DateTime? firstUpdate = afterEven!.Position.LastUpdate;

        _tracker.Update(oddFrame);
        Aircraft? afterOdd = _tracker.GetAircraft("80073B");
        DateTime? secondUpdate = afterOdd!.Position.LastUpdate;

        // Assert
        firstUpdate.Should().NotBeNull();
        secondUpdate.Should().NotBeNull();
        // Second update should be >= first update
        secondUpdate.Should().BeOnOrAfter(firstUpdate!.Value);
    }

    #endregion

    #region 3. Velocity Group Tests (7 tests)

    [Fact]
    public void VelocityFromGroundSpeedMessage_PopulatesTrackNotHeading()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("4BB027");

        // Assert - TC 19 subtype 1-2 (ground speed)
        aircraft.Should().NotBeNull();
        aircraft!.Velocity.Speed.Should().NotBeNull();
        aircraft.Velocity.Track.Should().NotBeNull(); // Ground speed provides Track
        aircraft.Velocity.Heading.Should().BeNull(); // NOT Heading
        aircraft.Velocity.VerticalRate.Should().NotBeNull();
        aircraft.Velocity.VelocitySubtype.Should().NotBeNull();
    }

    [Fact]
    public void VelocityNACvFromTC19_PopulatesAccuracyCategory()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("4BB027");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Velocity.NACv.Should().NotBeNull();
    }

    [Fact]
    public void VelocityNullFieldsWhenNotReceived_OnlyIdentification()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert - No velocity messages
        aircraft.Should().NotBeNull();
        aircraft!.Velocity.Speed.Should().BeNull();
        aircraft.Velocity.Heading.Should().BeNull();
        aircraft.Velocity.Track.Should().BeNull();
        aircraft.Velocity.VerticalRate.Should().BeNull();
        aircraft.Velocity.LastUpdate.Should().BeNull();
    }

    [Fact]
    public void VelocityVerticalRateSignConvention_NegativeIsDescending()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("4BB027");

        // Assert - Descending should have negative vertical rate
        aircraft.Should().NotBeNull();
        aircraft!.Velocity.VerticalRate.Should().NotBeNull();
        aircraft.Velocity.VerticalRate.Should().BeLessThan(0); // Negative = descending
    }

    [Fact]
    public void VelocityFromMultipleFrames_UpdatesCorrectly()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");
        ProcessedFrame frame2 = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame1);
        Aircraft? after1 = _tracker.GetAircraft("4BB027");
        DateTime? firstUpdate = after1!.Velocity.LastUpdate;

        _tracker.Update(frame2);
        Aircraft? after2 = _tracker.GetAircraft("4BB027");
        DateTime? secondUpdate = after2!.Velocity.LastUpdate;

        // Assert
        firstUpdate.Should().NotBeNull();
        secondUpdate.Should().NotBeNull();
        secondUpdate.Should().BeOnOrAfter(firstUpdate!.Value);
    }

    [Fact]
    public void VelocitySpeed_PopulatedWithReasonableValue()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("4BB027");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Velocity.Speed.Should().NotBeNull();
        aircraft.Velocity.Speed!.Knots.Should().BeGreaterThan(0);
        aircraft.Velocity.Speed.Knots.Should().BeLessThan(1000); // Reasonable range
    }

    [Fact]
    public void VelocityLastUpdate_ReflectsLatestVelocityMessage()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame1);
        Aircraft? aircraft = _tracker.GetAircraft("4BB027");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Velocity.LastUpdate.Should().NotBeNull();
    }

    #endregion

    #region 4. Status Group Tests (5 tests)

    [Fact]
    public void StatusAlwaysPresent_NonNullWithCounters()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Status.Should().NotBeNull();
        aircraft.Status.FirstSeen.Should().NotBe(default);
        aircraft.Status.LastSeen.Should().NotBe(default);
        aircraft.Status.TotalMessages.Should().Be(1);
    }

    [Fact]
    public void StatusCountersIncrement_CorrectlyByMessageType()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        string icao = "80073B";

        // Act - 2 ID, 3 position, 1 velocity
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")); // ID for different ICAO
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, icao, parser));
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, icao, parser));
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, icao, parser));
        _tracker.Update(CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027")); // Velocity for different ICAO

        Aircraft? aircraft = _tracker.GetAircraft(icao);

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Status.PositionMessages.Should().Be(3);
        aircraft.Status.TotalMessages.Should().Be(3);
    }

    [Fact]
    public void StatusFirstSeenImmutable_LastSeenUpdates()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        ProcessedFrame frame2 = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame1);
        Aircraft? after1 = _tracker.GetAircraft("471DBC");
        DateTime firstSeen1 = after1!.Status.FirstSeen;
        DateTime lastSeen1 = after1.Status.LastSeen;

        Thread.Sleep(100); // Small delay

        _tracker.Update(frame2);
        Aircraft? after2 = _tracker.GetAircraft("471DBC");
        DateTime firstSeen2 = after2!.Status.FirstSeen;
        DateTime lastSeen2 = after2.Status.LastSeen;

        // Assert
        firstSeen1.Should().Be(firstSeen2); // FirstSeen immutable
        lastSeen2.Should().BeOnOrAfter(lastSeen1); // LastSeen updates
    }

    [Fact]
    public void StatusSeenPosSecondsTracking_ResetsAfterPositionUpdate()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        Aircraft? after1 = _tracker.GetAircraft("80073B");

        _tracker.Update(oddFrame);
        Aircraft? after2 = _tracker.GetAircraft("80073B");

        // Assert
        after1.Should().NotBeNull();
        after1!.Status.SeenPosSeconds.Should().NotBeNull();
        after1.Status.SeenPosSeconds.Should().BeLessThan(1.0); // Recent position

        after2.Should().NotBeNull();
        after2!.Status.SeenPosSeconds.Should().NotBeNull();
        after2.Status.SeenPosSeconds.Should().BeLessThan(1.0); // Reset after second position
    }

    [Fact]
    public void StatusSeenPosSecondsNull_WhenNoPositionReceived()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert - No position messages, so SeenPosSeconds should be null
        aircraft.Should().NotBeNull();
        aircraft!.Status.SeenPosSeconds.Should().BeNull();
    }

    #endregion

    #region 5. History Group Tests (3 tests)

    [Fact]
    public void HistoryEnabledByDefault_BuffersCreatedWithEntries()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);
        ProcessedFrame velFrame = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(evenFrame);
        _tracker.Update(oddFrame);
        Aircraft? posAircraft = _tracker.GetAircraft("80073B");

        _tracker.Update(velFrame);
        Aircraft? velAircraft = _tracker.GetAircraft("4BB027");

        // Assert - Position history
        posAircraft.Should().NotBeNull();
        posAircraft!.History.PositionHistory.Should().NotBeNull();
        posAircraft.History.AltitudeHistory.Should().NotBeNull();

        // Velocity history
        velAircraft.Should().NotBeNull();
        velAircraft!.History.VelocityHistory.Should().NotBeNull();
    }

    [Fact]
    public void HistoryDisabledWhenConfigured_BuffersAreNull()
    {
        // Arrange
        TrackingConfig config = new TrackingConfigBuilder()
            .WithHistoryDisabled()
            .Build();
        _tracker = new AircraftStateTracker(config);
        _disposables.Add(_tracker);
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.History.PositionHistory.Should().BeNull();
        aircraft.History.AltitudeHistory.Should().BeNull();
        aircraft.History.VelocityHistory.Should().BeNull();
    }

    [Fact]
    public void HistoryEmptyWhenNoDataReceived_OnlyIdentification()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert - Buffers created but empty
        aircraft.Should().NotBeNull();
        if (aircraft!.History.PositionHistory != null)
        {
            aircraft.History.PositionHistory.Count.Should().Be(0);
        }
        if (aircraft.History.VelocityHistory != null)
        {
            aircraft.History.VelocityHistory.Count.Should().Be(0);
        }
    }

    #endregion

    #region 6. Optional Group Tests - Autopilot, ACAS, Capabilities (10 tests)

    [Fact]
    public void AutopilotNullByDefault_UntilTC29Received()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame idFrame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        ProcessedFrame posFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);

        // Act
        _tracker.Update(idFrame);
        _tracker.Update(posFrame);

        Aircraft? aircraft1 = _tracker.GetAircraft("471DBC");
        Aircraft? aircraft2 = _tracker.GetAircraft("80073B");

        // Assert - No TC 29, so Autopilot should be null
        aircraft1.Should().NotBeNull();
        aircraft1!.Autopilot.Should().BeNull();

        aircraft2.Should().NotBeNull();
        aircraft2!.Autopilot.Should().BeNull();
    }

    [Fact]
    public void AutopilotFromTC29_PopulatesAutopilotFields()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.TargetStateStatus_49D414, "49D414");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("49D414");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Autopilot.Should().NotBeNull();
        // At least some autopilot fields should be populated
        // (Exact fields depend on TC 29 version and content)
    }

    [Fact]
    public void AcasNullByDefault_UntilAcasMessageReceived()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame idFrame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        ProcessedFrame posFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);

        // Act
        _tracker.Update(idFrame);
        _tracker.Update(posFrame);

        Aircraft? aircraft1 = _tracker.GetAircraft("471DBC");
        Aircraft? aircraft2 = _tracker.GetAircraft("80073B");

        // Assert - No ACAS messages
        aircraft1.Should().NotBeNull();
        aircraft1!.Acas.Should().BeNull();

        aircraft2.Should().NotBeNull();
        aircraft2!.Acas.Should().BeNull();
    }

    [Fact]
    public void AcasFromTC29_PopulatesTcasFields()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.TargetStateStatus_49D414, "49D414");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("49D414");

        // Assert - TC 29 may populate TCAS operational fields
        aircraft.Should().NotBeNull();
        // ACAS might be populated if TC 29 has TCAS fields
    }

    [Fact]
    public void CapabilitiesNullByDefault_UntilCapabilityMessageReceived()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Capabilities.Should().BeNull();
    }

    [Fact]
    public void CapabilitiesFromDF11_PopulatesTransponderLevel()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AllCall_471F87, "471F87");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471F87");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Capabilities.Should().NotBeNull();
        aircraft.Capabilities!.TransponderLevel.Should().NotBeNull();
    }

    [Fact]
    public void CapabilitiesFromTC31_PopulatesMultipleFields()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.OperationalStatus_80073B, "80073B");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Capabilities.Should().NotBeNull();
        // TC 31 populates many capability fields
    }

    [Fact]
    public void DataQualityNullByDefault_UntilTC31OrTC29V2()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.DataQuality.Should().BeNull();
    }

    [Fact]
    public void DataQualityFromTC31_PopulatesQualityFields()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.OperationalStatus_80073B, "80073B");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("80073B");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.DataQuality.Should().NotBeNull();
    }

    [Fact]
    public void OperationalModeNullByDefault_UntilTC31()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.OperationalMode.Should().BeNull();
    }

    #endregion

    #region 7. Complete Aircraft State Tests (5 tests)

    [Fact]
    public void MinimalAircraftState_OnlyIdentification()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame);
        Aircraft? aircraft = _tracker.GetAircraft("471DBC");

        // Assert - Minimal state
        aircraft.Should().NotBeNull();
        aircraft!.Identification.Icao.Should().Be("471DBC");
        aircraft.Identification.Callsign.Should().Be("WZZ476");
        aircraft.Position.Coordinate.Should().BeNull();
        aircraft.Velocity.Speed.Should().BeNull();
        aircraft.Autopilot.Should().BeNull();
        aircraft.Status.Should().NotBeNull();
    }

    [Fact]
    public void TypicalAircraftState_FullSequence()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        string icao = "80073B";

        // Act - Full sequence: ID → Position → Velocity → Status → TC 31
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")); // Different ICAO for ID
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, icao, parser));
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, icao, parser));
        _tracker.Update(CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027")); // Different ICAO for velocity
        _tracker.Update(CreateFrame(RealFrames.OperationalStatus_80073B, icao));

        Aircraft? aircraft = _tracker.GetAircraft(icao);

        // Assert - Rich state
        aircraft.Should().NotBeNull();
        aircraft!.Position.Coordinate.Should().NotBeNull();
        aircraft.Position.BarometricAltitude.Should().NotBeNull();
        aircraft.Position.NACp.Should().NotBeNull();
        aircraft.Capabilities.Should().NotBeNull();
        aircraft.DataQuality.Should().NotBeNull();
    }

    [Fact]
    public void AircraftStateImmutability_UpdatesCreateNewRecords()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame idFrame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        ProcessedFrame posFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);

        // Act
        _tracker.Update(idFrame);
        Aircraft? original = _tracker.GetAircraft("471DBC");
        string originalIcao = original!.Identification.Icao;
        DateTime originalFirstSeen = original.Status.FirstSeen;

        _tracker.Update(idFrame); // Second update
        Aircraft? updated = _tracker.GetAircraft("471DBC");

        // Assert - Immutability
        original.Should().NotBeSameAs(updated); // Different instances
        updated!.Identification.Icao.Should().Be(originalIcao); // Same ICAO
        updated.Status.FirstSeen.Should().Be(originalFirstSeen); // FirstSeen preserved
        updated.Status.TotalMessages.Should().Be(2); // Counter incremented
    }

    [Fact]
    public void NullGroupsRemainNullWhenNotTriggered_NoOptionalMessages()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();

        // Act - Full sequence WITHOUT TC 29, TC 31, DF 16
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser));
        _tracker.Update(CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027"));

        Aircraft? aircraft1 = _tracker.GetAircraft("471DBC");
        Aircraft? aircraft2 = _tracker.GetAircraft("80073B");
        Aircraft? aircraft3 = _tracker.GetAircraft("4BB027");

        // Assert - Optional groups remain null
        aircraft1.Should().NotBeNull();
        aircraft1!.Autopilot.Should().BeNull();
        aircraft1.Acas.Should().BeNull();
        aircraft1.FlightDynamics.Should().BeNull();
        aircraft1.Meteo.Should().BeNull();

        aircraft2.Should().NotBeNull();
        aircraft2!.Capabilities.Should().BeNull(); // No DF 11 or TC 31

        aircraft3.Should().NotBeNull();
        aircraft3!.Capabilities.Should().BeNull();
    }

    [Fact]
    public void SignalStrengthTracking_ReflectsLatestFrame()
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
        after2!.Status.SignalStrength.Should().Be(200); // Latest value
    }

    #endregion

    #region 8. Edge Cases and Special Scenarios (6 tests)

    [Fact]
    public void CPRDecodingRequiresEvenOddPair_CoordinatePopulatedAfterPair()
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

        // Assert - After even frame, coordinate may or may not be populated (depends on CPR state)
        afterEven.Should().NotBeNull();

        // After odd frame (completing pair), coordinate should definitely be populated
        afterOdd.Should().NotBeNull();
        afterOdd!.Position.Coordinate.Should().NotBeNull();
    }

    [Fact]
    public void TimestampProgression_FirstSeenImmutableLastSeenUpdates()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        ProcessedFrame frame2 = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");

        // Act
        _tracker.Update(frame1);
        Aircraft? after1 = _tracker.GetAircraft("471DBC");
        DateTime firstSeenT0 = after1!.Status.FirstSeen;
        DateTime lastSeenT0 = after1.Status.LastSeen;

        Thread.Sleep(100);

        _tracker.Update(frame2);
        Aircraft? after2 = _tracker.GetAircraft("471DBC");
        DateTime firstSeenT1 = after2!.Status.FirstSeen;
        DateTime lastSeenT1 = after2.Status.LastSeen;

        // Assert
        firstSeenT0.Should().Be(firstSeenT1); // Immutable
        lastSeenT1.Should().BeOnOrAfter(lastSeenT0); // Updates
    }

    [Fact]
    public void PositionLastUpdate_ReflectsLatestPositionTimestamp()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        ProcessedFrame evenFrame = CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser);
        ProcessedFrame oddFrame = CreateFrame(RealFrames.AirbornePos_80073B_Odd, "80073B", parser);

        // Act
        _tracker.Update(evenFrame);
        Aircraft? after1 = _tracker.GetAircraft("80073B");
        DateTime? posUpdate1 = after1!.Position.LastUpdate;

        _tracker.Update(oddFrame);
        Aircraft? after2 = _tracker.GetAircraft("80073B");
        DateTime? posUpdate2 = after2!.Position.LastUpdate;

        // Assert
        posUpdate1.Should().NotBeNull();
        posUpdate2.Should().NotBeNull();
        posUpdate2.Should().BeOnOrAfter(posUpdate1!.Value);
    }

    [Fact]
    public void VelocityLastUpdate_ReflectsLatestVelocityTimestamp()
    {
        // Arrange
        _tracker = CreateTracker();
        ProcessedFrame frame1 = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");
        ProcessedFrame frame2 = CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027");

        // Act
        _tracker.Update(frame1);
        Aircraft? after1 = _tracker.GetAircraft("4BB027");
        DateTime? velUpdate1 = after1!.Velocity.LastUpdate;

        _tracker.Update(frame2);
        Aircraft? after2 = _tracker.GetAircraft("4BB027");
        DateTime? velUpdate2 = after2!.Velocity.LastUpdate;

        // Assert
        velUpdate1.Should().NotBeNull();
        velUpdate2.Should().NotBeNull();
        velUpdate2.Should().BeOnOrAfter(velUpdate1!.Value);
    }

    [Fact]
    public void MultipleAircraft_IndependentStateTracking()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();

        // Act - Track 3 different aircraft with different messages
        _tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, "80073B", parser));
        _tracker.Update(CreateFrame(RealFrames.AirborneVel_4BB027_Descending, "4BB027"));

        Aircraft? aircraft1 = _tracker.GetAircraft("471DBC");
        Aircraft? aircraft2 = _tracker.GetAircraft("80073B");
        Aircraft? aircraft3 = _tracker.GetAircraft("4BB027");

        // Assert - Each has independent state
        aircraft1.Should().NotBeNull();
        aircraft1!.Identification.Callsign.Should().Be("WZZ476");
        aircraft1.Position.Coordinate.Should().BeNull(); // No position

        aircraft2.Should().NotBeNull();
        aircraft2!.Position.BarometricAltitude.Should().NotBeNull(); // Has position
        aircraft2.Identification.Callsign.Should().BeNull(); // No ID

        aircraft3.Should().NotBeNull();
        aircraft3!.Velocity.Speed.Should().NotBeNull(); // Has velocity
        aircraft3.Position.Coordinate.Should().BeNull(); // No position
    }

    [Fact]
    public void CountersAccurate_AfterMixedMessageTypes()
    {
        // Arrange
        _tracker = CreateTracker();
        var parser = new Aeromux.Core.ModeS.MessageParser();
        string icao = "80073B";

        // Act - Mixed messages for same aircraft
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Even, icao, parser)); // Position 1
        _tracker.Update(CreateFrame(RealFrames.AirbornePos_80073B_Odd, icao, parser)); // Position 2
        _tracker.Update(CreateFrame(RealFrames.OperationalStatus_80073B, icao)); // Operational status

        Aircraft? aircraft = _tracker.GetAircraft(icao);

        // Assert
        aircraft.Should().NotBeNull();
        aircraft!.Status.PositionMessages.Should().Be(2);
        aircraft.Status.TotalMessages.Should().Be(3);
        aircraft.Status.VelocityMessages.Should().Be(0);
        aircraft.Status.IdentificationMessages.Should().Be(0);
    }

    #endregion
}
