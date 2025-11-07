namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// All-call reply message (Mode S surveillance identity reply).
/// Corresponds to Downlink Format 11 (all-call reply).
/// </summary>
/// <remarks>
/// Transmitted in response to Mode S all-call interrogations.
/// Used for aircraft acquisition - announces ICAO address and basic capability.
/// Does not include altitude or position.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 11).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Capability">Transponder capability level (0-7, see ICAO Annex 10).</param>
public sealed record AllCallReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    int Capability) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
