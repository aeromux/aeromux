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

using Aeromux.Core.ModeS;
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for <see cref="EnumValidator"/> — verifies every validation method
/// accepts all valid values and rejects boundary, negative, and out-of-range values.
/// </summary>
public class EnumValidatorTests
{
    // ========================================
    // Contiguous enums: range 0-1
    // ========================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    [InlineData(int.MaxValue, false)]
    [InlineData(int.MinValue, false)]
    public void IsValidAntennaFlag_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidAntennaFlag(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidCprFormat_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidCprFormat(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidHorizontalReferenceDirection_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidHorizontalReferenceDirection(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidBarometricAltitudeIntegrityCode_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidBarometricAltitudeIntegrityCode(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidTargetHeadingType_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidTargetHeadingType(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidSilSupplement_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidSilSupplement(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidOperationalStatusSubtype_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidOperationalStatusSubtype(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidTargetStateSubtype_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidTargetStateSubtype(value).Should().Be(expected);

    // ========================================
    // Contiguous enums: range 0-2
    // ========================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    public void IsValidAircraftStatusSubtype_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidAircraftStatusSubtype(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    public void IsValidVerticalMode_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidVerticalMode(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    public void IsValidHorizontalMode_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidHorizontalMode(value).Should().Be(expected);

    // ========================================
    // Contiguous enums: range 0-3
    // ========================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidSurveillanceStatus_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidSurveillanceStatus(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidTrajectoryChangeReportCapability_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidTrajectoryChangeReportCapability(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidSourceIntegrityLevel_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidSourceIntegrityLevel(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidSdaSupportedFailureCondition_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidSdaSupportedFailureCondition(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidGeometricVerticalAccuracy_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidGeometricVerticalAccuracy(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidSeverity_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidSeverity(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidBds40AltitudeSource_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidBds40AltitudeSource(value).Should().Be(expected);

    // ========================================
    // Contiguous enums: range 0-7
    // ========================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(8, false)]
    public void IsValidNavigationAccuracyCategoryVelocity_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidNavigationAccuracyCategoryVelocity(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(8, false)]
    public void IsValidEmergencyState_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidEmergencyState(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(8, false)]
    public void IsValidAdsbVersion_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidAdsbVersion(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(8, false)]
    public void IsValidFlightStatus_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidFlightStatus(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(8, false)]
    public void IsValidTransponderCapability_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidTransponderCapability(value).Should().Be(expected);

    // ========================================
    // Contiguous enums: range 0-15
    // ========================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(-1, false)]
    [InlineData(16, false)]
    public void IsValidNavigationAccuracyCategoryPosition_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidNavigationAccuracyCategoryPosition(value).Should().Be(expected);

    // ========================================
    // Sparse enums: lookup-based
    // ========================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(14, true)]
    [InlineData(1, false)]   // gap
    [InlineData(5, false)]   // gap
    [InlineData(6, false)]   // gap
    [InlineData(7, false)]   // gap
    [InlineData(15, false)]  // out of range
    [InlineData(-1, false)]
    [InlineData(100, false)]
    public void IsValidAcasReplyInformation_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidAcasReplyInformation(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(21, true)]
    [InlineData(23, true)]
    [InlineData(31, true)]
    [InlineData(32, true)]
    [InlineData(33, true)]
    [InlineData(40, true)]
    [InlineData(47, true)]
    [InlineData(1, false)]   // gap
    [InlineData(11, false)]  // gap
    [InlineData(22, false)]  // gap
    [InlineData(34, false)]  // gap
    [InlineData(48, false)]  // out of range
    [InlineData(-1, false)]
    [InlineData(200, false)]
    public void IsValidAircraftCategory_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidAircraftCategory(value).Should().Be(expected);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(24, true)]
    [InlineData(124, true)]
    [InlineData(127, true)]
    [InlineData(25, false)]  // gap
    [InlineData(100, false)] // gap
    [InlineData(123, false)] // gap
    [InlineData(125, false)] // gap
    [InlineData(126, false)] // gap
    [InlineData(128, false)] // out of range
    [InlineData(-1, false)]
    [InlineData(255, false)]
    public void IsValidSurfaceMovement_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidSurfaceMovement(value).Should().Be(expected);

    // ========================================
    // Startup-only: ConfidenceLevel
    // ========================================

    [Theory]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(15, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(6, false)]
    [InlineData(11, false)]
    [InlineData(16, false)]
    [InlineData(-1, false)]
    [InlineData(100, false)]
    public void IsValidConfidenceLevel_ReturnsExpected(int value, bool expected) =>
        EnumValidator.IsValidConfidenceLevel(value).Should().Be(expected);
}
