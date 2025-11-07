using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Surveillance altitude reply message (Mode S short air-air surveillance).
/// Corresponds to Downlink Format 0 (short air-air surveillance).
/// </summary>
/// <remarks>
/// Reports altitude and transponder status in response to Mode S interrogation.
/// Does not include position - only altitude and status.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 0).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Altitude">Decoded altitude (null if not available).</param>
/// <param name="FlightStatus">Flight status (0-7, see ICAO Annex 10).</param>
public sealed record SurveillanceReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    Altitude? Altitude,
    int FlightStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
