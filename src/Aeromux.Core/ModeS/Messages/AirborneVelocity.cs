using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Airborne velocity message with speed, heading, and vertical rate.
/// Corresponds to Type Code 19 in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <remarks>
/// Velocity, Heading, and VerticalRate may be null if not available in the message.
/// Subtype indicates Ground Speed (1-2), True Airspeed (3-4), or IAS (reserved).
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Velocity">Velocity (null if not available).</param>
/// <param name="Heading">Heading in degrees (0-360, null if not available).</param>
/// <param name="VerticalRate">Vertical rate in feet/minute (null if not available, negative = descending).</param>
/// <param name="VelocitySubtype">Velocity subtype (1-4).</param>
public sealed record AirborneVelocity(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    Velocity? Velocity,
    double? Heading,
    int? VerticalRate,
    int VelocitySubtype) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
