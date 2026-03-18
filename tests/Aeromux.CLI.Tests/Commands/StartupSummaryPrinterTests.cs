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

using System.Net;
using Aeromux.CLI.Commands;
using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Commands.Live;
using Aeromux.Core.Configuration;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands;

public class StartupSummaryPrinterTests
{
    // ─── Daemon Summary ───

    [Fact]
    public void PrintDaemonSummary_SdrOnly_PrintsSdrSources()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(sdr: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().Contain("Source(s):");
        output.Should().Contain("SDR    test-sdr");
        // Sources section should not contain Beast (outputs section may)
        string sourcesSection = output[..output.IndexOf("Output(s):", StringComparison.Ordinal)];
        sourcesSection.Should().NotContain("Beast");
    }

    [Fact]
    public void PrintDaemonSummary_BeastOnly_PrintsBeastSources()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(beast: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().Contain("Beast  piaware:30005");
        // Sources section should not contain SDR
        string sourcesSection = output[..output.IndexOf("Output(s):", StringComparison.Ordinal)];
        sourcesSection.Should().NotContain("SDR");
    }

    [Fact]
    public void PrintDaemonSummary_SdrAndBeast_PrintsBothSources()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(sdr: true, beast: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().Contain("SDR    test-sdr");
        output.Should().Contain("Beast  piaware:30005");
    }

    [Fact]
    public void PrintDaemonSummary_MlatEnabled_PrintsMlatSource()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(sdr: true, mlat: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().Contain("MLAT   port 30104");
    }

    [Fact]
    public void PrintDaemonSummary_MlatDisabled_NoMlatLine()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(sdr: true, mlat: false);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().NotContain("MLAT");
    }

    [Fact]
    public void PrintDaemonSummary_OutputsSection_ShowsEnabledOnly()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(sdr: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().Contain("Output(s):");
        output.Should().Contain("Beast  port 30005");
        output.Should().Contain("API    port 8080");
        // JSON and SBS are disabled by default
        output.Should().NotContain("JSON");
        output.Should().NotContain("SBS");
    }

    [Fact]
    public void PrintDaemonSummary_AllOutputsEnabled_ShowsAll()
    {
        DaemonValidatedConfig config = CreateDaemonConfig(sdr: true, allOutputs: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintDaemonSummary(config));

        output.Should().Contain("Beast  port 30005");
        output.Should().Contain("JSON   port 30006");
        output.Should().Contain("SBS    port 30003");
        output.Should().Contain("API    port 8080");
    }

    [Fact]
    public void PrintDaemonSummary_NullConfig_Throws()
    {
        Action act = () => StartupSummaryPrinter.PrintDaemonSummary(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Live Summary ───

    [Fact]
    public void PrintLiveSummary_SdrOnly_PrintsSdrSources()
    {
        LiveValidatedConfig config = CreateLiveConfig(sdr: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintLiveSummary(config));

        output.Should().Contain("Source(s):");
        output.Should().Contain("SDR    test-sdr");
        output.Should().NotContain("Output(s):");
    }

    [Fact]
    public void PrintLiveSummary_BeastOnly_PrintsBeastSources()
    {
        LiveValidatedConfig config = CreateLiveConfig(beast: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintLiveSummary(config));

        output.Should().Contain("Beast  piaware:30005");
        output.Should().NotContain("Output(s):");
    }

    [Fact]
    public void PrintLiveSummary_SdrAndBeast_PrintsBothSources()
    {
        LiveValidatedConfig config = CreateLiveConfig(sdr: true, beast: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintLiveSummary(config));

        output.Should().Contain("SDR    test-sdr");
        output.Should().Contain("Beast  piaware:30005");
    }

    [Fact]
    public void PrintLiveSummary_NoOutputSection()
    {
        // Live summary should never include Output(s) section
        LiveValidatedConfig config = CreateLiveConfig(sdr: true, beast: true);
        string output = CaptureConsole(() => StartupSummaryPrinter.PrintLiveSummary(config));

        output.Should().NotContain("Output(s):");
    }

    [Fact]
    public void PrintLiveSummary_NullConfig_Throws()
    {
        Action act = () => StartupSummaryPrinter.PrintLiveSummary(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ───

    private static string CaptureConsole(Action action)
    {
        using var writer = new StringWriter();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static DaemonValidatedConfig CreateDaemonConfig(
        bool sdr = false, bool beast = false, bool mlat = false, bool allOutputs = false)
    {
        return new DaemonValidatedConfig
        {
            Config = new AeromuxConfig { Tracking = new TrackingConfig(), Network = new NetworkConfig() },
            EnabledSdrSources = sdr
                ? [new SdrSourceConfig { Name = "test-sdr", DeviceIndex = 0, Enabled = true }]
                : [],
            BeastSources = beast
                ? [new BeastSourceConfig { Host = "piaware", Port = 30005 }]
                : [],
            BeastOutputPort = 30005,
            JsonOutputPort = 30006,
            SbsOutputPort = 30003,
            ApiPort = 8080,
            ApiEnabled = true,
            BindAddress = IPAddress.Any,
            ReceiverUuid = null,
            MlatConfig = new MlatConfig { Enabled = mlat, InputPort = 30104 },
            BeastEnabled = true,
            JsonEnabled = allOutputs,
            SbsEnabled = allOutputs
        };
    }

    private static LiveValidatedConfig CreateLiveConfig(bool sdr = false, bool beast = false)
    {
        return new LiveValidatedConfig
        {
            Config = new AeromuxConfig { Tracking = new TrackingConfig(), Network = new NetworkConfig() },
            EnabledSdrSources = sdr
                ? [new SdrSourceConfig { Name = "test-sdr", DeviceIndex = 0, Enabled = true }]
                : [],
            BeastSources = beast
                ? [new BeastSourceConfig { Host = "piaware", Port = 30005 }]
                : [],
            Receiver = null,
            Tracking = new TrackingConfig()
        };
    }
}
