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

    // ─── Precedence: CLI > YAML > Default for every port and flag ───

    public enum PortOption { Api, BeastOutput, JsonOutput, SbsOutput, MlatInput }

    public enum FlagOption { Api, BeastOutput, JsonOutput, SbsOutput, Mlat }

    private static void SetYamlPort(AeromuxConfig config, PortOption option, int port)
    {
        switch (option)
        {
            case PortOption.Api: config.Network!.ApiPort = port; break;
            case PortOption.BeastOutput: config.Network!.BeastOutputPort = port; break;
            case PortOption.JsonOutput: config.Network!.JsonOutputPort = port; break;
            case PortOption.SbsOutput: config.Network!.SbsOutputPort = port; break;
            case PortOption.MlatInput: config.Mlat = new MlatConfig { InputPort = port }; break;
        }
    }

    private static DaemonSettings CliPort(PortOption option, int port) => option switch
    {
        PortOption.Api => new DaemonSettings { ApiPort = port },
        PortOption.BeastOutput => new DaemonSettings { BeastOutputPort = port },
        PortOption.JsonOutput => new DaemonSettings { JsonOutputPort = port },
        PortOption.SbsOutput => new DaemonSettings { SbsOutputPort = port },
        PortOption.MlatInput => new DaemonSettings { MlatInputPort = port },
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    private static int ResolvedPort(DaemonValidatedConfig result, PortOption option) => option switch
    {
        PortOption.Api => result.ApiPort,
        PortOption.BeastOutput => result.BeastOutputPort,
        PortOption.JsonOutput => result.JsonOutputPort,
        PortOption.SbsOutput => result.SbsOutputPort,
        PortOption.MlatInput => result.MlatConfig.InputPort,
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    private static void SetYamlFlag(AeromuxConfig config, FlagOption option, bool value)
    {
        switch (option)
        {
            case FlagOption.Api: config.Network!.ApiEnabled = value; break;
            case FlagOption.BeastOutput: config.Network!.BeastOutputEnabled = value; break;
            case FlagOption.JsonOutput: config.Network!.JsonOutputEnabled = value; break;
            case FlagOption.SbsOutput: config.Network!.SbsOutputEnabled = value; break;
            case FlagOption.Mlat: config.Mlat = new MlatConfig { Enabled = value }; break;
        }
    }

    private static DaemonSettings CliFlag(FlagOption option, bool value) => option switch
    {
        FlagOption.Api => new DaemonSettings { ApiEnabled = value },
        FlagOption.BeastOutput => new DaemonSettings { BeastOutputEnabled = value },
        FlagOption.JsonOutput => new DaemonSettings { JsonOutputEnabled = value },
        FlagOption.SbsOutput => new DaemonSettings { SbsOutputEnabled = value },
        FlagOption.Mlat => new DaemonSettings { MlatEnabled = value },
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    private static bool ResolvedFlag(DaemonValidatedConfig result, FlagOption option) => option switch
    {
        FlagOption.Api => result.ApiEnabled,
        FlagOption.BeastOutput => result.BeastEnabled,
        FlagOption.JsonOutput => result.JsonEnabled,
        FlagOption.SbsOutput => result.SbsEnabled,
        FlagOption.Mlat => result.MlatConfig.Enabled,
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    [Theory]
    [InlineData(PortOption.Api, 8080)]
    [InlineData(PortOption.BeastOutput, 30005)]
    [InlineData(PortOption.JsonOutput, 30006)]
    [InlineData(PortOption.SbsOutput, 30003)]
    [InlineData(PortOption.MlatInput, 30104)]
    public void Validate_Port_DefaultWhenNeitherYamlNorCli(PortOption option, int expected)
    {
        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), CreateConfig(sdrEnabled: true));

        ResolvedPort(result, option).Should().Be(expected);
    }

    [Theory]
    [InlineData(PortOption.Api)]
    [InlineData(PortOption.BeastOutput)]
    [InlineData(PortOption.JsonOutput)]
    [InlineData(PortOption.SbsOutput)]
    [InlineData(PortOption.MlatInput)]
    public void Validate_Port_YamlOverridesDefault(PortOption option)
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        SetYamlPort(config, option, 40000);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), config);

        ResolvedPort(result, option).Should().Be(40000);
    }

    [Theory]
    [InlineData(PortOption.Api)]
    [InlineData(PortOption.BeastOutput)]
    [InlineData(PortOption.JsonOutput)]
    [InlineData(PortOption.SbsOutput)]
    [InlineData(PortOption.MlatInput)]
    public void Validate_Port_CliOverridesYaml(PortOption option)
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        SetYamlPort(config, option, 40000);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(CliPort(option, 41000), config);

        ResolvedPort(result, option).Should().Be(41000);
    }

    [Theory]
    [InlineData(FlagOption.Api, true)]
    [InlineData(FlagOption.BeastOutput, true)]
    [InlineData(FlagOption.JsonOutput, false)]
    [InlineData(FlagOption.SbsOutput, false)]
    [InlineData(FlagOption.Mlat, true)]
    public void Validate_Flag_DefaultWhenNeitherYamlNorCli(FlagOption option, bool expected)
    {
        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), CreateConfig(sdrEnabled: true));

        ResolvedFlag(result, option).Should().Be(expected);
    }

    [Theory]
    [InlineData(FlagOption.Api, false)]
    [InlineData(FlagOption.BeastOutput, false)]
    [InlineData(FlagOption.JsonOutput, true)]
    [InlineData(FlagOption.SbsOutput, true)]
    [InlineData(FlagOption.Mlat, false)]
    public void Validate_Flag_YamlOverridesDefault(FlagOption option, bool yamlValue)
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        SetYamlFlag(config, option, yamlValue);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), config);

        ResolvedFlag(result, option).Should().Be(yamlValue);
    }

    [Theory]
    [InlineData(FlagOption.Api, false)]
    [InlineData(FlagOption.Api, true)]
    [InlineData(FlagOption.BeastOutput, false)]
    [InlineData(FlagOption.BeastOutput, true)]
    [InlineData(FlagOption.JsonOutput, false)]
    [InlineData(FlagOption.JsonOutput, true)]
    [InlineData(FlagOption.SbsOutput, false)]
    [InlineData(FlagOption.SbsOutput, true)]
    [InlineData(FlagOption.Mlat, false)]
    [InlineData(FlagOption.Mlat, true)]
    public void Validate_Flag_CliOverridesYaml(FlagOption option, bool cliValue)
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        SetYamlFlag(config, option, !cliValue);

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(CliFlag(option, cliValue), config);

        ResolvedFlag(result, option).Should().Be(cliValue);
    }

    // ─── Privileged ports use the resolved value ───

    [Fact]
    public void Validate_CliApiPortAbovePrivilegedRange_PassesOverPrivilegedYamlPort()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.ApiPort = 80;

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings { ApiPort = 9000 }, config);

        result.ApiPort.Should().Be(9000);
    }

    [Fact]
    public void Validate_CliApiPortInPrivilegedRange_Throws()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(new DaemonSettings { ApiPort = 80 }, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API port must be between 1024 and 65535, but was 80*");
    }

    [Fact]
    public void Validate_CliOutputPortInPrivilegedRange_Throws()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);

        Action act = () => DaemonConfigValidator.Validate(new DaemonSettings { BeastOutputPort = 500 }, config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Beast port must be between 1024 and 65535, but was 500*");
    }

    [Fact]
    public void Validate_CliOutputPortAbovePrivilegedRange_PassesOverPrivilegedYamlPort()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.SbsOutputPort = 300;
        config.Network.SbsOutputEnabled = true;

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings { SbsOutputPort = 31003 }, config);

        result.SbsOutputPort.Should().Be(31003);
    }

    [Fact]
    public void Validate_PrivilegedPortOfDisabledOutput_IsAccepted()
    {
        DaemonValidatedConfig result = DaemonConfigValidator.Validate(
            new DaemonSettings { JsonOutputPort = 500, JsonOutputEnabled = false }, CreateConfig(sdrEnabled: true));

        result.JsonEnabled.Should().BeFalse();
    }

    // ─── Command-line keys ───

    [Fact]
    public void Validate_NoOptions_AddsNoCliKeys()
    {
        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), CreateConfig(sdrEnabled: true));

        result.Origin.CliKeys.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EveryOption_RecordsItsCliKey()
    {
        var settings = new DaemonSettings
        {
            BeastSource = ["host:30005"],
            BindAddress = "127.0.0.1",
            ApiPort = 9000,
            ApiEnabled = true,
            ApiCorsOrigin = ["http://dashboard.local:3000"],
            BeastOutputPort = 31005,
            BeastOutputEnabled = true,
            JsonOutputPort = 31006,
            JsonOutputEnabled = true,
            SbsOutputPort = 31003,
            SbsOutputEnabled = true,
            MlatEnabled = true,
            MlatInputPort = 31104,
            ReceiverUuid = "550e8400-e29b-41d4-a716-446655440000"
        };

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(settings, CreateConfig(sdrEnabled: true));

        result.Origin.CliKeys.Should().BeEquivalentTo(
            "beastSources", "network.bindAddress", "network.apiPort", "network.apiEnabled",
            "network.apiCorsOrigins", "network.beastOutputPort", "network.beastOutputEnabled",
            "network.jsonOutputPort", "network.jsonOutputEnabled", "network.sbsOutputPort",
            "network.sbsOutputEnabled", "mlat.enabled", "mlat.inputPort", "receiver.receiverUuid");
    }

    [Fact]
    public void Validate_KeepsTheGivenOriginAndAddsDaemonKeys()
    {
        var origin = new ConfigurationOrigin(
            "/etc/aeromux/aeromux.yaml",
            new HashSet<string> { "network", "network.apiPort" },
            new HashSet<string> { "logging.level" });

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(
            new DaemonSettings { ApiPort = 9000 }, CreateConfig(sdrEnabled: true), origin);

        result.Origin.FilePath.Should().Be("/etc/aeromux/aeromux.yaml");
        result.Origin.YamlKeys.Should().Contain("network.apiPort");
        result.Origin.CliKeys.Should().BeEquivalentTo("logging.level", "network.apiPort");
    }

    // ─── CORS origins ───

    [Theory]
    [InlineData("http://dashboard.local:3000", "http://dashboard.local:3000")]
    [InlineData("https://grafana.example.net", "https://grafana.example.net")]
    [InlineData("HTTP://Dashboard.Local:80/", "http://dashboard.local")]
    [InlineData("https://host:443", "https://host")]
    [InlineData("http://192.168.1.10:8000/", "http://192.168.1.10:8000")]
    [InlineData("  http://host:3000  ", "http://host:3000")]
    [InlineData("*", "*")]
    public void NormalizeCorsOrigin_AcceptsOriginsAndNormalizes(string origin, string expected)
    {
        DaemonConfigValidator.NormalizeCorsOrigin(origin).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dashboard.local")]
    [InlineData("ftp://host")]
    [InlineData("http://host/path")]
    [InlineData("http://host/?q=1")]
    [InlineData("http://host/#top")]
    [InlineData("http://user:pass@host")]
    [InlineData("file:///etc/passwd")]
    [InlineData("**")]
    public void NormalizeCorsOrigin_RejectsAnythingButAnOrigin(string origin)
    {
        Action act = () => DaemonConfigValidator.NormalizeCorsOrigin(origin);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Invalid CORS origin*expected http(s)://host[:port] or *");
    }

    [Fact]
    public void Validate_CorsOrigins_DefaultIsEmpty()
    {
        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), CreateConfig(sdrEnabled: true));

        result.ApiCorsOrigins.Should().BeEmpty();
    }

    [Fact]
    public void Validate_CorsOrigins_FromYaml_NormalizedAndDeduplicated()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.ApiCorsOrigins = ["http://a.local:3000", "HTTP://A.local:3000/", "https://b.local"];

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(new DaemonSettings(), config);

        result.ApiCorsOrigins.Should().Equal("http://a.local:3000", "https://b.local");
    }

    [Fact]
    public void Validate_CorsOrigins_CliReplacesYamlList()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.ApiCorsOrigins = ["http://yaml.local"];

        DaemonValidatedConfig result = DaemonConfigValidator.Validate(
            new DaemonSettings { ApiCorsOrigin = ["http://cli-a.local", "http://cli-b.local"] }, config);

        result.ApiCorsOrigins.Should().Equal("http://cli-a.local", "http://cli-b.local");
    }

    [Fact]
    public void Validate_InvalidCorsOrigin_Throws()
    {
        AeromuxConfig config = CreateConfig(sdrEnabled: true);
        config.Network!.ApiCorsOrigins = ["http://host/path"];

        Action act = () => DaemonConfigValidator.Validate(new DaemonSettings(), config);

        act.Should().Throw<InvalidOperationException>().WithMessage("Invalid CORS origin 'http://host/path'*");
    }

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
