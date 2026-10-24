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
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.Core.ModeS.ValueObjects;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for parsing the <c>bounds</c> query parameter. Edge validation itself belongs to
/// <see cref="GeographicBounds"/> and is tested in Core; these cover the text format.
/// </summary>
public class BoundsParsingTests
{
    [Fact]
    public void TryParseBounds_ValidText_Succeeds()
    {
        DaemonApiRoutes.TryParseBounds("46.5,18.0,48.0,20.5", out GeographicBounds bounds, out string? error)
            .Should().BeTrue();

        error.Should().BeNull();
        bounds.Should().Be(new GeographicBounds(46.5, 18.0, 48.0, 20.5));
    }

    [Theory]
    [InlineData("1,2,3", "Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got 3")]
    [InlineData("1,2,3,4,5", "Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got 5")]
    [InlineData("", "Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got 1")]
    [InlineData(null, "Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got 1")]
    [InlineData("a,2,3,4", "Invalid bounds values: all values must be valid numbers")]
    [InlineData("46,,48,20", "Invalid bounds values: all values must be valid numbers")]
    public void TryParseBounds_MalformedText_FailsWithMessage(string? text, string expected)
    {
        DaemonApiRoutes.TryParseBounds(text, out _, out string? error).Should().BeFalse();

        error.Should().Be(expected);
    }

    [Theory]
    [InlineData("NaN,18,48,20", "Invalid bounds values: all values must be finite numbers")]
    [InlineData("48,18,46.5,20", "Invalid bounds: south (48) must be less than north (46.5)")]
    [InlineData("46,190,48,170", "Invalid bounds: longitude must be between -180 and 180")]
    public void TryParseBounds_InvalidEdges_PassesOnTheValidationMessage(string text, string expected)
    {
        DaemonApiRoutes.TryParseBounds(text, out _, out string? error).Should().BeFalse();

        error.Should().Be(expected);
    }

    [Fact]
    public void TryParseBounds_UsesAnInvariantDecimalPointWhateverTheProcessCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("hu-HU");

            DaemonApiRoutes.TryParseBounds("46.5,18.25,48,20.5", out GeographicBounds bounds, out _)
                .Should().BeTrue();

            bounds.Should().Be(new GeographicBounds(46.5, 18.25, 48, 20.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
