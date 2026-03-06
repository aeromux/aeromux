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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Serilog;

namespace Aeromux.Core.ModeS;

/// <summary>
/// CPR (Compact Position Reporting) decoder for surface position messages (TC 5-8).
/// Uses receiver location for local decoding (surface positions require reference point).
/// </summary>
/// <remarks>
/// Surface CPR differs from airborne CPR:
/// - Uses different NL (Number of Longitude Zones) function with 1.5° latitude zones
/// - Requires receiver location as reference (no global decoding)
/// - Resolution: ~3.8 meters at the equator (better precision than airborne)
/// - Only local decoding supported (global decoding not applicable for surface)
///
/// Algorithm: ICAO Annex 10, Volume IV, 3.1.2.8.7 (Surface Position Format).
/// Reference implementation: pyModeS surface.position function.
/// </remarks>
public sealed class SurfaceCprDecoder
{
    // Cache for receiver location (set once, used for all decodings)
    private GeographicCoordinate? _receiverLocation;

    /// <summary>
    /// Sets the receiver location for surface CPR decoding.
    /// Must be called before DecodePosition() to enable surface position decoding.
    /// </summary>
    /// <param name="receiverLocation">Receiver geographic coordinates.</param>
    public void SetReceiverLocation(GeographicCoordinate receiverLocation)
    {
        ArgumentNullException.ThrowIfNull(receiverLocation);

        _receiverLocation = receiverLocation;
        // Note: Logging is done by DeviceWorker (coordinator) to include device context
    }

    /// <summary>
    /// Decodes surface position using local CPR decoding with receiver location.
    /// </summary>
    /// <param name="cprLat">CPR-encoded latitude (17 bits).</param>
    /// <param name="cprLon">CPR-encoded longitude (17 bits).</param>
    /// <param name="cprFormat">CPR format (Even=0, Odd=1).</param>
    /// <returns>Decoded position, or null if receiver location not set.</returns>
    public GeographicCoordinate? DecodePosition(int cprLat, int cprLon, CprFormat cprFormat)
    {
        // Require receiver location for surface CPR
        if (_receiverLocation == null)
        {
            Log.Debug("Surface CPR decoding skipped: receiver location not configured");
            return null;
        }

        // Surface CPR uses local decoding only (no global decoding)
        return DecodeLocalPosition(cprLat, cprLon, cprFormat, _receiverLocation);
    }

    /// <summary>
    /// Decodes surface position using local CPR algorithm with receiver reference.
    /// </summary>
    /// <param name="cprLat">CPR-encoded latitude (17 bits).</param>
    /// <param name="cprLon">CPR-encoded longitude (17 bits).</param>
    /// <param name="cprFormat">CPR format (Even=0, Odd=1).</param>
    /// <param name="reference">Reference position (receiver location).</param>
    /// <returns>Decoded position, or null if decoding failed.</returns>
    private static GeographicCoordinate? DecodeLocalPosition(
        int cprLat,
        int cprLon,
        CprFormat cprFormat,
        GeographicCoordinate reference)
    {
        // CPR parameters for surface encoding
        const int nb = 17;  // Number of bits (17 for surface)
        const double dLatEven = 90.0 / 60.0;  // Latitude zone size for even format (1.5°)
        const double dLatOdd = 90.0 / 59.0;   // Latitude zone size for odd format (~1.525°)

        // Select parameters based on format
        double dLat = (cprFormat == CprFormat.Even) ? dLatEven : dLatOdd;

        // Normalize CPR values (0-1 range)
        double yz = cprLat / (double)(1 << nb);  // CPR latitude (0-1)
        double xz = cprLon / (double)(1 << nb);  // CPR longitude (0-1)

        // Calculate latitude index j
        int j = (int)Math.Floor(reference.Latitude / dLat) +
                (int)Math.Floor(0.5 + ((reference.Latitude % dLat) / dLat) - yz);

        // Calculate latitude
        double lat = dLat * (j + yz);

        // Validate latitude range
        if (lat >= 270)
        {
            lat -= 360;
        }

        if (lat <= -270)
        {
            lat += 360;
        }

        // Check for invalid latitude
        if (lat < -90 || lat > 90)
        {
            Log.Debug("Surface CPR: invalid latitude {Lat:F4}° (out of range)", lat);
            return null;
        }

        // Calculate longitude using surface NL function
        int nl = CalculateSurfaceNL(lat);

        if (nl == 0)
        {
            Log.Debug("Surface CPR: NL=0 at latitude {Lat:F4}° (polar singularity)", lat);
            return null;
        }

        // Calculate longitude zone size
        double dLon = 90.0 / nl;

        // Calculate longitude index m
        int m = (int)Math.Floor(reference.Longitude / dLon) +
                (int)Math.Floor(0.5 + ((reference.Longitude % dLon) / dLon) - xz);

        // Calculate longitude
        double lon = dLon * (m + xz);

        // Normalize longitude to [-180, +180]
        if (lon >= 180)
        {
            lon -= 360;
        }

        if (lon <= -180)
        {
            lon += 360;
        }

        return new GeographicCoordinate(lat, lon);
    }

    /// <summary>
    /// Surface NL lookup table (ICAO Annex 10, Volume IV, Table 3-2).
    /// Maps latitude zone index (floor(abs(lat) / 1.5)) to number of longitude zones.
    /// </summary>
    private static readonly int[] SurfaceNlTable =
    [
        59, 58, 57, 56, 55, 54, 53, 52, 51, 50,  // 0-15°
        49, 48, 47, 46, 45, 44, 43, 42, 41, 40,  // 15-30°
        39, 38, 37, 36, 35, 34, 33, 32, 31, 30,  // 30-45°
        29, 28, 27, 26, 25, 24, 23, 22, 21, 20,  // 45-60°
        19, 18, 17, 16, 15, 14, 13, 12, 11, 10,  // 60-75°
        9,  8,  7,  6,  5,  4,  3,  2,  1,  1    // 75-90°
    ];

    /// <summary>
    /// Calculates the Number of Longitude Zones (NL) for surface CPR at a given latitude.
    /// Surface NL function differs from airborne NL function.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <returns>Number of longitude zones (1-59), or 0 if latitude is out of range.</returns>
    /// <remarks>
    /// Surface NL table (ICAO Annex 10, Volume IV, Table 3-2):
    /// Uses 1.5° latitude zones (60 zones total) instead of airborne's variable zones.
    /// NL decreases from 59 at the equator to 1 near poles.
    /// </remarks>
    private static int CalculateSurfaceNL(double latitude)
    {
        double absLat = Math.Abs(latitude);

        // Calculate table index
        int index = (int)Math.Floor(absLat / 1.5);

        // Clamp to valid range
        if (index < 0 || index >= SurfaceNlTable.Length)
        {
            return 0;  // Invalid latitude
        }

        return SurfaceNlTable[index];
    }
}
