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
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for SurfaceCprDecoder, focusing on correct decoding across all hemispheres.
/// Verifies the CprMod fix for Southern/Western Hemisphere receivers where C#'s
/// signed % operator previously produced incorrect zone indices.
/// </summary>
public class SurfaceCprDecoderTests
{
    /// <summary>
    /// Surface NL lookup table (matches SurfaceCprDecoder.SurfaceNlTable).
    /// </summary>
    private static readonly int[] SurfaceNlTable =
    [
        59, 58, 57, 56, 55, 54, 53, 52, 51, 50,
        49, 48, 47, 46, 45, 44, 43, 42, 41, 40,
        39, 38, 37, 36, 35, 34, 33, 32, 31, 30,
        29, 28, 27, 26, 25, 24, 23, 22, 21, 20,
        19, 18, 17, 16, 15, 14, 13, 12, 11, 10,
         9,  8,  7,  6,  5,  4,  3,  2,  1,  1
    ];

    [Theory]
    [InlineData(-33.86, 151.21, "Sydney (Southern Hemisphere, Eastern Hemisphere)")]
    [InlineData(-23.55, -46.63, "Sao Paulo (Southern Hemisphere, Western Hemisphere)")]
    [InlineData(47.43, 19.26, "Budapest (Northern Hemisphere, Eastern Hemisphere)")]
    [InlineData(40.64, -73.78, "New York JFK (Northern Hemisphere, Western Hemisphere)")]
    public void DecodePosition_AllHemispheres_ReturnsPositionNearTarget(
        double targetLat, double targetLon, string description)
    {
        // Arrange — receiver is at the target position (surface CPR uses receiver as reference)
        var decoder = new SurfaceCprDecoder();
        var receiverLocation = new GeographicCoordinate(targetLat, targetLon);
        decoder.SetReceiverLocation(receiverLocation);

        // Encode the target position into CPR values (even format)
        (int cprLat, int cprLon) = EncodeSurfaceCpr(targetLat, targetLon, CprFormat.Even);

        // Act
        GeographicCoordinate? result = decoder.DecodePosition(cprLat, cprLon, CprFormat.Even);

        // Assert — decoded position should be within ~0.01 degrees of target
        result.Should().NotBeNull(because: $"decoding should succeed for {description}");
        result!.Latitude.Should().BeApproximately(targetLat, 0.01,
            because: $"latitude should be near target for {description}");
        result.Longitude.Should().BeApproximately(targetLon, 0.01,
            because: $"longitude should be near target for {description}");
    }

    [Theory]
    [InlineData(-33.86, 151.21, "Sydney")]
    [InlineData(-23.55, -46.63, "Sao Paulo")]
    public void DecodePosition_SouthernHemisphere_LatitudeNotShiftedByZoneWidth(
        double targetLat, double targetLon, string description)
    {
        // Arrange
        var decoder = new SurfaceCprDecoder();
        decoder.SetReceiverLocation(new GeographicCoordinate(targetLat, targetLon));
        (int cprLat, int cprLon) = EncodeSurfaceCpr(targetLat, targetLon, CprFormat.Even);

        // Act
        GeographicCoordinate? result = decoder.DecodePosition(cprLat, cprLon, CprFormat.Even);

        // Assert — specifically check that latitude is NOT shifted by ~1.5 degrees (the bug)
        result.Should().NotBeNull();
        double latError = Math.Abs(result!.Latitude - targetLat);
        latError.Should().BeLessThan(0.1,
            because: $"latitude for {description} should not be shifted by a zone width (~1.5 degrees)");
    }

    [Fact]
    public void DecodePosition_WithoutReceiverLocation_ReturnsNull()
    {
        // Arrange — no receiver location set
        var decoder = new SurfaceCprDecoder();

        // Act
        GeographicCoordinate? result = decoder.DecodePosition(55924, 21512, CprFormat.Even);

        // Assert
        result.Should().BeNull(because: "surface CPR requires a receiver location");
    }

    /// <summary>
    /// Encodes a geographic position into surface CPR values.
    /// Uses mathematical modulo (always positive) to match the corrected decoder behavior.
    /// </summary>
    private static (int cprLat, int cprLon) EncodeSurfaceCpr(
        double lat, double lon, CprFormat format)
    {
        const double dLatEven = 90.0 / 60.0;
        const double dLatOdd = 90.0 / 59.0;

        double dLat = format == CprFormat.Even ? dLatEven : dLatOdd;
        double yzFrac = MathMod(lat, dLat) / dLat;
        int cprLat = (int)Math.Floor(131072.0 * yzFrac + 0.5);

        // Surface NL at this latitude
        int nlIndex = (int)Math.Floor(Math.Abs(lat) / 1.5);
        int nl = SurfaceNlTable[Math.Clamp(nlIndex, 0, SurfaceNlTable.Length - 1)];
        double dLon = 90.0 / nl;

        double xzFrac = MathMod(lon, dLon) / dLon;
        int cprLon = (int)Math.Floor(131072.0 * xzFrac + 0.5);

        return (cprLat, cprLon);
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
