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
/// Tests for DF 18 (non-transponder extended squitter, TIS-B, ADS-R) and DF 19 decoding.
/// </summary>
/// <remarks>
/// DF 18's control field (CF, frame bits 6-8) says what the frame carries. CF 0 is ADS-B
/// from a non-transponder device. CF 2 (fine TIS-B) and CF 6 (ADS-R) carry an ICAO address
/// only when their IMF bit is 0; its position depends on the type code. CF 1 and CF 5 carry
/// a non-ICAO address, CF 3 is coarse TIS-B with a different layout, and CF 4 and CF 7 have
/// no format to decode. Only ICAO-addressed frames in the ADS-B layout are decoded.
///
/// No capture holds a DF 18 or DF 19 frame, so these are made up from real DF 17 frames:
/// the DF, CF and IMF bits are changed and the parity recomputed.
/// </remarks>
public class Df18ControlFieldTests
{
    // Made up from AirbornePos_80073B_Even (TC 11): DF 18, CF and IMF (frame bit 40) set
    private const string PositionCf0 = "9080073B58CD7331C27497D5A9E8";
    private const string PositionCf1 = "9180073B58CD7331C274978DD890";
    private const string PositionCf2Imf0 = "9280073B58CD7331C27497654B18";
    private const string PositionCf2Imf1 = "9280073B59CD7331C27497B931EF";
    private const string PositionCf3Imf0 = "9380073B58CD7331C274973D3A60";
    private const string PositionCf4 = "9480073B58CD7331C274974B9801";
    private const string PositionCf5 = "9580073B58CD7331C2749713E979";
    private const string PositionCf6Imf0 = "9680073B58CD7331C27497FB7AF1";
    private const string PositionCf6Imf1 = "9680073B59CD7331C27497270006";
    private const string PositionCf7 = "9780073B58CD7331C27497A30B89";

    // Made up from AirborneVel_39CEAD_BaroRate (TC 19): IMF is frame bit 41
    private const string VelocityCf2Imf0 = "9239CEAD990D9004F00890047399";
    private const string VelocityCf2Imf1 = "9239CEAD998D9004F0089095B4E6";

    // Made up from SurfacePos_3461CF_Mov24 (TC 7): IMF is frame bit 53
    private const string SurfaceCf6Imf0 = "963461CF398D60597B4EA43F6A43";
    private const string SurfaceCf6Imf1 = "963461CF398D68597B4EA4273C33";

    // Made up from TargetStateStatus_4BB183_Lnav (TC 29): IMF is frame bit 83
    private const string TargetStateCf2Imf0 = "924BB183EA48585D74138C697AD2";
    private const string TargetStateCf2Imf1 = "924BB183EA48585D7413AC68BB62";

    // Made up from EmergencyStatus_80073B (TC 28 subtype 1): IMF is frame bit 88
    private const string StatusCf6Imf0 = "9680073BE10A1900000000EE2097";
    private const string StatusCf6Imf1 = "9680073BE10A190000000111D49E";

    // Made up from OperationalStatus_80073B (TC 31): IMF is frame bit 88
    private const string OperationalCf2Imf0 = "9280073BF82100020049BC1D0FF5";
    private const string OperationalCf2Imf1 = "9280073BF82100020049BDE2FBFC";

    // Made up from AircraftId_8964A0 (TC 4): identification has no IMF bit
    private const string IdentificationCf2 = "928964A025541171E32820C2A9F9";

    // Made up from AirbornePos_80073B_Even: DF set to 19 (AF 0)
    private const string Df19Position = "9D80073B58CD7331C27497D07EA2";

    private readonly MessageParser _parser = new();

