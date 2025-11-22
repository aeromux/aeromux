namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Base class for BDS register data.
/// Each specific BDS code has its own derived record.
/// Priority 4: ALL 10 Comm-B BDS registers implemented.
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
/// BDS 4,0: Selected vertical intention (MCP/FMS altitude, barometric pressure).
/// </summary>
/// <param name="McpSelectedAltitude">MCP/FCU selected altitude in feet (null if not available).</param>
/// <param name="FmsSelectedAltitude">FMS selected altitude in feet (null if not available).</param>
/// <param name="BarometricPressureSetting">Barometric pressure in millibars (null if not available).</param>
public sealed record Bds40SelectedVerticalIntention(
    int? McpSelectedAltitude,
    int? FmsSelectedAltitude,
    double? BarometricPressureSetting) : BdsData;

/// <summary>
/// BDS 4,4: Meteorological routine report (wind, temperature, pressure).
/// </summary>
/// <param name="FigureOfMerit">Figure of merit (0-7, quality indicator, null if not available).</param>
/// <param name="WindSpeed">Wind speed in knots (null if not available).</param>
/// <param name="WindDirection">Wind direction in degrees (null if not available).</param>
/// <param name="StaticAirTemperature">Static air temperature in °C (null if not available).</param>
/// <param name="Pressure">Pressure in hPa (null if not available).</param>
public sealed record Bds44MeteorologicalRoutine(
    int? FigureOfMerit,
    int? WindSpeed,
    double? WindDirection,
    double? StaticAirTemperature,
    double? Pressure) : BdsData;

/// <summary>
/// BDS 4,5: Meteorological hazard report (turbulence, wind shear, icing, etc).
/// </summary>
/// <param name="Turbulence">Turbulence severity (0=NIL, 1=Light, 2=Moderate, 3=Severe, null if not available).</param>
/// <param name="WindShear">Wind shear severity (0-3, null if not available).</param>
/// <param name="Microburst">Microburst severity (0-3, null if not available).</param>
/// <param name="Icing">Icing severity (0-3, null if not available).</param>
/// <param name="WakeVortex">Wake vortex severity (0-3, null if not available).</param>
/// <param name="StaticAirTemperature">Static air temperature in °C (null if not available).</param>
/// <param name="Pressure">Average static pressure in hPa (null if not available).</param>
/// <param name="RadioHeight">Radio height in feet (null if not available).</param>
public sealed record Bds45MeteorologicalHazard(
    int? Turbulence,
    int? WindShear,
    int? Microburst,
    int? Icing,
    int? WakeVortex,
    double? StaticAirTemperature,
    double? Pressure,
    int? RadioHeight) : BdsData;

/// <summary>
/// BDS 5,0: Track and turn report (roll, track, ground speed, TAS).
/// </summary>
/// <param name="RollAngle">Roll angle in degrees (null if not available).</param>
/// <param name="TrackAngle">True track angle in degrees (null if not available).</param>
/// <param name="GroundSpeed">Ground speed in knots (null if not available).</param>
/// <param name="TrueAirspeed">True airspeed in knots (null if not available).</param>
public sealed record Bds50TrackAndTurn(
    double? RollAngle,
    double? TrackAngle,
    int? GroundSpeed,
    int? TrueAirspeed) : BdsData;

/// <summary>
/// BDS 5,3: Air-referenced state vector (magnetic heading, IAS, Mach, TAS).
/// </summary>
/// <param name="MagneticHeading">Magnetic heading in degrees (null if not available).</param>
/// <param name="IndicatedAirspeed">Indicated airspeed in knots (null if not available).</param>
/// <param name="MachNumber">Mach number (null if not available).</param>
/// <param name="TrueAirspeed">True airspeed in knots (null if not available).</param>
public sealed record Bds53AirReferencedState(
    double? MagneticHeading,
    int? IndicatedAirspeed,
    double? MachNumber,
    int? TrueAirspeed) : BdsData;

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
