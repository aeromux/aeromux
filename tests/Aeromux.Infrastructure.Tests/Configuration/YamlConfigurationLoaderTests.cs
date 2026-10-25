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
using Aeromux.Core.Configuration;
using Aeromux.Core.Configuration.Enums;
using Aeromux.Infrastructure.Configuration;
using RtlSdrManager.Modes;
using Serilog.Events;
using YamlDotNet.Core;

namespace Aeromux.Infrastructure.Tests.Configuration;

/// <summary>
/// Tests for <see cref="YamlConfigurationLoader"/>: the key paths it records, the example
/// configurations that ship with Aeromux, and how it treats valid and invalid input.
/// </summary>
public sealed class YamlConfigurationLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aeromux-yaml-{Guid.NewGuid():N}");
    private readonly YamlConfigurationLoader _loader = new();

    public YamlConfigurationLoaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private YamlConfigurationDocument Load(string yaml)
    {
        string path = Path.Combine(_directory, $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return _loader.LoadDocument(path);
    }

    private static string ExamplePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "ExampleConfigs", fileName);

    // === Key paths ===

    [Fact]
    public void KeyPaths_NestedMappings_RecordSectionsAndLeaves()
    {
        YamlConfigurationDocument document = Load("""
            logging:
              level: debug
              file:
                path: "x.log"
            """);

        document.KeyPaths.Should().BeEquivalentTo("logging", "logging.level", "logging.file", "logging.file.path");
    }

    [Fact]
    public void KeyPaths_SequenceItems_RecordIndexedPaths()
    {
        YamlConfigurationDocument document = Load("""
            sdrSources:
              - name: a
              - name: b
                tunerGain: 40
            """);

        document.KeyPaths.Should().BeEquivalentTo(
            "sdrSources",
            "sdrSources[0]", "sdrSources[0].name",
            "sdrSources[1]", "sdrSources[1].name", "sdrSources[1].tunerGain");
    }

    [Fact]
    public void KeyPaths_ScalarSequence_IsOnePath()
    {
        YamlConfigurationDocument document = Load("""
            network:
              apiPort: 8080
            beastSources: []
            """);

        document.KeyPaths.Should().BeEquivalentTo("network", "network.apiPort", "beastSources");
    }

    [Fact]
    public void KeyPaths_SectionWithoutKeys_RecordsOnlyTheSection()
    {
        YamlConfigurationDocument document = Load("network:\n");

        document.KeyPaths.Should().BeEquivalentTo("network");
        document.Config.Network.Should().BeNull();
    }

    [Fact]
    public void KeyPaths_ValueEqualToDefault_IsStillRecorded()
    {
        YamlConfigurationDocument document = Load("network:\n  apiPort: 8080\n");

        document.KeyPaths.Should().Contain("network.apiPort");
    }

    // === Empty files ===

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    [InlineData("# every line commented out\n# network:\n#   apiPort: 9000\n")]
    public void EmptyOrCommentOnlyFile_GivesConfigurationWithNullSections(string yaml)
    {
        YamlConfigurationDocument document = Load(yaml);

        document.KeyPaths.Should().BeEmpty();
        document.Config.Logging.Should().BeNull();
        document.Config.SdrSources.Should().BeNull();
        document.Config.BeastSources.Should().BeNull();
        document.Config.Network.Should().BeNull();
        document.Config.Tracking.Should().BeNull();
        document.Config.Receiver.Should().BeNull();
        document.Config.Mlat.Should().BeNull();
        document.Config.Database.Should().BeNull();
        document.Config.Heatmap.Should().BeNull();
    }

    // === Parsing rules ===

    [Theory]
    [InlineData("debug")]
    [InlineData("Debug")]
    [InlineData("DEBUG")]
    public void EnumValues_AreCaseInsensitive(string level)
    {
        YamlConfigurationDocument document = Load($"logging:\n  level: {level}\n");

        document.Config.Logging!.Level.Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public void EnumValues_AreCaseInsensitive_ForEveryEnumKey()
    {
        YamlConfigurationDocument document = Load("""
            logging:
              file:
                rollingInterval: HOUR
            tracking:
              confidenceLevel: high
            sdrSources:
              - gainMode: MANUAL
            """);

        document.Config.Logging!.File.RollingInterval.Should().Be(RollingInterval.Hour);
        document.Config.Tracking!.ConfidenceLevel.Should().Be(ConfidenceLevel.High);
        document.Config.SdrSources![0].GainMode.Should().Be(TunerGainModes.Manual);
    }

    [Fact]
    public void BindAddress_ParsesAsIpAddress()
    {
        YamlConfigurationDocument document = Load("network:\n  bindAddress: \"127.0.0.1\"\n");

        document.Config.Network!.BindAddress.Should().Be(IPAddress.Loopback);
    }

    [Fact]
    public void UnknownKey_Throws_NamingTheKey()
    {
        Action act = () => Load("network:\n  apiPort: 9000\n  bogusKey: 1\n");

        act.Should().Throw<YamlException>().WithMessage("*bogusKey*");
    }

    [Fact]
    public void UnknownEnumValue_Throws()
    {
        Action act = () => Load("logging:\n  level: loud\n");

        act.Should().Throw<YamlException>();
    }

    [Fact]
    public void InvalidBindAddress_Throws()
    {
        Action act = () => Load("network:\n  bindAddress: \"not-an-ip\"\n");

        act.Should().Throw<YamlException>();
    }

    [Fact]
    public void MalformedYaml_Throws()
    {
        Action act = () => Load("network:\n  apiPort: [8080\n");

        act.Should().Throw<YamlException>();
    }

    [Fact]
    public void MissingFile_ThrowsFileNotFound()
    {
        Action act = () => _loader.LoadDocument(Path.Combine(_directory, "missing.yaml"));

        act.Should().Throw<FileNotFoundException>();
    }

    // === Example configurations ===

    [Theory]
    [InlineData("aeromux.example.yaml")]
    [InlineData("aeromux.example-single.yaml")]
    [InlineData("aeromux.example-multi.yaml")]
    [InlineData("aeromux.example-relay.yaml")]
    public void ExampleConfiguration_LoadsWithItsSharedSettings(string fileName)
    {
        YamlConfigurationDocument document = _loader.LoadDocument(ExamplePath(fileName));
        AeromuxConfig config = document.Config;

        config.Logging!.Level.Should().Be(LogEventLevel.Debug);
        config.Logging.Console.Colored.Should().BeTrue();
        config.Logging.File.Path.Should().Be("logs/aeromux-.log");
        config.Network!.BindAddress.Should().Be(IPAddress.Any);
        config.Network.ApiPort.Should().Be(8080);
        config.Tracking!.ConfidenceLevel.Should().Be(ConfidenceLevel.Medium);
        config.Receiver!.Name.Should().Be("Kecskemét");
        config.Receiver.Latitude.Should().NotBeNull();
        config.Database!.AutoUpdate.Should().NotBeNull();
        config.Mlat!.InputPort.Should().Be(30104);
        config.Heatmap!.Collect.Should().BeTrue();
    }

    [Theory]
    [InlineData("aeromux.example.yaml")]
    [InlineData("aeromux.example-single.yaml")]
    public void SingleDeviceExamples_HaveOneSdrAndNoBeastSource(string fileName)
    {
        AeromuxConfig config = _loader.LoadDocument(ExamplePath(fileName)).Config;

        config.SdrSources.Should().ContainSingle()
            .Which.GainMode.Should().Be(TunerGainModes.Manual);
        config.BeastSources.Should().BeNull();
    }

    [Fact]
    public void MultiDeviceExample_HasTwoSdrDevicesWithDistinctIndices()
    {
        AeromuxConfig config = _loader.LoadDocument(ExamplePath("aeromux.example-multi.yaml")).Config;

        config.SdrSources.Should().HaveCount(2);
        config.SdrSources!.Select(s => s.DeviceIndex).Should().Equal(0, 1);
        config.BeastSources.Should().BeNull();
    }

    [Fact]
    public void RelayExample_HasTwoBeastSourcesAndNoSdr()
    {
        AeromuxConfig config = _loader.LoadDocument(ExamplePath("aeromux.example-relay.yaml")).Config;

        config.SdrSources.Should().BeNull();
        config.BeastSources.Should().HaveCount(2);
        config.BeastSources!.Should().AllSatisfy(b => b.Port.Should().Be(30005));
    }
}
