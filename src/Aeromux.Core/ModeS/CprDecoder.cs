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

using System.Collections.Concurrent;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// Decodes Compact Position Reporting (CPR) encoded aircraft positions.
/// Implements global decoding (even+odd frame pairing) for airborne positions.
/// </summary>
/// <remarks>
/// CPR encoding compresses latitude/longitude into 17 bits each (34 bits total).
/// Requires pairing even (F=0) and odd (F=1) frames from the same aircraft.
/// Maintains per-aircraft state for frame pairing and automatic cleanup.
///
/// Reference: ICAO Annex 10, Volume IV, Section 3.1.2.8.
/// Algorithm: http://www.lll.lu/~edward/edward/adsb/DecodingADSBposition.html
/// </remarks>
public sealed class CprDecoder
{
    /// <summary>
    /// Per-aircraft CPR state storage for frame pairing.
    /// Each aircraft must maintain separate even/odd frames for global decoding.
    /// Each DeviceWorker owns its own CprDecoder instance (single-threaded access).
    /// </summary>
    private readonly ConcurrentDictionary<string, CprFramePair> _aircraftCprState = new();
    private readonly List<string> _staleKeys = [];  // Reusable list for cleanup (avoids LINQ allocation)

    /// <summary>
    /// Timestamp of last cleanup operation.
    /// Cleanup runs periodically to prevent unbounded memory growth.
    /// </summary>
    private DateTime _lastCleanup = DateTime.UtcNow;

    /// <summary>
    /// How often to clean up stale aircraft state (5 minutes).
    /// Balances memory usage with cleanup overhead.
    /// </summary>
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum age difference between even and odd frames for valid pairing (10 seconds).
    /// ADS-B transmits position messages every 0.5-2 seconds, so 10 seconds allows
    /// for missed frames while preventing incorrect pairing from stale data.
    /// </summary>
    private readonly TimeSpan _maxFrameAge = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum distance from receiver for a valid decoded position.
    /// Positions beyond this range are rejected as implausible (likely CPR decode errors from bit corruption).
    /// 300 NM is the practical maximum range for Mode S reception at typical antenna installations.
    /// </summary>
    private const double MaxReceiverRangeNm = 300.0;

    /// <summary>
    /// Receiver location for range-based position filtering.
    /// Null when receiver location is not configured — range check is skipped.
    /// </summary>
    private GeographicCoordinate? _receiverLocation;

    /// <summary>
    /// Sets the receiver location for range-based position filtering.
    /// Positions decoded beyond 300 NM from receiver are rejected as implausible.
    /// If not set, range filtering is disabled and all decoded positions are accepted.
    /// </summary>
    /// <param name="receiverLocation">Receiver geographic coordinates.</param>
    public void SetReceiverLocation(GeographicCoordinate receiverLocation)
    {
        _receiverLocation = receiverLocation;
    }

    /// <summary>
    /// Decodes CPR-encoded position from airborne position message.
    /// Stores frame and attempts global decoding when even+odd pair available.
    /// </summary>
    /// <param name="icaoAddress">Aircraft ICAO address (for frame pairing).</param>
    /// <param name="cprLat">CPR latitude (17 bits, 0-131071).</param>
    /// <param name="cprLon">CPR longitude (17 bits, 0-131071).</param>
    /// <param name="cprFormat">CPR format (even or odd frame).</param>
    /// <param name="timestamp">Message timestamp.</param>
    /// <returns>Decoded position, or null if insufficient data.</returns>
    public GeographicCoordinate? DecodePosition(
        string icaoAddress,
        int cprLat,
        int cprLon,
        CprFormat cprFormat,
        DateTime timestamp)
    {
        // Periodic cleanup to prevent unbounded memory growth
        // Removes state for aircraft not seen in 10+ minutes
        if ((timestamp - _lastCleanup) > _cleanupInterval)
        {
            CleanupStaleState(timestamp);
            _lastCleanup = timestamp;
        }

        // Get or create CPR state for this aircraft
        // Each aircraft needs separate state to pair even/odd frames correctly
        CprFramePair state = _aircraftCprState.GetOrAdd(icaoAddress, _ => new CprFramePair());

        // Store this frame (overwrites previous frame of same type)
        // Aircraft transmits alternating even/odd frames, we keep the latest of each
        var newFrame = new CprFrame(cprLat, cprLon, timestamp);
        if (cprFormat == CprFormat.Even)
        {
            state.EvenFrame = newFrame;
        }
        else
        {
            state.OddFrame = newFrame;
        }

        // Check if we have both frames for global decoding
        // Global CPR requires paired even+odd frames to resolve position ambiguity
        if (state is not { EvenFrame: not null, OddFrame: not null })
        {
            return null;  // Need both frame types before we can decode
        }

        // Check time difference (must be < 10 seconds)
        // Frames too far apart may not be from the same position transmission sequence
        // This prevents incorrect pairing after aircraft maneuvers or data gaps
        double timeDiff = Math.Abs((state.EvenFrame.Timestamp - state.OddFrame.Timestamp).TotalSeconds);
        if (!(timeDiff < _maxFrameAge.TotalSeconds))
        {
            return null;  // Frames too old, wait for fresh pair
        }

        // Use most recent frame type to determine final position
        // The newer frame is more likely to reflect current aircraft position
        bool useOdd = state.OddFrame.Timestamp > state.EvenFrame.Timestamp;

        GeographicCoordinate? result = DecodeCprGlobal(
            icaoAddress,
            state.EvenFrame.Lat,
            state.EvenFrame.Lon,
            state.OddFrame.Lat,
            state.OddFrame.Lon,
            useOdd);

        // Range check: reject positions beyond 300 NM from receiver
        if (result != null && _receiverLocation != null &&
            _receiverLocation.DistanceToNauticalMiles(result) > MaxReceiverRangeNm)
        {
            return null;
        }

        return result;
    }

