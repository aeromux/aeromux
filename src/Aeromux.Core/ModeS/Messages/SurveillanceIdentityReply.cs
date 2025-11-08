using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Surveillance identity reply message.
/// Corresponds to Downlink Format 5.
/// </summary>
/// <remarks>
/// Response to Mode S identity interrogation from ground stations.
/// Contains squawk code (4-digit octal transponder code) and flight status.
/// Special squawk codes: 7700 (emergency), 7600 (radio failure), 7500 (hijack).
/// Traffic: 5-10% of Mode S messages.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 5).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="SquawkCode">Squawk code as 4-digit octal string (e.g., "7700" for emergency).</param>
/// <param name="FlightStatus">Flight status (airborne/ground and alert conditions).</param>
public sealed record SurveillanceIdentityReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    string SquawkCode,
    FlightStatus FlightStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
