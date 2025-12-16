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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles CommBIdentityReply messages (DF 21) for Comm-B register data tracking.
/// Routes BDS register data to appropriate tracking groups (identical logic to DF 20):
/// - BDS 4,0 → TrackedAutopilot (MCP/FMS altitude, pressure setting)
/// - BDS 4,4 → TrackedMeteo (wind, temperature, pressure)
/// - BDS 4,5 → TrackedMeteo (turbulence, wind shear, hazards)
/// - BDS 5,0 → TrackedFlightDynamics + TrackedVelocity (roll, track, speeds)
/// - BDS 5,3 → TrackedFlightDynamics + TrackedVelocity (heading, mach, speeds)
/// - BDS 6,0 → TrackedFlightDynamics + TrackedVelocity (heading, mach, vertical rates, IAS)
/// </summary>
public sealed class CommBIdentityReplyHandler : ITrackingHandler
{
    public Type MessageType => typeof(CommBIdentityReply);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (CommBIdentityReply)message;
        var changedFields = new HashSet<string>();

        // Route to appropriate handler based on BDS data type
        Aircraft updatedAircraft = msg.BdsData switch
        {
            Bds40SelectedVerticalIntention data => HandleBds40(aircraft, data, timestamp, changedFields),
            Bds44MeteorologicalRoutine data => HandleBds44(aircraft, data, timestamp, changedFields),
            Bds45MeteorologicalHazard data => HandleBds45(aircraft, data, timestamp, changedFields),
            Bds50TrackAndTurn data => HandleBds50(aircraft, data, timestamp, changedFields),
            Bds53AirReferencedState data => HandleBds53(aircraft, data, timestamp, changedFields),
            Bds60HeadingAndSpeed data => HandleBds60(aircraft, data, timestamp, changedFields),
            _ => aircraft // Unknown/unsupported BDS code, no update
        };

