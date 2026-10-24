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
/// Tests for the status-bit rule in BDS inference.
/// ICAO Doc 9871 requires a field whose status bit is 0 to be all zeros, sign bit included,
/// so a frame that breaks this in a register's layout is not that register.
/// </summary>
public class BdsStatusRuleTests
{
    // No capture holds a real BDS 4,5 or 5,3 reply, so these are made up from realistic
    // values, each built so that every register tried before it rejects it. The parity
    // bytes are zero, which does not matter because the frame builder takes the ICAO
    // address directly.

    /// <summary>
    /// Made-up BDS 4,5 on approach: light turbulence, moderate wind shear, +10 °C, 942 hPa,
    /// radio height 1504 ft; microburst, icing and wake vortex invalid.
    /// </summary>
    private const string MadeUpBds45 = "A8000800B8010A2EBA0BC0000000";

    /// <summary>
    /// Made-up BDS 5,3 climbing: magnetic heading 90°, IAS 250 kt, Mach invalid,
    /// TAS 350 kt, altitude rate +128 ft/min (sign 0, raw 2).
    /// </summary>
    private const string MadeUpBds53Climbing = "A8000800A009F4004AF202000000";

    /// <summary>
    /// Made-up BDS 5,3 descending: as <see cref="MadeUpBds53Climbing"/> with altitude rate
    /// -128 ft/min (sign 1, raw 254 in two's complement).
    /// </summary>
    private const string MadeUpBds53Descending = "A8000800A009F4004AF3FE000000";

    private readonly MessageParser _parser = new();

    // ========================================
    // Real frames that break the rule
    // ========================================

    [Theory]
    [InlineData(BdsFrames.BdsInference_4D2407_Not53, "4D2407")]
    [InlineData(BdsFrames.BdsInference_80073B_Not45, "80073B")]
    [InlineData(BdsFrames.BdsInference_4D2407_Not60, "4D2407")]
    [InlineData(BdsFrames.BdsInference_472FD2_Not40, "472FD2")]
    public void ParseMessage_RealFrameBreakingStatusRule_IsUnknown(string hex, string icao)
    {
        // Act
        CommBIdentityReply reply = ParseIdentityReply(hex, icao);

        // Assert
        reply.BdsCode.Should().Be(BdsCode.Unknown, "an invalid field carries data, so no modeled register fits");
        reply.BdsData.Should().BeNull();
    }

    [Fact]
    public void ParseMessage_DF21_44058F_RejectedAsBds40_DecodesAsBds60()
    {
        // Act
        CommBIdentityReply reply = ParseIdentityReply(BdsFrames.BdsInference_44058F_Is60, "44058F");

        // Assert
        reply.BdsCode.Should().Be(BdsCode.Bds60, "the BDS 4,0 FMS altitude is invalid but carries data");
        Bds60HeadingAndSpeed? bds60 = reply.BdsData.Should().BeOfType<Bds60HeadingAndSpeed>().Subject;
        bds60.MagneticHeading.Should().BeApproximately(239.2, 0.1);
        bds60.IndicatedAirspeed.Should().Be(209);
        bds60.MachNumber.Should().BeApproximately(0.6, 0.001);
        bds60.BarometricVerticalRate.Should().Be(0);
        bds60.InertialVerticalRate.Should().BeNull("the inertial vertical rate is marked invalid");
    }

    // ========================================
    // One invalid field per register
    // ========================================

    /// <summary>
    /// Each base frame decodes as its register, so the rejection cases below start from a
    /// frame the decoder accepts.
    /// </summary>
    [Theory]
    [InlineData(RealFrames.CommB_Altitude_49D421_BDS40, "49D421", BdsCode.Bds40)]
    [InlineData(BdsFrames.Bds44_Meteorological_000169, "000169", BdsCode.Bds44)]
    [InlineData(MadeUpBds45, "000000", BdsCode.Bds45)]
    [InlineData(BdsFrames.BdsInference_4A068C_Is50, "4A068C", BdsCode.Bds50)]
    [InlineData(MadeUpBds53Climbing, "000000", BdsCode.Bds53)]
    [InlineData(BdsFrames.BdsInference_4CA6E3_Is60, "4CA6E3", BdsCode.Bds60)]
    public void ParseMessage_BaseFrame_DecodesAsItsRegister(string hex, string icao, BdsCode expected)
    {
        // Act
        BdsCode actual = ParseBdsCode(hex, icao);

        // Assert
        actual.Should().Be(expected);
    }

