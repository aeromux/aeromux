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
using Aeromux.Core.Services;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles CommBIdentityReply messages (DF 21) for Comm-B register data and ATC coordination tracking.
/// Routes BDS register data to appropriate tracking groups (identical logic to DF 20):
/// - BDS 1,0 → TrackedCapabilities (data link capability bits)
/// - BDS 1,7 → TrackedCapabilities (supported BDS registers mask)
/// - BDS 4,0 → TrackedAutopilot (MCP/FMS altitude, pressure setting)
/// - BDS 4,4 → TrackedMeteo (wind, temperature, pressure)
/// - BDS 4,5 → TrackedMeteo (turbulence, wind shear, hazards)
/// - BDS 5,0 → TrackedFlightDynamics + TrackedVelocity (roll, track rate, track angle, true airspeed, ground speed)
/// - BDS 5,3 → TrackedFlightDynamics + TrackedVelocity (magnetic heading, mach, indicated airspeed, true airspeed)
/// - BDS 6,0 → TrackedFlightDynamics + TrackedVelocity (heading, mach, vertical rates, indicated airspeed)
/// Also extracts DF 21 metadata: DownlinkRequest, UtilityMessage → TrackedOperationalMode.
/// </summary>
public sealed class CommBIdentityReplyHandler : ITrackingHandler
{
    public Type MessageType => typeof(CommBIdentityReply);

    public Aircraft Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (CommBIdentityReply)message;

        // === OPERATIONAL MODE: DF 21 metadata ===
        // Extract DF 21 metadata for ATC coordination tracking
        // DownlinkRequest and UtilityMessage indicate interrogator communication patterns
        TrackedOperationalMode? operationalMode = aircraft.OperationalMode ?? new();
        operationalMode = operationalMode with
        {
            DownlinkRequest = msg.DownlinkRequest,
            UtilityMessage = msg.UtilityMessage,
            LastUpdate = timestamp
        };

        aircraft = aircraft with { OperationalMode = operationalMode };

