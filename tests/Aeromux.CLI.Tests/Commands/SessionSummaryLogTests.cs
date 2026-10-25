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

    private static readonly SdrDeviceStatistics DefaultDevice =
        new("default", 0, DateTime.UnixEpoch, 0, 1_250_000, 790_000, 12_000, 300, 398_700);

    private static readonly BeastSourceStatistics Feeder =
        new("192.168.1.20", 30005, true, DateTime.UnixEpoch, 2, 12_000, 900, 11_100);

    [Fact]
    public void EverySource_MatchesTheDocumentedLayout()
    {
        var statistics = new ReceiverStatistics(
            412_400,
            new SdrStatistics(61_000, 399_000, [DefaultDevice]),
            [Feeder],
            new MlatStatistics(1, 2_600));

        Write(statistics, TimeSpan.FromHours(1)).Should().Equal(
            "Session duration        01:00:00",
            "Frames delivered         412,400   114.6 per second on average",
            Separator,
            "SDR, all devices",
            "  Extracted            1,250,000",
            "  CRC errors             790,000   63.2%",
            "  Corrected               12,000",
            "  Unconfirmed             61,000    4.9%",
            "  Accepted               399,000   31.9%",
            Separator,
            "Device 'default' (index 0)",
            "  Extracted            1,250,000",
            "  CRC errors             790,000   63.2%",
            "  Corrected               12,000",
            "  Duplicates                 300",
            "  Messages               398,700",
            Separator,
            "Beast source 192.168.1.20:30005",
            "  Received                12,000",
            "  Unconfirmed                900    7.5%",
            "  Accepted                11,100   92.5%",
            "  Reconnects                   2",
            Separator,
            "MLAT",
            "  Received                 2,600");
    }

    [Fact]
    public void SdrTotals_AreSummedOverDevices()
    {
        var statistics = new ReceiverStatistics(0, new SdrStatistics(0, 0,
        [
            DefaultDevice,
            new SdrDeviceStatistics("attic", 1, DateTime.UnixEpoch, 0, 750_000, 210_000, 3_000, 0, 0)
        ]), [], null);

        List<string> lines = Write(statistics, TimeSpan.FromHours(1));

        lines.Should().ContainInOrder(
            "SDR, all devices",
            "  Extracted            2,000,000",
            "  CRC errors           1,000,000   50.0%",
            "  Corrected               15,000");
        lines.Should().Contain("Device 'attic' (index 1)");
    }

    [Fact]
    public void BeastOnly_HasNoSdrBlock()
    {
        var statistics = new ReceiverStatistics(11_100, null, [Feeder], null);

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(30));

        lines.Should().NotContain("SDR, all devices");
        lines.Should().NotContain(l => l.StartsWith("Device ", StringComparison.Ordinal));
        lines.Should().NotContain("MLAT");
        lines.Should().Contain("Beast source 192.168.1.20:30005");
    }

    [Fact]
    public void AircraftTracked_FollowsTheFrames()
    {
        var statistics = new ReceiverStatistics(100, null, [Feeder], null);

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(1), aircraftTracked: 87);

        lines[1].Should().StartWith("Frames delivered");
        lines[2].Should().Be("Aircraft tracked              87");
    }

    [Fact]
    public void NothingReceived_HasNoShares()
    {
        var silent = new BeastSourceStatistics("feeder", 30005, false, null, 0, 0, 0, 0);
        var statistics = new ReceiverStatistics(0, null, [silent], null);

        List<string> lines = Write(statistics, TimeSpan.FromMinutes(1));

        lines.Should().Contain("  Unconfirmed                  0");
        lines.Should().Contain("  Accepted                     0");
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
            .Should().Be("Frames delivered       7,061,400   1,961.5 per second on average");
    }

    [Fact]
    public void ZeroDuration_HasAZeroAverage()
    {
        Write(new ReceiverStatistics(5, null, [], null), TimeSpan.Zero)[1]
            .Should().Be("Frames delivered               5   0.0 per second on average");
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
        var statistics = new ReceiverStatistics(412_400, new SdrStatistics(61_000, 399_000, [DefaultDevice]), [Feeder], null);

        SessionSummaryLog.Write(logger, statistics, TimeSpan.FromHours(1));

        LogEvent frames = sink.Events.Single(e => e.MessageTemplate.Text.StartsWith("Frames delivered", StringComparison.Ordinal));
        frames.Properties["FramesDelivered"].Should().Be(new ScalarValue(412_400L));
        LogEvent device = sink.Events.Single(e => e.MessageTemplate.Text.StartsWith("Device", StringComparison.Ordinal));
        device.Properties["DeviceName"].Should().Be(new ScalarValue("default"));
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