    /// <summary>
    /// Setting one data bit in a field whose status bit is 0 rules the register out.
    /// </summary>
    [Theory]
    [InlineData(RealFrames.CommB_Altitude_49D421_BDS40, "49D421", 55, BdsCode.Bds40)]   // altitude source (status 54)
    [InlineData(BdsFrames.Bds44_Meteorological_000169, "000169", 40, BdsCode.Bds44)]    // static pressure (status 35)
    [InlineData(MadeUpBds45, "000000", 8, BdsCode.Bds45)]                               // microburst (status 7)
    [InlineData(BdsFrames.BdsInference_4A068C_Is50, "4A068C", 40, BdsCode.Bds50)]      // track angle rate (status 35)
    [InlineData(MadeUpBds53Climbing, "000000", 30, BdsCode.Bds53)]                      // Mach (status 24)
    [InlineData(BdsFrames.BdsInference_4CA6E3_Is60, "4CA6E3", 50, BdsCode.Bds60)]      // inertial vertical rate (status 46)
    public void ParseMessage_DataInInvalidField_IsNotThatRegister(string hex, string icao, int mbBit, BdsCode register)
    {
        // Act
        BdsCode actual = ParseBdsCode(SetMbBit(hex, mbBit), icao);

        // Assert
        actual.Should().NotBe(register, $"MB bit {mbBit} is set in a field whose status bit is 0");
    }

    /// <summary>
    /// The sign bit belongs to the field (Doc 9871 §A.2.2.2), so a set sign bit alone in an
    /// invalid field also rules the register out.
    /// </summary>
    [Theory]
    [InlineData(BdsFrames.BdsInference_4A068C_Is50, "4A068C", 36, BdsCode.Bds50)]      // track angle rate sign
    [InlineData(BdsFrames.BdsInference_4CA6E3_Is60, "4CA6E3", 47, BdsCode.Bds60)]      // inertial vertical rate sign
    public void ParseMessage_SignBitInInvalidField_IsNotThatRegister(string hex, string icao, int mbBit, BdsCode register)
    {
        // Act
        BdsCode actual = ParseBdsCode(SetMbBit(hex, mbBit), icao);

        // Assert
        actual.Should().NotBe(register, $"sign bit {mbBit} is set in a field whose status bit is 0");
    }

    // ========================================
    // BDS 4,5 and 5,3 decoding
    // ========================================

    [Fact]
    public void ParseMessage_MadeUpBds45_DecodesAllValidFields()
    {
        // Act
        CommBIdentityReply reply = ParseIdentityReply(MadeUpBds45, "000000");

        // Assert
        Bds45MeteorologicalHazard? bds45 = reply.BdsData.Should().BeOfType<Bds45MeteorologicalHazard>().Subject;
        bds45.Turbulence.Should().Be(Severity.Light);
        bds45.WindShear.Should().Be(Severity.Moderate);
        bds45.Microburst.Should().BeNull();
        bds45.Icing.Should().BeNull();
        bds45.WakeVortex.Should().BeNull();
        bds45.StaticAirTemperature.Should().Be(10);
        bds45.Pressure.Should().Be(942);
        bds45.RadioHeight.Should().Be(1504);
    }

    [Fact]
    public void ParseMessage_MadeUpBds53Climbing_DecodesAllValidFields()
    {
        // Act
        CommBIdentityReply reply = ParseIdentityReply(MadeUpBds53Climbing, "000000");

        // Assert
        Bds53AirReferencedState? bds53 = reply.BdsData.Should().BeOfType<Bds53AirReferencedState>().Subject;
        bds53.MagneticHeading.Should().Be(90);
        bds53.IndicatedAirspeed.Should().Be(250);
        bds53.MachNumber.Should().BeNull("the Mach status bit is 0");
        bds53.TrueAirspeed.Should().Be(350);
        bds53.VerticalRate.Should().Be(128);
    }

    [Fact]
    public void ParseMessage_MadeUpBds53Descending_DecodesNegativeAltitudeRateAsTwosComplement()
    {
        // Act
        CommBIdentityReply reply = ParseIdentityReply(MadeUpBds53Descending, "000000");

        // Assert
        reply.BdsCode.Should().Be(BdsCode.Bds53);
        Bds53AirReferencedState? bds53 = reply.BdsData.Should().BeOfType<Bds53AirReferencedState>().Subject;
        bds53.VerticalRate.Should().Be(-128, "sign 1 with raw 254 is -2 in two's complement, × 64 ft/min");
    }

    // ========================================
    // Helpers
    // ========================================

    private CommBIdentityReply ParseIdentityReply(string hex, string icao)
    {
        ModeSMessage? message = _parser.ParseMessage(BuildFrame(hex, icao));
        return message.Should().BeOfType<CommBIdentityReply>().Subject;
    }

    private BdsCode ParseBdsCode(string hex, string icao)
    {
        ModeSMessage? message = _parser.ParseMessage(BuildFrame(hex, icao));
        return message switch
        {
            CommBAltitudeReply altitudeReply => altitudeReply.BdsCode,
            CommBIdentityReply identityReply => identityReply.BdsCode,
            _ => throw new InvalidOperationException($"Not a Comm-B reply: {hex}")
        };
    }

    private static ValidatedFrame BuildFrame(string hex, string icao) =>
        new ValidatedFrameBuilder()
            .WithHexData(hex)
            .WithIcaoAddress(icao)
            .Build();

    /// <summary>
    /// Returns the frame with one MB bit (1-56) set. The MB field starts at frame byte 4.
    /// </summary>
    private static string SetMbBit(string hex, int mbBit)
    {
        byte[] data = Convert.FromHexString(hex);
        data[4 + ((mbBit - 1) / 8)] |= (byte)(0x80 >> ((mbBit - 1) % 8));
        return Convert.ToHexString(data);
    }
}
