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

using System.Globalization;
using Aeromux.CLI.Commands;
using Aeromux.Infrastructure.Streaming;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Aeromux.CLI.Tests.Commands;

/// <summary>
/// Tests for <see cref="SessionSummaryLog"/>: the lines, their order and their alignment,
/// rendered with an English culture as a log file on such a machine shows them.
/// </summary>
public class SessionSummaryLogTests
{
    private const string Separator = SessionSummaryLog.BlockSeparator;

    private static readonly SdrDeviceStatistics Sdr0 = new("sdr0", 0, 0, 10_508, 112, 1_909, 8_495);
    private static readonly SdrDeviceStatistics Sdr1 = new("sdr1", 1, 0, 8_489, 97, 1_555, 6_877);
    private static readonly BeastSourceStatistics Feeder = new("10.1.10.24", 30005, true, DateTime.UnixEpoch, 0, 111_993);

    [Fact]
    public void EverySource_MatchesTheDocumentedLayout()
    {
        var statistics = new ReceiverStatistics(127_365, new SdrStatistics([Sdr0, Sdr1]), [Feeder], new MlatStatistics(0, 0));

        Write(statistics, TimeSpan.FromMinutes(1)).Should().Equal(
            "Session duration        00:01:00",
            "Messages                 127,365   2,122.8 per second on average",
            Separator,
            "SDR, all devices",
            "  Extracted               18,997",
            "  Corrected                  209",
            "  Duplicates               3,464",
            "  Messages                15,372   12.1%",
            Separator,
            "Device 'sdr0' (index 0)",
            "  Extracted               10,508",
            "  Corrected                  112",
            "  Duplicates               1,909",
            "  Messages                 8,495    6.7%",
            Separator,
            "Device 'sdr1' (index 1)",
            "  Extracted                8,489",
            "  Corrected                   97",
            "  Duplicates               1,555",
            "  Messages                 6,877    5.4%",
            Separator,
            "Beast source 10.1.10.24:30005",
            "  Messages               111,993   87.9%",
            "  Reconnects                   0",
            Separator,
            "MLAT",
            "  Messages                     0    0.0%");
    }

    [Fact]
    public void Shares_AreOfAllSourcesTogether()
    {
        // The aggregator's total can trail the sources by the messages in flight; the shares
        // still add up to 100% because they are taken of the sources' own sum
        var statistics = new ReceiverStatistics(990, new SdrStatistics([Sdr0 with { Messages = 250 }]),
            [Feeder with { Messages = 750 }], null);

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(1));

        lines.Should().Contain("  Messages                   250   25.0%");
        lines.Should().Contain("  Messages                   750   75.0%");
    }

    [Fact]
    public void BeastOnly_HasNoSdrBlock()
    {
        var statistics = new ReceiverStatistics(111_993, null, [Feeder], null);

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(30));

        lines.Should().NotContain("SDR, all devices");
        lines.Should().NotContain(l => l.StartsWith("Device ", StringComparison.Ordinal));
        lines.Should().NotContain("MLAT");
        lines.Should().ContainInOrder("Beast source 10.1.10.24:30005", "  Messages               111,993  100.0%");
    }

    [Fact]
    public void AircraftTracked_FollowsTheMessages()
    {
        var statistics = new ReceiverStatistics(100, null, [Feeder], null);

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(1), aircraftTracked: 87);

        lines[1].Should().StartWith("Messages ");
        lines[2].Should().Be("Aircraft tracked              87");
    }

    [Fact]
    public void NothingDelivered_HasNoShares()
    {
        var silent = new BeastSourceStatistics("feeder", 30005, false, null, 0, 0);
        var statistics = new ReceiverStatistics(0, null, [silent], new MlatStatistics(0, 0));

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(1));

        lines.Should().Contain("  Messages                     0");
        lines.Should().NotContain(l => l.Contains('%', StringComparison.Ordinal));
    }

    [Fact]
    public void NoStatistics_SaysTheReceiverDidNotStart()
    {
        Write(null, TimeSpan.FromSeconds(3)).Should().Equal(
            "Session duration        00:00:03",
            "No statistics: the receiver did not start");
    }

    [Fact]
    public void Average_HasAThousandsSeparator()
    {
        Write(new ReceiverStatistics(7_061_400, null, [], null), TimeSpan.FromHours(1))[1]
            .Should().Be("Messages               7,061,400   1,961.5 per second on average");
    }

    [Fact]
    public void ZeroDuration_HasAZeroAverage()
    {
        Write(new ReceiverStatistics(5, null, [], null), TimeSpan.Zero)[1]
            .Should().Be("Messages                       5   0.0 per second on average");
    }

    [Theory]
    [InlineData(0, 0, 0, "00:00:00")]
    [InlineData(0, 59, 59, "00:59:59")]
    [InlineData(25, 0, 0, "25:00:00")]
    [InlineData(100, 5, 7, "100:05:07")]
    public void Duration_CountsHoursInFull(int hours, int minutes, int seconds, string expected)
    {
        SessionSummaryLog.FormatDuration(new TimeSpan(hours, minutes, seconds)).Should().Be(expected);
    }

    [Fact]
    public void Values_AreStructuredProperties()
    {
        var sink = new CapturingSink();
        using Logger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var statistics = new ReceiverStatistics(127_365, new SdrStatistics([Sdr0]), [Feeder], null);

        SessionSummaryLog.Write(logger, statistics, TimeSpan.FromHours(1));

        LogEvent total = sink.Events.First(e => e.MessageTemplate.Text.StartsWith("Messages", StringComparison.Ordinal));
        total.Properties["Messages"].Should().Be(new ScalarValue(127_365L));
        LogEvent device = sink.Events.Single(e => e.MessageTemplate.Text.StartsWith("Device", StringComparison.Ordinal));
        device.Properties["DeviceName"].Should().Be(new ScalarValue("sdr0"));
    }

    // === Helpers ===

    private static List<string> Write(ReceiverStatistics? statistics, TimeSpan duration, int? aircraftTracked = null)
    {
        var sink = new CapturingSink();
        using Logger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        SessionSummaryLog.Write(logger, statistics, duration, aircraftTracked);

        CultureInfo english = CultureInfo.GetCultureInfo("en-US");
        return [.. sink.Events.Select(e => e.RenderMessage(english))];
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
