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
    string? TypeIcaoClass,
    string? TypeWtc,
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
    VerticalRateSource? VerticalRateSource,
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
/// Selects which entries a history request returns. With neither value set, every entry is
/// returned; <see cref="After"/> takes precedence over <see cref="Limit"/>.
/// </summary>
/// <param name="Limit">Return only the most recent N entries.</param>
/// <param name="After">Return only entries with a sequence ID greater than this value.</param>
public readonly record struct HistoryQuery(int? Limit, long? After);

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
/// State history entry: a combined position, altitude, and velocity snapshot.
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
/// Response for GET /api/v1/stats: measurements only. The configuration they run under
/// is reported by GET /api/v1/config.
/// </summary>
/// <remarks>
/// Counters are cumulative since their source started. Rates (<c>PerSecond</c>) cover
/// the last 60 seconds, or the time since start during the first minute.
/// </remarks>
public sealed record StatsResponse(
    string Version,
    DateTime Timestamp,
    int Uptime,
    int AircraftCount,
    StatsMessages Messages,
    StatsSdr? Sdr,
    IReadOnlyList<StatsBeastSource> BeastSources,
    StatsMlat? Mlat,
    StatsOutputs Outputs,
    StatsDatabase? Database);

/// <summary>
/// Messages handed to the aircraft tracker from every source (SDR, Beast and MLAT).
/// </summary>
public sealed record StatsMessages(
    long Total,
    double PerSecond,
    double AveragePerSecond);

/// <summary>
/// The SDR devices: their counters summed, with one entry per running device.
/// </summary>
public sealed record StatsSdr(
    long Extracted,
    double ExtractedPerSecond,
    long Corrected,
    double CorrectedPerSecond,
    long Duplicates,
    double DuplicatesPerSecond,
    long Messages,
    double MessagesPerSecond,
    IReadOnlyList<StatsSdrDevice> Devices);

/// <summary>
/// One running SDR device.
/// </summary>
public sealed record StatsSdrDevice(
    string Name,
    int DeviceIndex,
    double SamplesPerSecond,
    long Extracted,
    double ExtractedPerSecond,
    long Corrected,
    double CorrectedPerSecond,
    long Duplicates,
    double DuplicatesPerSecond,
    long Messages,
    double MessagesPerSecond,
    StatsMessageTypes MessageTypes);

/// <summary>
/// One Beast TCP input and its connection state.
/// </summary>
public sealed record StatsBeastSource(
    string Host,
    int Port,
    bool Connected,
    DateTime? Since,
    int Reconnects,
    long Messages,
    double MessagesPerSecond,
    StatsMessageTypes MessageTypes);

/// <summary>
/// The MLAT input.
/// </summary>
public sealed record StatsMlat(
    int Clients,
    long Messages,
    double MessagesPerSecond,
    StatsMessageTypes MessageTypes);

/// <summary>
/// A source's messages by kind. Each list holds the codes seen since the source started,
/// in code order (registers, then <c>Empty</c> and <c>Unknown</c> for Comm-B), as entries
/// rather than objects keyed by code, so every entry has a fixed shape.
/// </summary>
public sealed record StatsMessageTypes(
    IReadOnlyList<StatsDownlinkFormat> DownlinkFormats,
    IReadOnlyList<StatsTypeCode> TypeCodes,
    IReadOnlyList<StatsBdsCode> BdsCodes);

/// <summary>
/// Messages of one downlink format: in total, and per second over the last 60 seconds.
/// </summary>
public sealed record StatsDownlinkFormat(int DF, long Total, double PerSecond);

/// <summary>
/// ADS-B messages of one type code: in total, and per second over the last 60 seconds.
/// </summary>
public sealed record StatsTypeCode(int TC, long Total, double PerSecond);

/// <summary>
/// Comm-B replies of one register, written as in the documentation (<c>"4,0"</c>), or
/// <c>"Empty"</c> and <c>"Unknown"</c>: in total, and per second over the last 60 seconds.
/// </summary>
public sealed record StatsBdsCode(string BDS, long Total, double PerSecond);

/// <summary>
/// The TCP outputs; an output is null when it is disabled.
/// </summary>
public sealed record StatsOutputs(
    StatsOutput? Beast,
    StatsOutput? Json,
    StatsOutput? Sbs);

/// <summary>
/// One enabled TCP output.
/// </summary>
public sealed record StatsOutput(int Clients);

/// <summary>
/// The aircraft database used for enrichment. <c>Version</c> is null until one is loaded.
/// </summary>
public sealed record StatsDatabase(string? Version);

// === Health ===

/// <summary>
/// Response for GET /api/v1/health.
/// </summary>
/// <remarks>
/// <c>Status</c> is <c>OK</c> when no input source is failing, <c>Degraded</c> when some
/// are, and <c>Unhealthy</c> (served with 503) when all are. <c>Problems</c> has one
/// message per failing source and is empty when <c>OK</c>.
/// </remarks>
public sealed record HealthResponse(
    string Status,
    int Uptime,
    int AircraftCount,
    DateTime Timestamp,
    IReadOnlyList<string> Problems);

// === Error ===

/// <summary>
/// Standard error response.
/// </summary>
public sealed record ErrorResponse(string Error);