    /// <summary>
    /// Global CPR decoding using even and odd frame pair.
    /// Calculates unambiguous latitude/longitude from paired CPR-encoded positions.
    /// </summary>
    /// <param name="icaoAddress">Aircraft ICAO address (for diagnostic logging).</param>
    /// <param name="evenCprLat">CPR latitude from even frame (17 bits).</param>
    /// <param name="evenCprLon">CPR longitude from even frame (17 bits).</param>
    /// <param name="oddCprLat">CPR latitude from odd frame (17 bits).</param>
    /// <param name="oddCprLon">CPR longitude from odd frame (17 bits).</param>
    /// <param name="useOddFrame">True to use odd frame's position, false for even frame.</param>
    /// <returns>Decoded geographic coordinate, or null if validation fails.</returns>
    private static GeographicCoordinate? DecodeCprGlobal(
        string icaoAddress,
        int evenCprLat, int evenCprLon,
        int oddCprLat, int oddCprLon,
        bool useOddFrame)
    {
        // === STEP 1: LATITUDE DECODING ===
        // CPR divides Earth into latitude zones: 60 zones for even frames, 59 for odd frames
        // This creates overlapping grids that allow position disambiguation
        const double airDlat0 = 360.0 / 60.0;  // Even frame latitude zone width (6°)
        const double airDlat1 = 360.0 / 59.0;  // Odd frame latitude zone width (~6.1°)

        // Normalize CPR values from 17-bit integers (0-131071) to fractional [0, 1)
        // This represents position as fraction within the current latitude zone
        double lat0 = evenCprLat / 131072.0;  // 131072 = 2^17
        double lat1 = oddCprLat / 131072.0;

        // Calculate latitude zone index (j) by comparing even/odd frame positions
        // Formula: j = floor((59 × lat0) - (60 × lat1) + 0.5)
        // This resolves which of the 60/59 overlapping zones the aircraft is in
        // The +0.5 provides rounding to nearest integer zone
        // Reference: ICAO Annex 10, Volume IV, Section 3.1.2.8.3
        int j = (int)Math.Floor((59 * lat0) - (60 * lat1) + 0.5);

        // Compute actual latitudes for both frames
        // rlat0/rlat1 = (zone index) * (zone width) + (fractional position within zone)
        // CprMod ensures zone index wraps correctly (handles Southern Hemisphere)
        double rlat0 = airDlat0 * (CprMod(j, 60) + lat0);  // Even frame latitude
        double rlat1 = airDlat1 * (CprMod(j, 59) + lat1);  // Odd frame latitude

        // Normalize latitude to standard range [-90°, +90°]
        // CPR can produce values 0-360, need to wrap Southern Hemisphere (270-360 → -90-0)
        if (rlat0 >= 270)
        {
            rlat0 -= 360;
        }

        if (rlat1 >= 270)
        {
            rlat1 -= 360;
        }

        // Validation: Both latitudes must be in valid geographic range
        // If outside valid range, frame pair is corrupted or incorrectly paired
        if (rlat0 < -90 || rlat0 > 90 || rlat1 < -90 || rlat1 > 90)
        {
            return null;  // Invalid latitude, reject decode
        }

        // Validation: Both frames must be in same latitude zone (NL function)
        // NL (Number of Longitude zones) changes with latitude due to Earth's curvature
        // If even/odd frames have different NL, they can't be from the same position
        // This catches aircraft crossing latitude zone boundaries during frame pair
        if (CprNL(rlat0) != CprNL(rlat1))
        {
            Log.Debug("CPR: zone transition for {Icao}, NL({Rlat0:F4})={NL0} != NL({Rlat1:F4})={NL1}, dropping frame pair",
                icaoAddress, rlat0, CprNL(rlat0), rlat1, CprNL(rlat1));
            return null;
        }

        // Choose final latitude based on which frame is newer.
        // Newer frame better represents current aircraft position
        double rlat = useOddFrame ? rlat1 : rlat0;

        // === STEP 2: LONGITUDE DECODING ===
        // Longitude is more complex because number of zones varies with latitude
        // Near the equator: 59 zones (~6.1° wide), near poles: fewer zones (wider)
        // This accounts for meridian convergence (longitude lines meet at poles)

        // Normalize CPR longitude values to fractional [0, 1)
        double lon0 = evenCprLon / 131072.0;  // Even frame longitude (fraction)
        double lon1 = oddCprLon / 131072.0;   // Odd frame longitude (fraction)

        // Get number of longitude zones at computed latitude
        // nl (Number of Longitude zones) depends on latitude due to Earth's curvature
        // At the equator: nl=59, at poles: nl=1
        int nl = CprNL(rlat);

        // Get number of longitude zones for selected frame type (even or odd)
        // ni is used to determine which of the nl zones we're in
        // Math.Max ensures ni is at least 1 to avoid division by zero at poles
        int ni = Math.Max(CprN(rlat, useOddFrame ? 1 : 0), 1);

        // Calculate longitude zone index (m) by comparing even/odd frame positions
        // Formula: m = floor((lon0 × (nl - 1)) - (lon1 × nl) + 0.5)
        // Similar to latitude index j, but accounts for varying zone counts
        // This resolves which of the nl overlapping longitude zones the aircraft is in
        // Reference: ICAO Annex 10, Volume IV, Section 3.1.2.8.4
        int m = (int)Math.Floor((lon0 * (nl - 1)) - (lon1 * nl) + 0.5);

        // Compute actual longitude
        // rlon = (zone width) * (zone index + fractional position within zone)
        // CprDlon calculates zone width based on latitude and frame type
        // CprMod ensures zone index wraps correctly (handles Eastern/Western hemispheres)
        double rlon = CprDlon(rlat, useOddFrame ? 1 : 0) *
                      (CprMod(m, ni) + (useOddFrame ? lon1 : lon0));

        // Normalize longitude to standard range [-180°, +180°]
        // CPR can produce values 0-360, need to wrap Western Hemisphere (180-360 → -180-0)
        if (rlon > 180)
        {
            rlon -= 360;
        }

        return new GeographicCoordinate(rlat, rlon);
    }

