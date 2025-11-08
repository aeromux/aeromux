using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// MessageParser partial class: ACAS coordination messages (DF 16).
/// Handles long air-air surveillance with middle ground MV field decoding.
/// </summary>
public sealed partial class MessageParser
{
    /// <summary>
    /// Parses long air-air surveillance from Downlink Format 16.
    /// ACAS coordination message with middle ground MV decoding (VDS validation, RAC, RAT, MTE).
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>Long air-air surveillance message with ACAS data, or null if invalid.</returns>
    /// <remarks>
    /// DF 16 structure:
    /// - Bit 6: Vertical Status (VS)
    /// - Bits 9-10: Reserved
    /// - Bit 11: Cross-link Capability (CC)
    /// - Bits 14-16: Sensitivity Level (SL)
    /// - Bits 20-32: Altitude Code (AC)
    /// - Bits 33-36: Reply Information (RI)
    /// - Bits 41-96: Message Vertical (MV) - ACAS data
    ///
    /// Middle ground MV decoding approach:
    /// - Extract VDS field (bits 41-48), validate it's 0x30 for valid ACAS
    /// - Extract RAC (Resolution Advisory Complement, bits 49-52)
    /// - Extract RAT (Resolution Advisory Terminated, bit 53)
    /// - Extract MTE (Multiple Threat Encounter, bit 54)
    /// - Skip ARA field (bits 55-68) due to complex conditional decoding
    /// </remarks>
    private ModeSMessage? ParseLongAirAirSurveillance(ValidatedFrame frame)
    {
        // Extract Vertical Status (VS) - bit 6 (byte 0, bit 2)
        VerticalStatus verticalStatus = ((frame.Data[0] & 0x04) >> 2) == 0
            ? VerticalStatus.Airborne
            : VerticalStatus.Ground;

        // Extract Cross-link Capability (CC) - bit 11 (byte 1, bit 4)
        bool crossLinkCapability = (frame.Data[1] & 0x08) != 0;

        // Extract Sensitivity Level (SL) - bits 14-16 (byte 1, bits 1-3)
        int sensitivityLevel = frame.Data[1] & 0x07;

        // Extract Altitude Code (AC) - bits 20-32 (13 bits)
        int altitudeCode = ((frame.Data[2] & 0x1F) << 8) | (frame.Data[3] >> 1);

        // Decode altitude (null if invalid or unavailable)
        Altitude? altitude = DecodeAltitudeAC13(altitudeCode);

        // Extract Reply Information (RI) - bits 33-36 (4 bits)
        int riRaw = ((frame.Data[4] & 0x07) << 1) | ((frame.Data[5] & 0x80) >> 7);

        // Validate RI field (only 0, 2, 3, 4 are valid)
        if (!Enum.IsDefined(typeof(AcasReplyInformation), riRaw))
        {
            Log.Debug("Invalid ACAS reply information {RI} in DF 16 from {Icao}",
                riRaw, frame.IcaoAddress);
            return null;
        }

        var replyInformation = (AcasReplyInformation)riRaw;

        // Extract MV field (Message Vertical, bits 41-96, 56 bits = 7 bytes)
        // MV starts at bit 41 = byte 5, bit 1
        // VDS (Vertical Data Source) is bits 41-48 (first byte of MV)
        int vds = ((frame.Data[5] & 0x7F) << 1) | ((frame.Data[6] & 0x80) >> 7);

        // Check if VDS indicates valid ACAS data (VDS = 0x30 for DF 16)
        bool acasValid = vds == 0x30;

        bool? resolutionAdvisoryTerminated = null;
        bool? multipleThreatEncounter = null;
        bool? racNotBelow = null;
        bool? racNotAbove = null;
        bool? racNotLeft = null;
        bool? racNotRight = null;

        if (acasValid)
        {
            // Extract RAC (Resolution Advisory Complement, bits 49-52, 4 bits)
            // Bits 49-52 = byte 6, bits 1-4
            int rac = (frame.Data[6] & 0x78) >> 3;
            racNotBelow = (rac & 0x08) != 0;   // bit 49
            racNotAbove = (rac & 0x04) != 0;   // bit 50
            racNotLeft = (rac & 0x02) != 0;    // bit 51
            racNotRight = (rac & 0x01) != 0;   // bit 52

            // Extract RAT (Resolution Advisory Terminated, bit 53)
            // Bit 53 = byte 6, bit 5
            resolutionAdvisoryTerminated = (frame.Data[6] & 0x04) != 0;

            // Extract MTE (Multiple Threat Encounter, bit 54)
            // Bit 54 = byte 6, bit 6
            multipleThreatEncounter = (frame.Data[6] & 0x02) != 0;
        }

        return new LongAirAirSurveillance(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            altitude,
            verticalStatus,
            crossLinkCapability,
            sensitivityLevel,
            replyInformation,
            acasValid,
            resolutionAdvisoryTerminated,
            multipleThreatEncounter,
            racNotBelow,
            racNotAbove,
            racNotLeft,
            racNotRight);
    }

}
