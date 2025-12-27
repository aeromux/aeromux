// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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

using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Core.Tracking.Handlers;

/// <summary>
/// Handles OperationalStatus messages (TC 31, Version 0/1/2).
/// Updates aircraft capability version and position data quality metrics.
/// </summary>
/// <remarks>
/// <para><strong>TC 31 provides equipment capability and data quality information:</strong></para>
/// <list type="bullet">
/// <item>ADS-B Version: Equipment capability level (DO-260, DO-260A, DO-260B)</item>
/// <item>Capability Class (CC): Aircraft capabilities (TCAS, ADS-B receive, ARV, TS, UAT, etc.)</item>
/// <item>Operational Mode (OM): Current operational state (TCAS RA, IDENT, ATC services, antenna config, SDA)</item>
/// <item>NACp (Navigation Accuracy Category - Position): GPS horizontal accuracy indicator</item>
/// <item>NIC Supplement-A: For position accuracy determination with CPR</item>
/// <item>GVA (Geometric Vertical Accuracy): GNSS vertical accuracy (Version 1+)</item>
/// <item>SIL (Source Integrity Level): Probability of position error exceeding containment radius</item>
/// <item>NICbaro (Barometric Altitude Integrity Code): Cross-check status (Version 1+)</item>
/// <item>HRD (Horizontal Reference Direction): True North vs Magnetic North</item>
/// <item>SIL Supplement: Per-hour vs per-sample basis (Version 2)</item>
/// </list>
/// <para><strong>Updated fields:</strong></para>
/// <list type="bullet">
/// <item>Status.Version: ADS-B protocol version (0=DO-260, 1=DO-260A, 2=DO-260B)</item>
/// <item>Position.NACp: Horizontal position accuracy (11=&lt;3m, 10=&lt;10m, 9=&lt;30m, down to 0=unknown)</item>
/// <item>Position.NICbaro: Barometric altitude integrity cross-check status</item>
/// <item>Position.SIL: Source integrity level (3=highest &lt;10^-7 per hour, down to 0)</item>
/// </list>
/// <para>
/// These metrics are used for data quality assessment, UI confidence indicators, and filtering low-quality data.
/// TC 31 messages are transmitted less frequently than position/velocity updates (typically every 5-10 seconds).
/// </para>
/// <para><strong>Note:</strong> NACv (velocity accuracy) is NOT in TC 31; it comes from TC 19 (Airborne Velocity).</para>
/// <para><strong>Reference:</strong> https://mode-s.org/1090mhz/content/ads-b/6-operation-status.html</para>
/// </remarks>
public sealed class OperationalStatusHandler : ITrackingHandler
{
    public Type MessageType => typeof(OperationalStatus);

    public (Aircraft updated, HashSet<string> changedFields) Apply(
        Aircraft aircraft,
        ModeSMessage message,
        ProcessedFrame frame,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(message);

        var msg = (OperationalStatus)message;
        var changedFields = new HashSet<string>();
        TrackedPosition position = aircraft.Position;
        TrackedStatus status = aircraft.Status;
        bool positionChanged = false;
        bool statusChanged = false;

        // Update Position data quality metrics from TC 31
        // NACp: Navigation Accuracy Category for Position (horizontal GPS accuracy)
        //       Scale 0-11: 11=<3m, 10=<10m, 9=<30m, 8=<92.6m, ..., 0=unknown
        // NICbaro: Barometric Altitude Integrity Code (1-bit, indicates cross-check status)
        //          Enum: NotCrossChecked (0) or CrossCheckedOrNonGilham (1)
        //          Converted to bool: true if cross-checked, false if not, null if unavailable
        // SIL: Source Integrity Level (probability of error containment, bits 83-84)
        //      Scale 0-3: 3=<10^-7 per hour (highest), 2=<10^-5, 1=<10^-3, 0=unknown
        // Note: NACv (velocity accuracy) is NOT in TC 31; it comes from TC 19 (Airborne Velocity)

        // Convert NICbaro from enum to bool for storage
        bool? nicBaroValue = msg.NICbaro.HasValue
            ? msg.NICbaro.Value == ModeS.Enums.BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham
            : null;

        if (position.NACp != msg.NACp ||
            position.NICbaro != nicBaroValue ||
            position.SIL != msg.SIL)
        {
            position = position with
            {
                NACp = msg.NACp,
                NICbaro = nicBaroValue,
                SIL = msg.SIL
            };
            changedFields.Add(nameof(Aircraft.Position));
            positionChanged = true;
        }

        // Update ADS-B protocol version (equipment capability indicator)
        // Version 0: DO-260 (original ADS-B standard)
        // Version 1: DO-260A (improved NACp/NIC categories)
        // Version 2: DO-260B/C (enhanced surveillance, emergency codes)
        // Used to determine supported features and data quality expectations
        if (status.Version != msg.Version)
        {
            status = status with { Version = msg.Version };
            changedFields.Add(nameof(Aircraft.Status));
            statusChanged = true;
        }

        // Return updated aircraft state if anything changed
        if (positionChanged || statusChanged)
        {
            return (aircraft with { Position = position, Status = status }, changedFields);
        }

        return (aircraft, changedFields);
    }
}
