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

using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tests.TestData;

namespace Aeromux.Core.Tests.MessageParsing;

/// <summary>
/// Tests for parsing Operational Status messages (TC 31).
/// Covers Version 0, 1, and 2 (DO-260, DO-260A, DO-260B) with comprehensive field validation.
/// </summary>
public class OperationalStatusTest
{
    private readonly MessageParser _parser = new();

    // ========================================
    // Basic Message Fields
    // ========================================

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, "471DBC", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_71C011, "71C011", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, "3C55C5", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_4BB027, "4BB027", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_80073B, "80073B", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_06A081, "06A081", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, "4BB0F4", DownlinkFormat.ExtendedSquitter)]
    [InlineData(RealFrames.OperationalStatus_5082A0, "5082A0", DownlinkFormat.ExtendedSquitter)]
    public void ParseMessage_DF17_TC31_OperationalStatus_BasicFields(
        string hexFrame,
        string expectedIcao,
        DownlinkFormat expectedDF)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .WithIcaoAddress(expectedIcao)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.IcaoAddress.Should().Be(expectedIcao);
        opStatus.DownlinkFormat.Should().Be(expectedDF);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_71C011, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_4BB027, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_80073B, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_06A081, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, OperationalStatusSubtype.Airborne)]
    [InlineData(RealFrames.OperationalStatus_5082A0, OperationalStatusSubtype.Airborne)]
    public void ParseMessage_DF17_TC31_OperationalStatus_Subtype(
        string hexFrame,
        OperationalStatusSubtype expectedSubtype)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.Subtype.Should().Be(expectedSubtype);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_71C011, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_4BB027, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_80073B, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_06A081, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, AdsbVersion.DO260B)]
    [InlineData(RealFrames.OperationalStatus_5082A0, AdsbVersion.DO260B)]
    public void ParseMessage_DF17_TC31_OperationalStatus_Version(
        string hexFrame,
        AdsbVersion expectedVersion)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.Version.Should().Be(expectedVersion);
    }

    // ========================================
    // Accuracy and Integrity Fields
    // ========================================

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.OperationalStatus_71C011, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.OperationalStatus_4BB027, NavigationAccuracyCategoryPosition.LessThan3m)]
    [InlineData(RealFrames.OperationalStatus_80073B, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.OperationalStatus_06A081, NavigationAccuracyCategoryPosition.LessThan30m)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, NavigationAccuracyCategoryPosition.LessThan3m)]
    [InlineData(RealFrames.OperationalStatus_5082A0, NavigationAccuracyCategoryPosition.LessThan3m)]
    public void ParseMessage_DF17_TC31_OperationalStatus_NACp(
        string hexFrame,
        NavigationAccuracyCategoryPosition expectedNACp)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.NACp.Should().Be(expectedNACp);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_71C011, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_4BB027, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_80073B, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_06A081, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, GeometricVerticalAccuracy.LessThan45Meters)]
    [InlineData(RealFrames.OperationalStatus_5082A0, GeometricVerticalAccuracy.LessThan45Meters)]
    public void ParseMessage_DF17_TC31_OperationalStatus_GeometricVerticalAccuracy(
        string hexFrame,
        GeometricVerticalAccuracy expectedGVA)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.GeometricVerticalAccuracy.Should().Be(expectedGVA, "All test frames have GVA=2 (reserved, treated as ≤ 45m)");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_71C011, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_4BB027, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_80073B, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_06A081, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, SourceIntegrityLevel.PerHour1E7)]
    [InlineData(RealFrames.OperationalStatus_5082A0, SourceIntegrityLevel.PerHour1E7)]
    public void ParseMessage_DF17_TC31_OperationalStatus_SIL(
        string hexFrame,
        SourceIntegrityLevel expectedSIL)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.SIL.Should().Be(expectedSIL, "All test frames have SIL=3 (1×10⁻⁷ per hour)");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_71C011, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_4BB027, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_80073B, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_06A081, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    [InlineData(RealFrames.OperationalStatus_5082A0, BarometricAltitudeIntegrityCode.CrossCheckedOrNonGilham)]
    public void ParseMessage_DF17_TC31_OperationalStatus_NICbaro(
        string hexFrame,
        BarometricAltitudeIntegrityCode expectedNICbaro)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.NICbaro.Should().Be(expectedNICbaro, "All test frames have barometric altitude cross-checked");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, false)]
    [InlineData(RealFrames.OperationalStatus_71C011, false)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, false)]
    [InlineData(RealFrames.OperationalStatus_4BB027, false)]
    [InlineData(RealFrames.OperationalStatus_80073B, true)]
    [InlineData(RealFrames.OperationalStatus_06A081, false)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, true)]
    [InlineData(RealFrames.OperationalStatus_5082A0, true)]
    public void ParseMessage_DF17_TC31_OperationalStatus_NicSupplementA(
        string hexFrame,
        bool expectedNicSupplementA)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.NICSupplementA.Should().Be(expectedNicSupplementA);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, HorizontalReferenceDirection.TrueNorth)]
    [InlineData(RealFrames.OperationalStatus_71C011, HorizontalReferenceDirection.TrueNorth)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, HorizontalReferenceDirection.TrueNorth)]
    [InlineData(RealFrames.OperationalStatus_4BB027, HorizontalReferenceDirection.TrueNorth)]
    [InlineData(RealFrames.OperationalStatus_80073B, HorizontalReferenceDirection.MagneticNorth)]
    [InlineData(RealFrames.OperationalStatus_06A081, HorizontalReferenceDirection.TrueNorth)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, HorizontalReferenceDirection.MagneticNorth)]
    [InlineData(RealFrames.OperationalStatus_5082A0, HorizontalReferenceDirection.MagneticNorth)]
    public void ParseMessage_DF17_TC31_OperationalStatus_HRD(
        string hexFrame,
        HorizontalReferenceDirection expectedHRD)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.HRD.Should().Be(expectedHRD);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_71C011, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_4BB027, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_80073B, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_06A081, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, SilSupplement.PerHour)]
    [InlineData(RealFrames.OperationalStatus_5082A0, SilSupplement.PerHour)]
    public void ParseMessage_DF17_TC31_OperationalStatus_SILSupplement(
        string hexFrame,
        SilSupplement expectedSILSupplement)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.SILSupplement.Should().Be(expectedSILSupplement, "All test frames indicate SIL is per-hour basis");
    }

    // ========================================
    // Capability Class Tests
    // ========================================

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_IsNotNull()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_471DBC)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull("Version 2 airborne messages should have capability class");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, true)]
    [InlineData(RealFrames.OperationalStatus_71C011, true)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, true)]
    [InlineData(RealFrames.OperationalStatus_4BB027, true)]
    [InlineData(RealFrames.OperationalStatus_80073B, true)]
    [InlineData(RealFrames.OperationalStatus_06A081, true)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, true)]
    [InlineData(RealFrames.OperationalStatus_5082A0, true)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_TcasOperational(
        string hexFrame,
        bool expectedTcasOperational)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.TCASOperational.Should().Be(expectedTcasOperational, "All test aircraft have TCAS operational");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, true, "has ARV capability")]
    [InlineData(RealFrames.OperationalStatus_71C011, true, "has ARV capability")]
    [InlineData(RealFrames.OperationalStatus_3C55C5, true, "has ARV capability")]
    [InlineData(RealFrames.OperationalStatus_4BB027, false, "does NOT have ARV capability")]
    [InlineData(RealFrames.OperationalStatus_80073B, false, "does NOT have ARV capability")]
    [InlineData(RealFrames.OperationalStatus_06A081, false, "does NOT have ARV capability")]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, true, "has ARV capability")]
    [InlineData(RealFrames.OperationalStatus_5082A0, false, "does NOT have ARV capability")]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_ArvCapability(
        string hexFrame,
        bool expectedArvCapability,
        string reason)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.ARVCapability.Should().Be(expectedArvCapability, reason);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, true)]
    [InlineData(RealFrames.OperationalStatus_71C011, true)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, true)]
    [InlineData(RealFrames.OperationalStatus_4BB027, true)]
    [InlineData(RealFrames.OperationalStatus_80073B, true)]
    [InlineData(RealFrames.OperationalStatus_06A081, true)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, true)]
    [InlineData(RealFrames.OperationalStatus_5082A0, true)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_TsCapability(
        string hexFrame,
        bool expectedTsCapability)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.TSCapability.Should().Be(expectedTsCapability, "All test aircraft have Target State capability");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, false)]
    [InlineData(RealFrames.OperationalStatus_71C011, false)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, false)]
    [InlineData(RealFrames.OperationalStatus_4BB027, false)]
    [InlineData(RealFrames.OperationalStatus_80073B, false)]
    [InlineData(RealFrames.OperationalStatus_06A081, false)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, false)]
    [InlineData(RealFrames.OperationalStatus_5082A0, false)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_Adsb1090EsCapability(
        string hexFrame,
        bool expectedAdsb1090EsCapability)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.ADSB1090ESCapability.Should().Be(expectedAdsb1090EsCapability, "Test aircraft do not have ADS-B 1090ES receive capability");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_71C011, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_4BB027, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_80073B, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_06A081, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, TrajectoryChangeReportCapability.NoCapability)]
    [InlineData(RealFrames.OperationalStatus_5082A0, TrajectoryChangeReportCapability.NoCapability)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_TcCapabilityLevel(
        string hexFrame,
        TrajectoryChangeReportCapability expectedTcCapabilityLevel)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.TCCapabilityLevel.Should().Be(expectedTcCapabilityLevel, "Test aircraft have no trajectory change capability");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, false)]
    [InlineData(RealFrames.OperationalStatus_71C011, false)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, false)]
    [InlineData(RealFrames.OperationalStatus_4BB027, false)]
    [InlineData(RealFrames.OperationalStatus_80073B, false)]
    [InlineData(RealFrames.OperationalStatus_06A081, false)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, false)]
    [InlineData(RealFrames.OperationalStatus_5082A0, false)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_UatCapability(
        string hexFrame,
        bool expectedUatCapability)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.UATCapability.Should().Be(expectedUatCapability, "Test aircraft do not support UAT (978 MHz)");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC)]
    [InlineData(RealFrames.OperationalStatus_71C011)]
    [InlineData(RealFrames.OperationalStatus_3C55C5)]
    [InlineData(RealFrames.OperationalStatus_4BB027)]
    [InlineData(RealFrames.OperationalStatus_80073B)]
    [InlineData(RealFrames.OperationalStatus_06A081)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4)]
    [InlineData(RealFrames.OperationalStatus_5082A0)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_CdtiCapability_IsNull(string hexFrame)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.CDTICapability.Should().BeNull("CDTI capability is only present in Version 0, test frames are Version 2");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC)]
    [InlineData(RealFrames.OperationalStatus_71C011)]
    [InlineData(RealFrames.OperationalStatus_3C55C5)]
    [InlineData(RealFrames.OperationalStatus_4BB027)]
    [InlineData(RealFrames.OperationalStatus_80073B)]
    [InlineData(RealFrames.OperationalStatus_06A081)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4)]
    [InlineData(RealFrames.OperationalStatus_5082A0)]
    public void ParseMessage_DF17_TC31_OperationalStatus_CapabilityClass_SurfaceFields_AreNull(string hexFrame)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.POA.Should().BeNull("POA is only for surface messages");
        opStatus.CapabilityClass!.B2Low.Should().BeNull("B2Low is only for surface messages");
        opStatus.CapabilityClass!.NACv.Should().BeNull("NACv is only for surface messages");
        opStatus.CapabilityClass!.NICSupplementC.Should().BeNull("NIC Supplement-C is only for surface messages");
    }

    // ========================================
    // Operational Mode Tests
    // ========================================

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_IsNotNull()
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_471DBC)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull("Version 2 airborne messages should have operational mode");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, SdaSupportedFailureCondition.Hazardous)]
    [InlineData(RealFrames.OperationalStatus_71C011, SdaSupportedFailureCondition.Major)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, SdaSupportedFailureCondition.Major)]
    [InlineData(RealFrames.OperationalStatus_4BB027, SdaSupportedFailureCondition.Major)]
    [InlineData(RealFrames.OperationalStatus_80073B, SdaSupportedFailureCondition.Major)]
    [InlineData(RealFrames.OperationalStatus_06A081, SdaSupportedFailureCondition.Major)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, SdaSupportedFailureCondition.Major)]
    [InlineData(RealFrames.OperationalStatus_5082A0, SdaSupportedFailureCondition.Major)]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_Sda(
        string hexFrame,
        SdaSupportedFailureCondition expectedSda)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull();
        opStatus.OperationalMode!.SDA.Should().Be(expectedSda);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.OperationalStatus_71C011, AntennaFlag.SingleAntenna)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.OperationalStatus_4BB027, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.OperationalStatus_80073B, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.OperationalStatus_06A081, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, AntennaFlag.DiversityAntenna)]
    [InlineData(RealFrames.OperationalStatus_5082A0, AntennaFlag.DiversityAntenna)]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_SingleAntenna(
        string hexFrame,
        AntennaFlag expectedSingleAntenna)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull();
        opStatus.OperationalMode!.SingleAntenna.Should().Be(expectedSingleAntenna);
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, false)]
    [InlineData(RealFrames.OperationalStatus_71C011, false)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, false)]
    [InlineData(RealFrames.OperationalStatus_4BB027, false)]
    [InlineData(RealFrames.OperationalStatus_80073B, false)]
    [InlineData(RealFrames.OperationalStatus_06A081, false)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, false)]
    [InlineData(RealFrames.OperationalStatus_5082A0, false)]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_TcasRaActive(
        string hexFrame,
        bool expectedTcasRaActive)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull();
        opStatus.OperationalMode!.TCASRAActive.Should().Be(expectedTcasRaActive, "No TCAS Resolution Advisory active in test frames");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, false)]
    [InlineData(RealFrames.OperationalStatus_71C011, false)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, false)]
    [InlineData(RealFrames.OperationalStatus_4BB027, false)]
    [InlineData(RealFrames.OperationalStatus_80073B, false)]
    [InlineData(RealFrames.OperationalStatus_06A081, false)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, false)]
    [InlineData(RealFrames.OperationalStatus_5082A0, false)]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_IdentSwitchActive(
        string hexFrame,
        bool expectedIdentSwitchActive)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull();
        opStatus.OperationalMode!.IdentSwitchActive.Should().Be(expectedIdentSwitchActive, "IDENT switch not active in test frames");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC, false)]
    [InlineData(RealFrames.OperationalStatus_71C011, false)]
    [InlineData(RealFrames.OperationalStatus_3C55C5, false)]
    [InlineData(RealFrames.OperationalStatus_4BB027, false)]
    [InlineData(RealFrames.OperationalStatus_80073B, false)]
    [InlineData(RealFrames.OperationalStatus_06A081, false)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4, false)]
    [InlineData(RealFrames.OperationalStatus_5082A0, false)]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_AtcServices(
        string hexFrame,
        bool expectedAtcServices)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull();
        opStatus.OperationalMode!.ATCServices.Should().Be(expectedAtcServices, "Not receiving ATC services in test frames");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC)]
    [InlineData(RealFrames.OperationalStatus_71C011)]
    [InlineData(RealFrames.OperationalStatus_3C55C5)]
    [InlineData(RealFrames.OperationalStatus_4BB027)]
    [InlineData(RealFrames.OperationalStatus_80073B)]
    [InlineData(RealFrames.OperationalStatus_06A081)]
    [InlineData(RealFrames.OperationalStatus_4BB0F4)]
    [InlineData(RealFrames.OperationalStatus_5082A0)]
    public void ParseMessage_DF17_TC31_OperationalStatus_OperationalMode_GpsAntennaOffset_IsNull(string hexFrame)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.OperationalMode.Should().NotBeNull();
        opStatus.OperationalMode!.GPSLatOffset.Should().BeNull("Airborne messages do not include GPS antenna offset (surface only)");
        opStatus.OperationalMode!.GPSLongOffset.Should().BeNull("Airborne messages do not include GPS antenna offset (surface only)");
    }

    // ========================================
    // Airborne-Specific Tests
    // ========================================

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC)]
    [InlineData(RealFrames.OperationalStatus_71C011)]
    [InlineData(RealFrames.OperationalStatus_3C55C5)]
    [InlineData(RealFrames.OperationalStatus_4BB027)]
    [InlineData(RealFrames.OperationalStatus_80073B)]
    public void ParseMessage_DF17_TC31_OperationalStatus_Airborne_AircraftLengthWidth_IsNull(string hexFrame)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.AircraftLengthAndWidth.Should().BeNull("Airborne messages do not contain aircraft length/width");
    }

    [Theory]
    [InlineData(RealFrames.OperationalStatus_471DBC)]
    [InlineData(RealFrames.OperationalStatus_71C011)]
    [InlineData(RealFrames.OperationalStatus_3C55C5)]
    [InlineData(RealFrames.OperationalStatus_4BB027)]
    [InlineData(RealFrames.OperationalStatus_80073B)]
    public void ParseMessage_DF17_TC31_OperationalStatus_Airborne_TargetHeading_IsNull(string hexFrame)
    {
        // Arrange
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(hexFrame)
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.TargetHeading.Should().BeNull("Airborne messages use HRD instead of TargetHeading (surface only)");
    }

    // ========================================
    // Specific Scenario Tests
    // ========================================

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_TrueNorth_ParsesCorrectly()
    {
        // Arrange - Frame with true north heading reference
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_471DBC)
            .WithIcaoAddress("471DBC")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.HRD.Should().Be(HorizontalReferenceDirection.TrueNorth, "HRD bit 86 is 0 for true north");
    }

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_MagneticNorth_ParsesCorrectly()
    {
        // Arrange - Frame with magnetic north heading reference
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_80073B)
            .WithIcaoAddress("80073B")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.HRD.Should().Be(HorizontalReferenceDirection.MagneticNorth, "HRD bit 86 is 1 for magnetic north");
    }

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_HighNACp_ParsesCorrectly()
    {
        // Arrange - Frame with NACp=11 (high accuracy < 3m)
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_4BB027)
            .WithIcaoAddress("4BB027")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.NACp.Should().Be(NavigationAccuracyCategoryPosition.LessThan3m, "Frame has NACp=11 for < 3m accuracy");
    }

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_WithARV_ParsesCorrectly()
    {
        // Arrange - Frame from aircraft with ARV (Air-Referenced Velocity) capability
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_471DBC)
            .WithIcaoAddress("471DBC")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.TCASOperational.Should().BeTrue("Aircraft has TCAS operational");
        opStatus.CapabilityClass!.ARVCapability.Should().BeTrue("Aircraft supports Air-Referenced Velocity");
        opStatus.CapabilityClass!.TSCapability.Should().BeTrue("Aircraft supports Target State");
    }

    [Fact]
    public void ParseMessage_DF17_TC31_OperationalStatus_WithoutARV_ParsesCorrectly()
    {
        // Arrange - Frame from aircraft WITHOUT ARV capability
        ValidatedFrame frame = new ValidatedFrameBuilder()
            .WithHexData(RealFrames.OperationalStatus_4BB027)
            .WithIcaoAddress("4BB027")
            .Build();

        // Act
        ModeSMessage? message = _parser.ParseMessage(frame);

        // Assert
        message.Should().NotBeNull();
        OperationalStatus? opStatus = message.Should().BeOfType<OperationalStatus>().Subject;
        opStatus.CapabilityClass.Should().NotBeNull();
        opStatus.CapabilityClass!.TCASOperational.Should().BeTrue("Aircraft has TCAS operational");
        opStatus.CapabilityClass!.ARVCapability.Should().BeFalse("Aircraft does NOT support Air-Referenced Velocity");
        opStatus.CapabilityClass!.TSCapability.Should().BeTrue("Aircraft supports Target State");
        opStatus.NACp.Should().Be(NavigationAccuracyCategoryPosition.LessThan3m, "This aircraft has very high position accuracy");
    }
}
