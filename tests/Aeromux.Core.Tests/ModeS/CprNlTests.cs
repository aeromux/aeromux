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

using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for the CPR number-of-longitude-zones function (NL) shared by airborne and surface
/// position decoding.
/// </summary>
public class CprNlTests
{
    [Theory]
    [InlineData(0.0, 59)]
    [InlineData(10.47, 59)]
    [InlineData(10.48, 58)]
    [InlineData(47.5, 40)]
    [InlineData(86.99, 2)]
    [InlineData(87.0, 2)]
    [InlineData(-87.0, 2)]
    [InlineData(87.01, 1)]
    [InlineData(90.0, 1)]
    public void CprNL_ReturnsStandardZoneCount(double latitude, int expected)
    {
        // Act
        int nl = CprDecoder.CprNL(latitude);

        // Assert - the closed-form NL function, with 2 zones at exactly ±87° and 1 beyond
        nl.Should().Be(expected);
    }
}
