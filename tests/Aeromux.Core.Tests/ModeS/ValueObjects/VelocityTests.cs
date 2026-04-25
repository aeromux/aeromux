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
/// Tests for the Velocity value object boundary validation.
/// Verifies the 4096 knot cap covers full supersonic encoding range
/// while rejecting corrupt data.
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
}