    /// <summary>
    /// NL (Number of Longitude zones) function: Returns the number of longitude zones at given latitude.
    /// Returns values from 1 (at poles) to 59 (at the equator) based on latitude.
    /// Used in CPR (Compact Position Reporting) decoding to handle varying longitude zone widths.
    /// </summary>
    /// <remarks>
    /// The NL function accounts for meridian convergence (longitude lines meet at poles).
    /// At the equator, longitude zones are narrow (59 zones, ~6.1° each).
    /// Near the poles, longitude zones are wider (fewer zones, eventually 1 zone at pole).
    /// This lookup table is defined by ICAO Annex 10, Volume IV, Table A-2-1 and must match
    /// exactly for correct decoding. The latitude thresholds are computed from the CPR reference
    /// latitude formula: NL(lat) = floor(2π / arccos(1 - (1-cos(π/(2×NZ))) / cos²(lat×π/180)))
    /// where NZ is the number of geographic latitude zones (15 for Mode S).
    /// </remarks>
    /// <param name="lat">Latitude in decimal degrees (-90 to +90).</param>
    /// <returns>Number of longitude zones (1 at poles, 59 at the equator).</returns>
    private static int CprNL(double lat)
    {
        double absLat = Math.Abs(lat);

        return absLat switch
        {
            < 10.47047130 => 59,
            < 14.82817437 => 58,
            < 18.18626357 => 57,
            < 21.02939493 => 56,
            < 23.54504487 => 55,
            < 25.82924707 => 54,
            < 27.93898710 => 53,
            < 29.91135686 => 52,
            < 31.77209708 => 51,
            < 33.53993436 => 50,
            < 35.22899598 => 49,
            < 36.85025108 => 48,
            < 38.41241892 => 47,
            < 39.92256684 => 46,
            < 41.38651832 => 45,
            < 42.80914012 => 44,
            < 44.19454951 => 43,
            < 45.54626723 => 42,
            < 46.86733252 => 41,
            < 48.16039128 => 40,
            < 49.42776439 => 39,
            < 50.67150166 => 38,
            < 51.89342469 => 37,
            < 53.09516153 => 36,
            < 54.27817472 => 35,
            < 55.44378444 => 34,
            < 56.59318756 => 33,
            < 57.72747354 => 32,
            < 58.84763776 => 31,
            < 59.95459277 => 30,
            < 61.04917774 => 29,
            < 62.13216659 => 28,
            < 63.20427479 => 27,
            < 64.26616523 => 26,
            < 65.31845310 => 25,
            < 66.36171008 => 24,
            < 67.39646774 => 23,
            < 68.42322022 => 22,
            < 69.44242631 => 21,
            < 70.45451075 => 20,
            < 71.45986473 => 19,
            < 72.45884545 => 18,
            < 73.45177442 => 17,
            < 74.43893416 => 16,
            < 75.42056257 => 15,
            < 76.39684391 => 14,
            < 77.36789461 => 13,
            < 78.33374083 => 12,
            < 79.29428225 => 11,
            < 80.24923213 => 10,
            < 81.19801349 => 9,
            < 82.13956981 => 8,
            < 83.07199445 => 7,
            < 83.99173563 => 6,
            < 84.89166191 => 5,
            < 85.75541621 => 4,
            < 86.53536998 => 3,
            < 87.00000000 => 2,
            _ => 1
        };
    }

