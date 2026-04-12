// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.

using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.ModeS.Enums;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// MessageParser partial class: All-Call Reply messages (DF 11).
/// Handles all-call messages.
/// </summary>
public sealed partial class MessageParser
{
    /// <summary>
    /// Parses All-Call Reply message (DF 11).
    /// Extracts transponder capability field from bits 6-8.
    /// </summary>
    /// <param name="frame">Validated frame to parse.</param>
    /// <returns>All-call reply message with capability, or <see langword="null"/> if invalid.</returns>
    /// <remarks>
    /// All-call replies are transmitted in response to Mode S all-call interrogations.
    /// All-call interrogation (UF=11) is a broadcast request asking all aircraft to identify themselves.
    /// They announce the aircraft's presence and ICAO (International Civil Aviation Organization) address
    /// with basic capability information.
    /// Capability values:
    ///   0 = Level 1 transponder (basic Mode S)
    ///   1-3 = Reserved (not assigned, rejected if encountered)
    ///   4 = Level 2+ transponder, on-ground
    ///   5 = Level 2+ transponder, airborne
    ///   6 = Level 2+ transponder, on-ground or airborne status uncertain
    ///   7 = Downlink Request value is 0, or Flight Status is 2, 3, 4, or 5 (alert/SPI/emergency condition)
    /// </remarks>
    private ModeSMessage? ParseAllCallReply(ValidatedFrame frame)
    {
        // Extract Capability (CA - Capability) field from bits 6-8 (byte 0, bits 0-2)
        int capabilityRaw = ExtractBits(frame.Data, 6, 3);

        // Validate capability value (0-7 are defined in TransponderCapability enum)
        if (!EnumValidator.IsValidTransponderCapability(capabilityRaw))
        {
            Log.Debug("Invalid capability value {Capability} in DF 11 from {Icao}",
                capabilityRaw, frame.IcaoAddress);
            return null;
        }

        var capability = (TransponderCapability)capabilityRaw;

        // Extract ICAO from AA field - bits 9-32 (24 bits)
        int extractedRawIcao = ExtractBits(frame.Data, 9, 24);
        string extractedIcao = $"{extractedRawIcao:X6}";

        return new AllCallReply(
            frame.IcaoAddress,
            frame.Timestamp,
            frame.DownlinkFormat,
            frame.SignalStrength,
            frame.WasCorrected,
            extractedIcao,
            capability);
    }
}
