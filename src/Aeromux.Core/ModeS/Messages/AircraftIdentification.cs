using Aeromux.Core.ModeS.Enums;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Aircraft identification and category (callsign) message.
/// Corresponds to Type Code 1-4 in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <remarks>
/// TC value determines the general class:
/// - TC 1: No category information
/// - TC 2: Surface vehicles
/// - TC 3: Ground obstructions
/// - TC 4: Aircraft (with wake vortex categories: Light, Small, Large, Heavy, etc.)
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Callsign">Aircraft callsign (flight number), 8 characters, trimmed.</param>
/// <param name="Category">Aircraft category (wake vortex classification and vehicle type).</param>
public sealed record AircraftIdentification(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    string Callsign,
    AircraftCategory Category) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
