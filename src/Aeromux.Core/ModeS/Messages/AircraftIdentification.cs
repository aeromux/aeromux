namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Aircraft identification and category (callsign) message.
/// Corresponds to Type Code 1-4 in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Callsign">Aircraft callsign (flight number), 8 characters, trimmed.</param>
/// <param name="Category">Aircraft category (A0-D7, see ICAO Annex 10).</param>
public sealed record AircraftIdentification(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    string Callsign,
    int Category) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
