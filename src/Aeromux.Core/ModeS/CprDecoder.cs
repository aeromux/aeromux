using System.Collections.Concurrent;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;

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
    // NL lookup table (Number of Longitude zones by latitude)
    // Precomputed for latitudes 0-87 degrees (symmetric for negative latitudes)
    private static readonly int[] NlTable =
    [
        59, 58, 57, 56, 55, 54, 53, 52, 51, 50,  // 0-9°
        49, 48, 47, 46, 45, 44, 43, 42, 41, 40,  // 10-19°
        39, 38, 37, 36, 35, 34, 33, 32, 31, 30,  // 20-29°
        29, 28, 27, 26, 25, 24, 23, 22, 21, 20,  // 30-39°
        19, 18, 17, 16, 15, 14, 13, 12, 11, 10,  // 40-49°
        9, 8, 7, 6, 5, 4, 3, 2, 1, 1,            // 50-59°
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1,            // 60-69°
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1,            // 70-79°
        1, 1, 1, 1, 1, 1, 1, 1                   // 80-87°
    ];

    // Per-aircraft CPR state for frame pairing
    private readonly ConcurrentDictionary<string, CprFramePair> _aircraftCprState = new();

    // Cleanup timer (remove stale state)
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _maxFrameAge = TimeSpan.FromSeconds(10);

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
        // Periodic cleanup
        if ((timestamp - _lastCleanup) > _cleanupInterval)
        {
            CleanupStaleState(timestamp);
            _lastCleanup = timestamp;
        }

        // Get or create CPR state for this aircraft
        CprFramePair state = _aircraftCprState.GetOrAdd(icaoAddress, _ => new CprFramePair());

        // Store this frame
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
        if (state is not { EvenFrame: not null, OddFrame: not null })
        {
            return null;
        }

        // Check time difference (must be < 10 seconds)
        double timeDiff = Math.Abs((state.EvenFrame.Timestamp - state.OddFrame.Timestamp).TotalSeconds);
        if (!(timeDiff < _maxFrameAge.TotalSeconds))
        {
            return null;
        }

        // Use most recent frame type
        bool useOdd = state.OddFrame.Timestamp > state.EvenFrame.Timestamp;

        return DecodeCprGlobal(
            state.EvenFrame.Lat,
            state.EvenFrame.Lon,
            state.OddFrame.Lat,
            state.OddFrame.Lon,
            useOdd);
    }

    /// <summary>
    /// Global CPR decoding using even and odd frame pair.
    /// Calculates unambiguous latitude/longitude from paired CPR-encoded positions.
    /// </summary>
    /// <param name="evenCprLat">CPR latitude from even frame (17 bits).</param>
    /// <param name="evenCprLon">CPR longitude from even frame (17 bits).</param>
    /// <param name="oddCprLat">CPR latitude from odd frame (17 bits).</param>
    /// <param name="oddCprLon">CPR longitude from odd frame (17 bits).</param>
    /// <param name="useOddFrame">True to use odd frame's position, false for even frame.</param>
    /// <returns>Decoded geographic coordinate, or null if validation fails.</returns>
    private static GeographicCoordinate? DecodeCprGlobal(
        int evenCprLat, int evenCprLon,
        int oddCprLat, int oddCprLon,
        bool useOddFrame)
    {
        const double airDlat0 = 360.0 / 60.0;  // Even frame latitude zone (6°)
        const double airDlat1 = 360.0 / 59.0;  // Odd frame latitude zone (~6.1°)

        // Normalize CPR values to [0, 1)
        double lat0 = evenCprLat / 131072.0;
        double lat1 = oddCprLat / 131072.0;

        // Calculate latitude index j
        int j = (int)Math.Floor((59 * lat0) - (60 * lat1) + 0.5);

        // Compute latitudes for even and odd frames
        double rlat0 = airDlat0 * (CprMod(j, 60) + lat0);
        double rlat1 = airDlat1 * (CprMod(j, 59) + lat1);

        // Normalize to [-90, +90]
        if (rlat0 >= 270)
        {
            rlat0 -= 360;
        }

        if (rlat1 >= 270)
        {
            rlat1 -= 360;
        }

        // Validation: Both latitudes must be in valid range
        if (rlat0 < -90 || rlat0 > 90 || rlat1 < -90 || rlat1 > 90)
        {
            return null;
        }

        // Validation: Same latitude zone (NL must match)
        if (CprNL(rlat0) != CprNL(rlat1))
        {
            return null;
        }

        // Choose latitude based on frame type
        double rlat = useOddFrame ? rlat1 : rlat0;

        // Calculate longitude
        double lon0 = evenCprLon / 131072.0;
        double lon1 = oddCprLon / 131072.0;

        int nl = CprNL(rlat);
        int ni = Math.Max(CprN(rlat, useOddFrame ? 1 : 0), 1);
        int m = (int)Math.Floor((lon0 * (nl - 1)) - (lon1 * nl) + 0.5);

        double rlon = CprDlon(rlat, useOddFrame ? 1 : 0) *
                      (CprMod(m, ni) + (useOddFrame ? lon1 : lon0));

        // Normalize to [-180, +180]
        if (rlon > 180)
        {
            rlon -= 360;
        }

        return new GeographicCoordinate(rlat, rlon);
    }

    /// <summary>
    /// NL function: Number of longitude zones at given latitude.
    /// Returns the number of longitude zones (1-59) based on latitude.
    /// Used in CPR decoding to handle varying longitude zone widths.
    /// </summary>
    /// <param name="lat">Latitude in decimal degrees (-90 to +90).</param>
    /// <returns>Number of longitude zones (1 at poles, 59 at the equator).</returns>
    private static int CprNL(double lat)
    {
        double absLat = Math.Abs(lat);

        if (absLat >= 87)
        {
            return 1;
        }

        // Lookup in precomputed table
        int index = (int)Math.Floor(absLat);
        if (index >= 0 && index < NlTable.Length)
        {
            return NlTable[index];
        }

        return 59;  // Default for equator
    }

    /// <summary>
    /// N function: Number of longitude zones for even or odd frame.
    /// Calculates NL(lat) for even frames, NL(lat)-1 for odd frames.
    /// </summary>
    /// <param name="lat">Latitude in decimal degrees (-90 to +90).</param>
    /// <param name="fflag">Format flag (0 = even frame, 1 = odd frame).</param>
    /// <returns>Number of longitude zones for the given frame type.</returns>
    private static int CprN(double lat, int fflag)
    {
        int nl = CprNL(lat) - (fflag == 1 ? 1 : 0);
        return nl < 1 ? 1 : nl;
    }

    /// <summary>
    /// Dlon function: Longitude zone width at given latitude.
    /// Calculates the width (in degrees) of each longitude zone.
    /// </summary>
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
        var staleAircraft = _aircraftCprState
            .Where(kvp =>
            {
                CprFramePair state = kvp.Value;
                DateTime lastSeen = state.EvenFrame?.Timestamp ?? state.OddFrame?.Timestamp ?? DateTime.MinValue;
                return (now - lastSeen) > TimeSpan.FromMinutes(10);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string icao in staleAircraft)
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
