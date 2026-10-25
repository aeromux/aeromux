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

using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tests.TestData;
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for SurfaceCprDecoder: local decoding of surface positions (TC 5-8) against the
/// receiver location.
/// </summary>
/// <remarks>
/// The expected positions never come from the decoder's own formulas. They come either from
/// a published worked example ("The 1090MHz Riddle") or from an encoder that computes the
/// number of longitude zones from the closed-form NL formula instead of a lookup table.
/// </remarks>
public class SurfaceCprDecoderTests
{
    // ========================================
    // Published example
    // ========================================

    /// <summary>
    /// "The 1090MHz Riddle", chapter 6, worked example 2: an odd surface frame decoded locally.
    /// The decoded latitude has NL 36, so the odd longitude zone width is 90/35.
    /// </summary>
    [Theory]
    [InlineData(52.320607, 4.734735, "previously decoded position (the book's reference)")]
    [InlineData(51.990, 4.375, "the airfield")]
    public void DecodePosition_RiddleWorkedExample_MatchesPublishedPosition(
        double referenceLat, double referenceLon, string description)
    {
        // Arrange
        var decoder = new SurfaceCprDecoder();
        decoder.SetReceiverLocation(new GeographicCoordinate(referenceLat, referenceLon));
        (int cprLat, int cprLon, CprFormat format) = ExtractCpr(RealFrames.SurfacePos_484175_Riddle);

        // Act
        GeographicCoordinate? result = decoder.DecodePosition(cprLat, cprLon, format);

        // Assert
        result.Should().NotBeNull(because: $"the reference is {description}");
        result!.Latitude.Should().BeApproximately(52.320561, 0.000001);
        result.Longitude.Should().BeApproximately(4.735735, 0.000001);
    }

    // ========================================
    // Round trip with an independent encoder
    // ========================================

    /// <summary>
    /// Encodes a position, decodes it with a receiver about 20 km away, and expects the
    /// original position back, in both CPR formats and all four hemispheres.
    /// </summary>
    [Theory]
    [InlineData(-33.9461, 151.1772, "Sydney airport (south, east)")]
    [InlineData(-23.4356, -46.4731, "Sao Paulo Guarulhos (south, west)")]
    [InlineData(47.4405, 19.2500, "Budapest Liszt Ferenc airport (north, east)")]
    [InlineData(40.6413, -73.7781, "New York JFK (north, west)")]
    [InlineData(0.3800, 32.4400, "Entebbe area (just north of the equator)")]
    [InlineData(64.1300, -21.9400, "Reykjavik (high latitude, few longitude zones)")]
    public void DecodePosition_IndependentlyEncodedPosition_RoundTripsInBothFormats(
        double lat, double lon, string description)
    {
        // Arrange - receiver offset by about 13 km north and 11-15 km east of the aircraft
        var decoder = new SurfaceCprDecoder();
        decoder.SetReceiverLocation(new GeographicCoordinate(lat + 0.12, lon + 0.15));

        foreach (CprFormat format in new[] { CprFormat.Even, CprFormat.Odd })
        {
            (int cprLat, int cprLon) = EncodeSurfaceCpr(lat, lon, format);

            // Act
            GeographicCoordinate? result = decoder.DecodePosition(cprLat, cprLon, format);

            // Assert - one CPR step is about 1.3 m, far below the tolerance
            result.Should().NotBeNull(because: $"{description}, {format} format");
            result!.Latitude.Should().BeApproximately(lat, 0.0001, because: $"{description}, {format} format");
            result.Longitude.Should().BeApproximately(lon, 0.0001, because: $"{description}, {format} format");
        }
    }

    /// <summary>
    /// A point at Budapest airport with a receiver in the city. With a linear 1.5° NL table
    /// and no odd-format adjustment, this decoded 105 km west (even) and 85 km east (odd).
    /// </summary>
    [Theory]
    [InlineData(CprFormat.Even)]
    [InlineData(CprFormat.Odd)]
    public void DecodePosition_BudapestAirportWithReceiverInCity_IsAtTheAirport(CprFormat format)
    {
        // Arrange
        var decoder = new SurfaceCprDecoder();
        decoder.SetReceiverLocation(new GeographicCoordinate(47.50, 19.05));
        (int cprLat, int cprLon) = EncodeSurfaceCpr(47.4405, 19.2500, format);

        // Act
        GeographicCoordinate? result = decoder.DecodePosition(cprLat, cprLon, format);

        // Assert
        result.Should().NotBeNull();
        result!.Latitude.Should().BeApproximately(47.4405, 0.001);
        result.Longitude.Should().BeApproximately(19.2500, 0.001);
    }

    [Fact]
    public void DecodePosition_WithoutReceiverLocation_ReturnsNull()
    {
        // Arrange - no receiver location set
        var decoder = new SurfaceCprDecoder();

        // Act
        GeographicCoordinate? result = decoder.DecodePosition(55924, 21512, CprFormat.Even);

        // Assert
        result.Should().BeNull(because: "surface CPR requires a receiver location");
    }

    // ========================================
    // Helpers
    // ========================================

    /// <summary>
    /// Encodes a position into 17-bit surface CPR values (90° base, ICAO Doc 9871 / DO-260B
    /// encoding). The longitude zone count comes from <see cref="ClosedFormNl"/>, evaluated at
    /// the latitude a decoder will reconstruct, as the standard specifies.
    /// </summary>
    private static (int CprLat, int CprLon) EncodeSurfaceCpr(double lat, double lon, CprFormat format)
    {
        int odd = format == CprFormat.Odd ? 1 : 0;
        double dLat = 90.0 / (60 - odd);
        int yz = (int)Math.Floor((131072.0 * MathMod(lat, dLat) / dLat) + 0.5);
        double rLat = dLat * ((yz / 131072.0) + Math.Floor(lat / dLat));

        double dLon = 90.0 / Math.Max(ClosedFormNl(rLat) - odd, 1);
        int xz = (int)Math.Floor((131072.0 * MathMod(lon, dLon) / dLon) + 0.5);

        return (yz % 131072, xz % 131072);
    }

    /// <summary>
    /// Number of longitude zones from the closed-form formula, with the standard special
    /// cases: 59 at the equator, 2 at ±87°, 1 beyond.
    /// </summary>
    private static int ClosedFormNl(double lat)
    {
        double absLat = Math.Abs(lat);
        if (absLat == 0)
        {
            return 59;
        }

        if (absLat == 87)
        {
            return 2;
        }

        if (absLat > 87)
        {
            return 1;
        }

        double a = 1 - Math.Cos(Math.PI / 30);
        double b = Math.Pow(Math.Cos(Math.PI / 180 * absLat), 2);
        return (int)Math.Floor(2 * Math.PI / Math.Acos(1 - (a / b)));
    }

    /// <summary>
    /// Extracts the CPR format and the 17-bit latitude and longitude from a 112-bit frame.
    /// </summary>
    private static (int CprLat, int CprLon, CprFormat Format) ExtractCpr(string hex)
    {
        UInt128 value = UInt128.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        var format = (CprFormat)(int)((value >> (112 - 54)) & 1);
        int cprLat = (int)((value >> (112 - 71)) & 0x1FFFF);
        int cprLon = (int)((value >> (112 - 88)) & 0x1FFFF);
        return (cprLat, cprLon, format);
    }

    /// <summary>
    /// Mathematical modulo that always returns a positive result.
    /// </summary>
    private static double MathMod(double a, double b)
    {
        double res = a % b;
        return res < 0 ? res + b : res;
    }
}
