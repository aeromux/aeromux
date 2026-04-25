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
/// Tests for the Velocity value object boundary validation and unit conversions.
/// Verifies the 4096 knot cap covers full supersonic encoding range
/// while rejecting corrupt data, and rounding correctness for km/h and mph conversions.
/// </summary>
public class VelocityTests
{
    [Fact]
    public void FromKnots_AtMaximum_Succeeds()
    {
        // 4096 is the cap — full single-axis supersonic encoding (4088) fits within
        Velocity velocity = Velocity.FromKnots(4096, VelocityType.GroundSpeed);
        velocity.Knots.Should().Be(4096);
    }

    [Fact]
    public void FromKnots_AboveMaximum_Throws()
    {
        Action act = () => Velocity.FromKnots(4097, VelocityType.GroundSpeed);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromKnots_Zero_Succeeds()
    {
        Velocity velocity = Velocity.FromKnots(0, VelocityType.GroundSpeed);
        velocity.Knots.Should().Be(0);
    }

    [Fact]
    public void FromKnots_Negative_Throws()
    {
        Action act = () => Velocity.FromKnots(-1, VelocityType.GroundSpeed);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromKnots_SupersonicSingleAxis_Succeeds()
    {
        // TC 19 supersonic max per axis: (1023 - 1) * 4 = 4088
        Velocity velocity = Velocity.FromKnots(4088, VelocityType.GroundSpeed);
        velocity.Knots.Should().Be(4088);
    }

    [Fact]
    public void FromKnots_Bds50MaxTas_Succeeds()
    {
        // BDS 5,0 TAS max: 2046 knots
        Velocity velocity = Velocity.FromKnots(2046, VelocityType.TrueAirspeed);
        velocity.Knots.Should().Be(2046);
    }

    [Fact]
    public void FromKnots_KilometersPerHour_RoundsCorrectly()
    {
        // 450 kts × 1.852 = 833.4 → rounds to 833
        Velocity velocity = Velocity.FromKnots(450, VelocityType.GroundSpeed);
        velocity.KilometersPerHour.Should().Be(833);
    }

    [Fact]
    public void FromKnots_MilesPerHour_RoundsCorrectly()
    {
        // 450 kts × 1.15078 = 517.851 → rounds to 518
        Velocity velocity = Velocity.FromKnots(450, VelocityType.GroundSpeed);
        velocity.MilesPerHour.Should().Be(518);
    }

    [Fact]
    public void FromKilometersPerHour_Knots_RoundsCorrectly()
    {
        // 833 km/h / 1.852 = 449.784... → rounds to 450 (was 449 before rounding fix)
        Velocity velocity = Velocity.FromKilometersPerHour(833, VelocityType.GroundSpeed);
        velocity.Knots.Should().Be(450);
    }

    [Fact]
    public void FromMilesPerHour_Knots_RoundsCorrectly()
    {
        // 518 mph / 1.15078 = 450.06... → rounds to 450
        Velocity velocity = Velocity.FromMilesPerHour(518, VelocityType.GroundSpeed);
        velocity.Knots.Should().Be(450);
    }

    [Fact]
    public void FromKnots_KilometersPerHour_RoundTrip()
    {
        // FromKnots(450) → .KilometersPerHour → FromKilometersPerHour → .Knots = 450
        Velocity original = Velocity.FromKnots(450, VelocityType.GroundSpeed);
        Velocity roundTripped = Velocity.FromKilometersPerHour(original.KilometersPerHour, VelocityType.GroundSpeed);
        roundTripped.Knots.Should().Be(450);
    }
}