        // === BDS DATA ROUTING: Route to appropriate BDS handler ===
        // Route to appropriate handler based on BDS data type
        return msg.BdsData switch
        {
            Bds10DataLinkCapability data => HandleBds10(aircraft, data, timestamp),
            Bds17GicbCapability data => HandleBds17(aircraft, data, timestamp),
            Bds40SelectedVerticalIntention data => HandleBds40(aircraft, data, timestamp),
            Bds44MeteorologicalRoutine data => HandleBds44(aircraft, data, timestamp),
            Bds45MeteorologicalHazard data => HandleBds45(aircraft, data, timestamp),
            Bds50TrackAndTurn data => HandleBds50(aircraft, data, timestamp),
            Bds53AirReferencedState data => HandleBds53(aircraft, data, timestamp),
            Bds60HeadingAndSpeed data => HandleBds60(aircraft, data, timestamp),
            _ => aircraft // Unknown/unsupported BDS code, no update
        };
    }

    /// <summary>
    /// Handles BDS 1,0 (Data Link Capability) - updates TrackedCapabilities.
    /// Provides: 16-bit capability flags for Comm-A/B/C/D services.
    /// </summary>
    /// <remarks>
    /// Indicates which Mode S data link services the aircraft supports.
    /// Used by interrogators to determine which Comm-B registers can be requested.
    /// </remarks>
    private static Aircraft HandleBds10(
        Aircraft aircraft,
        Bds10DataLinkCapability data,
        DateTime timestamp)
    {
        TrackedCapabilities? capabilities = aircraft.Capabilities ?? new();
        capabilities = capabilities with
        {
            DataLinkCapabilityBits = data.CapabilityBits,
            LastUpdate = timestamp
        };

        return aircraft with { Capabilities = capabilities };
    }

    /// <summary>
    /// Handles BDS 1,7 (GICB Capability Report) - updates TrackedCapabilities.
    /// Provides: 56-bit bitmask indicating which BDS registers are supported.
    /// </summary>
    /// <remarks>
    /// Each bit represents support for a specific BDS register.
    /// Allows interrogators to intelligently query only supported registers,
    /// reducing unnecessary interrogations.
    /// </remarks>
    private static Aircraft HandleBds17(
        Aircraft aircraft,
        Bds17GicbCapability data,
        DateTime timestamp)
    {
        TrackedCapabilities? capabilities = aircraft.Capabilities ?? new();
        capabilities = capabilities with
        {
            SupportedBDSRegisters = data.CapabilityMask,
            LastUpdate = timestamp
        };

        return aircraft with { Capabilities = capabilities };
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
        DateTime timestamp)
    {
        TrackedAutopilot? existing = aircraft.Autopilot;

        // Determine selected altitude from MCP or FMS fields
        // MCP (Mode Control Panel) takes priority over FMS (Flight Management System)
        Altitude? selectedAltitude = null;
        if (data.McpSelectedAltitude.HasValue)
        {
            selectedAltitude = Altitude.FromFeet(data.McpSelectedAltitude.Value, AltitudeType.Barometric);
        }
        else if (data.FmsSelectedAltitude.HasValue)
        {
            selectedAltitude = Altitude.FromFeet(data.FmsSelectedAltitude.Value, AltitudeType.Barometric);
        }

        // Handle altitude source from BDS 4,0 explicit field or infer from altitude fields
        AltitudeSource? altitudeSource = null;
        if (data.AltitudeSource.HasValue)
        {
            // Use explicit altitude source from BDS 4,0
            altitudeSource = data.AltitudeSource.Value switch
            {
                Bds40AltitudeSource.McpFcu => AltitudeSource.McpFcu,
                Bds40AltitudeSource.Fms => AltitudeSource.Fms,
                Bds40AltitudeSource.Aircraft => AltitudeSource.Unknown,
                _ => null  // Unknown: don't update
            };
        }
        else
        {
            // Infer from which altitude field is populated (legacy logic)
            if (data.McpSelectedAltitude.HasValue)
            {
                altitudeSource = AltitudeSource.McpFcu;
            }
            else if (data.FmsSelectedAltitude.HasValue)
            {
                altitudeSource = AltitudeSource.Fms;
            }
        }

        // Extract navigation mode flags from BDS 4,0
        // IMPORTANT: Only set to true when flag is present, leave as null when absent
        // This prevents overwriting TC 29 values when BDS 4,0 doesn't have the flag set
        bool? vnavMode = null;
        bool? altitudeHoldMode = null;
        bool? approachMode = null;
        if (data.NavigationModes.HasValue)
        {
            Bds40NavigationMode modes = data.NavigationModes.Value;
            if (modes.HasFlag(Bds40NavigationMode.Vnav))
            {
                vnavMode = true;
            }

            if (modes.HasFlag(Bds40NavigationMode.AltitudeHold))
            {
                altitudeHoldMode = true;
            }

            if (modes.HasFlag(Bds40NavigationMode.Approach))
            {
                approachMode = true;
            }
        }

        // Create new autopilot state with field-level merging:
        // - Update altitude, pressure, altitude source, and navigation modes from BDS 4,0
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
            VNAVMode = vnavMode ?? existing?.VNAVMode,
            LNAVMode = existing?.LNAVMode,                                            // From TC 29 V2
            AltitudeHoldMode = altitudeHoldMode ?? existing?.AltitudeHoldMode,
            ApproachMode = approachMode ?? existing?.ApproachMode,
            LastUpdate = timestamp
        };

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
        DateTime timestamp)
    {
        TrackedMeteo? existing = aircraft.Meteo;

        // Update routine weather fields from BDS 4,4 using 'with' expression
        // Only specify fields that change; all others (hazards from BDS 4,5, TAT) are preserved
        TrackedMeteo meteo = (existing ?? new TrackedMeteo()) with
        {
            WindSpeed = data.WindSpeed ?? existing?.WindSpeed,
            WindDirection = data.WindDirection ?? existing?.WindDirection,
            StaticAirTemperature = data.StaticAirTemperature ?? existing?.StaticAirTemperature,
            Pressure = data.Pressure ?? existing?.Pressure,
            Turbulence = data.Turbulence ?? existing?.Turbulence,
            Humidity = data.Humidity ?? existing?.Humidity,
            FigureOfMerit = data.FigureOfMerit ?? existing?.FigureOfMerit,
            LastUpdate = timestamp
        };

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
        DateTime timestamp)
    {
        TrackedMeteo? existing = aircraft.Meteo;

        // Update hazard fields from BDS 4,5 using 'with' expression
        // Only specify fields that change; all others (routine weather, wind, TAT) are preserved
        TrackedMeteo meteo = (existing ?? new TrackedMeteo()) with
        {
            StaticAirTemperature = data.StaticAirTemperature ?? existing?.StaticAirTemperature,
            Pressure = data.Pressure ?? existing?.Pressure,
            Turbulence = data.Turbulence ?? existing?.Turbulence,
            WindShear = data.WindShear ?? existing?.WindShear,
            Microburst = data.Microburst ?? existing?.Microburst,
            Icing = data.Icing ?? existing?.Icing,
            WakeVortex = data.WakeVortex ?? existing?.WakeVortex,
            RadioHeight = data.RadioHeight ?? existing?.RadioHeight,
            LastUpdate = timestamp
        };

        return aircraft with { Meteo = meteo };
    }

    /// <summary>
    /// Handles BDS 5,0 (Track and Turn) - updates TrackedFlightDynamics + TrackedVelocity.
    /// Provides: Roll angle, track rate, track angle, true airspeed, ground speed.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates roll and track rate from BDS 5,0,
    /// preserves magnetic heading and mach from BDS 5,3/6,0, and vertical rates from BDS 6,0.
    /// Now also updates TrackedVelocity with track angle, TAS, and ground speed for redundancy/cross-validation.
    /// </remarks>
    private static Aircraft HandleBds50(
        Aircraft aircraft,
        Bds50TrackAndTurn data,
        DateTime timestamp)
    {
        TrackedFlightDynamics? existing = aircraft.FlightDynamics;

        // Create new dynamics state with field-level merging:
        // - Update roll angle and track rate from BDS 5,0
        // - Preserve other fields from BDS 5,3/6,0
        var dynamics = new TrackedFlightDynamics
        {
            RollAngle = data.RollAngle ?? existing?.RollAngle,
            MagneticHeading = existing?.MagneticHeading,                       // From BDS 5,3 or BDS 6,0
            TrueHeading = existing?.TrueHeading,                               // From BDS 5,3 or BDS 6,0
            MagneticDeclination = existing?.MagneticDeclination,               // From BDS 5,3 or BDS 6,0
            BarometricVerticalRate = existing?.BarometricVerticalRate,         // From BDS 6,0
            InertialVerticalRate = existing?.InertialVerticalRate,             // From BDS 6,0
            MachNumber = existing?.MachNumber,                                 // From BDS 5,3 or BDS 6,0
            TrackRate = data.TrackRate ?? existing?.TrackRate,
            LastUpdate = timestamp
        };

        // Update velocity with BDS 5,0 speed and track data
        // These fields provide redundancy to TC 19 for cross-validation
        // Note: TAS can be 0-2046 knots, but Velocity value object only supports 0-1500 knots
        TrackedVelocity velocity = aircraft.Velocity with
        {
            TrackAngle = data.TrackAngle ?? aircraft.Velocity.TrackAngle,
            CommBTrueAirspeed = data.TrueAirspeed is <= 1500
                ? Velocity.FromKnots(data.TrueAirspeed.Value, VelocityType.TrueAirspeed)
                : aircraft.Velocity.CommBTrueAirspeed,
            CommBGroundSpeed = data.GroundSpeed is <= 1500
                ? Velocity.FromKnots(data.GroundSpeed.Value, VelocityType.GroundSpeed)
                : aircraft.Velocity.CommBGroundSpeed,
            LastUpdate = timestamp
        };

        aircraft = aircraft with { FlightDynamics = dynamics, Velocity = velocity };

        // Trigger wind and temperature calculations
        return UpdateCalculatedMeteo(aircraft, timestamp);
    }

    /// <summary>
    /// Handles BDS 5,3 (Air-Referenced State) - updates TrackedFlightDynamics + TrackedVelocity.
    /// Provides: Magnetic heading, Mach number, indicated airspeed, true airspeed.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates magnetic heading and Mach from BDS 5,3,
    /// preserves roll from BDS 5,0, vertical rates from BDS 6,0.
    /// Now also updates TrackedVelocity with IAS and TAS for cross-validation with BDS 6,0.
    /// </remarks>
    private static Aircraft HandleBds53(
        Aircraft aircraft,
        Bds53AirReferencedState data,
        DateTime timestamp)
    {
        TrackedFlightDynamics? existing = aircraft.FlightDynamics;

        // Calculate true heading with magnetic declination
        double? trueHeading = existing?.TrueHeading;
        MagneticDeclination? magneticDeclination = existing?.MagneticDeclination;

        if ((data.MagneticHeading ?? existing?.MagneticHeading).HasValue && aircraft.Position.Coordinate != null)
        {
            double magneticHeading = (data.MagneticHeading ?? existing?.MagneticHeading)!.Value;
            GeographicCoordinate? coord = aircraft.Position.Coordinate;
            double altKm = (aircraft.Position.GeometricAltitude?.Feet ?? 0) * 0.0003048;

            // Get or calculate magnetic declination (service handles caching)
            magneticDeclination = MagneticDeclinationCalculator.GetOrCalculate(
                existing?.MagneticDeclination,
                coord.Latitude,
                coord.Longitude,
                altKm,
                timestamp);

            // Calculate true heading - only update if validation succeeds
            double? track = aircraft.Velocity.TrackAngle ?? aircraft.Velocity.Track;
            double? calculated = TrueHeadingCalculator.Calculate(
                magneticHeading,
                magneticDeclination.Declination,
                track);
            if (calculated.HasValue)
            {
                trueHeading = calculated;
            }
        }

        // Create new dynamics state with field-level merging:
        // - Update magnetic heading, true heading, Mach, and barometric vertical rate from BDS 5,3
        // - Preserve roll and track rate from BDS 5,0, inertial vertical rate from BDS 6,0
        var dynamics = new TrackedFlightDynamics
        {
            RollAngle = existing?.RollAngle,                       // From BDS 5,0
            MagneticHeading = data.MagneticHeading ?? existing?.MagneticHeading,
            TrueHeading = trueHeading,                             // Calculated or preserved
            MagneticDeclination = magneticDeclination,             // Cached declination
            BarometricVerticalRate = data.VerticalRate ?? existing?.BarometricVerticalRate,
            InertialVerticalRate = existing?.InertialVerticalRate,              // From BDS 6,0
            MachNumber = data.MachNumber ?? existing?.MachNumber,
            TrackRate = existing?.TrackRate,                       // From BDS 5,0
            LastUpdate = timestamp
        };

        // Update velocity with BDS 5,3 airspeed data
        // Note: IAS range 0-500 knots (OK), TAS range 0-2046 knots (validate ≤1500)
        TrackedVelocity velocity = aircraft.Velocity with
        {
            CommBIndicatedAirspeed = data.IndicatedAirspeed.HasValue
                ? Velocity.FromKnots(data.IndicatedAirspeed.Value, VelocityType.IndicatedAirspeed)
                : aircraft.Velocity.CommBIndicatedAirspeed,
            CommBTrueAirspeed = data.TrueAirspeed is <= 1500
                ? Velocity.FromKnots(data.TrueAirspeed.Value, VelocityType.TrueAirspeed)
                : aircraft.Velocity.CommBTrueAirspeed,
            LastUpdate = timestamp
        };

        aircraft = aircraft with { FlightDynamics = dynamics, Velocity = velocity };

        // Trigger wind and temperature calculations
        return UpdateCalculatedMeteo(aircraft, timestamp);
    }

    /// <summary>
    /// Handles BDS 6,0 (Heading and Speed) - updates TrackedFlightDynamics + TrackedVelocity.
    /// Provides: Magnetic heading, Mach number, barometric and inertial vertical rates, indicated airspeed.
    /// </summary>
    /// <remarks>
    /// Field-level merging: Updates heading, Mach, and vertical rates from BDS 6,0,
    /// preserves roll and track rate from BDS 5,0.
    /// Now also updates TrackedVelocity with IAS for cross-validation with BDS 5,3.
    /// </remarks>
    private static Aircraft HandleBds60(
        Aircraft aircraft,
        Bds60HeadingAndSpeed data,
        DateTime timestamp)
    {
        TrackedFlightDynamics? existing = aircraft.FlightDynamics;

        // Calculate true heading with magnetic declination
        double? trueHeading = existing?.TrueHeading;
        MagneticDeclination? magneticDeclination = existing?.MagneticDeclination;

        if ((data.MagneticHeading ?? existing?.MagneticHeading).HasValue && aircraft.Position.Coordinate != null)
        {
            double magneticHeading = (data.MagneticHeading ?? existing?.MagneticHeading)!.Value;
            GeographicCoordinate? coord = aircraft.Position.Coordinate;
            double altKm = (aircraft.Position.GeometricAltitude?.Feet ?? 0) * 0.0003048;

            // Get or calculate magnetic declination (service handles caching)
            magneticDeclination = MagneticDeclinationCalculator.GetOrCalculate(
                existing?.MagneticDeclination,
                coord.Latitude,
                coord.Longitude,
                altKm,
                timestamp);

            // Calculate true heading - only update if validation succeeds
            double? track = aircraft.Velocity.TrackAngle ?? aircraft.Velocity.Track;
            double? calculated = TrueHeadingCalculator.Calculate(
                magneticHeading,
                magneticDeclination.Declination,
                track);
            if (calculated.HasValue)
            {
                trueHeading = calculated;
            }
        }

        // Create new dynamics state with field-level merging:
        // - Update magnetic heading, true heading, Mach, and vertical rates from BDS 6,0
        // - Preserve roll and track rate from BDS 5,0
        var dynamics = new TrackedFlightDynamics
        {
            RollAngle = existing?.RollAngle,                       // From BDS 5,0
            MagneticHeading = data.MagneticHeading ?? existing?.MagneticHeading,
            TrueHeading = trueHeading,                             // Calculated or preserved
            MagneticDeclination = magneticDeclination,             // Cached declination
            BarometricVerticalRate = data.BarometricVerticalRate ?? existing?.BarometricVerticalRate,
            InertialVerticalRate = data.InertialVerticalRate ?? existing?.InertialVerticalRate,
            MachNumber = data.MachNumber ?? existing?.MachNumber,
            TrackRate = existing?.TrackRate,                       // From BDS 5,0
            LastUpdate = timestamp
        };

        // Update velocity with BDS 6,0 indicated airspeed
        TrackedVelocity velocity = aircraft.Velocity with
        {
            CommBIndicatedAirspeed = data.IndicatedAirspeed.HasValue
                ? Velocity.FromKnots(data.IndicatedAirspeed.Value, VelocityType.IndicatedAirspeed)
                : aircraft.Velocity.CommBIndicatedAirspeed,
            LastUpdate = timestamp
        };

        aircraft = aircraft with { FlightDynamics = dynamics, Velocity = velocity };

        // Trigger wind and temperature calculations
        return UpdateCalculatedMeteo(aircraft, timestamp);
    }

    /// <summary>
    /// Updates calculated meteorological values (wind and temperature) from aircraft state.
    /// Triggered after velocity/dynamics updates from BDS 5,0, 5,3, or 6,0 messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wind calculation requires: TAS, ground speed, true heading, and track (all fresh data).
    /// Temperature calculation requires: TAS and Mach number (both fresh data).
    /// </para>
    /// <para>
    /// Rate limited to 1 calculation per second per aircraft to reduce computation overhead.
    /// Calculations only performed when aircraft is airborne and source data age is less than 2.5 seconds.
    /// </para>
    /// <para>
    /// Latest wins pattern: calculated values can overwrite direct BDS 4,4 values and vice versa,
    /// ensuring most recent data is always used regardless of source.
    /// </para>
    /// </remarks>
    private static Aircraft UpdateCalculatedMeteo(
        Aircraft aircraft,
        DateTime timestamp)
    {
        TrackedMeteo? existing = aircraft.Meteo;

        // Rate limiting: skip if meteo updated less than 1 second ago
        if (existing?.LastUpdate != null &&
            (timestamp - existing.LastUpdate.Value).TotalSeconds < 1.0)
        {
            return aircraft;
        }

        MeteoCalculationHelper.TryCalculateWind(aircraft, timestamp,
            out int? windSpeed, out double? windDirection);

        MeteoCalculationHelper.TryCalculateTemperatures(aircraft, timestamp,
            out double? oat, out double? tat);

        if (windSpeed == null && windDirection == null && oat == null && tat == null)
        {
            return aircraft;
        }

        // Latest wins: calculated values overwrite existing regardless of source
        TrackedMeteo meteo = (existing ?? new TrackedMeteo()) with
        {
            WindSpeed = windSpeed ?? existing?.WindSpeed,
            WindDirection = windDirection ?? existing?.WindDirection,
            StaticAirTemperature = oat ?? existing?.StaticAirTemperature,
            TotalAirTemperature = tat ?? existing?.TotalAirTemperature,
            LastUpdate = timestamp
        };

        return aircraft with { Meteo = meteo };
    }
}
