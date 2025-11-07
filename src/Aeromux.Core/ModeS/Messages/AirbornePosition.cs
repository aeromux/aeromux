using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Airborne position message with altitude and CPR (Compact Position Reporting) encoded location.
/// Corresponds to Type Code 9-18 (barometric) and 20-22 (GNSS) in ADS-B Extended Squitter (DF 17/18).
/// </summary>
/// <remarks>
/// Position may be null if CPR decoding is not yet possible (requires paired even/odd frames).
/// CPR encoding reduces position data to 17 bits each for latitude and longitude.
/// CprLat, CprLon, and CprFormat are always present for later decoding.
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (typically DF 17 or 18).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Position">Decoded geographic position (null if not yet decoded).</param>
/// <param name="Altitude">Decoded altitude (null if not available).</param>
/// <param name="CprLat">CPR-encoded latitude (17 bits, 0-131071).</param>
/// <param name="CprLon">CPR-encoded longitude (17 bits, 0-131071).</param>
/// <param name="CprFormat">CPR format (0 = even, 1 = odd).</param>
/// <param name="SurveillanceStatus">Surveillance status (0-3).</param>
public sealed record AirbornePosition(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    GeographicCoordinate? Position,
    Altitude? Altitude,
    int CprLat,
    int CprLon,
    int CprFormat,
    int SurveillanceStatus) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
