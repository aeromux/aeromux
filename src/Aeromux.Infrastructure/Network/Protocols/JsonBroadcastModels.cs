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

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Identification section for JSON broadcast.
/// </summary>
public sealed record BroadcastIdentification(
    string ICAO,
    string? Callsign,
    string? Squawk,
    AircraftCategory? Category,
    EmergencyState EmergencyState,
    FlightStatus? FlightStatus,
    AdsbVersion? AdsbVersion);

/// <summary>
/// Status section for JSON broadcast.
/// </summary>
public sealed record BroadcastStatus(
    DateTime FirstSeen,
    DateTime LastSeen,
    int TotalMessages,
    int PositionMessages,
    int VelocityMessages,
    int IdentificationMessages,
    double? SignalStrength);

/// <summary>
/// Position section for JSON broadcast.
/// </summary>
public sealed record BroadcastPosition(
    GeographicCoordinate? Coordinate,
    Altitude? BarometricAltitude,
    Altitude? GeometricAltitude,
    int? GeometricBarometricDelta,
    bool IsOnGround,
    SurfaceMovement? MovementCategory,
    FrameSource? Source,
    bool HadMlatPosition,
    DateTime? LastUpdate);

/// <summary>
/// VelocityAndDynamics section for JSON broadcast.
/// Combines TrackedVelocity + TrackedFlightDynamics + relevant DataQuality fields.
/// </summary>
public sealed record BroadcastVelocityAndDynamics(
    Velocity? Speed,
    Velocity? IndicatedAirspeed,
    Velocity? TrueAirspeed,
    Velocity? GroundSpeed,
    double? MachNumber,
    double? Track,
    double? TrackAngle,
    double? MagneticHeading,
    double? TrueHeading,
    double? Heading,
    TargetHeadingType? HeadingType,
    HorizontalReferenceDirection? HorizontalReference,
    int? VerticalRate,
    int? BarometricVerticalRate,
    int? InertialVerticalRate,
    double? RollAngle,
    double? TrackRate,
    Velocity? SpeedOnGround,
    double? TrackOnGround,
    DateTime? LastUpdate);

/// <summary>
/// Autopilot section for JSON broadcast.
/// </summary>
public sealed record BroadcastAutopilot(
    bool? AutopilotEngaged,
    Altitude? SelectedAltitude,
    AltitudeSource? AltitudeSource,
    double? SelectedHeading,
    double? BarometricPressureSetting,
    VerticalMode? VerticalMode,
    HorizontalMode? HorizontalMode,
    bool? VNAVMode,
    bool? LNAVMode,
    bool? AltitudeHoldMode,
    bool? ApproachMode,
    DateTime? LastUpdate);

/// <summary>
/// Meteorology section for JSON broadcast.
/// </summary>
public sealed record BroadcastMeteorology(
    int? WindSpeed,
    double? WindDirection,
    double? TotalAirTemperature,
    double? StaticAirTemperature,
    double? Pressure,
    int? RadioHeight,
    Severity? Turbulence,
    Severity? WindShear,
    Severity? Microburst,
    Severity? Icing,
    Severity? WakeVortex,
    double? Humidity,
    int? FigureOfMerit,
    DateTime? LastUpdate);

/// <summary>
/// Acas section for JSON broadcast.
/// </summary>
public sealed record BroadcastAcas(
    bool? TCASOperational,
    int? SensitivityLevel,
    bool? CrossLinkCapability,
    AcasReplyInformation? ReplyInformation,
    bool? TCASRAActive_BDS30,
    bool? TCASRAActive_TC31,
    bool? ResolutionAdvisoryTerminated,
    bool? MultipleThreatEncounter,
    bool? RACNotBelow,
    bool? RACNotAbove,
    bool? RACNotLeft,
    bool? RACNotRight,
    DateTime? LastUpdate);

/// <summary>
/// Capabilities section for JSON broadcast.
/// Combines TrackedCapabilities + relevant TrackedOperationalMode fields.
/// </summary>
public sealed record BroadcastCapabilities(
    AdsbVersion? AdsbVersion,
    TransponderCapability? TransponderLevel,
    bool? ADSB1090ES,
    bool? UAT978Support,
    bool? LowPower1090ES,
    bool? TCASCapability,
    bool? CockpitDisplayTraffic,
    bool? AirReferencedVelocity,
    bool? TargetStateReporting,
    TrajectoryChangeReportCapability? TrajectoryChangeLevel,
    bool? PositionOffsetApplied,
    bool? IdentSwitchActive,
    bool? ReceivingATCServices,
    AircraftLengthAndWidth? Dimensions,
    LateralGpsAntennaOffset? GPSLateralOffset,
    LongitudinalGpsAntennaOffset? GPSLongitudinalOffset,
    DateTime? LastUpdate);

/// <summary>
/// DataQuality section for JSON broadcast.
/// Combines fields from Position, Velocity, DataQuality, Capabilities, and OperationalMode.
/// </summary>
public sealed record BroadcastDataQuality(
    AntennaFlag? Antenna_TC918,
    AntennaFlag? Antenna_TC31,
    NavigationAccuracyCategoryPosition? NACp_TC918,
    NavigationAccuracyCategoryPosition? NACp_TC29,
    NavigationAccuracyCategoryVelocity? NACv_TC19,
    NavigationAccuracyCategoryVelocity? NACv_TC31,
    BarometricAltitudeIntegrityCode? NICbaro_TC918,
    BarometricAltitudeIntegrityCode? NICbaro_TC29,
    bool? NICSupplementA,
    bool? NICSupplementC,
    SourceIntegrityLevel? SIL_TC918,
    SourceIntegrityLevel? SIL_TC29,
    SilSupplement? SILSupplement,
    GeometricVerticalAccuracy? GeometricVerticalAccuracy,
    SdaSupportedFailureCondition? SystemDesignAssurance,
    DateTime? LastUpdate);
