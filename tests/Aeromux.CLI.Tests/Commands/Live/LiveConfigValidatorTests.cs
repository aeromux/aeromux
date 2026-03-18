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

using Aeromux.CLI.Commands.Live;
using Aeromux.Core.Configuration;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands.Live;

public class LiveConfigValidatorTests
{
    // ─── Input Source Resolution ───

    [Fact]
    public void Validate_NoFlags_SdrAvailable_UsesSdrOnly()
    {
        var settings = new LiveSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeFalse();
        result.EnabledSdrSources.Should().HaveCount(1);
    }

    [Fact]
    public void Validate_NoFlags_NoSdr_Throws()
    {
        var settings = new LiveSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        Action act = () => LiveConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No enabled SDR sources*");
    }

    [Fact]
    public void Validate_BeastSourceOnly_UsesBeastOnly()
    {
        var settings = new LiveSettings { BeastSource = ["192.168.1.10:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
        result.BeastSources.Should().HaveCount(1);
        result.BeastSources[0].Host.Should().Be("192.168.1.10");
        result.BeastSources[0].Port.Should().Be(30005);
    }

    [Fact]
    public void Validate_SdrSourceOnly_NoYamlBeast_UsesSdrOnly()
    {
        var settings = new LiveSettings { SdrSource = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeFalse();
    }

    [Fact]
    public void Validate_SdrAndBeastSource_UsesBoth()
    {
        var settings = new LiveSettings { SdrSource = true, BeastSource = ["host:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeTrue();
        result.EnabledSdrSources.Should().HaveCount(1);
        result.BeastSources.Should().HaveCount(1);
    }

    [Fact]
    public void Validate_SdrSourceFlag_NoSdrAvailable_Throws()
    {
        var settings = new LiveSettings { SdrSource = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        Action act = () => LiveConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No enabled SDR sources*");
    }

    [Fact]
    public void Validate_YamlBeastOnly_NoCli_UsesBeastOnly()
    {
        var settings = new LiveSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: false, yamlBeast: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
        result.BeastSources.Should().HaveCount(1);
        result.BeastSources[0].Host.Should().Be("yaml-host");
    }

    [Fact]
    public void Validate_YamlBeast_CliSdrSource_UsesBoth()
    {
        var settings = new LiveSettings { SdrSource = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: true, yamlBeast: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeTrue();
        result.BeastSources[0].Host.Should().Be("yaml-host");
    }

    [Fact]
    public void Validate_CliBeastOverridesYamlBeast()
    {
        var settings = new LiveSettings { BeastSource = ["cli-host:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: false, yamlBeast: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.BeastSources.Should().HaveCount(1);
        result.BeastSources[0].Host.Should().Be("cli-host");
    }

    [Fact]
    public void Validate_YamlBeastAndSdr_NoCli_UsesBeastOnly()
    {
        // When YAML has both beastSources and sdrSources but no CLI flags,
        // Beast presence means SDR is NOT implied (requires explicit --sdr-source)
        var settings = new LiveSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true, yamlBeast: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidBeastConnectionString_Throws()
    {
        var settings = new LiveSettings { BeastSource = ["host:port:extra"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => LiveConfigValidator.Validate(settings, config);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_DuplicateBeastSources_Throws()
    {
        var settings = new LiveSettings { BeastSource = ["host:30005", "host:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => LiveConfigValidator.Validate(settings, config);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*");
    }

    // ─── Backward Compatibility ───

    [Fact]
    public void Validate_DefaultLive_NoFlags_SdrImplied()
    {
        // "aeromux live" with no flags and SDR available in YAML → SDR implied
        var settings = new LiveSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        LiveValidatedConfig result = LiveConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeFalse();
    }

    // ─── Helpers ───

    private static AeromuxConfig CreateConfig(bool sdrEnabled = false, bool yamlBeast = false)
    {
        var config = new AeromuxConfig
        {
            Tracking = new TrackingConfig(),
            Network = new NetworkConfig(),
            Receiver = new ReceiverConfig { Latitude = 47.5, Longitude = 19.0 }
        };

        if (sdrEnabled)
        {
            config.SdrSources =
            [
                new SdrSourceConfig { Name = "test-sdr", DeviceIndex = 0, Enabled = true }
            ];
        }

        if (yamlBeast)
        {
            config.BeastSources =
            [
                new BeastSourceConfig { Host = "yaml-host", Port = 30005 }
            ];
        }

        return config;
    }
}
