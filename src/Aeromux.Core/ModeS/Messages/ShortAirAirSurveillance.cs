using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Short air-air surveillance message (ACAS coordination).
/// Corresponds to Downlink Format 0.
/// </summary>
/// <remarks>
/// Used for ACAS (Airborne Collision Avoidance System) coordination between aircraft.
/// Contains altitude and flight status for collision avoidance calculations.
/// Structure identical to DF 4, but semantic purpose is aircraft-to-aircraft coordination.
/// Traffic: Less than 1% of Mode S messages.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 0).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Altitude">Decoded altitude (null if unavailable or invalid).</param>
/// <param name="FlightStatus">Flight status (airborne/ground and alert conditions).</param>
public sealed record ShortAirAirSurveillance(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    Altitude? Altitude,
    FlightStatus FlightStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
