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

using System.Text;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Infrastructure.Network.Protocols;

/// <summary>
/// Encodes parsed ModeSMessage to SBS (BaseStation) CSV format.
/// SBS format is used by Virtual Radar Server and other legacy ADS-B clients.
/// </summary>
/// <remarks>
/// IMPORTANT - Compatibility Status:
/// This is an implementation with PARTIAL readsb SBS compatibility.
/// Supports ADS-B messages (MSG,1/3/4) but NOT surveillance messages (MSG,2/5/6/7/8).
/// See Issue #003 for complete compatibility analysis and enhancement roadmap.
///
/// Current Support:
/// - MSG,1: ES Aircraft identification (DF 17/18, TC 1-4) ✅
/// - MSG,3: ES Airborne position (DF 17/18, TC 9-18) ✅
/// - MSG,3: ES Surface position (DF 17/18, TC 5-8) ✅ (output as MSG,3, should be MSG,2)
/// - MSG,4: ES Airborne velocity (DF 17/18, TC 19) ✅
///
/// Missing from readsb:
/// - MSG,2: ES Surface position as separate type (currently merged with MSG,3)
/// - MSG,5: Surveillance altitude reply (DF 4, 20) ❌
/// - MSG,6: Surveillance identity reply (DF 5, 21) ❌
/// - MSG,7: Air-Air surveillance (DF 0, 16) ❌
/// - MSG,8: All-Call reply (DF 11) ❌
/// - Fields 19-22: Alert, Emergency, SPI, IsOnGround flags ❌
///
/// Format Differences from readsb:
/// - Line terminator: `\n` (should be `\r\n`) ⚠️
/// - Timestamp fields 9-10: Use message time (should be current time) ⚠️
/// - Session/Aircraft/Flight ID fields: Empty (readsb uses "1")
///
/// SBS (BaseStation) Format Specification:
/// - CSV format with 22 comma-separated fields
/// - Each line represents one message (MSG,{type},...)
/// - Date/time fields: YYYY/MM/DD HH:MM:SS.fff format
/// - Fields 7-8: Message reception date/time
/// - Fields 9-10: Current date/time (when SBS line generated)
/// - Fields 19-22: Alert/Emergency/SPI/Ground flags
///
/// Impact:
/// - Works with Virtual Radar Server for ADS-B aircraft tracking
/// - Missing Mode S surveillance messages (legacy transponders without ADS-B)
/// - Missing alert/emergency/ground status indicators
/// - May have minor compatibility issues due to line terminator/timestamp differences
///
/// Workaround:
/// Use Beast format for complete Mode S message coverage including surveillance messages.
/// </remarks>
public static class SbsEncoder
{
    /// <summary>
    /// Encodes a parsed message to SBS format with newline terminator.
    /// Returns null if message type cannot be represented in SBS format.
    /// </summary>
    /// <param name="message">Parsed Mode S message to encode</param>
    /// <returns>UTF-8 encoded SBS CSV line with newline, or null if not representable</returns>
    public static byte[]? Encode(ModeSMessage? message)
    {
        if (message == null)
        {
            // Skip unparseable frames
            return null;
        }

        // SBS format structure (22 comma-separated fields):
        // MSG,{TransmissionType},{SessionId},{AircraftId},{IcaoAddress},{FlightId},
        // {DateGenerated},{TimeGenerated},{DateLogged},{TimeLogged},{Callsign},
        // {Altitude},{GroundSpeed},{Track},{Lat},{Lon},{VerticalRate},{Squawk},
        // {Alert},{Emergency},{SPI},{IsOnGround}
        //
        // Only certain ModeSMessage types have SBS equivalents:
        // - AircraftIdentification → MSG,1 (callsign)
        // - AirbornePosition → MSG,3 (position with altitude)
        // - SurfacePosition → MSG,3 (position without altitude)
        // - AirborneVelocity → MSG,4 (speed, track, vertical rate)
        //
        // All other message types (e.g., surveillance replies, Comm-B, ACAS) have
        // no SBS representation and are filtered out by returning null.

        string? sbsLine = message switch
        {
            AircraftIdentification ident => FormatIdentification(ident),
            AirbornePosition pos => FormatPosition(pos),
            SurfacePosition surf => FormatSurfacePosition(surf),
            AirborneVelocity vel => FormatVelocity(vel),
            _ => null // No SBS equivalent for this message type
        };

        // Append newline terminator if we have a valid SBS line
        return sbsLine != null ? Encoding.UTF8.GetBytes(sbsLine + "\n") : null;
    }

