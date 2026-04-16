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

namespace Aeromux.CLI.Commands.Daemon.Api;

// === Aircraft List ===

/// <summary>
/// Response for GET /api/v1/aircraft.
/// </summary>
public sealed record AircraftListResponse(
    int Count,
    DateTime Timestamp,
    IReadOnlyList<AircraftListItem> Aircraft);

/// <summary>
/// Compact projection of an aircraft for the list endpoint.
/// </summary>
public sealed record AircraftListItem(
    string ICAO,
    string? Callsign,
    string? Squawk,
    AircraftCategory? Category,
    GeographicCoordinate? Coordinate,
    Altitude? BarometricAltitude,
    Altitude? GeometricAltitude,
    bool IsOnGround,
    Velocity? Speed,
    double? Track,
    Velocity? SpeedOnGround,
    double? TrackOnGround,
    int? VerticalRate,
    double? SignalStrength,
    int TotalMessages,
    DateTime LastSeen,
    bool DatabaseEnabled,
    string? Registration,
    string? TypeCode,
    string? OperatorName,
    bool? Military,
    bool? Ladd,
    bool? Pia);

// === Aircraft Detail Sections ===

/// <summary>
/// Identification section for the detail endpoint.
/// </summary>
public sealed record DetailIdentification(
    string ICAO,
    string? Callsign,
    string? Squawk,
    AircraftCategory? Category,
    EmergencyState EmergencyState,
    FlightStatus? FlightStatus,
    AdsbVersion? AdsbVersion);

/// <summary>
/// Status section for the detail endpoint.
/// </summary>
public sealed record DetailStatus(
    DateTime FirstSeen,
    DateTime LastSeen,
    int TotalMessages,
    int PositionMessages,
    int VelocityMessages,
    int IdentificationMessages,
    double? SignalStrength);

/// <summary>
/// Position section for the detail endpoint.
/// </summary>
public sealed record DetailPosition(
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
/// VelocityAndDynamics section for the detail endpoint.
/// Combines TrackedVelocity + TrackedFlightDynamics + relevant DataQuality fields.
/// </summary>
public sealed record DetailVelocityAndDynamics(
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
    double? MagneticDeclination,
    DateTime? LastUpdate);

/// <summary>
/// Autopilot section for the detail endpoint.
/// </summary>
public sealed record DetailAutopilot(
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
/// Meteorology section for the detail endpoint.
/// </summary>
public sealed record DetailMeteorology(
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
/// Acas section for the detail endpoint.
/// </summary>
public sealed record DetailAcas(
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
/// Capabilities section for the detail endpoint.
/// Combines TrackedCapabilities + relevant TrackedOperationalMode fields.
/// </summary>
public sealed record DetailCapabilities(
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
    int? DownlinkRequest,
    string? UtilityMessage,
    string? DataLinkCapability,
    string? SupportedBDSRegisters,
    DateTime? LastUpdate);

/// <summary>
/// DataQuality section for the detail endpoint.
/// Combines fields from Position, Velocity, DataQuality, Capabilities, and OperationalMode.
/// </summary>
public sealed record DetailDataQuality(
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

// === History ===

/// <summary>
/// Wrapper for a history type providing buffer metadata.
/// </summary>
/// <typeparam name="T">The history entry type (e.g., position, altitude, velocity).</typeparam>
public sealed record HistoryTypeWrapper<T>
{
    /// <summary>Whether this history type is enabled in the tracking configuration.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Maximum number of entries the circular buffer can hold. Absent when disabled.</summary>
    public int? Capacity { get; init; }

    /// <summary>Total number of entries currently in the buffer. Absent when disabled.</summary>
    public int? Count { get; init; }

    /// <summary>Sequence ID of the oldest entry in the buffer. Null when empty, absent when disabled.</summary>
    public long? MinSequenceId { get; init; }

    /// <summary>Sequence ID of the newest entry in the buffer. Null when empty, absent when disabled.</summary>
    public long? MaxSequenceId { get; init; }

    /// <summary>History entries in chronological order (oldest first). Absent when disabled.</summary>
    public IReadOnlyList<T>? Entries { get; init; }
}

/// <summary>
/// Position history entry.
/// </summary>
public sealed record PositionHistoryEntry(
    long SequenceId,
    DateTime Timestamp,
    GeographicCoordinate Position,
    NavigationAccuracyCategoryPosition? NACp);

/// <summary>
/// Altitude history entry.
/// </summary>
public sealed record AltitudeHistoryEntry(
    long SequenceId,
    DateTime Timestamp,
    Altitude Altitude);

/// <summary>
/// Velocity history entry.
/// </summary>
public sealed record VelocityHistoryEntry(
    long SequenceId,
    DateTime Timestamp,
    Velocity? Speed,
    double? Heading,
    double? Track,
    Velocity? SpeedOnGround,
    double? TrackOnGround,
    int? VerticalRate);

/// <summary>
/// State history entry — combined position, altitude, and velocity snapshot.
/// </summary>
public sealed record StateHistoryEntry(
    long SequenceId,
    DateTime Timestamp,
    GeographicCoordinate Position,
    NavigationAccuracyCategoryPosition? NACp,
    Altitude? Altitude,
    Velocity? Speed,
    double? Heading,
    double? Track,
    Velocity? SpeedOnGround,
    double? TrackOnGround,
    int? VerticalRate);

// === Stats ===

/// <summary>
/// Response for GET /api/v1/stats.
/// </summary>
public sealed record StatsResponse(
    string Version,
    DateTime Timestamp,
    int Uptime,
    int AircraftCount,
    int Devices,
    StatsStream Stream,
    StatsReceiver? Receiver);

/// <summary>
/// Stream statistics sub-object.
/// </summary>
public sealed record StatsStream(
    long TotalFrames,
    long ValidFrames,
    long CrcErrors,
    double FramesPerSecond);

/// <summary>
/// Receiver metadata sub-object.
/// </summary>
public sealed record StatsReceiver(
    double? Latitude,
    double? Longitude,
    int? AltitudeMeters,
    string? Name);

// === Health ===

/// <summary>
/// Response for GET /api/v1/health.
/// </summary>
public sealed record HealthResponse(
    string Status,
    int Uptime,
    int AircraftCount,
    DateTime Timestamp);

// === Error ===

/// <summary>
/// Standard error response.
/// </summary>
public sealed record ErrorResponse(string Error);
