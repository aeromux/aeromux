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

using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tests.Tracking;

namespace Aeromux.Core.Tests.MessageParsing;

/// <summary>
/// Tests for <see cref="MessageParser.GetMessageTypeCounts"/>: messages counted by
/// downlink format, type code and Comm-B register. Frames go through the real
/// <see cref="ValidatedFrameFactory"/>, as in production.
/// </summary>
public class MessageTypeCountsTests
{
    // Made up from AirbornePos_80073B_Even: DF 18 with CF 1 (non-ICAO address), parity recomputed
    private const string Df18Cf1Position = "9180073B58CD7331C274978DD890";

    private readonly ValidatedFrameFactory _factory = new();

    [Fact]
    public void NothingParsed_IsEmpty()
    {
        MessageTypeCounts counts = new MessageParser().GetMessageTypeCounts();

        counts.DownlinkFormats.Should().BeEmpty();
        counts.TypeCodes.Should().BeEmpty();
        counts.BdsCodes.Should().BeEmpty();
    }

    [Fact]
    public void EachKind_IsCounted()
    {
        var parser = new MessageParser();

        Parse(parser, RealFrames.AirbornePos_80073B_Even);   // DF 17, TC 11
        Parse(parser, RealFrames.AirbornePos_80073B_Odd);    // DF 17, TC 11
        Parse(parser, RealFrames.AircraftId_471DBC);         // DF 17, TC 4
        Parse(parser, RealFrames.AllCall_471F87);            // DF 11
        Parse(parser, RealFrames.CommB_Altitude_4D2407);     // DF 20, one Comm-B register

        MessageTypeCounts counts = parser.GetMessageTypeCounts();

        counts.DownlinkFormats.Should().Equal(new Dictionary<int, long> { [17] = 3, [11] = 1, [20] = 1 });
        counts.TypeCodes.Should().Equal(new Dictionary<int, long> { [11] = 2, [4] = 1 });
        counts.BdsCodes.Values.Sum().Should().Be(1, "every DF 20 and 21 reply gets a register, Unknown included");
    }

    [Fact]
    public void DownlinkFormats_AddUpToTheMessagesParsed()
    {
        var parser = new MessageParser();
        foreach (string frame in new[]
                 {
                     RealFrames.AirbornePos_80073B_Even, RealFrames.AllCall_471F87,
                     RealFrames.CommB_Altitude_4D2407, Df18Cf1Position
                 })
        {
            Parse(parser, frame);
        }

        parser.GetMessageTypeCounts().DownlinkFormats.Values.Sum().Should().Be(parser.MessagesParsed);
    }

    [Fact]
    public void Df18WithoutIcaoAddress_HasADownlinkFormatButNoTypeCode()
    {
        var parser = new MessageParser();

        Parse(parser, Df18Cf1Position);

        MessageTypeCounts counts = parser.GetMessageTypeCounts();
        counts.DownlinkFormats.Should().Equal(new Dictionary<int, long> { [18] = 1 });
        counts.TypeCodes.Should().BeEmpty("only ADS-B with an ICAO address is decoded by type code");
    }

    [Fact]
    public async Task ParsingFromSeveralThreads_CountsExactly()
    {
        // The MLAT input parses the frames of every connected mlat-client through one parser
        const int threads = 8;
        const int framesPerThread = 2_000;
        var parser = new MessageParser();
        ValidatedFrame frame = Validate(RealFrames.AircraftId_471DBC);

        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            [.. Enumerable.Range(0, threads).Select(_ => (Action)(() =>
            {
                for (int i = 0; i < framesPerThread; i++)
                {
                    parser.ParseMessage(frame);
                }
            }))]);

        errors.Should().BeEmpty();
        MessageTypeCounts counts = parser.GetMessageTypeCounts();
        counts.DownlinkFormats[17].Should().Be(threads * framesPerThread);
        counts.TypeCodes[4].Should().Be(threads * framesPerThread);
    }

    private void Parse(MessageParser parser, string hex) => parser.ParseMessage(Validate(hex));

    private ValidatedFrame Validate(string hex) =>
        _factory.ValidateFrame(new RawFrame(Convert.FromHexString(hex), DateTime.UtcNow, 0, 0), 0)
        ?? throw new InvalidOperationException($"Test frame {hex} does not pass the CRC check");
}
