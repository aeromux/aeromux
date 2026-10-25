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
/// - Zones are four times smaller: a 90° base instead of 360° (latitude zones of 90/60° even, 90/59° odd)
/// - Requires receiver location as reference (no global decoding)
/// - Resolution: about 1.3 meters (1.5° / 2^17), finer than airborne
/// - Only local decoding supported (global decoding not applicable for surface)
///
/// The number of longitude zones uses the same NL function as airborne positions
/// (<see cref="CprDecoder.CprNL"/>), and odd frames use one zone fewer.
/// The result is correct while the aircraft is within half a zone of the receiver
/// (0.75° of latitude, about 45 NM north or south).
///
/// Algorithm: ICAO Annex 10, Volume IV, 3.1.2.8.7 (Surface Position Format).
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
                (int)Math.Floor(0.5 + (CprMod(reference.Latitude, dLat) / dLat) - yz);

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

        // Longitude zone size: 90° divided by the number of longitude zones at this latitude,
        // one zone fewer for odd frames (same NL function as airborne positions)
        int oddFlag = cprFormat == CprFormat.Odd ? 1 : 0;
        double dLon = 90.0 / Math.Max(CprDecoder.CprNL(lat) - oddFlag, 1);

        // Calculate longitude index m
        int m = (int)Math.Floor(reference.Longitude / dLon) +
                (int)Math.Floor(0.5 + (CprMod(reference.Longitude, dLon) / dLon) - xz);

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
    /// Modulo operation that always returns a positive result.
    /// C#'s % operator preserves the sign of the dividend, which produces
    /// incorrect zone indices for negative coordinates (Southern/Western Hemisphere).
    /// </summary>
    /// <param name="a">Dividend.</param>
    /// <param name="b">Divisor.</param>
    /// <returns>Positive modulo result.</returns>
    private static double CprMod(double a, double b)
    {
        double res = a % b;
        return res < 0 ? res + b : res;
    }
}
