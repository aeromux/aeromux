using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Comm-B altitude reply message (DF 20).
/// Response to Comm-B interrogation containing altitude + 56-bit BDS register data.
/// Priority 4: ALL BDS codes (1,0/1,7/2,0/3,0/4,0/4,4/4,5/5,0/5,3/6,0) implemented.
/// </summary>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 20).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Altitude">Decoded altitude (null if unavailable).</param>
/// <param name="FlightStatus">Flight status (airborne/ground and alert conditions).</param>
/// <param name="DownlinkRequest">Downlink request field (0-31).</param>
/// <param name="UtilityMessage">Utility message field (IIS + IDS).</param>
/// <param name="BdsCode">Inferred BDS register code.</param>
/// <param name="BdsData">Parsed BDS data (specific to BDS code, null if unknown/empty).</param>
public sealed record CommBAltitudeReply(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    Altitude? Altitude,
    FlightStatus FlightStatus,
    int DownlinkRequest,
    int UtilityMessage,
    BdsCode BdsCode,
    BdsData? BdsData) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