    [Theory]
    [InlineData(PositionCf0, typeof(AirbornePosition))]
    [InlineData(PositionCf2Imf0, typeof(AirbornePosition))]
    [InlineData(PositionCf6Imf0, typeof(AirbornePosition))]
    [InlineData(VelocityCf2Imf0, typeof(AirborneVelocity))]
    [InlineData(SurfaceCf6Imf0, typeof(SurfacePosition))]
    [InlineData(TargetStateCf2Imf0, typeof(TargetStateAndStatus))]
    [InlineData(StatusCf6Imf0, typeof(AircraftStatus))]
    [InlineData(OperationalCf2Imf0, typeof(OperationalStatus))]
    [InlineData(IdentificationCf2, typeof(AircraftIdentification))]
    public void ParseMessage_Df18WithIcaoAddressInAdsbLayout_IsDecoded(string hex, Type expectedType)
    {
        // Act
        ModeSMessage? message = _parser.ParseMessage(BuildFrame(hex));

        // Assert
        message.Should().NotBeNull();
        message.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData(PositionCf1, "CF 1: anonymous or non-ICAO address")]
    [InlineData(PositionCf2Imf1, "CF 2 with IMF 1: Mode A code and track number, not an ICAO address")]
    [InlineData(PositionCf3Imf0, "CF 3: coarse TIS-B, a different layout")]
    [InlineData(PositionCf4, "CF 4: TIS-B and ADS-R management")]
    [InlineData(PositionCf5, "CF 5: fine TIS-B with a non-ICAO address")]
    [InlineData(PositionCf6Imf1, "CF 6 with IMF 1: non-ICAO address")]
    [InlineData(PositionCf7, "CF 7: reserved")]
    [InlineData(VelocityCf2Imf1, "velocity, IMF 1")]
    [InlineData(SurfaceCf6Imf1, "surface position, IMF 1")]
    [InlineData(TargetStateCf2Imf1, "target state, IMF 1")]
    [InlineData(StatusCf6Imf1, "emergency status, IMF 1")]
    [InlineData(OperationalCf2Imf1, "operational status, IMF 1")]
    public void ParseMessage_Df18WithoutIcaoAddressOrAdsbLayout_IsNotDecoded(string hex, string reason)
    {
        // Act
        ModeSMessage? message = _parser.ParseMessage(BuildFrame(hex));

        // Assert
        message.Should().BeNull(because: reason);
    }

    [Fact]
    public void ParseMessage_Df19_IsNotDecoded()
    {
        // Act
        ModeSMessage? message = _parser.ParseMessage(BuildFrame(Df19Position));

        // Assert
        message.Should().BeNull(because: "the DF 19 body is military-specific and is not decoded");
    }

    // Made up from AirbornePos_80073B_Even: coarse TIS-B (CF 3) with its IMF (frame bit 33) set
    private const string PositionCf3Imf1 = "9380073BD8CD7331C27497025771";

    [Theory]
    [InlineData(RealFrames.AirbornePos_80073B_Even, true)]
    [InlineData(PositionCf0, true)]
    [InlineData(PositionCf1, false)]
    [InlineData(PositionCf2Imf0, true)]
    [InlineData(PositionCf2Imf1, false)]
    [InlineData(PositionCf3Imf0, true)]
    [InlineData(PositionCf3Imf1, false)]
    [InlineData(PositionCf4, false)]
    [InlineData(PositionCf5, false)]
    [InlineData(PositionCf6Imf0, true)]
    [InlineData(PositionCf6Imf1, false)]
    [InlineData(PositionCf7, false)]
    [InlineData(VelocityCf2Imf0, true)]
    [InlineData(VelocityCf2Imf1, false)]
    [InlineData(SurfaceCf6Imf0, true)]
    [InlineData(SurfaceCf6Imf1, false)]
    [InlineData(TargetStateCf2Imf0, true)]
    [InlineData(TargetStateCf2Imf1, false)]
    [InlineData(StatusCf6Imf0, true)]
    [InlineData(StatusCf6Imf1, false)]
    [InlineData(OperationalCf2Imf0, true)]
    [InlineData(OperationalCf2Imf1, false)]
    [InlineData(IdentificationCf2, true)]
    [InlineData(Df19Position, false)]
    public void HasIcaoAddress_FollowsControlFieldAndImf(string hex, bool expected)
    {
        // Act
        bool hasIcaoAddress = IcaoAddressClassifier.HasIcaoAddress(Convert.FromHexString(hex));

        // Assert
        hasIcaoAddress.Should().Be(expected);
    }

    [Theory]
    [InlineData(PositionCf2Imf0, true)]
    [InlineData(PositionCf2Imf1, false)]
    [InlineData(Df19Position, false)]
    public void ValidateFrame_SetsHasIcaoAddress(string hex, bool expected)
    {
        // Arrange
        var factory = new ValidatedFrameFactory();

        // Act
        ValidatedFrame? frame = factory.ValidateFrame(new RawFrame(Convert.FromHexString(hex), DateTime.UtcNow, 0, 0.0), 0.0);

        // Assert
        frame.Should().NotBeNull();
        frame!.HasIcaoAddress.Should().Be(expected);
    }

    // The decode decision depends on DF, CF and IMF only, so the builder's default address is enough
    private static ValidatedFrame BuildFrame(string hex) =>
        new ValidatedFrameBuilder()
            .WithHexData(hex)
            .Build();
}
