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
using System.Text.Json;
using Aeromux.CLI.Commands.Daemon;
using Aeromux.Core.Configuration;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for <c>GET /api/v1/config</c>: the effective configuration in the order of
/// <c>aeromux.yaml</c>, with every value optionally wrapped with its source.
/// </summary>
public class ConfigTests
{
    // === Shape ===

    [Fact]
    public async Task Plain_HasEverySectionInYamlOrder()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig());

        Names(doc.RootElement).Should().Equal(
            "Version", "Timestamp", "ConfigFile", "Logging", "SdrSources", "BeastSources",
            "Network", "Tracking", "Receiver", "Database", "Mlat", "Heatmap");
    }

    [Fact]
    public async Task Plain_EverySectionHasEveryField()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig { SdrSources = [new SdrSourceConfig()] },
            BeastSources = [new BeastSourceConfig { Host = "feeder", Port = 30005 }]
        });
        JsonElement root = doc.RootElement;

        Names(root.GetProperty("Logging")).Should().Equal("Level", "Console", "File");
        Names(root.GetProperty("Logging").GetProperty("Console")).Should().Equal("Enabled", "Colored");
        Names(root.GetProperty("Logging").GetProperty("File")).Should().Equal(
            "Enabled", "Path", "RollingInterval", "RetainedFileCount", "FileSizeLimitMb");
        Names(root.GetProperty("SdrSources")[0]).Should().Equal(
            "Name", "DeviceIndex", "GainMode", "TunerGain", "PpmCorrection", "Enabled",
            "PreambleThreshold", "DeduplicationWindow", "MaxTrackedFrames");
        Names(root.GetProperty("BeastSources")[0]).Should().Equal("Host", "Port");
        Names(root.GetProperty("Network")).Should().Equal(
            "BeastOutputPort", "BeastOutputEnabled", "JsonOutputPort", "JsonOutputEnabled",
            "SbsOutputPort", "SbsOutputEnabled", "ApiPort", "ApiEnabled", "ApiCorsOrigins", "BindAddress");
        Names(root.GetProperty("Tracking")).Should().Equal(
            "ConfidenceLevel", "IcaoTimeoutSeconds", "AircraftTimeoutSeconds", "EnablePositionHistory",
            "EnableAltitudeHistory", "EnableVelocityHistory", "EnableStateHistory", "MaxHistorySize");
        Names(root.GetProperty("Receiver")).Should().Equal("Latitude", "Longitude", "Altitude", "Name", "ReceiverUuid");
        Names(root.GetProperty("Database")).Should().Equal("Enabled", "Path", "AutoUpdate");
        Names(root.GetProperty("Database").GetProperty("AutoUpdate")).Should().Equal(
            "Enabled", "CheckOnStartup", "CheckIntervalHours", "PruneOldDatabases");
        Names(root.GetProperty("Mlat")).Should().Equal("Enabled", "InputPort");
        Names(root.GetProperty("Heatmap")).Should().Equal("Collect");
    }

    [Fact]
    public async Task Plain_ValuesKeepTheirJsonTypes()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig());
        JsonElement root = doc.RootElement;
        JsonElement network = root.GetProperty("Network");

        root.GetProperty("Version").ValueKind.Should().Be(JsonValueKind.String);
        root.GetProperty("ConfigFile").ValueKind.Should().Be(JsonValueKind.Null);
        network.GetProperty("ApiPort").ValueKind.Should().Be(JsonValueKind.Number);
        network.GetProperty("ApiEnabled").ValueKind.Should().Be(JsonValueKind.True);
        network.GetProperty("ApiCorsOrigins").ValueKind.Should().Be(JsonValueKind.Array);
        network.GetProperty("BindAddress").GetString().Should().Be("127.0.0.1");
        root.GetProperty("SdrSources").GetArrayLength().Should().Be(0);
        root.GetProperty("BeastSources").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Plain_EnumsAreStrings()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig { SdrSources = [new SdrSourceConfig()] }
        });
        JsonElement root = doc.RootElement;

        root.GetProperty("Logging").GetProperty("Level").GetString().Should().Be("Information");
        root.GetProperty("Logging").GetProperty("File").GetProperty("RollingInterval").GetString().Should().Be("Day");
        root.GetProperty("SdrSources")[0].GetProperty("GainMode").GetString().Should().Be("Manual");
        root.GetProperty("Tracking").GetProperty("ConfidenceLevel").GetString().Should().Be("Medium");
    }

    // === Effective values ===

    [Fact]
    public async Task NetworkAndMlat_ReportTheValidatedValues()
    {
        // A command-line override changes the validated value only, never the loaded one
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig
            {
                Network = new NetworkConfig { BeastOutputPort = 30005, ApiCorsOrigins = [] },
                Mlat = new MlatConfig { InputPort = 30104 }
            },
            BeastOutputPort = 31005,
            ApiCorsOrigins = ["http://dashboard.local:3000"],
            MlatConfig = new MlatConfig { Enabled = false, InputPort = 31104 }
        });
        JsonElement root = doc.RootElement;

        root.GetProperty("Network").GetProperty("BeastOutputPort").GetInt32().Should().Be(31005);
        root.GetProperty("Network").GetProperty("ApiCorsOrigins")[0].GetString().Should().Be("http://dashboard.local:3000");
        root.GetProperty("Mlat").GetProperty("Enabled").GetBoolean().Should().BeFalse();
        root.GetProperty("Mlat").GetProperty("InputPort").GetInt32().Should().Be(31104);
    }

    [Fact]
    public async Task SdrSources_IncludeDisabledDevices()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig
            {
                SdrSources =
                [
                    new SdrSourceConfig { Name = "roof", DeviceIndex = 0 },
                    new SdrSourceConfig { Name = "spare", DeviceIndex = 1, Enabled = false }
                ]
            }
        });
        JsonElement devices = doc.RootElement.GetProperty("SdrSources");

        devices.GetArrayLength().Should().Be(2);
        devices[1].GetProperty("Name").GetString().Should().Be("spare");
        devices[1].GetProperty("Enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UnconfiguredReceiver_IsASectionOfNulls()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig()
        });

        foreach (JsonProperty field in doc.RootElement.GetProperty("Receiver").EnumerateObject())
        {
            field.Value.ValueKind.Should().Be(JsonValueKind.Null, field.Name);
        }
    }

    [Fact]
    public async Task ReceiverUuid_IsRedacted()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            ReceiverUuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000")
        });

        doc.RootElement.GetProperty("Receiver").GetProperty("ReceiverUuid").GetString().Should().Be("(redacted)");
        doc.RootElement.GetRawText().Should().NotContain("550e8400");
    }

    [Fact]
    public async Task DatabaseAutoUpdate_ReportsTheResolvedSettings()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig
            {
                Database = new DatabaseConfig
                {
                    Enabled = true,
                    Path = "/var/lib/aeromux/db",
                    AutoUpdate = new DatabaseAutoUpdateConfig { CheckIntervalHours = 0 }
                }
            }
        });
        JsonElement database = doc.RootElement.GetProperty("Database");

        database.GetProperty("Path").GetString().Should().Be("/var/lib/aeromux/db");
        database.GetProperty("AutoUpdate").GetProperty("CheckIntervalHours").GetInt32()
            .Should().Be(DatabaseAutoUpdateConfig.MinCheckIntervalHours, "the daemon clamps it");
    }

    [Fact]
    public async Task DatabaseWithoutAutoUpdateSection_ShowsItsDefaults()
    {
        using JsonDocument doc = await GetAsync(ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig { Database = new DatabaseConfig { AutoUpdate = null } }
        });
        JsonElement autoUpdate = doc.RootElement.GetProperty("Database").GetProperty("AutoUpdate");

        autoUpdate.GetProperty("Enabled").GetBoolean().Should().BeTrue();
        autoUpdate.GetProperty("CheckIntervalHours").GetInt32().Should().Be(24);
    }

    // === Sources ===

    [Fact]
    public async Task Source_ReportsCliYamlAndDefault()
    {
        using JsonDocument doc = await GetAsync(SourcedConfig(), "?source=true");
        JsonElement network = doc.RootElement.GetProperty("Network");

        Source(network.GetProperty("ApiPort")).Should().Be("Yaml");
        Source(network.GetProperty("BindAddress")).Should().Be("Cli");
        Source(network.GetProperty("ApiEnabled")).Should().Be("Default");
        network.GetProperty("BindAddress").GetProperty("Value").GetString().Should().Be("127.0.0.1");
        network.GetProperty("ApiPort").GetProperty("Value").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task Source_ListItemFieldsAreJudgedOneByOne()
    {
        using JsonDocument doc = await GetAsync(SourcedConfig(), "?source=true");
        JsonElement device = doc.RootElement.GetProperty("SdrSources")[0];

        Source(device.GetProperty("Name")).Should().Be("Yaml");
        Source(device.GetProperty("TunerGain")).Should().Be("Default");
    }

    [Fact]
    public async Task Source_ListReplacedOnTheCommandLine_CoversEveryItem()
    {
        using JsonDocument doc = await GetAsync(SourcedConfig(), "?source=true");
        JsonElement beast = doc.RootElement.GetProperty("BeastSources")[0];

        Source(beast.GetProperty("Host")).Should().Be("Cli");
        Source(beast.GetProperty("Port")).Should().Be("Cli");
    }

    [Fact]
    public async Task Source_WrapsEnumsAsStringsAndNullsAsNull()
    {
        using JsonDocument doc = await GetAsync(SourcedConfig() with { Config = new AeromuxConfig() }, "?source=true");
        JsonElement root = doc.RootElement;

        root.GetProperty("Logging").GetProperty("Level").GetProperty("Value").GetString().Should().Be("Information");
        JsonElement latitude = root.GetProperty("Receiver").GetProperty("Latitude");
        latitude.GetProperty("Value").ValueKind.Should().Be(JsonValueKind.Null);
        Source(latitude).Should().Be("Default");
    }

    [Fact]
    public async Task Source_LeavesVersionTimestampAndConfigFilePlain()
    {
        using JsonDocument doc = await GetAsync(SourcedConfig(), "?source=true");
        JsonElement root = doc.RootElement;

        root.GetProperty("Version").ValueKind.Should().Be(JsonValueKind.String);
        root.GetProperty("Timestamp").ValueKind.Should().Be(JsonValueKind.String);
        root.GetProperty("ConfigFile").GetString().Should().Be("/etc/aeromux/aeromux.yaml");
    }

    // === The source parameter ===

    [Theory]
    [InlineData("?source=TRUE")]
    [InlineData("?source=True")]
    public async Task SourceParameter_IsCaseInsensitive(string query)
    {
        using JsonDocument doc = await GetAsync(SourcedConfig(), query);

        Source(doc.RootElement.GetProperty("Network").GetProperty("ApiPort")).Should().Be("Yaml");
    }

    [Theory]
    [InlineData("?source=false")]
    [InlineData("?source=")]
    public async Task SourceFalseOrEmpty_IsTheSameAsPlain(string query)
    {
        using JsonDocument doc = await GetAsync(SourcedConfig(), query);

        doc.RootElement.GetProperty("Network").GetProperty("ApiPort").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Theory]
    [InlineData("?source=yes", "Invalid source: yes. Must be true or false.")]
    [InlineData("?source=true&source=false", "Parameter 'source' must not be repeated")]
    public async Task SourceParameter_Invalid_Is400(string query, string message)
    {
        await using ApiTestFixture fixture = await ApiTestFixture.CreateAsync();

        HttpResponseMessage response = await fixture.Client.GetAsync($"/api/v1/config{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("Error").GetString().Should().Be(message);
    }

    // === Helpers ===

    /// <summary>
    /// A configuration loaded from a YAML file that writes <c>network.apiPort</c> and one
    /// device's name, run with <c>--bind-address</c> and <c>--beast-source</c>.
    /// </summary>
    private static DaemonValidatedConfig SourcedConfig() =>
        ApiTestFixture.CreateDefaultConfig() with
        {
            Config = new AeromuxConfig { SdrSources = [new SdrSourceConfig { Name = "roof" }] },
            BeastSources = [new BeastSourceConfig { Host = "feeder", Port = 30005 }],
            Origin = new ConfigurationOrigin(
                "/etc/aeromux/aeromux.yaml",
                new HashSet<string> { "network", "network.apiPort", "sdrSources", "sdrSources[0].name" },
                new HashSet<string> { "network.bindAddress", "beastSources" })
        };

    private static async Task<JsonDocument> GetAsync(DaemonValidatedConfig config, string query = "")
    {
        await using ApiTestFixture fixture = await ApiTestFixture.CreateAsync(config);
        HttpResponseMessage response = await fixture.Client.GetAsync($"/api/v1/config{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static IEnumerable<string> Names(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name);

    private static string? Source(JsonElement wrapped) => wrapped.GetProperty("Source").GetString();
}
