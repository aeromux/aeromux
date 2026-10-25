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

using System.Text.Json;
using System.Text.Json.Nodes;
using Aeromux.Core.Configuration;

namespace Aeromux.CLI.Commands.Daemon.Api;

/// <summary>
/// Builds the response of <c>GET /api/v1/config</c>: the configuration the daemon runs
/// with, in the structure and order of <c>aeromux.yaml</c>, optionally with the layer
/// (default, YAML or command line) each value came from.
/// </summary>
/// <remarks>
/// <para>
/// The values are the effective ones. Network, MLAT, receiver UUID and Beast source
/// overrides from the command line are resolved into <see cref="DaemonValidatedConfig"/>
/// and never written back into the loaded configuration, so those sections are read
/// from the validated values; reading the loaded configuration would report the YAML
/// value under a command-line override. Database auto-update shows its resolved
/// settings, after defaults and clamping.
/// </para>
/// <para>
/// The response is a JSON node tree rather than typed records because its shape depends
/// on the <c>source</c> flag: each value is either written as is or wrapped as
/// <c>{ "Value": …, "Source": … }</c>. One description builds both.
/// </para>
/// </remarks>
internal static class DaemonConfigReport
{
    /// <summary>
    /// Written in place of a configured receiver UUID. MLAT aggregators identify a feeder
    /// by it, so publishing it would let anyone who can reach the API feed timing data
    /// under the operator's identity.
    /// </summary>
    internal const string Redacted = "(redacted)";

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="config">The validated daemon configuration.</param>
    /// <param name="includeSource">Wrap every configuration value with its source.</param>
    /// <returns>The response body.</returns>
    public static JsonObject Build(DaemonValidatedConfig config, bool includeSource)
    {
        ArgumentNullException.ThrowIfNull(config);

        AeromuxConfig loaded = config.Config;
        LoggingConfig logging = loaded.Logging ?? new LoggingConfig();
        TrackingConfig tracking = loaded.Tracking ?? new TrackingConfig();
        ReceiverConfig? receiver = loaded.Receiver;
        DatabaseConfig database = loaded.Database ?? new DatabaseConfig();
        DatabaseAutoUpdateConfig autoUpdate = DatabaseAutoUpdateConfig.Resolve(database.AutoUpdate);

        var report = new Report(config.Origin, includeSource);
        var root = new JsonObject
        {
            ["Version"] = DaemonApiMapper.AppVersion,
            ["Timestamp"] = Serialize(DateTime.UtcNow),
            ["ConfigFile"] = config.Origin.FilePath
        };

        Section loggingSection = report.Section(root, "Logging")
            .Add("Level", logging.Level);
        loggingSection.Child("Console")
            .Add("Enabled", logging.Console.Enabled)
            .Add("Colored", logging.Console.Colored);
        loggingSection.Child("File")
            .Add("Enabled", logging.File.Enabled)
            .Add("Path", logging.File.Path)
            .Add("RollingInterval", logging.File.RollingInterval)
            .Add("RetainedFileCount", logging.File.RetainedFileCount)
            .Add("FileSizeLimitMb", logging.File.FileSizeLimitMb);

        // Every configured device, enabled or not: which ones run is a measurement
        root["SdrSources"] = report.List("SdrSources", loaded.SdrSources ?? [], (item, device) => item
            .Add("Name", device.Name)
            .Add("DeviceIndex", device.DeviceIndex)
            .Add("GainMode", device.GainMode)
            .Add("TunerGain", device.TunerGain)
            .Add("PpmCorrection", device.PpmCorrection)
            .Add("Enabled", device.Enabled)
            .Add("PreambleThreshold", device.PreambleThreshold)
            .Add("DeduplicationWindow", device.DeduplicationWindow)
            .Add("MaxTrackedFrames", device.MaxTrackedFrames));

        root["BeastSources"] = report.List("BeastSources", config.BeastSources, (item, source) => item
            .Add("Host", source.Host)
            .Add("Port", source.Port));

        report.Section(root, "Network")
            .Add("BeastOutputPort", config.BeastOutputPort)
            .Add("BeastOutputEnabled", config.BeastEnabled)
            .Add("JsonOutputPort", config.JsonOutputPort)
            .Add("JsonOutputEnabled", config.JsonEnabled)
            .Add("SbsOutputPort", config.SbsOutputPort)
            .Add("SbsOutputEnabled", config.SbsEnabled)
            .Add("ApiPort", config.ApiPort)
            .Add("ApiEnabled", config.ApiEnabled)
            .Add("ApiCorsOrigins", config.ApiCorsOrigins)
            .Add("BindAddress", config.BindAddress.ToString());

        report.Section(root, "Tracking")
            .Add("ConfidenceLevel", tracking.ConfidenceLevel)
            .Add("IcaoTimeoutSeconds", tracking.IcaoTimeoutSeconds)
            .Add("AircraftTimeoutSeconds", tracking.AircraftTimeoutSeconds)
            .Add("EnablePositionHistory", tracking.EnablePositionHistory)
            .Add("EnableAltitudeHistory", tracking.EnableAltitudeHistory)
            .Add("EnableVelocityHistory", tracking.EnableVelocityHistory)
            .Add("EnableStateHistory", tracking.EnableStateHistory)
            .Add("MaxHistorySize", tracking.MaxHistorySize);

        // An unconfigured receiver is a section of nulls, so the schema never changes
        report.Section(root, "Receiver")
            .Add("Latitude", receiver?.Latitude)
            .Add("Longitude", receiver?.Longitude)
            .Add("Altitude", receiver?.Altitude)
            .Add("Name", receiver?.Name)
            .Add("ReceiverUuid", config.ReceiverUuid.HasValue ? Redacted : null);

        Section databaseSection = report.Section(root, "Database")
            .Add("Enabled", database.Enabled)
            .Add("Path", database.Path);
        databaseSection.Child("AutoUpdate")
            .Add("Enabled", autoUpdate.Enabled)
            .Add("CheckOnStartup", autoUpdate.CheckOnStartup)
            .Add("CheckIntervalHours", autoUpdate.CheckIntervalHours)
            .Add("PruneOldDatabases", autoUpdate.PruneOldDatabases);

        report.Section(root, "Mlat")
            .Add("Enabled", config.MlatConfig.Enabled)
            .Add("InputPort", config.MlatConfig.InputPort);

        report.Section(root, "Heatmap")
            .Add("Collect", (loaded.Heatmap ?? new HeatmapConfig()).Collect);

        return root;
    }

