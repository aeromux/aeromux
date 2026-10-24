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

using System.Globalization;
using Aeromux.Core.ModeS.ValueObjects;
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS.ValueObjects;

/// <summary>
/// Tests for the GeographicBounds value object: edge validation, and containment and
/// overlap for ordinary boxes and boxes that cross the 180° meridian.
/// </summary>
public class GeographicBoundsTests
{
    [Fact]
    public void TryCreate_ValidEdges_Succeeds()
    {
        GeographicBounds.TryCreate(46.5, 18.0, 48.0, 20.5, out GeographicBounds bounds, out string? error)
            .Should().BeTrue();

        error.Should().BeNull();
        bounds.Should().Be(new GeographicBounds(46.5, 18.0, 48.0, 20.5));
        bounds.CrossesAntimeridian.Should().BeFalse();
    }

    [Fact]
    public void TryCreate_WestEastOfEast_IsAcceptedAsCrossingTheMeridian()
    {
        GeographicBounds.TryCreate(-50, 170, -30, -170, out GeographicBounds bounds, out _).Should().BeTrue();

        bounds.CrossesAntimeridian.Should().BeTrue();
    }

    [Theory]
    [InlineData(double.NaN, 18, 48, 20, "Invalid bounds values: all values must be finite numbers")]
    [InlineData(46, 18, 48, double.NaN, "Invalid bounds values: all values must be finite numbers")]
    [InlineData(46, double.NegativeInfinity, 48, 20, "Invalid bounds values: all values must be finite numbers")]
    [InlineData(48, 18, 46.5, 20, "Invalid bounds: south (48) must be less than north (46.5)")]
    [InlineData(-91, 18, 48, 20, "Invalid bounds: latitude must be between -90 and 90")]
    [InlineData(46, 18, 91, 20, "Invalid bounds: latitude must be between -90 and 90")]
    [InlineData(46, 190, 48, 170, "Invalid bounds: longitude must be between -180 and 180")]
    [InlineData(46, -170, 48, -200, "Invalid bounds: longitude must be between -180 and 180")]
    [InlineData(46, -181, 48, 20, "Invalid bounds: longitude must be between -180 and 180")]
    [InlineData(46, 18, 48, 181, "Invalid bounds: longitude must be between -180 and 180")]
    public void TryCreate_InvalidEdges_FailsWithMessage(
        double south, double west, double north, double east, string expected)
    {
        GeographicBounds.TryCreate(south, west, north, east, out _, out string? error).Should().BeFalse();

        error.Should().Be(expected);
    }

    [Fact]
    public void TryCreate_ErrorMessage_IgnoresProcessCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("hu-HU");

            GeographicBounds.TryCreate(48.5, 18, 46.5, 20, out _, out string? error).Should().BeFalse();

            error.Should().Be("Invalid bounds: south (48.5) must be less than north (46.5)");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TryCreate_WestEqualsEast_IsAccepted()
    {
        GeographicBounds.TryCreate(46, 19, 48, 19, out GeographicBounds bounds, out _).Should().BeTrue();

        bounds.Contains(47, 19).Should().BeTrue();
        bounds.Contains(47, 19.1).Should().BeFalse();
    }

    [Theory]
    [InlineData(47.0, 19.0, true)]
    [InlineData(46.5, 18.0, true)]   // corner, edges included
    [InlineData(48.1, 19.0, false)]
    [InlineData(47.0, 20.6, false)]
    public void Contains_PlainBox(double lat, double lon, bool expected)
    {
        new GeographicBounds(46.5, 18.0, 48.0, 20.5).Contains(lat, lon).Should().Be(expected);
    }

    [Theory]
    [InlineData(-40.0, 175.0, true)]
    [InlineData(-40.0, 180.0, true)]
    [InlineData(-40.0, -180.0, true)]
    [InlineData(-40.0, -175.0, true)]
    [InlineData(-40.0, 0.0, false)]
    [InlineData(-40.0, 169.9, false)]
    [InlineData(-40.0, -169.9, false)]
    [InlineData(-29.0, 175.0, false)]
    public void Contains_BoxAcrossTheMeridian(double lat, double lon, bool expected)
    {
        new GeographicBounds(-50, 170, -30, -170).Contains(lat, lon).Should().Be(expected);
    }

    [Fact]
    public void Contains_Coordinate_MatchesLatitudeLongitudeOverload()
    {
        var bounds = new GeographicBounds(-50, 170, -30, -170);

        bounds.Contains(new GeographicCoordinate(-40, -175)).Should().BeTrue();
        bounds.Contains(new GeographicCoordinate(-40, 0)).Should().BeFalse();
    }

    [Fact]
    public void Contains_NullCoordinate_Throws()
    {
        Action act = () => new GeographicBounds(46, 18, 48, 20).Contains(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(-41, 175, -40, 176, true)]     // inside the eastern-hemisphere half
    [InlineData(-41, -176, -40, -175, true)]   // inside the western-hemisphere half
    [InlineData(-41, 179.9, -40, 180.1, true)] // a grid cell running past 180
    [InlineData(-41, 160, -40, 170, true)]     // touches the west edge
    [InlineData(-41, 0, -40, 1, false)]
    [InlineData(-20, 175, -19, 176, false)]    // right longitude, wrong latitude
    public void Intersects_BoxAcrossTheMeridian(double s, double w, double n, double e, bool expected)
    {
        new GeographicBounds(-50, 170, -30, -170).Intersects(s, w, n, e).Should().Be(expected);
    }

    [Theory]
    [InlineData(47, 19, 47.5, 19.5, true)]
    [InlineData(48, 20.5, 49, 21, true)]   // touching corner
    [InlineData(49, 19, 50, 20, false)]
    [InlineData(47, 21, 47.5, 22, false)]
    public void Intersects_PlainBox(double s, double w, double n, double e, bool expected)
    {
        new GeographicBounds(46.5, 18.0, 48.0, 20.5).Intersects(s, w, n, e).Should().Be(expected);
    }
}
