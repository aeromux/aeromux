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

using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Commands.Live;
using FluentAssertions;
using Serilog.Events;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Tests.Commands.Daemon;

/// <summary>
/// Parses command lines with Spectre.Console.Cli into <see cref="DaemonSettings"/> and
/// <see cref="LiveSettings"/>, so the option names and syntax that docs/CLI.md documents are
/// pinned. The validator tests construct settings directly and cannot catch a renamed option
/// or a flag that stops accepting an explicit value.
/// </summary>
public class DaemonSettingsParsingTests
{
    /// <summary>
    /// Records the parsed settings instead of running anything. The application's
    /// configuration interceptor is not registered, so no configuration file is read. Public
    /// because Spectre creates commands by reflection.
    /// </summary>
    public sealed class CaptureDaemon : Command<DaemonSettings>
    {
        public static DaemonSettings? Last { get; set; }

        public override int Execute(CommandContext context, DaemonSettings settings, CancellationToken cancellationToken)
        {
            Last = settings;
            return 0;
        }
    }

    public sealed class CaptureLive : Command<LiveSettings>
    {
        public static LiveSettings? Last { get; set; }

        public override int Execute(CommandContext context, LiveSettings settings, CancellationToken cancellationToken)
        {
            Last = settings;
            return 0;
        }
    }

    private static readonly Lock ParseLock = new();

    internal static DaemonSettings ParseDaemon(params string[] args)
    {
        // The capture is static, so parses run one at a time.
        lock (ParseLock)
        {
            var app = new CommandApp();
            app.Configure(c =>
            {
                c.PropagateExceptions();
                c.AddCommand<CaptureDaemon>("daemon");
            });
            CaptureDaemon.Last = null;
            app.Run(["daemon", .. args]).Should().Be(0);
            return CaptureDaemon.Last!;
        }
    }

    private static LiveSettings ParseLive(params string[] args)
    {
        lock (ParseLock)
        {
            var app = new CommandApp();
            app.Configure(c =>
            {
                c.PropagateExceptions();
                c.AddCommand<CaptureLive>("live");
            });
            CaptureLive.Last = null;
            app.Run(["live", .. args]).Should().Be(0);
            return CaptureLive.Last!;
        }
    }

    // === Nothing given ===

    [Fact]
    public void NoOptions_LeaveEveryOverrideUnset()
    {
        DaemonSettings settings = ParseDaemon();

        settings.ConfigPath.Should().BeNull();
        settings.LogLevel.Should().BeNull();
        settings.DatabasePath.Should().BeNull();
        settings.SdrSource.Should().BeFalse();
        settings.BeastSource.Should().BeNull();
        settings.BeastOutputPort.Should().BeNull();
        settings.JsonOutputPort.Should().BeNull();
        settings.SbsOutputPort.Should().BeNull();
        settings.BeastOutputEnabled.Should().BeNull();
        settings.JsonOutputEnabled.Should().BeNull();
        settings.SbsOutputEnabled.Should().BeNull();
        settings.BindAddress.Should().BeNull();
        settings.ApiPort.Should().BeNull();
        settings.ApiEnabled.Should().BeNull();
        settings.ApiCorsOrigin.Should().BeNull();
        settings.MlatEnabled.Should().BeNull();
        settings.MlatInputPort.Should().BeNull();
        settings.ReceiverUuid.Should().BeNull();
    }

    // === Value options, by their documented names ===

    [Fact]
    public void GlobalOptions_Parse()
    {
        DaemonSettings settings = ParseDaemon("--config", "/etc/aeromux/aeromux.yaml", "--log-level", "Debug", "--database", "/var/lib/aeromux");

        settings.ConfigPath.Should().Be("/etc/aeromux/aeromux.yaml");
        settings.LogLevel.Should().Be(LogEventLevel.Debug);
        settings.DatabasePath.Should().Be("/var/lib/aeromux");
    }

