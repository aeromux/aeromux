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
using System.Text.Json.Nodes;
using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.CLI.Tests.Commands.Daemon;
using Aeromux.Core.Configuration;
using Aeromux.Core.Configuration.Enums;
using Aeromux.Infrastructure.Configuration;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Configuration;

/// <summary>
/// Runs the configuration chain the daemon runs: arguments parsed by Spectre.Console.Cli, a
/// real YAML file passed with <c>--config</c>, <see cref="ConfigurationBuilder"/> with the real
/// <see cref="YamlConfigurationLoader"/>, then <see cref="DaemonConfigValidator"/>. Every rule
/// of "CLI > YAML > Default" is checked here on the resolved value and on the source the
/// origin reports, because a rule can hold in each piece and still break where they meet.
/// </summary>
/// <remarks>
/// <see cref="ConfigurationProvider"/> is never touched: its static state would make these
/// tests interfere with others running in parallel.
/// </remarks>
public sealed class ConfigurationPrecedenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aeromux-precedence-{Guid.NewGuid():N}");

    public ConfigurationPrecedenceTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private const string Uuid1 = "550e8400-e29b-41d4-a716-446655440000";
    private const string Uuid2 = "6ba7b810-9dad-11d1-80b4-00c04fd430c8";

    /// <summary>
    /// A YAML file that writes none of the keys under test, so their values are defaults.
    /// </summary>
    private const string UnrelatedYaml = "heatmap:\n  collect: true\n";

    private DaemonValidatedConfig Resolve(string yaml, params string[] args)
    {
        string path = Path.Combine(_directory, $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);

        DaemonSettings settings = DaemonSettingsParsingTests.ParseDaemon(["--config", path, .. args]);
        ConfigurationResult built = new ConfigurationBuilder().Build(settings, new YamlConfigurationLoader());
        return DaemonConfigValidator.Validate(settings, built.Config, built.Origin);
    }

    /// <summary>
    /// Reads the resolved value at a key path as invariant text.
    /// </summary>
    private static string Read(DaemonValidatedConfig config, string keyPath) => keyPath switch
    {
        "logging.level" => config.Config.Logging!.Level.ToString(),
        "database.path" => config.Config.Database!.Path ?? "null",
        "database.enabled" => Flag(config.Config.Database!.Enabled),
        "beastSources" => string.Join(",", config.BeastSources.Select(b => $"{b.Host}:{b.Port}")),
        "network.bindAddress" => config.BindAddress.ToString(),
        "network.apiPort" => Number(config.ApiPort),
        "network.apiEnabled" => Flag(config.ApiEnabled),
        "network.apiCorsOrigins" => string.Join(",", config.ApiCorsOrigins),
        "network.beastOutputPort" => Number(config.BeastOutputPort),
        "network.beastOutputEnabled" => Flag(config.BeastEnabled),
        "network.jsonOutputPort" => Number(config.JsonOutputPort),
        "network.jsonOutputEnabled" => Flag(config.JsonEnabled),
        "network.sbsOutputPort" => Number(config.SbsOutputPort),
        "network.sbsOutputEnabled" => Flag(config.SbsEnabled),
        "mlat.enabled" => Flag(config.MlatConfig.Enabled),
        "mlat.inputPort" => Number(config.MlatConfig.InputPort),
        "receiver.receiverUuid" => config.ReceiverUuid?.ToString() ?? "null",
        _ => throw new ArgumentOutOfRangeException(nameof(keyPath), keyPath, null)
    };

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// For every command-line option that maps to a configuration key: the key, the YAML that
    /// sets it, the arguments that override it, and the three expected values (default, YAML,
    /// CLI over YAML).
    /// </summary>
    public static TheoryData<string, string, string[], string, string, string> Options => new()
    {
        { "logging.level", "logging:\n  level: warning\n", ["--log-level", "Error"], "Information", "Warning", "Error" },
        { "database.path", "database:\n  path: /yaml/db\n", ["--database", "/cli/db"], "null", "/yaml/db", "/cli/db" },
        { "beastSources", "beastSources:\n  - host: yaml-a\n    port: 30005\n  - host: yaml-b\n    port: 30005\n",
            ["--beast-source", "cli:31005"], "", "yaml-a:30005,yaml-b:30005", "cli:31005" },
        { "network.bindAddress", "network:\n  bindAddress: \"192.168.1.10\"\n", ["--bind-address", "127.0.0.1"],
            "0.0.0.0", "192.168.1.10", "127.0.0.1" },
        { "network.apiPort", "network:\n  apiPort: 9000\n", ["--api-port", "9100"], "8080", "9000", "9100" },
        { "network.apiEnabled", "network:\n  apiEnabled: false\n", ["--api-enabled", "true"], "true", "false", "true" },
        { "network.apiCorsOrigins", "network:\n  apiCorsOrigins:\n    - http://yaml.local\n",
            ["--api-cors-origin", "http://cli.local:3000"], "", "http://yaml.local", "http://cli.local:3000" },
        { "network.beastOutputPort", "network:\n  beastOutputPort: 31005\n", ["--beast-output-port", "32005"], "30005", "31005", "32005" },
        { "network.beastOutputEnabled", "network:\n  beastOutputEnabled: false\n", ["--beast-output-enabled", "true"], "true", "false", "true" },
        { "network.jsonOutputPort", "network:\n  jsonOutputPort: 31006\n", ["--json-output-port", "32006"], "30006", "31006", "32006" },
        { "network.jsonOutputEnabled", "network:\n  jsonOutputEnabled: true\n", ["--json-output-enabled", "false"], "false", "true", "false" },
        { "network.sbsOutputPort", "network:\n  sbsOutputPort: 31003\n", ["--sbs-output-port", "32003"], "30003", "31003", "32003" },
        { "network.sbsOutputEnabled", "network:\n  sbsOutputEnabled: true\n", ["--sbs-output-enabled", "false"], "false", "true", "false" },
        { "mlat.enabled", "mlat:\n  enabled: false\n", ["--mlat-enabled", "true"], "true", "false", "true" },
        { "mlat.inputPort", "mlat:\n  inputPort: 31104\n", ["--mlat-input-port", "32104"], "30104", "31104", "32104" },
        { "receiver.receiverUuid", $"receiver:\n  receiverUuid: \"{Uuid1}\"\n", ["--receiver-uuid", Uuid2], "null", Uuid1, Uuid2 }
    };

    [Theory]
    [MemberData(nameof(Options))]
    public void Option_NotGiven_IsTheDefault(
        string keyPath, string yaml, string[] args, string expectedDefault, string expectedYaml, string expectedCli)
    {
        _ = (yaml, args, expectedYaml, expectedCli);

        DaemonValidatedConfig config = Resolve(UnrelatedYaml);

        Read(config, keyPath).Should().Be(expectedDefault);
        config.Origin.SourceOf(keyPath).Should().Be(ConfigurationSource.Default);
    }

    [Theory]
    [MemberData(nameof(Options))]
    public void Option_InYamlOnly_IsTheYamlValue(
        string keyPath, string yaml, string[] args, string expectedDefault, string expectedYaml, string expectedCli)
    {
        _ = (args, expectedDefault, expectedCli);

        DaemonValidatedConfig config = Resolve(yaml);

        Read(config, keyPath).Should().Be(expectedYaml);
        config.Origin.SourceOf(keyPath).Should().Be(ConfigurationSource.Yaml);
    }

    [Theory]
    [MemberData(nameof(Options))]
    public void Option_InYamlAndOnCommandLine_IsTheCommandLineValue(
        string keyPath, string yaml, string[] args, string expectedDefault, string expectedYaml, string expectedCli)
    {
        _ = (expectedDefault, expectedYaml);

        DaemonValidatedConfig config = Resolve(yaml, args);

        Read(config, keyPath).Should().Be(expectedCli);
        config.Origin.SourceOf(keyPath).Should().Be(ConfigurationSource.Cli);
    }

    // === What /api/v1/config reports ===

    [Theory]
    [MemberData(nameof(Options))]
    public void ConfigReport_ShowsTheYamlValueAndSource(
        string keyPath, string yaml, string[] args, string expectedDefault, string expectedYaml, string expectedCli)
    {
        _ = (args, expectedDefault, expectedCli);

        JsonObject report = DaemonConfigReport.Build(Resolve(yaml), includeSource: true);

        ReportedSources(report, keyPath).Should().AllBe("Yaml");
        ReportedValue(report, keyPath).Should().Be(keyPath == "receiver.receiverUuid" ? DaemonConfigReport.Redacted : expectedYaml);
    }

    [Theory]
    [MemberData(nameof(Options))]
    public void ConfigReport_ShowsTheCommandLineValueAndSource(
        string keyPath, string yaml, string[] args, string expectedDefault, string expectedYaml, string expectedCli)
    {
        _ = (expectedDefault, expectedYaml);

        JsonObject report = DaemonConfigReport.Build(Resolve(yaml, args), includeSource: true);

        ReportedSources(report, keyPath).Should().AllBe("Cli");
        ReportedValue(report, keyPath).Should().Be(keyPath == "receiver.receiverUuid" ? DaemonConfigReport.Redacted : expectedCli);
    }

    /// <summary>
    /// The report's node for a key path: each segment in PascalCase.
    /// </summary>
    private static JsonNode ReportNode(JsonObject report, string keyPath) =>
        keyPath.Split('.').Aggregate<string, JsonNode>(report,
            (node, segment) => node[string.Concat(char.ToUpperInvariant(segment[0]).ToString(), segment[1..])]!);

    /// <summary>
    /// The sources reported for a key: one per field for the Beast source list, one otherwise.
    /// </summary>
    private static IEnumerable<string?> ReportedSources(JsonObject report, string keyPath) =>
        keyPath == "beastSources"
            ? ReportNode(report, keyPath).AsArray()
                .SelectMany(item => item!.AsObject().Select(field => (string?)field.Value!["Source"]))
            : [(string?)ReportNode(report, keyPath)["Source"]];

    /// <summary>
    /// The reported value as the invariant text <see cref="Read"/> produces.
    /// </summary>
    private static string ReportedValue(JsonObject report, string keyPath)
    {
        JsonNode node = ReportNode(report, keyPath);
        if (keyPath == "beastSources")
        {
            return string.Join(",", node.AsArray().Select(item => $"{item!["Host"]!["Value"]}:{item["Port"]!["Value"]}"));
        }

        return node["Value"] switch
        {
            null => "null",
            JsonArray array => string.Join(",", array.Select(item => item!.ToString())),
            JsonNode value => value.ToJsonString().Trim('"')
        };
    }

    // === Implied values ===

    [Fact]
    public void DatabaseOption_AlsoEnablesTheDatabase()
    {
        DaemonValidatedConfig config = Resolve("database:\n  enabled: false\n  path: /yaml/db\n", "--database", "/cli/db");

        config.Config.Database!.Enabled.Should().BeTrue();
        config.Origin.SourceOf("database.enabled").Should().Be(ConfigurationSource.Cli);
    }

    [Fact]
    public void SdrSourceWithBeastSource_KeepsBothInputs()
    {
        DaemonValidatedConfig config = Resolve(UnrelatedYaml, "--sdr-source", "--beast-source", "host:30005");

        config.UseSdr.Should().BeTrue();
        config.UseBeast.Should().BeTrue();
    }

    [Fact]
    public void BeastSourceAlone_ReplacesSdrInput()
    {
        DaemonValidatedConfig config = Resolve(UnrelatedYaml, "--beast-source", "host:30005");

        config.UseSdr.Should().BeFalse();
        config.UseBeast.Should().BeTrue();
    }

    // === Written keys, partial sections and lists ===

    [Fact]
    public void YamlKeyWithTheDefaultValue_StillReportsYaml()
    {
        DaemonValidatedConfig config = Resolve("network:\n  apiPort: 8080\n");

        config.ApiPort.Should().Be(8080);
        config.Origin.SourceOf("network.apiPort").Should().Be(ConfigurationSource.Yaml);
    }

    [Fact]
    public void PartialYamlSection_WrittenKeysAreYaml_TheRestDefaults()
    {
        DaemonValidatedConfig config = Resolve("network:\n  apiPort: 9000\n");

        config.ApiPort.Should().Be(9000);
        config.BeastOutputPort.Should().Be(30005);
        config.SbsEnabled.Should().BeFalse();
        config.Origin.SourceOf("network.apiPort").Should().Be(ConfigurationSource.Yaml);
        config.Origin.SourceOf("network.beastOutputPort").Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void CommandLineBeastSource_CoversEveryItemOfTheList()
    {
        DaemonValidatedConfig config = Resolve(
            "beastSources:\n  - host: yaml-a\n    port: 30005\n  - host: yaml-b\n    port: 30005\n",
            "--beast-source", "cli:31005");

        config.BeastSources.Should().ContainSingle();
        config.Origin.SourceOf("beastSources[0].host").Should().Be(ConfigurationSource.Cli);
        config.Origin.SourceOf("beastSources[1].port").Should().Be(ConfigurationSource.Cli);
    }

    [Fact]
    public void YamlSdrSources_ReplaceTheBuiltInDevice_AndReportPerField()
    {
        DaemonValidatedConfig config = Resolve(
            "sdrSources:\n  - name: roof\n    tunerGain: 40.2\n  - name: garage\n    deviceIndex: 1\n");

        config.EnabledSdrSources.Select(s => s.Name).Should().Equal("roof", "garage");
        config.Origin.SourceOf("sdrSources[0].tunerGain").Should().Be(ConfigurationSource.Yaml);
        config.Origin.SourceOf("sdrSources[0].deviceIndex").Should().Be(ConfigurationSource.Default);
        config.Origin.SourceOf("sdrSources[1].deviceIndex").Should().Be(ConfigurationSource.Yaml);
    }

    [Fact]
    public void YamlSectionAndCommandLine_OnlyTheOverriddenKeyIsCli()
    {
        DaemonValidatedConfig config = Resolve("network:\n  apiPort: 9000\n  beastOutputPort: 31005\n", "--api-port", "9100");

        config.ApiPort.Should().Be(9100);
        config.BeastOutputPort.Should().Be(31005);
        config.Origin.SourceOf("network.apiPort").Should().Be(ConfigurationSource.Cli);
        config.Origin.SourceOf("network.beastOutputPort").Should().Be(ConfigurationSource.Yaml);
    }

    // === Empty files ===

    [Theory]
    [InlineData("")]
    [InlineData("# network:\n#   apiPort: 9000\n")]
    public void EmptyOrCommentOnlyFile_BehavesLikeNoFile(string yaml)
    {
        DaemonValidatedConfig config = Resolve(yaml);

        config.ApiPort.Should().Be(8080);
        config.EnabledSdrSources.Should().ContainSingle().Which.Name.Should().Be("default");
        config.Origin.YamlKeys.Should().BeEmpty();
        config.Origin.FilePath.Should().NotBeNull();
        config.Origin.SourceOf("network.apiPort").Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void Origin_RecordsTheLoadedFileAsAnAbsolutePath()
    {
        DaemonValidatedConfig config = Resolve(UnrelatedYaml);

        config.Origin.FilePath.Should().StartWith(_directory);
        Path.IsPathRooted(config.Origin.FilePath).Should().BeTrue();
    }
}
