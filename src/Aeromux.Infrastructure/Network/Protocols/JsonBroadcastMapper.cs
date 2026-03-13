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

using Aeromux.Core.Tracking;

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Maps internal Aircraft records to consumer-friendly JSON broadcast models.
/// Produces the same JSON structure as the REST API detail endpoint.
/// Fields are renamed, regrouped, and combined from multiple tracked classes.
/// </summary>
public static class JsonBroadcastMapper
{
    /// <summary>
    /// Maps the Identification section.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The identification broadcast section.</returns>
    public static BroadcastIdentification ToIdentification(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new BroadcastIdentification(
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
    /// <returns>The status broadcast section.</returns>
    public static BroadcastStatus ToStatus(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new BroadcastStatus(
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
    /// <returns>The position broadcast section.</returns>
    public static BroadcastPosition ToPosition(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        return new BroadcastPosition(
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
    /// <returns>The velocity and dynamics broadcast section.</returns>
    public static BroadcastVelocityAndDynamics ToVelocityAndDynamics(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedFlightDynamics? dynamics = aircraft.FlightDynamics;

        return new BroadcastVelocityAndDynamics(
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
            LastUpdate: aircraft.Velocity.LastUpdate);
    }

    /// <summary>
    /// Maps the Autopilot section.
    /// Returns null if no autopilot data has been received.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The autopilot broadcast section, or null if no data received.</returns>
    public static BroadcastAutopilot? ToAutopilot(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedAutopilot? ap = aircraft.Autopilot;
        if (ap == null)
        {
            return null;
        }

        return new BroadcastAutopilot(
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
    /// <returns>The meteorology broadcast section, or null if no data received.</returns>
    public static BroadcastMeteorology? ToMeteorology(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedMeteo? meteo = aircraft.Meteo;
        if (meteo == null)
        {
            return null;
        }

        return new BroadcastMeteorology(
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
    /// <returns>The ACAS broadcast section, or null if no data received.</returns>
    public static BroadcastAcas? ToAcas(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedAcas? acas = aircraft.Acas;
        if (acas == null)
        {
            return null;
        }

        return new BroadcastAcas(
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
    /// <returns>The capabilities broadcast section, or null if no data received.</returns>
    public static BroadcastCapabilities? ToCapabilities(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedCapabilities? caps = aircraft.Capabilities;
        TrackedOperationalMode? opMode = aircraft.OperationalMode;

        if (caps == null && opMode == null)
        {
            return null;
        }

        return new BroadcastCapabilities(
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
            LastUpdate: caps?.LastUpdate ?? opMode?.LastUpdate);
    }

    /// <summary>
    /// Maps the DataQuality section.
    /// Combines fields from Position, Velocity, DataQuality, Capabilities, and OperationalMode.
    /// Returns null if all contributing nullable classes are null.
    /// </summary>
    /// <param name="aircraft">The aircraft to map.</param>
    /// <returns>The data quality broadcast section, or null if no data received.</returns>
    public static BroadcastDataQuality? ToDataQuality(Aircraft aircraft)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        TrackedDataQuality? dq = aircraft.DataQuality;
        TrackedCapabilities? caps = aircraft.Capabilities;
        TrackedOperationalMode? opMode = aircraft.OperationalMode;

        if (dq == null && caps == null && opMode == null)
        {
            return null;
        }

        return new BroadcastDataQuality(
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
}
