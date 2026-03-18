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
using Aeromux.Core.Configuration;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands.Daemon;

public class DaemonConfigValidatorTests
{
    // ─── Input Source Resolution ───

    [Fact]
    public void Validate_NoFlags_SdrAvailable_UsesSdrOnly()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeFalse();
        result.EnabledSdrSources.Should().HaveCount(1);
    }

    [Fact]
    public void Validate_NoFlags_NoSdr_Throws()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No enabled SDR sources*");
    }

    [Fact]
    public void Validate_BeastSourceOnly_UsesBeastOnly()
    {
        var settings = new DaemonSettings { BeastSource = ["192.168.1.10:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
        result.BeastSources.Should().HaveCount(1);
        result.BeastSources[0].Host.Should().Be("192.168.1.10");
    }

    [Fact]
    public void Validate_SdrSourceOnly_NoYamlBeast_UsesSdrOnly()
    {
        var settings = new DaemonSettings { SdrSource = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeFalse();
    }

    [Fact]
    public void Validate_SdrAndBeastSource_UsesBoth()
    {
        var settings = new DaemonSettings { SdrSource = true, BeastSource = ["host:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeTrue();
    }

    [Fact]
    public void Validate_SdrSourceFlag_NoSdrAvailable_Throws()
    {
        var settings = new DaemonSettings { SdrSource = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No enabled SDR sources*");
    }

    [Fact]
    public void Validate_YamlBeastOnly_NoCli_UsesBeastOnly()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: false, yamlBeast: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
        result.BeastSources[0].Host.Should().Be("yaml-host");
    }

    [Fact]
    public void Validate_YamlBeast_CliSdrSource_UsesBoth()
    {
        var settings = new DaemonSettings { SdrSource = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: true, yamlBeast: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeTrue();
    }

    [Fact]
    public void Validate_CliBeastOverridesYamlBeast()
    {
        var settings = new DaemonSettings { BeastSource = ["cli-host:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: false, yamlBeast: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.BeastSources.Should().HaveCount(1);
        result.BeastSources[0].Host.Should().Be("cli-host");
    }

    [Fact]
    public void Validate_YamlBeastAndSdr_NoCli_UsesBeastOnly()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true, yamlBeast: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidBeastConnectionString_Throws()
    {
        var settings = new DaemonSettings { BeastSource = ["host:port:extra"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_DuplicateBeastSources_Throws()
    {
        var settings = new DaemonSettings { BeastSource = ["host:30005", "host:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*");
    }

    // ─── Backward Compatibility ───

    [Fact]
    public void Validate_DefaultDaemon_NoFlags_SdrImplied()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeTrue();
        result.UseBeast.Should().BeFalse();
    }

    // ─── Beast-Only (Pure Aggregator) ───

    [Fact]
    public void Validate_BeastOnly_NoSdrNeeded_PureAggregator()
    {
        // Pure aggregator mode: Beast source, no SDR hardware at all
        var settings = new DaemonSettings { BeastSource = ["rpi1:30005", "rpi2:30005"] };
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseSdr.Should().BeFalse();
        result.UseBeast.Should().BeTrue();
        result.BeastSources.Should().HaveCount(2);
        result.EnabledSdrSources.Should().BeEmpty();
    }

    // ─── Port Override (CLI > YAML > Default) ───

    [Fact]
    public void Validate_BeastOutputPort_CliOverridesYaml()
    {
        var settings = new DaemonSettings { BeastOutputPort = 31005 };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.BeastOutputPort = 30005;

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.BeastOutputPort.Should().Be(31005);
    }

    [Fact]
    public void Validate_JsonOutputPort_CliOverridesYaml()
    {
        var settings = new DaemonSettings { JsonOutputPort = 31006 };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.JsonOutputPort = 30006;

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.JsonOutputPort.Should().Be(31006);
    }

    [Fact]
    public void Validate_SbsOutputPort_CliOverridesYaml()
    {
        var settings = new DaemonSettings { SbsOutputPort = 31003 };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.SbsOutputPort = 30003;

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.SbsOutputPort.Should().Be(31003);
    }

    [Fact]
    public void Validate_PortsDefault_WhenNoCliOverride()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.BeastOutputPort.Should().Be(30005);
        result.JsonOutputPort.Should().Be(30006);
        result.SbsOutputPort.Should().Be(30003);
        result.ApiPort.Should().Be(8080);
    }

    // ─── Port Range Validation ───

    [Fact]
    public void Validate_PortBelowRange_Throws()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.BeastOutputPort = 500;

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Beast port must be between 1024 and 65535*");
    }

    // ─── Bind Address ───

    [Fact]
    public void Validate_BindAddress_CliOverridesYaml()
    {
        var settings = new DaemonSettings { BindAddress = "127.0.0.1" };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.BindAddress.ToString().Should().Be("127.0.0.1");
    }

    [Fact]
    public void Validate_InvalidBindAddress_Throws()
    {
        var settings = new DaemonSettings { BindAddress = "not-an-ip" };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a valid IP address*");
    }

    // ─── Receiver UUID ───

    [Fact]
    public void Validate_ReceiverUuid_CliOverridesYaml()
    {
        string uuid = Guid.NewGuid().ToString();
        var settings = new DaemonSettings { ReceiverUuid = uuid };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.ReceiverUuid.Should().Be(Guid.Parse(uuid));
    }

    [Fact]
    public void Validate_InvalidReceiverUuid_Throws()
    {
        var settings = new DaemonSettings { ReceiverUuid = "not-a-uuid" };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a valid RFC 4122 UUID*");
    }

    // ─── Receiver Location ───

    [Fact]
    public void Validate_ReceiverLatitudeOutOfRange_Throws()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Receiver = new ReceiverConfig { Latitude = 91.0, Longitude = 19.0 };

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*latitude must be between -90 and +90*");
    }

    [Fact]
    public void Validate_ReceiverLatWithoutLon_Throws()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Receiver = new ReceiverConfig { Latitude = 47.5 };

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*latitude and longitude must both be provided*");
    }

    // ─── MLAT Config Validation ───

    [Fact]
    public void Validate_MlatEnabled_DefaultPort_Succeeds()
    {
        var settings = new DaemonSettings();
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Mlat = new MlatConfig { Enabled = true, InputPort = 30104 };

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.MlatConfig.Enabled.Should().BeTrue();
        result.MlatConfig.InputPort.Should().Be(30104);
    }

    [Fact]
    public void Validate_MlatDisabled_Succeeds()
    {
        var settings = new DaemonSettings { MlatEnabled = false };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.MlatConfig.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Validate_MlatPort_CliOverridesYaml()
    {
        var settings = new DaemonSettings { MlatInputPort = 31104 };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Mlat = new MlatConfig { Enabled = true, InputPort = 30104 };

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.MlatConfig.InputPort.Should().Be(31104);
    }

    [Fact]
    public void Validate_MlatPortBelowRange_Throws()
    {
        var settings = new DaemonSettings { MlatInputPort = 500 };
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(settings, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MLAT input port must be 1024-65535*");
    }

    [Fact]
    public void Validate_MlatWithBeastSource_Succeeds()
    {
        // MLAT works alongside Beast sources
        var settings = new DaemonSettings { BeastSource = ["rpi:30005"], MlatEnabled = true };
        AeromuxConfig config = CreateConfig(sdrEnabled: false);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, config);

        result.UseBeast.Should().BeTrue();
        result.MlatConfig.Enabled.Should().BeTrue();
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