    /// <summary>
    /// Formats AircraftIdentification message as SBS MSG,1 (callsign).
    /// </summary>
    private static string FormatIdentification(AircraftIdentification msg)
    {
        // MSG,1: Aircraft identification (callsign only)
        // Format: MSG,1,,,{ICAO},,{Date},{Time},{Date},{Time},{Callsign},,,,,,,,,,
        // Empty fields: SessionId, AircraftId, FlightId, and all fields after Callsign
        (string date, string time) = FormatTimestamp(msg.Timestamp);
        return $"MSG,1,,,{msg.IcaoAddress},,{date},{time},{date},{time},{msg.Callsign},,,,,,,,,,";
    }

    /// <summary>
    /// Formats AirbornePosition message as SBS MSG,3 (position with altitude).
    /// </summary>
    private static string FormatPosition(AirbornePosition msg)
    {
        // MSG,3: Airborne position (altitude, lat, lon)
        // Format: MSG,3,,,{ICAO},,{Date},{Time},{Date},{Time},,{Altitude},,,{Lat},{Lon},,,,,,
        // Latitude/Longitude: 6 decimal places (±0.11 meter precision)
        // Altitude: Feet (integer)
        (string date, string time) = FormatTimestamp(msg.Timestamp);
        string lat = msg.Position?.Latitude.ToString("F6") ?? "";
        string lon = msg.Position?.Longitude.ToString("F6") ?? "";
        string alt = msg.Altitude?.Feet.ToString() ?? "";
        return $"MSG,3,,,{msg.IcaoAddress},,{date},{time},{date},{time},,{alt},,,{lat},{lon},,,,,,";
    }

    /// <summary>
    /// Formats SurfacePosition message as SBS MSG,3 (position without altitude).
    /// </summary>
    private static string FormatSurfacePosition(SurfacePosition msg)
    {
        // MSG,3: Surface position (lat, lon, no altitude)
        // Same format as airborne MSG,3 but altitude field is empty
        // Surface vehicles don't have meaningful altitude (ground level)
        (string date, string time) = FormatTimestamp(msg.Timestamp);
        string lat = msg.Position?.Latitude.ToString("F6") ?? "";
        string lon = msg.Position?.Longitude.ToString("F6") ?? "";
        return $"MSG,3,,,{msg.IcaoAddress},,{date},{time},{date},{time},,,,,{lat},{lon},,,,,,";
    }

    /// <summary>
    /// Formats AirborneVelocity message as SBS MSG,4 (speed, track, vertical rate).
    /// </summary>
    private static string FormatVelocity(AirborneVelocity msg)
    {
        // MSG,4: Airborne velocity (ground speed, track, vertical rate)
        // Format: MSG,4,,,{ICAO},,{Date},{Time},{Date},{Time},,,{Speed},{Track},,,{VRate},,,,,
        // Speed: Knots (integer)
        // Track: Degrees (1 decimal place)
        // Vertical Rate: Feet per minute (integer, positive=climbing, negative=descending)
        (string date, string time) = FormatTimestamp(msg.Timestamp);
        string speed = msg.Velocity?.Knots.ToString("F0") ?? "";
        string track = msg.Heading?.ToString("F1") ?? "";
        string vrate = msg.VerticalRate?.ToString() ?? "";
        return $"MSG,4,,,{msg.IcaoAddress},,{date},{time},{date},{time},,,{speed},{track},,,{vrate},,,,,";
    }

    /// <summary>
    /// Formats DateTime as SBS date and time strings.
    /// </summary>
    /// <returns>Tuple of (date: "YYYY/MM/DD", time: "HH:mm:ss.fff")</returns>
    private static (string date, string time) FormatTimestamp(DateTime timestamp)
    {
        // SBS timestamp format:
        // - Date: YYYY/MM/DD (with forward slashes)
        // - Time: HH:mm:ss.fff (24-hour format with milliseconds)
        string date = timestamp.ToString("yyyy/MM/dd");
        string time = timestamp.ToString("HH:mm:ss.fff");
        return (date, time);
    }
}
