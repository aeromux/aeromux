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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.Core.Tracking;

/// <summary>
/// Real-time operational status and ATC coordination information.
/// Contains cockpit mode indicators, system status, GPS antenna configuration, and ATC coordination metadata.
/// Sources: TC 31 (Operational Status), DF 20/21 (Comm-B Altitude/Identity Reply).
/// </summary>
public sealed record TrackedOperationalMode
{
    /// <summary>
    /// Indicates whether a TCAS Resolution Advisory (RA) is currently active (TC 31 OperationalMode).
    /// An RA is issued when TCAS detects a collision threat and provides escape maneuvers to the flight crew.
    /// True if TCAS RA is active, false if not active.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// Note: This is different from RA information in DF 16 (Long Air-Air Surveillance) and BDS 3,0 (ACAS RA).
    /// </summary>
    public bool? TcasRaActive { get; init; }

    /// <summary>
    /// Indicates whether the aircraft's IDENT switch is currently active (TC 31 OperationalMode).
    /// IDENT is used by ATC to positively identify an aircraft on radar by causing a special indicator
    /// to appear on the controller's display.
    /// True if IDENT switch is active, false if not active.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? IdentSwitchActive { get; init; }

    /// <summary>
    /// Indicates whether the aircraft is receiving ATC (Air Traffic Control) services (TC 31 OperationalMode).
    /// True if receiving ATC services (under ATC control), false if not receiving ATC services.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public bool? ReceivingATCServices { get; init; }

    /// <summary>
    /// Indicates the antenna configuration (TC 31 OperationalMode).
    /// Single antenna systems may have reduced position accuracy during aircraft maneuvers
    /// compared to dual (diversity) antenna systems.
    /// - SingleAntenna: Single antenna configuration.
    /// - DiversityAntenna: Dual antenna configuration (diversity).
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public AntennaFlag? SingleAntenna { get; init; }

    /// <summary>
    /// System Design Assurance (SDA) level (TC 31 OperationalMode).
    /// Indicates the probability of failure conditions that could cause loss of ADS-B function.
    /// Higher SDA levels indicate more rigorous design assurance:
    /// - Level 0: Unknown or no assurance.
    /// - Level 1: Minor failure condition (probability &lt; 10⁻³ per flight hour).
    /// - Level 2: Major failure condition (probability &lt; 10⁻⁵ per flight hour).
    /// - Level 3: Hazardous failure condition (probability &lt; 10⁻⁷ per flight hour).
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public SdaSupportedFailureCondition? SystemDesignAssurance { get; init; }

    /// <summary>
    /// Lateral offset of the GPS antenna from the aircraft reference point (TC 31 OperationalMode).
    /// Used to accurately determine the aircraft's position based on antenna location.
    /// Range: -6 meters (left) to +6 meters (right) of aircraft centerline.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public LateralGpsAntennaOffset? GpsLateralOffset { get; init; }

    /// <summary>
    /// Longitudinal offset of the GPS antenna from the aircraft reference point (TC 31 OperationalMode).
    /// Used to accurately determine the aircraft's position based on antenna location.
    /// Measured from nose (positive values indicate antenna is aft of nose).
    /// Range: 0 to 62 meters aft of the nose.
    /// Null if TC 31 not yet received or if ADS-B version does not provide this field.
    /// </summary>
    public LongitudinalGpsAntennaOffset? GpsLongitudinalOffset { get; init; }

    /// <summary>
    /// Downlink request field from Comm-B replies (DF 20/21).
    /// 5-bit field (0-31) indicating overlay capability and Comm-B request status.
    /// Values 0-15: No downlink request.
    /// Values 16-31: Request for Comm-B broadcast or overlay.
    /// Used by ground stations to coordinate Comm-B interrogations.
    /// Null if DF 20/21 not yet received (requires ground interrogation).
    /// </summary>
    public int? DownlinkRequest { get; init; }

    /// <summary>
    /// Utility message field (IIS + IDS) from Comm-B replies (DF 20/21).
    /// 6-bit field containing Interrogator Identifier Sequence (IIS) and Interrogator Identifier (IDS).
    /// Identifies which ground interrogator triggered this Comm-B reply.
    /// Used for interrogator coordination and to prevent duplicate interrogations.
    /// Null if DF 20/21 not yet received (requires ground interrogation).
    /// </summary>
    public int? UtilityMessage { get; init; }

    /// <summary>
    /// Timestamp of the most recent operational mode update.
    /// Updated whenever any operational mode field changes.
    /// Null if no operational mode data has been received yet.
    /// </summary>
    public DateTime? LastUpdate { get; init; }
}
