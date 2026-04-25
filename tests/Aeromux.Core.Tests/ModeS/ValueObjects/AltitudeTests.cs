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

namespace Aeromux.Core.Tests.ModeS.ValueObjects;

/// <summary>
/// Tests for the Altitude value object unit conversions.
/// Verifies rounding correctness and round-trip stability for feet/meters conversions.
/// </summary>
public class AltitudeTests
{
    [Fact]
    public void FromFeet_Meters_RoundsCorrectly()
    {
        // 35000 ft × 0.3048 = 10668.0 exactly
        Altitude altitude = Altitude.FromFeet(35000, AltitudeType.Barometric);
        altitude.Meters.Should().Be(10668);
    }

    [Fact]
    public void FromMeters_Feet_RoundsCorrectly()
    {
        // 10668 / 0.3048 = 34999.999... → rounds to 35000 (was 34999 before rounding fix)
        Altitude altitude = Altitude.FromMeters(10668, AltitudeType.Barometric);
        altitude.Feet.Should().Be(35000);
    }

    [Fact]
    public void FromFeet_Meters_RoundTrip()
    {
        // FromFeet(35000) → .Meters → FromMeters → .Feet should be 35000
        Altitude original = Altitude.FromFeet(35000, AltitudeType.Barometric);
        Altitude roundTripped = Altitude.FromMeters(original.Meters, AltitudeType.Barometric);
        roundTripped.Feet.Should().Be(35000);
    }

    [Fact]
    public void FromFeet_Zero_Meters()
    {
        Altitude altitude = Altitude.FromFeet(0, AltitudeType.Barometric);
        altitude.Meters.Should().Be(0);
    }

    [Fact]
    public void FromFeet_Negative_Meters()
    {
        // -1000 ft × 0.3048 = -304.8 → rounds to -305
        Altitude altitude = Altitude.FromFeet(-1000, AltitudeType.Barometric);
        altitude.Meters.Should().Be(-305);
    }

    [Fact]
    public void FromFeet_FlightLevel()
    {
        Altitude altitude = Altitude.FromFeet(35000, AltitudeType.Barometric);
        altitude.FlightLevel.Should().Be(350);
    }

    [Fact]
    public void FromMeters_GnssAltitude()
    {
        // TC 20-22 GNSS path: 3048 m / 0.3048 = 10000.0 exactly
        Altitude altitude = Altitude.FromMeters(3048, AltitudeType.Geometric);
        altitude.Feet.Should().Be(10000);
    }
}
