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

using System.Reflection;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;

namespace Aeromux.CLI.Commands.Daemon.Api;

/// <summary>
/// Maps internal Aircraft records to consumer-friendly API response models.
/// This is NOT a direct serialization — fields are renamed, regrouped, and combined
/// from multiple tracked classes to match the API specification.
/// </summary>
public static class DaemonApiMapper
{
    private static readonly string AppVersion = (Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown")
        .Split('+')[0];

    /// <summary>
    /// Maps an Aircraft to a compact list item projection.
    /// </summary>
    /// <param name="aircraft">The aircraft to project.</param>
    /// <returns>A compact list item for the aircraft list endpoint.</returns>
    public static AircraftListItem ToListItem(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new AircraftListItem(
            ICAO: aircraft.Identification.ICAO,
            Callsign: aircraft.Identification.Callsign,
            Squawk: aircraft.Identification.Squawk,
            Category: aircraft.Identification.Category,
            Coordinate: aircraft.Position.Coordinate,
            BarometricAltitude: aircraft.Position.BarometricAltitude,
            GeometricAltitude: aircraft.Position.GeometricAltitude,
            IsOnGround: aircraft.Position.IsOnGround,
            Speed: aircraft.Velocity.Speed,
            Track: aircraft.Velocity.Track,
            SpeedOnGround: aircraft.Velocity.GroundSpeed,
            TrackOnGround: aircraft.Velocity.GroundTrack,
            VerticalRate: aircraft.Velocity.VerticalRate,
            SignalStrength: aircraft.Status.SignalStrengthDecibel,
            TotalMessages: aircraft.Status.TotalMessages,
            LastSeen: aircraft.Status.LastSeen,
            DatabaseEnabled: aircraft.DatabaseEnabled,
            Registration: aircraft.DatabaseRecord.Registration,
            TypeCode: aircraft.DatabaseRecord.TypeCode,
            OperatorName: aircraft.DatabaseRecord.OperatorName);
    }

    /// <summary>
    /// Maps the Identification section.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The identification detail section.</returns>
    public static DetailIdentification ToIdentification(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new DetailIdentification(
            ICAO: aircraft.Identification.ICAO,
            Callsign: aircraft.Identification.Callsign,
            Squawk: aircraft.Identification.Squawk,
            Category: aircraft.Identification.Category,
            EmergencyState: aircraft.Identification.EmergencyState,
            FlightStatus: aircraft.Identification.FlightStatus,
            AdsbVersion: aircraft.Identification.Version);
    }

    /// <summary>
    /// Maps the DatabaseRecord section.
    /// Returns null if database enrichment is disabled.
    /// Returns AircraftDatabaseRecord directly (all-null fields if no match).
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The database record, or null if database enrichment is disabled.</returns>
    public static AircraftDatabaseRecord? ToDatabaseRecord(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        if (!aircraft.DatabaseEnabled)
        {
            return null;
        }

        return aircraft.DatabaseRecord;
    }

    /// <summary>
    /// Maps the Status section.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The status detail section.</returns>
    public static DetailStatus ToStatus(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new DetailStatus(
            FirstSeen: aircraft.Status.FirstSeen,
            LastSeen: aircraft.Status.LastSeen,
            TotalMessages: aircraft.Status.TotalMessages,
            PositionMessages: aircraft.Status.PositionMessages,
            VelocityMessages: aircraft.Status.VelocityMessages,
            IdentificationMessages: aircraft.Status.IdentificationMessages,
            SignalStrength: aircraft.Status.SignalStrengthDecibel);
    }

    /// <summary>
    /// Maps the Position section.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The position detail section.</returns>
    public static DetailPosition ToPosition(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new DetailPosition(
            Coordinate: aircraft.Position.Coordinate,
            BarometricAltitude: aircraft.Position.BarometricAltitude,
            GeometricAltitude: aircraft.Position.GeometricAltitude,
            GeometricBarometricDelta: aircraft.Position.GeometricBarometricDelta,
            IsOnGround: aircraft.Position.IsOnGround,
            MovementCategory: aircraft.Position.MovementCategory,
            Source: aircraft.Position.PositionSource,
            HadMlatPosition: aircraft.Position.HadMlatPosition,
            LastUpdate: aircraft.Position.LastUpdate);
    }

    /// <summary>
    /// Maps the VelocityAndDynamics section.
    /// Combines TrackedVelocity + TrackedFlightDynamics + relevant DataQuality fields.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The velocity and dynamics detail section.</returns>
    public static DetailVelocityAndDynamics ToVelocityAndDynamics(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedFlightDynamics? dynamics = aircraft.FlightDynamics;

        return new DetailVelocityAndDynamics(
            Speed: aircraft.Velocity.Speed,
            IndicatedAirspeed: aircraft.Velocity.CommBIndicatedAirspeed,
            TrueAirspeed: aircraft.Velocity.CommBTrueAirspeed,
            GroundSpeed: aircraft.Velocity.CommBGroundSpeed,
            MachNumber: dynamics?.MachNumber,
            Track: aircraft.Velocity.Track,
            TrackAngle: aircraft.Velocity.TrackAngle,
            MagneticHeading: dynamics?.MagneticHeading,
            TrueHeading: dynamics?.TrueHeading,
            Heading: aircraft.Velocity.Heading,
            HeadingType: aircraft.DataQuality?.HeadingType,
            HorizontalReference: aircraft.DataQuality?.HorizontalReference,
            VerticalRate: aircraft.Velocity.VerticalRate,
            BarometricVerticalRate: dynamics?.BarometricVerticalRate,
            InertialVerticalRate: dynamics?.InertialVerticalRate,
            RollAngle: dynamics?.RollAngle,
            TrackRate: dynamics?.TrackRate,
            SpeedOnGround: aircraft.Velocity.GroundSpeed,
            TrackOnGround: aircraft.Velocity.GroundTrack,
            MagneticDeclination: dynamics?.MagneticDeclination?.Declination,
            LastUpdate: aircraft.Velocity.LastUpdate);
    }

    /// <summary>
    /// Maps the Autopilot section.
    /// Returns null if no autopilot data has been received.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The autopilot detail section, or null if no data received.</returns>
    public static DetailAutopilot? ToAutopilot(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedAutopilot? ap = aircraft.Autopilot;
        if (ap == null)
        {
            return null;
        }

        return new DetailAutopilot(
            AutopilotEngaged: ap.AutopilotEngaged,
            SelectedAltitude: ap.SelectedAltitude,
            AltitudeSource: ap.AltitudeSource,
            SelectedHeading: ap.SelectedHeading,
            BarometricPressureSetting: ap.BarometricPressureSetting,
            VerticalMode: ap.VerticalMode,
            HorizontalMode: ap.HorizontalMode,
            VNAVMode: ap.VNAVMode,
            LNAVMode: ap.LNAVMode,
            AltitudeHoldMode: ap.AltitudeHoldMode,
            ApproachMode: ap.ApproachMode,
            LastUpdate: ap.LastUpdate);
    }

    /// <summary>
    /// Maps the Meteorology section.
    /// Returns null if no meteorology data has been received.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The meteorology detail section, or null if no data received.</returns>
    public static DetailMeteorology? ToMeteorology(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedMeteo? meteo = aircraft.Meteo;
        if (meteo == null)
        {
            return null;
        }

        return new DetailMeteorology(
            WindSpeed: meteo.WindSpeed,
            WindDirection: meteo.WindDirection,
            TotalAirTemperature: meteo.TotalAirTemperature,
            StaticAirTemperature: meteo.StaticAirTemperature,
            Pressure: meteo.Pressure,
            RadioHeight: meteo.RadioHeight,
            Turbulence: meteo.Turbulence,
            WindShear: meteo.WindShear,
            Microburst: meteo.Microburst,
            Icing: meteo.Icing,
            WakeVortex: meteo.WakeVortex,
            Humidity: meteo.Humidity,
            FigureOfMerit: meteo.FigureOfMerit,
            LastUpdate: meteo.LastUpdate);
    }

    /// <summary>
    /// Maps the Acas section.
    /// Returns null if no ACAS data has been received.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The ACAS detail section, or null if no data received.</returns>
    public static DetailAcas? ToAcas(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedAcas? acas = aircraft.Acas;
        if (acas == null)
        {
            return null;
        }

        return new DetailAcas(
            TCASOperational: acas.TCASOperational,
            SensitivityLevel: acas.SensitivityLevel,
            CrossLinkCapability: acas.CrossLinkCapability,
            ReplyInformation: acas.ReplyInformation,
            TCASRAActive_BDS30: acas.TCASRAActive,
            TCASRAActive_TC31: aircraft.OperationalMode?.TCASRAActive,
            ResolutionAdvisoryTerminated: acas.ResolutionAdvisoryTerminated,
            MultipleThreatEncounter: acas.MultipleThreatEncounter,
            RACNotBelow: acas.RACNotBelow,
            RACNotAbove: acas.RACNotAbove,
            RACNotLeft: acas.RACNotLeft,
            RACNotRight: acas.RACNotRight,
            LastUpdate: acas.LastUpdate);
    }

    /// <summary>
    /// Maps the Capabilities section.
    /// Combines TrackedCapabilities + relevant TrackedOperationalMode fields.
    /// Returns null if both Capabilities and OperationalMode are null.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The capabilities detail section, or null if no data received.</returns>
    public static DetailCapabilities? ToCapabilities(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedCapabilities? caps = aircraft.Capabilities;
        TrackedOperationalMode? opMode = aircraft.OperationalMode;

        if (caps == null && opMode == null)
        {
            return null;
        }

        return new DetailCapabilities(
            AdsbVersion: aircraft.Identification.Version,
            TransponderLevel: caps?.TransponderLevel,
            ADSB1090ES: caps?.ADSB1090ES,
            UAT978Support: caps?.UAT978Support,
            LowPower1090ES: caps?.LowPower1090ES,
            TCASCapability: caps?.TCASCapability,
            CockpitDisplayTraffic: caps?.CockpitDisplayTraffic,
            AirReferencedVelocity: caps?.AirReferencedVelocity,
            TargetStateReporting: caps?.TargetStateReporting,
            TrajectoryChangeLevel: caps?.TrajectoryChangeLevel,
            PositionOffsetApplied: caps?.PositionOffsetApplied,
            IdentSwitchActive: opMode?.IdentSwitchActive,
            ReceivingATCServices: opMode?.ReceivingATCServices,
            Dimensions: caps?.Dimensions,
            GPSLateralOffset: opMode?.GPSLateralOffset,
            GPSLongitudinalOffset: opMode?.GPSLongitudinalOffset,
            DownlinkRequest: opMode?.DownlinkRequest,
            UtilityMessage: opMode?.UtilityMessage.HasValue == true
                ? $"0x{opMode.UtilityMessage.Value:X2}" : null,
            DataLinkCapability: caps?.DataLinkCapabilityBits.HasValue == true
                ? $"0x{caps.DataLinkCapabilityBits.Value:X4}" : null,
            SupportedBDSRegisters: caps?.SupportedBDSRegisters.HasValue == true
                ? $"0x{caps.SupportedBDSRegisters.Value:X14}" : null,
            LastUpdate: caps?.LastUpdate ?? opMode?.LastUpdate);
    }

    /// <summary>
    /// Maps the DataQuality section.
    /// Combines fields from Position, Velocity, DataQuality, Capabilities, and OperationalMode.
    /// Returns null if all contributing nullable classes are null.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The data quality detail section, or null if no data received.</returns>
    public static DetailDataQuality? ToDataQuality(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedDataQuality? dq = aircraft.DataQuality;
        TrackedCapabilities? caps = aircraft.Capabilities;
        TrackedOperationalMode? opMode = aircraft.OperationalMode;

        if (dq == null && caps == null && opMode == null)
        {
            return null;
        }

        return new DetailDataQuality(
            Antenna_TC918: aircraft.Position.Antenna,
            Antenna_TC31: opMode?.SingleAntenna,
            NACp_TC918: aircraft.Position.NACp,
            NACp_TC29: dq?.NACp_TC29,
            NACv_TC19: aircraft.Velocity.NACv,
            NACv_TC31: caps?.NACv,
            NICbaro_TC918: aircraft.Position.NICbaro,
            NICbaro_TC29: dq?.NICbaro_TC29,
            NICSupplementA: dq?.NICSupplementA,
            NICSupplementC: caps?.NICSupplementC,
            SIL_TC918: aircraft.Position.SIL,
            SIL_TC29: dq?.SIL_TC29,
            SILSupplement: dq?.SILSupplement,
            GeometricVerticalAccuracy: dq?.GeometricVerticalAccuracy,
            SystemDesignAssurance: opMode?.SystemDesignAssurance,
            LastUpdate: dq?.LastUpdate ?? caps?.LastUpdate ?? opMode?.LastUpdate);
    }

    // === Position History ===

    /// <summary>
    /// Maps position history entries.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>Position history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<PositionHistoryEntry> ToPositionHistory(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<PositionSnapshot>? buffer = aircraft.History.PositionHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<PositionHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<PositionHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAllWithSequenceIds()
                .Select(s => new PositionHistoryEntry(s.SequenceId, s.Item.Timestamp, s.Item.Position, s.Item.NACp))
                .ToArray()
        };
    }

    /// <summary>
    /// Maps position history entries with a limit.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="limit">Maximum number of recent entries to return.</param>
    /// <returns>Position history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<PositionHistoryEntry> ToPositionHistory(Aircraft aircraft, int limit)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<PositionSnapshot>? buffer = aircraft.History.PositionHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<PositionHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<PositionHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetRecentWithSequenceIds(limit)
                .Select(s => new PositionHistoryEntry(s.SequenceId, s.Item.Timestamp, s.Item.Position, s.Item.NACp))
                .ToArray()
        };
    }

    /// <summary>
    /// Maps position history entries with after-based filtering.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="afterSequenceId">Return only entries with sequence ID greater than this value.</param>
    /// <returns>Position history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<PositionHistoryEntry> ToPositionHistory(Aircraft aircraft, long afterSequenceId)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<PositionSnapshot>? buffer = aircraft.History.PositionHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<PositionHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<PositionHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAfter(afterSequenceId)
                .Select(s => new PositionHistoryEntry(s.SequenceId, s.Item.Timestamp, s.Item.Position, s.Item.NACp))
                .ToArray()
        };
    }

    // === Altitude History ===

    /// <summary>
    /// Maps altitude history entries.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>Altitude history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<AltitudeHistoryEntry> ToAltitudeHistory(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<AltitudeSnapshot>? buffer = aircraft.History.AltitudeHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<AltitudeHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<AltitudeHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAllWithSequenceIds()
                .Select(s => new AltitudeHistoryEntry(s.SequenceId, s.Item.Timestamp, s.Item.Altitude))
                .ToArray()
        };
    }

    /// <summary>
    /// Maps altitude history entries with a limit.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="limit">Maximum number of recent entries to return.</param>
    /// <returns>Altitude history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<AltitudeHistoryEntry> ToAltitudeHistory(Aircraft aircraft, int limit)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<AltitudeSnapshot>? buffer = aircraft.History.AltitudeHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<AltitudeHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<AltitudeHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetRecentWithSequenceIds(limit)
                .Select(s => new AltitudeHistoryEntry(s.SequenceId, s.Item.Timestamp, s.Item.Altitude))
                .ToArray()
        };
    }

    /// <summary>
    /// Maps altitude history entries with after-based filtering.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="afterSequenceId">Return only entries with sequence ID greater than this value.</param>
    /// <returns>Altitude history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<AltitudeHistoryEntry> ToAltitudeHistory(Aircraft aircraft, long afterSequenceId)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<AltitudeSnapshot>? buffer = aircraft.History.AltitudeHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<AltitudeHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<AltitudeHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAfter(afterSequenceId)
                .Select(s => new AltitudeHistoryEntry(s.SequenceId, s.Item.Timestamp, s.Item.Altitude))
                .ToArray()
        };
    }

    // === Velocity History ===

    /// <summary>
    /// Maps velocity history entries.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>Velocity history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<VelocityHistoryEntry> ToVelocityHistory(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<VelocitySnapshot>? buffer = aircraft.History.VelocityHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<VelocityHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<VelocityHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAllWithSequenceIds().Select(MapVelocitySnapshot).ToArray()
        };
    }

    /// <summary>
    /// Maps velocity history entries with a limit.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="limit">Maximum number of recent entries to return.</param>
    /// <returns>Velocity history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<VelocityHistoryEntry> ToVelocityHistory(Aircraft aircraft, int limit)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<VelocitySnapshot>? buffer = aircraft.History.VelocityHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<VelocityHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<VelocityHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetRecentWithSequenceIds(limit).Select(MapVelocitySnapshot).ToArray()
        };
    }

    /// <summary>
    /// Maps velocity history entries with after-based filtering.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="afterSequenceId">Return only entries with sequence ID greater than this value.</param>
    /// <returns>Velocity history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<VelocityHistoryEntry> ToVelocityHistory(Aircraft aircraft, long afterSequenceId)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<VelocitySnapshot>? buffer = aircraft.History.VelocityHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<VelocityHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<VelocityHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAfter(afterSequenceId).Select(MapVelocitySnapshot).ToArray()
        };
    }

    // === State History ===

    /// <summary>
    /// Maps state history entries.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>State history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<StateHistoryEntry> ToStateHistory(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<StateSnapshot>? buffer = aircraft.History.StateHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<StateHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<StateHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAllWithSequenceIds().Select(MapStateSnapshot).ToArray()
        };
    }

    /// <summary>
    /// Maps state history entries with a limit.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="limit">Maximum number of recent entries to return.</param>
    /// <returns>State history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<StateHistoryEntry> ToStateHistory(Aircraft aircraft, int limit)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<StateSnapshot>? buffer = aircraft.History.StateHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<StateHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<StateHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetRecentWithSequenceIds(limit).Select(MapStateSnapshot).ToArray()
        };
    }

    /// <summary>
    /// Maps state history entries with after-based filtering.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <param name="afterSequenceId">Return only entries with sequence ID greater than this value.</param>
    /// <returns>State history with buffer metadata, or disabled wrapper if history is off.</returns>
    public static HistoryTypeWrapper<StateHistoryEntry> ToStateHistory(Aircraft aircraft, long afterSequenceId)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        CircularBuffer<StateSnapshot>? buffer = aircraft.History.StateHistory;
        if (buffer == null)
        {
            return new HistoryTypeWrapper<StateHistoryEntry> { Enabled = false };
        }

        return new HistoryTypeWrapper<StateHistoryEntry>
        {
            Enabled = true,
            Capacity = buffer.Capacity,
            Count = buffer.Count,
            MinSequenceId = buffer.MinSequenceId,
            MaxSequenceId = buffer.MaxSequenceId,
            Entries = buffer.GetAfter(afterSequenceId).Select(MapStateSnapshot).ToArray()
        };
    }

    // === Stats ===

    /// <summary>
    /// Maps a StreamStatistics to StatsResponse.
    /// </summary>
    /// <param name="stats">Stream statistics, or null if not yet available.</param>
    /// <param name="tracker">Aircraft state tracker for current aircraft count.</param>
    /// <param name="deviceCount">Number of enabled SDR devices.</param>
    /// <param name="startTime">Daemon start time for uptime calculation.</param>
    /// <param name="receiverConfig">Receiver configuration, or null if not configured.</param>
    /// <returns>The stats response with derived fields (CrcErrors, FramesPerSecond).</returns>
    public static StatsResponse ToStats(
        StreamStatistics? stats,
        IAircraftStateTracker tracker,
        int deviceCount,
        DateTime startTime,
        Core.Configuration.ReceiverConfig? receiverConfig)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        int uptime = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        StatsStream stream;
        if (stats != null)
        {
            long crcErrors = stats.TotalFrames - stats.ValidFrames;
            double fps = stats.Uptime.TotalSeconds > 0
                ? stats.ValidFrames / stats.Uptime.TotalSeconds
                : 0;

            stream = new StatsStream(
                TotalFrames: stats.TotalFrames,
                ValidFrames: stats.ValidFrames,
                CrcErrors: crcErrors,
                FramesPerSecond: Math.Round(fps, 1));
        }
        else
        {
            stream = new StatsStream(0, 0, 0, 0);
        }

        StatsReceiver? receiver = null;
        if (receiverConfig != null)
        {
            receiver = new StatsReceiver(
                Latitude: receiverConfig.Latitude,
                Longitude: receiverConfig.Longitude,
                AltitudeMeters: receiverConfig.Altitude,
                Name: receiverConfig.Name);
        }

        return new StatsResponse(
            Version: AppVersion,
            Timestamp: DateTime.UtcNow,
            Uptime: uptime,
            AircraftCount: tracker.Count,
            Devices: deviceCount,
            Stream: stream,
            Receiver: receiver);
    }

    // === Private Helpers ===

    /// <summary>
    /// Maps a VelocitySnapshot tuple to a VelocityHistoryEntry.
    /// </summary>
    private static VelocityHistoryEntry MapVelocitySnapshot((VelocitySnapshot Item, long SequenceId) s)
    {
        return new VelocityHistoryEntry(
            SequenceId: s.SequenceId,
            Timestamp: s.Item.Timestamp,
            Speed: s.Item.Velocity,
            Heading: s.Item.Heading,
            Track: s.Item.Track,
            SpeedOnGround: s.Item.GroundSpeed,
            TrackOnGround: s.Item.GroundTrack,
            VerticalRate: s.Item.VerticalRate);
    }

    /// <summary>
    /// Maps a StateSnapshot tuple to a StateHistoryEntry.
    /// Reconstructs the Altitude value object from raw int + AltitudeType.
    /// </summary>
    private static StateHistoryEntry MapStateSnapshot((StateSnapshot Item, long SequenceId) s)
    {
        Altitude? altitude = s.Item is { Altitude: not null, AltitudeType: not null }
            ? Altitude.FromFeet(s.Item.Altitude.Value, s.Item.AltitudeType.Value)
            : null;

        return new StateHistoryEntry(
            SequenceId: s.SequenceId,
            Timestamp: s.Item.Timestamp,
            Position: s.Item.Position,
            NACp: s.Item.NACp,
            Altitude: altitude,
            Speed: s.Item.Speed,
            Heading: s.Item.Heading,
            Track: s.Item.Track,
            SpeedOnGround: s.Item.SpeedOnGround,
            TrackOnGround: s.Item.TrackOnGround,
            VerticalRate: s.Item.VerticalRate);
    }
}