    /// <summary>
    /// N function: Number of longitude zones for even or odd frame.
    /// Calculates NL(lat) for even frames, NL(lat)-1 for odd frames.
    /// </summary>
    /// <remarks>
    /// Even frames use NL(lat) zones, odd frames use NL(lat)-1 zones.
    /// This difference creates the overlapping grid pattern needed for CPR disambiguation.
    /// The minimum return value is 1 to prevent division by zero at polar latitudes.
    /// </remarks>
    /// <param name="lat">Latitude in decimal degrees (-90 to +90).</param>
    /// <param name="fflag">Format flag (0 = even frame, 1 = odd frame).</param>
    /// <returns>Number of longitude zones for the given frame type (minimum 1).</returns>
    private static int CprN(double lat, int fflag)
    {
        int nl = CprNL(lat) - (fflag == 1 ? 1 : 0);
        return nl < 1 ? 1 : nl;  // Ensure at least 1 zone (prevents division by zero)
    }

    /// <summary>
    /// Dlon function: Longitude zone width at given latitude.
    /// Calculates the width (in degrees) of each longitude zone.
    /// </summary>
    /// <remarks>
    /// Zone width = 360° / number of zones at this latitude.
    /// Near the equator with 59 zones: ~6.1° per zone.
    /// Near poles with fewer zones: wider zones (e.g., 10 zones = 36° each).
    /// At pole with 1 zone: 360° (entire circumference is one zone).
    /// </remarks>
    /// <param name="lat">Latitude in decimal degrees (-90 to +90).</param>
    /// <param name="fflag">Format flag (0 = even frame, 1 = odd frame).</param>
    /// <returns>Longitude zone width in degrees.</returns>
    private static double CprDlon(double lat, int fflag) => 360.0 / CprN(lat, fflag);

    /// <summary>
    /// Modulo operation (always returns positive result).
    /// Standard C# modulo can return negative values; this ensures positive results.
    /// </summary>
    /// <param name="a">Dividend.</param>
    /// <param name="b">Divisor.</param>
    /// <returns>Positive modulo result (0 to b-1).</returns>
    private static int CprMod(int a, int b)
    {
        int res = a % b;
        return res < 0 ? res + b : res;
    }

    /// <summary>
    /// Removes stale CPR state for aircraft not seen recently.
    /// </summary>
    private void CleanupStaleState(DateTime now)
    {
        _staleKeys.Clear();

        foreach (KeyValuePair<string, CprFramePair> kvp in _aircraftCprState)
        {
            DateTime lastSeen = kvp.Value.EvenFrame?.Timestamp
                             ?? kvp.Value.OddFrame?.Timestamp
                             ?? DateTime.MinValue;
            if ((now - lastSeen) > TimeSpan.FromMinutes(10))
            {
                _staleKeys.Add(kvp.Key);
            }
        }

        foreach (string icao in _staleKeys)
        {
            _aircraftCprState.TryRemove(icao, out _);
        }
    }

    /// <summary>
    /// CPR frame data (latitude, longitude, timestamp).
    /// </summary>
    private record CprFrame(int Lat, int Lon, DateTime Timestamp);

    /// <summary>
    /// Per-aircraft CPR state (even and odd frames).
    /// </summary>
    private class CprFramePair
    {
        public CprFrame? EvenFrame { get; set; }
        public CprFrame? OddFrame { get; set; }
    }
}
