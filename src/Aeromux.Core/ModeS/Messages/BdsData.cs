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

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Base class for BDS register data.
/// Each specific BDS code has its own derived record.
/// ALL 10 Comm-B BDS registers implemented.
/// </summary>
public abstract record BdsData;

/// <summary>
/// BDS 1,0: Data link capability report.
/// </summary>
/// <param name="CapabilityBits">16-bit capability flags.</param>
public sealed record Bds10DataLinkCapability(
    int CapabilityBits) : BdsData;

/// <summary>
/// BDS 1,7: Common usage GICB capability report.
/// </summary>
/// <param name="CapabilityMask">56-bit bitmask indicating supported BDS registers.</param>
public sealed record Bds17GicbCapability(
    ulong CapabilityMask) : BdsData;

/// <summary>
/// BDS 2,0: Aircraft identification (callsign).
/// </summary>
/// <param name="Callsign">Aircraft callsign (8 characters, AIS-encoded).</param>
public sealed record Bds20AircraftIdentification(
    string Callsign) : BdsData;

/// <summary>
/// BDS 3,0: ACAS Resolution Advisory (simplified - VDS validation only).
/// </summary>
public sealed record Bds30AcasResolutionAdvisory : BdsData;

/// <summary>
/// BDS 4,0: Selected vertical intention (MCP/FMS altitude, barometric pressure, navigation modes, altitude source).
/// </summary>
/// <param name="McpSelectedAltitude">MCP/FCU selected altitude in feet (null if not available).</param>
/// <param name="FmsSelectedAltitude">FMS selected altitude in feet (null if not available).</param>
/// <param name="BarometricPressureSetting">Barometric pressure in millibars (null if not available).</param>
/// <param name="NavigationModes">Active navigation modes (VNAV, ALT HOLD, APPROACH), null if not available.</param>
/// <param name="AltitudeSource">Source of altitude selection (Unknown, Aircraft, MCP, FMS), null if not available.</param>
public sealed record Bds40SelectedVerticalIntention(
    int? McpSelectedAltitude,
    int? FmsSelectedAltitude,
    double? BarometricPressureSetting,
    Bds40NavigationMode? NavigationModes,
    Bds40AltitudeSource? AltitudeSource) : BdsData;

/// <summary>
/// BDS 4,4: Meteorological routine report (wind, temperature, pressure, turbulence, humidity).
/// </summary>
/// <param name="FigureOfMerit">Figure of merit (0-7, quality indicator, null if not available).</param>
/// <param name="WindSpeed">Wind speed in knots (null if not available).</param>
/// <param name="WindDirection">Wind direction in degrees (null if not available).</param>
/// <param name="StaticAirTemperature">Static air temperature in °C (null if not available).</param>
/// <param name="Pressure">Pressure in hPa (null if not available).</param>
/// <param name="Turbulence">Turbulence severity level (Nil, Light, Moderate, Severe, null if not available).</param>
/// <param name="Humidity">Relative humidity percentage (0-100%, null if not available).</param>
public sealed record Bds44MeteorologicalRoutine(
    int? FigureOfMerit,
    int? WindSpeed,
    double? WindDirection,
    double? StaticAirTemperature,
    double? Pressure,
    Severity? Turbulence,
    double? Humidity) : BdsData;

/// <summary>
/// BDS 4,5: Meteorological hazard report (turbulence, wind shear, icing, etc).
/// </summary>
/// <param name="Turbulence">Turbulence severity level (Nil, Light, Moderate, Severe, null if not available).</param>
/// <param name="WindShear">Wind shear severity level (Nil, Light, Moderate, Severe, null if not available).</param>
/// <param name="Microburst">Microburst severity level (Nil, Light, Moderate, Severe, null if not available).</param>
/// <param name="Icing">Icing severity level (Nil, Light, Moderate, Severe, null if not available).</param>
/// <param name="WakeVortex">Wake vortex severity level (Nil, Light, Moderate, Severe, null if not available).</param>
/// <param name="StaticAirTemperature">Static air temperature in °C (null if not available).</param>
/// <param name="Pressure">Average static pressure in hPa (null if not available).</param>
/// <param name="RadioHeight">Radio height in feet (null if not available).</param>
public sealed record Bds45MeteorologicalHazard(
    Severity? Turbulence,
    Severity? WindShear,
    Severity? Microburst,
    Severity? Icing,
    Severity? WakeVortex,
    double? StaticAirTemperature,
    double? Pressure,
    int? RadioHeight) : BdsData;

/// <summary>
/// BDS 5,0: Track and turn report (roll, track, ground speed, TAS, track rate).
/// </summary>
/// <param name="RollAngle">Roll angle in degrees (null if not available).</param>
/// <param name="TrackAngle">True track angle in degrees (null if not available).</param>
/// <param name="GroundSpeed">Ground speed in knots (null if not available).</param>
/// <param name="TrueAirspeed">True airspeed in knots (null if not available).</param>
/// <param name="TrackRate">Track angle rate (rate of turn) in degrees per second (null if not available).</param>
public sealed record Bds50TrackAndTurn(
    double? RollAngle,
    double? TrackAngle,
    int? GroundSpeed,
    int? TrueAirspeed,
    double? TrackRate) : BdsData;

/// <summary>
/// BDS 5,3: Air-referenced state vector (magnetic heading, IAS, Mach, TAS, vertical rate).
/// </summary>
/// <param name="MagneticHeading">Magnetic heading in degrees (null if not available).</param>
/// <param name="IndicatedAirspeed">Indicated airspeed in knots (null if not available).</param>
/// <param name="MachNumber">Mach number (null if not available).</param>
/// <param name="TrueAirspeed">True airspeed in knots (null if not available).</param>
/// <param name="VerticalRate">Vertical rate in feet/minute (null if not available).</param>
public sealed record Bds53AirReferencedState(
    double? MagneticHeading,
    int? IndicatedAirspeed,
    double? MachNumber,
    int? TrueAirspeed,
    int? VerticalRate) : BdsData;

/// <summary>
/// BDS 6,0: Heading and speed report (magnetic heading, IAS, Mach, vertical rate).
/// </summary>
/// <param name="MagneticHeading">Magnetic heading in degrees (null if not available).</param>
/// <param name="IndicatedAirspeed">Indicated airspeed in knots (null if not available).</param>
/// <param name="MachNumber">Mach number (null if not available).</param>
/// <param name="BarometricVerticalRate">Barometric vertical rate in feet/minute (null if not available).</param>
/// <param name="InertialVerticalRate">Inertial vertical rate in feet/minute (null if not available).</param>
public sealed record Bds60HeadingAndSpeed(
    double? MagneticHeading,
    int? IndicatedAirspeed,
    double? MachNumber,
    int? BarometricVerticalRate,
    int? InertialVerticalRate) : BdsData;
