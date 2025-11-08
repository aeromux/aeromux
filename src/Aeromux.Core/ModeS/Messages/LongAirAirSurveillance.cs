using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Long air-air surveillance message (DF 16).
/// ACAS coordination message containing Resolution Advisory data.
/// Priority 4: Middle ground approach - extract useful MV fields (RAC, RAT, MTE), skip complex ARA.
/// </summary>
/// <param name="IcaoAddress">ICAO aircraft address.</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (DF 16).</param>
/// <param name="SignalStrength">Signal strength in dBFS (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied.</param>
/// <param name="Altitude">Decoded altitude (null if unavailable).</param>
/// <param name="VerticalStatus">Vertical status (Airborne or Ground).</param>
/// <param name="CrossLinkCapability">True if aircraft supports DF 16 replies.</param>
/// <param name="SensitivityLevel">ACAS sensitivity level (0-7).</param>
/// <param name="ReplyInformation">ACAS operational state (0=no ACAS, 2=RA active, 3=vertical RA, 4=RA terminated).</param>
/// <param name="AcasValid">True if MV field contains valid ACAS data (VDS=0x30).</param>
/// <param name="ResolutionAdvisoryTerminated">True if RA terminated (null if not ACAS valid).</param>
/// <param name="MultipleThreatEncounter">True if multiple threats (null if not ACAS valid).</param>
/// <param name="RacNotBelow">RAC: Do not pass below threat (null if not ACAS valid).</param>
/// <param name="RacNotAbove">RAC: Do not pass above threat (null if not ACAS valid).</param>
/// <param name="RacNotLeft">RAC: Do not turn left of threat (null if not ACAS valid).</param>
/// <param name="RacNotRight">RAC: Do not turn right of threat (null if not ACAS valid).</param>
public sealed record LongAirAirSurveillance(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    byte SignalStrength,
    bool WasCorrected,
    Altitude? Altitude,
    VerticalStatus VerticalStatus,
    bool CrossLinkCapability,
    int SensitivityLevel,
    AcasReplyInformation ReplyInformation,
    bool AcasValid,
    bool? ResolutionAdvisoryTerminated,
    bool? MultipleThreatEncounter,
    bool? RacNotBelow,
    bool? RacNotAbove,
    bool? RacNotLeft,
    bool? RacNotRight) : ModeSMessage(IcaoAddress, Timestamp, DownlinkFormat, SignalStrength, WasCorrected);