        return (updatedAircraft, changedFields);
    }

    /// <summary>
    /// Handles BDS 4,0 (Selected Vertical Intention) - updates TrackedAutopilot.
    /// Provides: MCP/FMS selected altitude and barometric pressure setting.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Only updates altitude/pressure fields from BDS 4,0,
    /// preserves other autopilot fields from TC 29 (heading, modes, TCAS).
    /// </remarks>
    private static Aircraft HandleBds40(
        Aircraft aircraft,
        Bds40SelectedVerticalIntention data,
        DateTime timestamp,
        HashSet<string> changedFields)
    {
        TrackedAutopilot? existing = aircraft.Autopilot;

        // Determine selected altitude source based on which field is available
        // MCP (Mode Control Panel) takes priority over FMS (Flight Management System)
        Altitude? selectedAltitude = null;
        string? altitudeSource = null;

        if (data.McpSelectedAltitude.HasValue)
        {
            selectedAltitude = Altitude.FromFeet(data.McpSelectedAltitude.Value, AltitudeType.Barometric);
            altitudeSource = "MCP/FCU";
        }
        else if (data.FmsSelectedAltitude.HasValue)
        {
            selectedAltitude = Altitude.FromFeet(data.FmsSelectedAltitude.Value, AltitudeType.Barometric);
            altitudeSource = "FMS/RNAV";
        }

        // Create new autopilot state with field-level merging:
        // - Update altitude and pressure from BDS 4,0
        // - Preserve all other fields from existing state (from TC 29)
        var autopilot = new TrackedAutopilot
        {
            SelectedAltitude = selectedAltitude ?? existing?.SelectedAltitude,
            AltitudeSource = altitudeSource ?? existing?.AltitudeSource,
            SelectedHeading = existing?.SelectedHeading,                              // From TC 29
            BarometricPressureSetting = data.BarometricPressureSetting ?? existing?.BarometricPressureSetting,
            VerticalMode = existing?.VerticalMode,                                    // From TC 29 V1
            HorizontalMode = existing?.HorizontalMode,                                // From TC 29 V1
            AutopilotEngaged = existing?.AutopilotEngaged,                            // From TC 29 V2
            VnavMode = existing?.VnavMode,                                            // From TC 29 V2
            LnavMode = existing?.LnavMode,                                            // From TC 29 V2
            AltitudeHoldMode = existing?.AltitudeHoldMode,                            // From TC 29 V2
            ApproachMode = existing?.ApproachMode,                                    // From TC 29 V2
            TcasOperational = existing?.TcasOperational,                              // From TC 29
            TcasRaActive = existing?.TcasRaActive,                                    // From TC 29 V1
            LastUpdate = timestamp
        };

        changedFields.Add(nameof(Aircraft.Autopilot));
        return aircraft with { Autopilot = autopilot };
    }

    /// <summary>
    /// Handles BDS 4,4 (Meteorological Routine) - updates TrackedMeteo.
    /// Provides: Wind speed/direction, temperature, pressure, figure of merit.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates routine weather fields from BDS 4,4,
    /// preserves hazard fields from BDS 4,5 (turbulence, wind shear, etc.).
    /// </remarks>
    private static Aircraft HandleBds44(
        Aircraft aircraft,
        Bds44MeteorologicalRoutine data,
        DateTime timestamp,
        HashSet<string> changedFields)
    {
        TrackedMeteo? existing = aircraft.Meteo;

        var meteo = new TrackedMeteo
        {
            WindSpeed = data.WindSpeed ?? existing?.WindSpeed,
            WindDirection = data.WindDirection ?? existing?.WindDirection,
            StaticAirTemperature = data.StaticAirTemperature ?? existing?.StaticAirTemperature,
            Pressure = data.Pressure ?? existing?.Pressure,
            Turbulence = existing?.Turbulence,
            WindShear = existing?.WindShear,
            Microburst = existing?.Microburst,
            Icing = existing?.Icing,
            WakeVortex = existing?.WakeVortex,
            RadioHeight = existing?.RadioHeight,
            FigureOfMerit = data.FigureOfMerit ?? existing?.FigureOfMerit,
            LastUpdate = timestamp
        };

        changedFields.Add(nameof(Aircraft.Meteo));
        return aircraft with { Meteo = meteo };
    }

    /// <summary>
    /// Handles BDS 4,5 (Meteorological Hazard) - updates TrackedMeteo.
    /// Provides: Turbulence, wind shear, microburst, icing, wake vortex severity, radio height.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates hazard fields from BDS 4,5,
    /// preserves routine weather fields from BDS 4,4 (wind, temperature, pressure).
    /// </remarks>
    private static Aircraft HandleBds45(
        Aircraft aircraft,
        Bds45MeteorologicalHazard data,
        DateTime timestamp,
        HashSet<string> changedFields)
    {
        TrackedMeteo? existing = aircraft.Meteo;

        var meteo = new TrackedMeteo
        {
            WindSpeed = existing?.WindSpeed,
            WindDirection = existing?.WindDirection,
            StaticAirTemperature = data.StaticAirTemperature ?? existing?.StaticAirTemperature,
            Pressure = data.Pressure ?? existing?.Pressure,
            Turbulence = data.Turbulence ?? existing?.Turbulence,
            WindShear = data.WindShear ?? existing?.WindShear,
            Microburst = data.Microburst ?? existing?.Microburst,
            Icing = data.Icing ?? existing?.Icing,
            WakeVortex = data.WakeVortex ?? existing?.WakeVortex,
            RadioHeight = data.RadioHeight ?? existing?.RadioHeight,
            FigureOfMerit = existing?.FigureOfMerit,
            LastUpdate = timestamp
        };

        changedFields.Add(nameof(Aircraft.Meteo));
        return aircraft with { Meteo = meteo };
    }

    /// <summary>
    /// Handles BDS 5,0 (Track and Turn) - updates TrackedFlightDynamics.
    /// Provides: Roll angle and track rate.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates roll and track rate from BDS 5,0,
    /// preserves magnetic heading and mach from BDS 5,3/6,0, and vertical rates from BDS 6,0.
    /// Note: TC 19 velocity data takes priority, so we don't update TrackedVelocity here.
    /// </remarks>
    private static Aircraft HandleBds50(
        Aircraft aircraft,
        Bds50TrackAndTurn data,
        DateTime timestamp,
        HashSet<string> changedFields)
    {
        TrackedFlightDynamics? existing = aircraft.FlightDynamics;

        var dynamics = new TrackedFlightDynamics
        {
            RollAngle = data.RollAngle ?? existing?.RollAngle,
            MagneticHeading = existing?.MagneticHeading,
            BarometricVerticalRate = existing?.BarometricVerticalRate,
            InertialVerticalRate = existing?.InertialVerticalRate,
            MachNumber = existing?.MachNumber,
            TrackRate = data.TrackRate ?? existing?.TrackRate,
            LastUpdate = timestamp
        };

        changedFields.Add(nameof(Aircraft.FlightDynamics));
        return aircraft with { FlightDynamics = dynamics };
    }

    /// <summary>
    /// Handles BDS 5,3 (Air-Referenced State) - updates TrackedFlightDynamics.
    /// Provides: Magnetic heading and Mach number.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates magnetic heading and Mach from BDS 5,3,
    /// preserves roll from BDS 5,0, vertical rates from BDS 6,0.
    /// </remarks>
    private static Aircraft HandleBds53(
        Aircraft aircraft,
        Bds53AirReferencedState data,
        DateTime timestamp,
        HashSet<string> changedFields)
    {
        TrackedFlightDynamics? existing = aircraft.FlightDynamics;

        var dynamics = new TrackedFlightDynamics
        {
            RollAngle = existing?.RollAngle,
            MagneticHeading = data.MagneticHeading ?? existing?.MagneticHeading,
            BarometricVerticalRate = existing?.BarometricVerticalRate,
            InertialVerticalRate = existing?.InertialVerticalRate,
            MachNumber = data.MachNumber ?? existing?.MachNumber,
            TrackRate = existing?.TrackRate, // Will be added with decoder extension
            LastUpdate = timestamp
        };

        changedFields.Add(nameof(Aircraft.FlightDynamics));
        return aircraft with { FlightDynamics = dynamics };
    }

    /// <summary>
    /// Handles BDS 6,0 (Heading and Speed) - updates TrackedFlightDynamics.
    /// Provides: Magnetic heading, Mach number, barometric and inertial vertical rates.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates heading, Mach, and vertical rates from BDS 6,0,
    /// preserves roll and track rate from BDS 5,0.
    /// </remarks>
    private static Aircraft HandleBds60(
        Aircraft aircraft,
        Bds60HeadingAndSpeed data,
        DateTime timestamp,
        HashSet<string> changedFields)
    {
        TrackedFlightDynamics? existing = aircraft.FlightDynamics;

        var dynamics = new TrackedFlightDynamics
        {
            RollAngle = existing?.RollAngle,
            MagneticHeading = data.MagneticHeading ?? existing?.MagneticHeading,
            BarometricVerticalRate = data.BarometricVerticalRate ?? existing?.BarometricVerticalRate,
            InertialVerticalRate = data.InertialVerticalRate ?? existing?.InertialVerticalRate,
            MachNumber = data.MachNumber ?? existing?.MachNumber,
            TrackRate = existing?.TrackRate, // Will be added with decoder extension
            LastUpdate = timestamp
        };

        changedFields.Add(nameof(Aircraft.FlightDynamics));
        return aircraft with { FlightDynamics = dynamics };
    }
}
