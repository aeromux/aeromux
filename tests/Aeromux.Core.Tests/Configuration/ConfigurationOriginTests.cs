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

using Aeromux.Core.Configuration;
using Aeromux.Core.Configuration.Enums;

namespace Aeromux.Core.Tests.Configuration;

/// <summary>
/// Tests for <see cref="ConfigurationOrigin.SourceOf"/>: CLI over YAML over default, and CLI
/// keys covering the lists and sections beneath them.
/// </summary>
public class ConfigurationOriginTests
{
    private static ConfigurationOrigin Origin(string[] yaml, string[] cli) =>
        new("/etc/aeromux/aeromux.yaml", new HashSet<string>(yaml), new HashSet<string>(cli));

    [Fact]
    public void SourceOf_KeyNeitherWrittenNorSet_IsDefault()
    {
        Origin(["network.apiPort"], []).SourceOf("network.sbsOutputPort")
            .Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void SourceOf_KeyWrittenInYaml_IsYaml()
    {
        Origin(["network", "network.apiPort"], []).SourceOf("network.apiPort")
            .Should().Be(ConfigurationSource.Yaml);
    }

    [Fact]
    public void SourceOf_KeySetOnCommandLine_IsCli()
    {
        Origin([], ["network.apiPort"]).SourceOf("network.apiPort")
            .Should().Be(ConfigurationSource.Cli);
    }

    [Fact]
    public void SourceOf_KeyWrittenAndSet_CliWins()
    {
        Origin(["network.apiPort"], ["network.apiPort"]).SourceOf("network.apiPort")
            .Should().Be(ConfigurationSource.Cli);
    }

    [Theory]
    [InlineData("beastSources[0].host")]
    [InlineData("beastSources[1].port")]
    public void SourceOf_ListSetOnCommandLine_CoversItsItems(string path)
    {
        Origin(["beastSources", "beastSources[0].host", "beastSources[1].port"], ["beastSources"])
            .SourceOf(path).Should().Be(ConfigurationSource.Cli);
    }

    [Fact]
    public void SourceOf_SectionWrittenInYaml_DoesNotCoverUnwrittenKeys()
    {
        Origin(["network", "network.apiPort"], []).SourceOf("network.bindAddress")
            .Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void SourceOf_SiblingCliKey_DoesNotCoverOtherKeys()
    {
        Origin([], ["network.apiPort"]).SourceOf("network.apiEnabled")
            .Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void SourceOf_KeyWithSharedPrefix_IsNotTreatedAsChild()
    {
        // "network.apiPort" is not an ancestor of "network.apiPortX"; only "." and "[" separate.
        Origin([], ["network.apiPort"]).SourceOf("network.apiPortX")
            .Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void None_ReportsDefaultForEverything()
    {
        ConfigurationOrigin.None.FilePath.Should().BeNull();
        ConfigurationOrigin.None.SourceOf("logging.level").Should().Be(ConfigurationSource.Default);
    }

    [Fact]
    public void WithCliKeys_AddsKeysWithoutChangingTheOriginal()
    {
        ConfigurationOrigin original = Origin(["network.apiPort"], ["logging.level"]);

        ConfigurationOrigin extended = original.WithCliKeys(["network.apiPort"]);

        extended.SourceOf("network.apiPort").Should().Be(ConfigurationSource.Cli);
        extended.SourceOf("logging.level").Should().Be(ConfigurationSource.Cli);
        original.SourceOf("network.apiPort").Should().Be(ConfigurationSource.Yaml);
        extended.FilePath.Should().Be(original.FilePath);
    }
}
