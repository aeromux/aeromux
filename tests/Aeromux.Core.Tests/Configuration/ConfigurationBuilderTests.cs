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
using System.Reflection;
using Aeromux.Core.Configuration;
using Aeromux.Core.Configuration.Enums;
using Serilog.Events;

namespace Aeromux.Core.Tests.Configuration;

/// <summary>
/// Tests for <see cref="ConfigurationBuilder"/>: defaults, YAML sections replacing default
/// sections, the global CLI overrides, the recorded origin, and that every built-in default
/// equals the default of its class.
/// </summary>
public sealed class ConfigurationBuilderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aeromux-builder-{Guid.NewGuid():N}");

    public ConfigurationBuilderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed record Settings(string? ConfigPath = null, LogEventLevel? LogLevel = null, string? DatabasePath = null)
        : IGlobalSettings;

    /// <summary>
    /// Returns a fixed document, so the tests exercise the merge without YAML parsing.
    /// </summary>
    private sealed class StubLoader(AeromuxConfig config, params string[] keys) : IYamlConfigurationLoader
    {
        public YamlConfigurationDocument LoadDocument(string path) => new(config, new HashSet<string>(keys));
    }

    private sealed class ThrowingLoader : IYamlConfigurationLoader
    {
        public YamlConfigurationDocument LoadDocument(string path) => throw new FormatException("bad yaml");
    }

    /// <summary>
    /// Creates an existing file for the builder's existence check; its content is never read.
    /// </summary>
    private string ExistingFile()
    {
        string path = Path.Combine(_directory, "aeromux.yaml");
        File.WriteAllText(path, "# read by the stub loader, not parsed\n");
        return path;
    }

    private static ConfigurationResult BuildWithoutFile(Settings? settings = null)
    {
        File.Exists("aeromux.yaml").Should().BeFalse("the test assumes no aeromux.yaml in the working directory");
        return new ConfigurationBuilder().Build(settings ?? new Settings(), new StubLoader(new AeromuxConfig()));
    }

    // === No file ===

    [Fact]
    public void Build_NoFileAndNoConfigOption_UsesDefaultsAndRecordsNoFile()
    {
        ConfigurationResult result = BuildWithoutFile();

        result.Origin.FilePath.Should().BeNull();
        result.Origin.YamlKeys.Should().BeEmpty();
        result.Origin.CliKeys.Should().BeEmpty();
        result.Config.Network!.ApiPort.Should().Be(8080);
        result.Config.SdrSources.Should().ContainSingle().Which.Name.Should().Be("default");
        result.Config.Receiver.Should().BeNull();
    }

    [Fact]
    public void Build_ConfigOptionNamingMissingFile_Throws()
    {
        string missing = Path.Combine(_directory, "missing.yaml");

        Action act = () => new ConfigurationBuilder().Build(new Settings(missing), new StubLoader(new AeromuxConfig()));

        act.Should().Throw<FileNotFoundException>().WithMessage($"*{missing}*");
    }

    [Fact]
    public void Build_LoaderFails_ThrowsNamingTheFile()
    {
        string path = ExistingFile();

        Action act = () => new ConfigurationBuilder().Build(new Settings(path), new ThrowingLoader());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{path}*")
            .WithInnerException<FormatException>();
    }

    // === YAML merge ===

    [Fact]
    public void Build_YamlFile_RecordsAbsolutePathAndKeys()
    {
        string path = ExistingFile();
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);

        ConfigurationResult result = new ConfigurationBuilder().Build(
            new Settings(relative), new StubLoader(new AeromuxConfig(), "network", "network.apiPort"));

        result.Origin.FilePath.Should().Be(Path.GetFullPath(path));
        Path.IsPathRooted(result.Origin.FilePath).Should().BeTrue();
        result.Origin.YamlKeys.Should().BeEquivalentTo("network", "network.apiPort");
    }

    [Fact]
    public void Build_YamlSection_ReplacesDefaultSectionAsAWhole()
    {
        var yaml = new AeromuxConfig { Network = new NetworkConfig { ApiPort = 9000, BindAddress = IPAddress.Loopback } };

        ConfigurationResult result = new ConfigurationBuilder().Build(
            new Settings(ExistingFile()), new StubLoader(yaml, "network", "network.apiPort", "network.bindAddress"));

        result.Config.Network.Should().BeSameAs(yaml.Network);
        result.Config.Network!.ApiPort.Should().Be(9000);
        result.Config.Network.BeastOutputPort.Should().Be(30005, "a key the section leaves out keeps the class default");
    }

    [Fact]
    public void Build_SectionNotWrittenInYaml_KeepsItsDefault()
    {
        var yaml = new AeromuxConfig { Network = new NetworkConfig { ApiPort = 9000 } };

        ConfigurationResult result = new ConfigurationBuilder().Build(
            new Settings(ExistingFile()), new StubLoader(yaml, "network", "network.apiPort"));

        result.Config.Tracking!.AircraftTimeoutSeconds.Should().Be(60);
        result.Config.Mlat!.InputPort.Should().Be(30104);
        result.Config.Heatmap!.Collect.Should().BeTrue();
    }

    [Fact]
    public void Build_YamlSdrSources_ReplaceTheBuiltInDevice()
    {
        var yaml = new AeromuxConfig
        {
            SdrSources = [new SdrSourceConfig { Name = "a", DeviceIndex = 1 }, new SdrSourceConfig { Name = "b", DeviceIndex = 2 }]
        };

        ConfigurationResult result = new ConfigurationBuilder().Build(new Settings(ExistingFile()), new StubLoader(yaml, "sdrSources"));

        result.Config.SdrSources!.Select(s => s.Name).Should().Equal("a", "b");
    }

    [Fact]
    public void Build_EmptyYamlDocument_KeepsEveryDefault()
    {
        ConfigurationResult fromEmpty = new ConfigurationBuilder().Build(new Settings(ExistingFile()), new StubLoader(new AeromuxConfig()));
        ConfigurationResult fromNone = BuildWithoutFile();

        AssertSameValues(fromNone.Config, fromEmpty.Config, "config");
        fromEmpty.Origin.FilePath.Should().NotBeNull();
    }

    // === Global CLI overrides ===

    [Fact]
    public void Build_LogLevelOption_OverridesYamlAndRecordsCliKey()
    {
        var yaml = new AeromuxConfig { Logging = new LoggingConfig { Level = LogEventLevel.Debug } };

        ConfigurationResult result = new ConfigurationBuilder().Build(
            new Settings(ExistingFile(), LogLevel: LogEventLevel.Warning), new StubLoader(yaml, "logging", "logging.level"));

        result.Config.Logging!.Level.Should().Be(LogEventLevel.Warning);
        result.Origin.SourceOf("logging.level").Should().Be(ConfigurationSource.Cli);
    }

    [Fact]
    public void Build_DatabaseOption_OverridesYamlPathAndEnables()
    {
        var yaml = new AeromuxConfig { Database = new DatabaseConfig { Enabled = false, Path = "/yaml/db" } };

        ConfigurationResult result = new ConfigurationBuilder().Build(
            new Settings(ExistingFile(), DatabasePath: "/cli/db"),
            new StubLoader(yaml, "database", "database.enabled", "database.path"));

        result.Config.Database!.Path.Should().Be("/cli/db");
        result.Config.Database.Enabled.Should().BeTrue();
        result.Origin.SourceOf("database.path").Should().Be(ConfigurationSource.Cli);
        result.Origin.SourceOf("database.enabled").Should().Be(ConfigurationSource.Cli);
    }

    [Fact]
    public void Build_NoCliOverrides_RecordsNoCliKeys()
    {
        ConfigurationResult result = new ConfigurationBuilder().Build(
            new Settings(ExistingFile()), new StubLoader(new AeromuxConfig(), "logging", "logging.level"));

        result.Origin.CliKeys.Should().BeEmpty();
        result.Origin.SourceOf("logging.level").Should().Be(ConfigurationSource.Yaml);
    }

    [Fact]
    public void BuildFromSettings_ReturnsTheSameConfigurationAsBuild()
    {
        AeromuxConfig config = new ConfigurationBuilder().BuildFromSettings(new Settings(), new StubLoader(new AeromuxConfig()));

        AssertSameValues(BuildWithoutFile().Config, config, "config");
    }

    // === Default consistency ===

    // A YAML section replaces the built-in default section as a whole, so every key the section
    // leaves out takes its class initializer. "CLI > YAML > Default, per setting" therefore only
    // holds if each built-in default equals the class default; these tests fail when one drifts.

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_Logging() =>
        AssertSameValues(new LoggingConfig(), BuildWithoutFile().Config.Logging!, "logging");

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_Network() =>
        AssertSameValues(new NetworkConfig(), BuildWithoutFile().Config.Network!, "network");

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_Tracking() =>
        AssertSameValues(new TrackingConfig(), BuildWithoutFile().Config.Tracking!, "tracking");

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_Mlat() =>
        AssertSameValues(new MlatConfig(), BuildWithoutFile().Config.Mlat!, "mlat");

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_Heatmap() =>
        AssertSameValues(new HeatmapConfig(), BuildWithoutFile().Config.Heatmap!, "heatmap");

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_SdrDevice() =>
        AssertSameValues(new SdrSourceConfig(), BuildWithoutFile().Config.SdrSources!.Single(), "sdrSources[0]");

    [Fact]
    public void BuiltInDefaults_EqualClassDefaults_DatabaseWithoutAutoUpdate()
    {
        // AutoUpdate is excluded: a missing section is resolved by DatabaseAutoUpdateConfig.Resolve,
        // which has its own tests.
        DatabaseConfig database = BuildWithoutFile().Config.Database!;
        var expected = new DatabaseConfig();

        database.Enabled.Should().Be(expected.Enabled);
        database.Path.Should().Be(expected.Path);
    }

    /// <summary>
    /// Compares two configuration objects property by property, descending into nested
    /// configuration classes and lists, and names the first differing key path.
    /// </summary>
    private static void AssertSameValues(object? expected, object? actual, string path)
    {
        if (expected is null || actual is null)
        {
            actual.Should().Be(expected, $"{path} should match");
            return;
        }

        Type type = expected.GetType();
        if (type.IsPrimitive || type.IsEnum || expected is string or decimal or IPAddress or Guid)
        {
            actual.Should().Be(expected, $"{path} should match");
            return;
        }

        if (expected is System.Collections.IList expectedList)
        {
            var actualList = (System.Collections.IList)actual;
            actualList.Count.Should().Be(expectedList.Count, $"{path} should have the same length");
            for (int i = 0; i < expectedList.Count; i++)
            {
                AssertSameValues(expectedList[i], actualList[i], $"{path}[{i}]");
            }

            return;
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            AssertSameValues(property.GetValue(expected), property.GetValue(actual), $"{path}.{name}");
        }
    }
}
