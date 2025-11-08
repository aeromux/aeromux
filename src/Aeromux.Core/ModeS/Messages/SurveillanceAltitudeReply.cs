using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Surveillance altitude reply message.
/// Corresponds to Downlink Format 4.
/// </summary>
/// <remarks>
/// Response to Mode S altitude interrogation from ground stations.
/// Contains barometric altitude encoded using Q-bit (25-foot increments),
/// metric altitude (M=1), or Gillham code (100-foot increments).
/// Traffic: 5-10% of Mode S messages.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 4).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Altitude">Decoded altitude (null if unavailable or invalid).</param>
/// <param name="FlightStatus">Flight status (airborne/ground and alert conditions).</param>
public sealed record SurveillanceAltitudeReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    Altitude? Altitude,
    FlightStatus FlightStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
