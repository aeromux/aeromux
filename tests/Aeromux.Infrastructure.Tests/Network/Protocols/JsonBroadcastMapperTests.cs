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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;

namespace Aeromux.Infrastructure.Tests.Network.Protocols;

/// <summary>
/// Tests for JsonBroadcastMapper - validates mapping from Aircraft domain to broadcast DTOs.
/// Ensures broadcast JSON output matches the REST API detail endpoint format.
/// </summary>
public class JsonBroadcastMapperTests
{
    private static readonly DateTime TestTime = new(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc);

    // === Identification ===

    [Fact]
    public void ToIdentification_MapsAllFields()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastIdentification result = JsonBroadcastMapper.ToIdentification(aircraft);

        result.ICAO.Should().Be("407F19");
        result.Callsign.Should().Be("VIR359");
        result.Squawk.Should().Be("2646");
        result.Category.Should().Be(AircraftCategory.Heavy);
        result.EmergencyState.Should().Be(EmergencyState.NoEmergency);
        result.FlightStatus.Should().Be(FlightStatus.AirborneNormal);
        result.AdsbVersion.Should().Be(AdsbVersion.DO260B);
    }

    [Fact]
    public void ToIdentification_ThrowsOnNull()
    {
        Action act = () => JsonBroadcastMapper.ToIdentification(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // === DatabaseRecord ===

    [Fact]
    public void ToDatabaseRecord_ReturnsRecord_WhenDatabaseEnabled()
    {
        Aircraft aircraft = CreateTestAircraft(databaseEnabled: true);

        AircraftDatabaseRecord? result = JsonBroadcastMapper.ToDatabaseRecord(aircraft);

        result.Should().NotBeNull();
        result!.Registration.Should().Be("G-VWHO");
        result.TypeCode.Should().Be("A346");
        result.OperatorName.Should().Be("Virgin Atlantic");
    }

    [Fact]
    public void ToDatabaseRecord_ReturnsNull_WhenDatabaseDisabled()
    {
        Aircraft aircraft = CreateTestAircraft(databaseEnabled: false);

        AircraftDatabaseRecord? result = JsonBroadcastMapper.ToDatabaseRecord(aircraft);

        result.Should().BeNull();
    }

    // === Status ===

    [Fact]
    public void ToStatus_MapsAllFields()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastStatus result = JsonBroadcastMapper.ToStatus(aircraft);

        result.TotalMessages.Should().Be(437);
        result.PositionMessages.Should().Be(27);
        result.VelocityMessages.Should().Be(37);
        result.IdentificationMessages.Should().Be(2);
        result.SignalStrength.Should().Be(aircraft.Status.SignalStrengthDecibel);
    }

    // === Position ===

    [Fact]
    public void ToPosition_MapsAllFields()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastPosition result = JsonBroadcastMapper.ToPosition(aircraft);

        result.Coordinate.Should().NotBeNull();
        result.Coordinate!.Latitude.Should().BeApproximately(47.3975, 0.001);
        result.Coordinate.Longitude.Should().BeApproximately(18.5238, 0.001);
        result.BarometricAltitude.Should().NotBeNull();
        result.BarometricAltitude!.Feet.Should().Be(38000);
        result.GeometricAltitude.Should().NotBeNull();
        result.GeometricBarometricDelta.Should().Be(-275);
        result.IsOnGround.Should().BeFalse();
        result.Source.Should().Be(FrameSource.Sdr);
        result.HadMlatPosition.Should().BeFalse();
        result.LastUpdate.Should().NotBeNull();
    }

    [Fact]
    public void ToPosition_RenamesPositionSourceToSource()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastPosition result = JsonBroadcastMapper.ToPosition(aircraft);

        result.Source.Should().Be(aircraft.Position.PositionSource);
    }

    // === VelocityAndDynamics ===

    [Fact]
    public void ToVelocityAndDynamics_MapsVelocityFields()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastVelocityAndDynamics result = JsonBroadcastMapper.ToVelocityAndDynamics(aircraft);

        result.Speed.Should().NotBeNull();
        result.Speed!.Knots.Should().Be(480);
        result.Track.Should().Be(294.59);
        result.VerticalRate.Should().Be(0);
        result.LastUpdate.Should().NotBeNull();
    }

    [Fact]
    public void ToVelocityAndDynamics_MergesFlightDynamics()
    {
        var dynamics = new TrackedFlightDynamics
        {
            MachNumber = 0.82,
            MagneticHeading = 290.5,
            TrueHeading = 288.3,
            BarometricVerticalRate = -64,
            InertialVerticalRate = -32,
            RollAngle = -2.5,
            TrackRate = 0.25,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(flightDynamics: dynamics);

        BroadcastVelocityAndDynamics result = JsonBroadcastMapper.ToVelocityAndDynamics(aircraft);

        result.MachNumber.Should().Be(0.82);
        result.MagneticHeading.Should().Be(290.5);
        result.TrueHeading.Should().Be(288.3);
        result.BarometricVerticalRate.Should().Be(-64);
        result.InertialVerticalRate.Should().Be(-32);
        result.RollAngle.Should().Be(-2.5);
        result.TrackRate.Should().Be(0.25);
    }

    [Fact]
    public void ToVelocityAndDynamics_MergesDataQualityFields()
    {
        var dataQuality = new TrackedDataQuality
        {
            HeadingType = TargetHeadingType.Track,
            HorizontalReference = HorizontalReferenceDirection.TrueNorth,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(dataQuality: dataQuality);

        BroadcastVelocityAndDynamics result = JsonBroadcastMapper.ToVelocityAndDynamics(aircraft);

        result.HeadingType.Should().Be(TargetHeadingType.Track);
        result.HorizontalReference.Should().Be(HorizontalReferenceDirection.TrueNorth);
    }

    [Fact]
    public void ToVelocityAndDynamics_NullDynamicsAndQuality_SetsNulls()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastVelocityAndDynamics result = JsonBroadcastMapper.ToVelocityAndDynamics(aircraft);

        result.MachNumber.Should().BeNull();
        result.MagneticHeading.Should().BeNull();
        result.TrueHeading.Should().BeNull();
        result.BarometricVerticalRate.Should().BeNull();
        result.InertialVerticalRate.Should().BeNull();
        result.RollAngle.Should().BeNull();
        result.TrackRate.Should().BeNull();
        result.HeadingType.Should().BeNull();
        result.HorizontalReference.Should().BeNull();
    }

    // === Autopilot ===

    [Fact]
    public void ToAutopilot_ReturnsNull_WhenNoData()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastAutopilot? result = JsonBroadcastMapper.ToAutopilot(aircraft);

        result.Should().BeNull();
    }

    [Fact]
    public void ToAutopilot_MapsAllFields()
    {
        var autopilot = new TrackedAutopilot
        {
            AutopilotEngaged = true,
            SelectedAltitude = Altitude.FromFeet(36000, AltitudeType.Barometric),
            AltitudeSource = AltitudeSource.McpFcu,
            SelectedHeading = 270.0,
            BarometricPressureSetting = 1013.25,
            VNAVMode = true,
            LNAVMode = false,
            AltitudeHoldMode = true,
            ApproachMode = false,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(autopilot: autopilot);

        BroadcastAutopilot? result = JsonBroadcastMapper.ToAutopilot(aircraft);

        result.Should().NotBeNull();
        result!.AutopilotEngaged.Should().BeTrue();
        result.SelectedAltitude!.Feet.Should().Be(36000);
        result.AltitudeSource.Should().Be(AltitudeSource.McpFcu);
        result.SelectedHeading.Should().Be(270.0);
        result.BarometricPressureSetting.Should().Be(1013.25);
        result.VNAVMode.Should().BeTrue();
        result.LNAVMode.Should().BeFalse();
        result.AltitudeHoldMode.Should().BeTrue();
        result.ApproachMode.Should().BeFalse();
        result.LastUpdate.Should().Be(TestTime);
    }

    // === Meteorology ===

    [Fact]
    public void ToMeteorology_ReturnsNull_WhenNoData()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastMeteorology? result = JsonBroadcastMapper.ToMeteorology(aircraft);

        result.Should().BeNull();
    }

    [Fact]
    public void ToMeteorology_MapsAllFields()
    {
        var meteo = new TrackedMeteo
        {
            WindSpeed = 45,
            WindDirection = 270.0,
            StaticAirTemperature = -56.5,
            TotalAirTemperature = -42.0,
            Pressure = 226.3,
            Turbulence = Severity.Light,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(meteo: meteo);

        BroadcastMeteorology? result = JsonBroadcastMapper.ToMeteorology(aircraft);

        result.Should().NotBeNull();
        result!.WindSpeed.Should().Be(45);
        result.WindDirection.Should().Be(270.0);
        result.StaticAirTemperature.Should().Be(-56.5);
        result.TotalAirTemperature.Should().Be(-42.0);
        result.Pressure.Should().Be(226.3);
        result.Turbulence.Should().Be(Severity.Light);
        result.LastUpdate.Should().Be(TestTime);
    }

    // === Acas ===

    [Fact]
    public void ToAcas_ReturnsNull_WhenNoData()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastAcas? result = JsonBroadcastMapper.ToAcas(aircraft);

        result.Should().BeNull();
    }

    [Fact]
    public void ToAcas_MapsFields_IncludingOperationalMode()
    {
        var acas = new TrackedAcas
        {
            TCASOperational = true,
            SensitivityLevel = 7,
            CrossLinkCapability = true,
            TCASRAActive = false,
            LastUpdate = TestTime
        };
        var opMode = new TrackedOperationalMode
        {
            TCASRAActive = true,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(acas: acas, operationalMode: opMode);

        BroadcastAcas? result = JsonBroadcastMapper.ToAcas(aircraft);

        result.Should().NotBeNull();
        result!.TCASOperational.Should().BeTrue();
        result.SensitivityLevel.Should().Be(7);
        result.CrossLinkCapability.Should().BeTrue();
        result.TCASRAActive_BDS30.Should().BeFalse();
        result.TCASRAActive_TC31.Should().BeTrue();
    }

    // === Capabilities ===

    [Fact]
    public void ToCapabilities_ReturnsNull_WhenBothNull()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastCapabilities? result = JsonBroadcastMapper.ToCapabilities(aircraft);

        result.Should().BeNull();
    }

    [Fact]
    public void ToCapabilities_MergesCapabilitiesAndOperationalMode()
    {
        var caps = new TrackedCapabilities
        {
            TransponderLevel = TransponderCapability.Level2PlusAirborne,
            TCASCapability = true,
            ADSB1090ES = true,
            LastUpdate = TestTime
        };
        var opMode = new TrackedOperationalMode
        {
            IdentSwitchActive = false,
            ReceivingATCServices = true,
            GPSLateralOffset = new LateralGpsAntennaOffset(0),
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(capabilities: caps, operationalMode: opMode);

        BroadcastCapabilities? result = JsonBroadcastMapper.ToCapabilities(aircraft);

        result.Should().NotBeNull();
        result!.AdsbVersion.Should().Be(AdsbVersion.DO260B);
        result.TransponderLevel.Should().Be(TransponderCapability.Level2PlusAirborne);
        result.TCASCapability.Should().BeTrue();
        result.ADSB1090ES.Should().BeTrue();
        result.IdentSwitchActive.Should().BeFalse();
        result.ReceivingATCServices.Should().BeTrue();
        result.GPSLateralOffset.Should().Be(new LateralGpsAntennaOffset(0));
        result.LastUpdate.Should().Be(TestTime);
    }

    [Fact]
    public void ToCapabilities_OnlyOperationalMode_ReturnsNonNull()
    {
        var opMode = new TrackedOperationalMode
        {
            IdentSwitchActive = true,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(operationalMode: opMode);

        BroadcastCapabilities? result = JsonBroadcastMapper.ToCapabilities(aircraft);

        result.Should().NotBeNull();
        result!.IdentSwitchActive.Should().BeTrue();
        result.TransponderLevel.Should().BeNull();
        result.LastUpdate.Should().Be(TestTime);
    }

    // === DataQuality ===

    [Fact]
    public void ToDataQuality_ReturnsNull_WhenAllSourcesNull()
    {
        Aircraft aircraft = CreateTestAircraft();

        BroadcastDataQuality? result = JsonBroadcastMapper.ToDataQuality(aircraft);

        result.Should().BeNull();
    }

    [Fact]
    public void ToDataQuality_MergesMultipleSources()
    {
        var dq = new TrackedDataQuality
        {
            NACp_TC29 = NavigationAccuracyCategoryPosition.LessThan10NM,
            NICbaro_TC29 = BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham,
            NICSupplementA = true,
            SIL_TC29 = SourceIntegrityLevel.PerHour1E7,
            SILSupplement = SilSupplement.PerSample,
            LastUpdate = TestTime
        };
        var caps = new TrackedCapabilities
        {
            NACv = NavigationAccuracyCategoryVelocity.LessThan3MetersPerSecond,
            NICSupplementC = false,
            LastUpdate = TestTime
        };
        var opMode = new TrackedOperationalMode
        {
            SingleAntenna = AntennaFlag.DiversityAntenna,
            SystemDesignAssurance = SdaSupportedFailureCondition.Major,
            LastUpdate = TestTime
        };
        Aircraft aircraft = CreateTestAircraft(
            dataQuality: dq, capabilities: caps, operationalMode: opMode);

        BroadcastDataQuality? result = JsonBroadcastMapper.ToDataQuality(aircraft);

        result.Should().NotBeNull();
        result!.NACp_TC29.Should().Be(NavigationAccuracyCategoryPosition.LessThan10NM);
        result.NACv_TC31.Should().Be(NavigationAccuracyCategoryVelocity.LessThan3MetersPerSecond);
        result.NICbaro_TC29.Should().Be(BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham);
        result.NICSupplementA.Should().BeTrue();
        result.NICSupplementC.Should().BeFalse();
        result.SIL_TC29.Should().Be(SourceIntegrityLevel.PerHour1E7);
        result.SILSupplement.Should().Be(SilSupplement.PerSample);
        result.Antenna_TC31.Should().Be(AntennaFlag.DiversityAntenna);
        result.SystemDesignAssurance.Should().Be(SdaSupportedFailureCondition.Major);
        result.LastUpdate.Should().Be(TestTime);
    }

    [Fact]
    public void ToDataQuality_CoalescesLastUpdate()
    {
        DateTime laterTime = TestTime.AddMinutes(5);
        var caps = new TrackedCapabilities
        {
            NACv = NavigationAccuracyCategoryVelocity.LessThan10MetersPerSecond,
            LastUpdate = laterTime
        };
        Aircraft aircraft = CreateTestAircraft(capabilities: caps);

        BroadcastDataQuality? result = JsonBroadcastMapper.ToDataQuality(aircraft);

        result.Should().NotBeNull();
        result!.LastUpdate.Should().Be(laterTime);
    }

    // === Test Helper ===

    private static Aircraft CreateTestAircraft(
        string icao = "407F19",
        string? callsign = "VIR359",
        bool databaseEnabled = true,
        TrackedAutopilot? autopilot = null,
        TrackedMeteo? meteo = null,
        TrackedAcas? acas = null,
        TrackedFlightDynamics? flightDynamics = null,
        TrackedCapabilities? capabilities = null,
        TrackedDataQuality? dataQuality = null,
        TrackedOperationalMode? operationalMode = null)
    {
        return new Aircraft
        {
            Identification = new TrackedIdentification
            {
                ICAO = icao,
                Callsign = callsign,
                Squawk = "2646",
                Category = AircraftCategory.Heavy,
                EmergencyState = EmergencyState.NoEmergency,
                FlightStatus = FlightStatus.AirborneNormal,
                Version = AdsbVersion.DO260B
            },
            Status = new TrackedStatus
            {
                SignalStrength = 20.0,
                TotalMessages = 437,
                PositionMessages = 27,
                VelocityMessages = 37,
                IdentificationMessages = 2,
                FirstSeen = TestTime.AddMinutes(-5),
                LastSeen = TestTime,
                SeenSeconds = 300
            },
            Position = new TrackedPosition
            {
                Coordinate = new GeographicCoordinate(47.39753723144531, 18.523773193359375),
                BarometricAltitude = Altitude.FromFeet(38000, AltitudeType.Barometric),
                GeometricAltitude = Altitude.FromFeet(37725, AltitudeType.Geometric),
                GeometricBarometricDelta = -275,
                IsOnGround = false,
                PositionSource = FrameSource.Sdr,
                LastUpdate = TestTime
            },
            Velocity = new TrackedVelocity
            {
                Speed = Velocity.FromKnots(480, VelocityType.GroundSpeed),
                Track = 294.59,
                VerticalRate = 0,
                LastUpdate = TestTime
            },
            DatabaseEnabled = databaseEnabled,
            DatabaseRecord = databaseEnabled
                ? new AircraftDatabaseRecord
                {
                    Registration = "G-VWHO",
                    Country = "United Kingdom",
                    TypeCode = "A346",
                    TypeDescription = "Airbus A340-642",
                    OperatorName = "Virgin Atlantic"
                }
                : AircraftDatabaseRecord.Empty,
            Autopilot = autopilot,
            FlightDynamics = flightDynamics,
            Meteo = meteo,
            Acas = acas,
            Capabilities = capabilities,
            DataQuality = dataQuality,
            OperationalMode = operationalMode
        };
    }
}
