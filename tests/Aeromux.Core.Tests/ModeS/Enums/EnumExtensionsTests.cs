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

namespace Aeromux.Core.Tests.ModeS.Enums;

/// <summary>
/// Tests for <see cref="EnumExtensions.ToDisplayString{TEnum}(TEnum)"/> and its nullable overload.
/// </summary>
public class EnumExtensionsTests
{
    [Theory]
    [InlineData(EmergencyState.NoEmergency, "No Emergency")]
    [InlineData(EmergencyState.GeneralEmergency, "General Emergency")]
    [InlineData(EmergencyState.LifeguardMedical, "Lifeguard/Medical")]
    [InlineData(EmergencyState.MinimumFuel, "Minimum Fuel")]
    [InlineData(EmergencyState.NoCommunications, "No Communications")]
    [InlineData(EmergencyState.UnlawfulInterference, "Unlawful Interference")]
    [InlineData(EmergencyState.DownedAircraft, "Downed Aircraft")]
    [InlineData(EmergencyState.Reserved, "Reserved")]
    public void ToDisplayString_EmergencyState_ReturnsAttributeValue(EmergencyState value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    [Theory]
    [InlineData(FlightStatus.AirborneNormal, "Airborne")]
    [InlineData(FlightStatus.OnGroundNormal, "On Ground")]
    public void ToDisplayString_FlightStatus_ReturnsAttributeValue(FlightStatus value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    [Theory]
    [InlineData(Severity.Nil, "Nil")]
    [InlineData(Severity.Light, "Light")]
    [InlineData(Severity.Moderate, "Moderate")]
    [InlineData(Severity.Severe, "Severe")]
    public void ToDisplayString_Severity_ReturnsAttributeValue(Severity value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    [Theory]
    [InlineData(VerticalMode.None, "None")]
    [InlineData(VerticalMode.Acquiring, "Acquiring")]
    [InlineData(VerticalMode.CapturingOrMaintaining, "Capturing or Maintaining")]
    public void ToDisplayString_VerticalMode_ReturnsAttributeValue(VerticalMode value, string expected)
    {
        Assert.Equal(expected, value.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_Nullable_ReturnsNullForNullInput()
    {
        EmergencyState? value = null;
        Assert.Null(value.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_Nullable_ReturnsDisplayNameForNonNullInput()
    {
        EmergencyState? value = EmergencyState.GeneralEmergency;
        Assert.Equal("General Emergency", value.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_SameValueCalledTwice_ReturnsSameResult()
    {
        string first = EmergencyState.MinimumFuel.ToDisplayString();
        string second = EmergencyState.MinimumFuel.ToDisplayString();
        Assert.Equal(first, second);
        Assert.Equal("Minimum Fuel", first);
    }
}