    [Theory]
    [InlineData("debug")]
    [InlineData("DEBUG")]
    [InlineData("Debug")]
    public void LogLevel_IsCaseInsensitive(string value)
    {
        ParseDaemon("--log-level", value).LogLevel.Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public void PortOptions_Parse()
    {
        DaemonSettings settings = ParseDaemon(
            "--beast-output-port", "31005", "--json-output-port", "31006", "--sbs-output-port", "31003",
            "--api-port", "9000", "--mlat-input-port", "31104");

        settings.BeastOutputPort.Should().Be(31005);
        settings.JsonOutputPort.Should().Be(31006);
        settings.SbsOutputPort.Should().Be(31003);
        settings.ApiPort.Should().Be(9000);
        settings.MlatInputPort.Should().Be(31104);
    }

    [Fact]
    public void StringOptions_Parse()
    {
        DaemonSettings settings = ParseDaemon(
            "--bind-address", "127.0.0.1", "--receiver-uuid", "550e8400-e29b-41d4-a716-446655440000");

        settings.BindAddress.Should().Be("127.0.0.1");
        settings.ReceiverUuid.Should().Be("550e8400-e29b-41d4-a716-446655440000");
    }

    [Fact]
    public void NonNumericPort_FailsToParse()
    {
        Action act = () => ParseDaemon("--api-port", "abc");

        act.Should().Throw<Exception>();
    }

    // === Repeatable options ===

    [Fact]
    public void BeastSource_Repeated_KeepsEveryValueInOrder()
    {
        ParseDaemon("--beast-source", "a:30005", "--beast-source", "b:30005", "--beast-source", "c:30005")
            .BeastSource.Should().Equal("a:30005", "b:30005", "c:30005");
    }

    [Fact]
    public void ApiCorsOrigin_Repeated_KeepsEveryValueInOrder()
    {
        ParseDaemon("--api-cors-origin", "http://a.local:3000", "--api-cors-origin", "https://b.local")
            .ApiCorsOrigin.Should().Equal("http://a.local:3000", "https://b.local");
    }

    // === Boolean options: --x, --x true|false, --x=true|false ===

    private static bool? ReadFlag(DaemonSettings settings, string option) => option switch
    {
        "--api-enabled" => settings.ApiEnabled,
        "--beast-output-enabled" => settings.BeastOutputEnabled,
        "--json-output-enabled" => settings.JsonOutputEnabled,
        "--sbs-output-enabled" => settings.SbsOutputEnabled,
        "--mlat-enabled" => settings.MlatEnabled,
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
    };

    [Theory]
    [InlineData("--api-enabled")]
    [InlineData("--beast-output-enabled")]
    [InlineData("--json-output-enabled")]
    [InlineData("--sbs-output-enabled")]
    [InlineData("--mlat-enabled")]
    public void BooleanOption_Bare_IsTrue(string option)
    {
        ReadFlag(ParseDaemon(option), option).Should().BeTrue();
    }

    [Theory]
    [InlineData("--api-enabled")]
    [InlineData("--beast-output-enabled")]
    [InlineData("--json-output-enabled")]
    [InlineData("--sbs-output-enabled")]
    [InlineData("--mlat-enabled")]
    public void BooleanOption_WithSeparateValue_TakesTheValue(string option)
    {
        ReadFlag(ParseDaemon(option, "false"), option).Should().BeFalse();
        ReadFlag(ParseDaemon(option, "true"), option).Should().BeTrue();
    }

    [Theory]
    [InlineData("--api-enabled")]
    [InlineData("--beast-output-enabled")]
    [InlineData("--json-output-enabled")]
    [InlineData("--sbs-output-enabled")]
    [InlineData("--mlat-enabled")]
    public void BooleanOption_WithEqualsValue_TakesTheValue(string option)
    {
        ReadFlag(ParseDaemon($"{option}=false"), option).Should().BeFalse();
        ReadFlag(ParseDaemon($"{option}=true"), option).Should().BeTrue();
    }

    [Fact]
    public void BooleanOption_FollowedByAnotherOption_DoesNotConsumeIt()
    {
        DaemonSettings settings = ParseDaemon("--api-enabled", "--api-port", "9000");

        settings.ApiEnabled.Should().BeTrue();
        settings.ApiPort.Should().Be(9000);
    }

    [Fact]
    public void SdrSource_IsAFlag()
    {
        ParseDaemon("--sdr-source").SdrSource.Should().BeTrue();
    }

    // === Live command ===

    [Fact]
    public void Live_SourceAndGlobalOptions_Parse()
    {
        LiveSettings settings = ParseLive(
            "--sdr-source", "--beast-source", "a:30005", "--beast-source", "b:30005",
            "--config", "aeromux.yaml", "--log-level", "warning", "--database", "db");

        settings.SdrSource.Should().BeTrue();
        settings.BeastSource.Should().Equal("a:30005", "b:30005");
        settings.ConfigPath.Should().Be("aeromux.yaml");
        settings.LogLevel.Should().Be(LogEventLevel.Warning);
        settings.DatabasePath.Should().Be("db");
    }

    [Fact]
    public void Live_NoOptions_LeaveEveryOverrideUnset()
    {
        LiveSettings settings = ParseLive();

        settings.SdrSource.Should().BeFalse();
        settings.BeastSource.Should().BeNull();
        settings.ConfigPath.Should().BeNull();
    }
}