    /// <summary>
    /// Serializes with the API's shared settings, so enums are written as strings exactly
    /// as in every other response.
    /// </summary>
    private static JsonNode? Serialize(object? value) =>
        JsonSerializer.SerializeToNode(value, DaemonJsonOptions.Default);

    /// <summary>
    /// Turns a response field name into the YAML key it reports: the same name in camelCase.
    /// </summary>
    private static string YamlKey(string fieldName) =>
        string.Concat(char.ToLowerInvariant(fieldName[0]).ToString(), fieldName[1..]);

    /// <summary>
    /// Writes values plainly or wrapped with their source.
    /// </summary>
    private sealed class Report(ConfigurationOrigin origin, bool includeSource)
    {
        public JsonNode? Value(object? value, string keyPath)
        {
            JsonNode? node = Serialize(value);
            if (!includeSource)
            {
                return node;
            }

            return new JsonObject
            {
                ["Value"] = node,
                ["Source"] = origin.SourceOf(keyPath).ToString()
            };
        }

        public Section Section(JsonObject parent, string name)
        {
            var node = new JsonObject();
            parent[name] = node;
            return new Section(this, node, YamlKey(name));
        }

        /// <summary>
        /// A list of sections. Its items' key paths carry their index, so each field is
        /// judged on its own, and a command-line option that replaced the list covers them all.
        /// </summary>
        public JsonArray List<T>(string name, IEnumerable<T> items, Action<Section, T> write)
        {
            var array = new JsonArray();
            int index = 0;
            foreach (T item in items)
            {
                var node = new JsonObject();
                write(new Section(this, node, $"{YamlKey(name)}[{index++}]"), item);
                array.Add(node);
            }
            return array;
        }
    }

    /// <summary>
    /// One section of the response, at a YAML key path.
    /// </summary>
    private sealed class Section(Report report, JsonObject node, string keyPath)
    {
        public Section Add(string name, object? value)
        {
            node[name] = report.Value(value, $"{keyPath}.{YamlKey(name)}");
            return this;
        }

        public Section Child(string name)
        {
            var child = new JsonObject();
            node[name] = child;
            return new Section(report, child, $"{keyPath}.{YamlKey(name)}");
        }
    }
}
