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
using Serilog;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aeromux.Infrastructure.Configuration;

/// <summary>
/// Loads and validates Aeromux configuration from YAML files.
/// Uses YamlDotNet for deserialization with camelCase naming convention.
/// Uses Serilog for structured logging.
/// </summary>
public class YamlConfigurationLoader : IYamlConfigurationLoader
{
    /// <summary>
    /// Loads configuration from a YAML file and validates it.
    /// </summary>
    /// <param name="path">The absolute or relative path to the YAML configuration file.</param>
    /// <returns>A validated AeromuxConfig instance.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration validation fails.</exception>
    public AeromuxConfig LoadFromFile(string path) => LoadDocument(path).Config;

    /// <summary>
    /// Loads configuration from a YAML file together with the key paths it writes.
    /// </summary>
    /// <param name="path">The absolute or relative path to the YAML configuration file.</param>
    /// <returns>The deserialized configuration and every key path written in the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
    public YamlConfigurationDocument LoadDocument(string path)
    {
        Log.Debug("Loading configuration from {ConfigPath}", path);

        // Verify file exists before attempting to read
        // Infrastructure layer just throws - boundary layer will handle logging
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}", path);
        }

        // Read the entire YAML file into memory
        string yaml = File.ReadAllText(path);

        // Configure YamlDotNet deserializer with camelCase convention
        // This allows YAML keys like "centerFrequency" to map to C# properties like "CenterFrequency"
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // Deserialize YAML into strongly-typed configuration object.
        // Enums are converted from strings case-insensitively (e.g., "debug" -> LogEventLevel.Debug).
        // An empty or comment-only file has no document, for which YamlDotNet returns null; that
        // is a file that writes nothing, so every section keeps its default.
        AeromuxConfig config = deserializer.Deserialize<AeromuxConfig?>(yaml) ?? new AeromuxConfig();

        // Log successful load with source counts
        int sdrCount = config.SdrSources?.Count ?? 0;
        int beastCount = config.BeastSources?.Count ?? 0;
        Log.Information("Configuration loaded successfully: {SdrCount} SDR source(s), {BeastCount} Beast source(s)",
            sdrCount, beastCount);

        // Debug: Log receiver config status
        if (config.Receiver != null)
        {
            Log.Debug("Receiver config loaded: Lat={Lat}, Lon={Lon}, Alt={Alt}, Name={Name}",
                config.Receiver.Latitude, config.Receiver.Longitude,
                config.Receiver.Altitude, config.Receiver.Name);
        }
        else
        {
            Log.Debug("Receiver config is null after YAML deserialization");
        }

        return new YamlConfigurationDocument(config, ReadKeyPaths(yaml));
    }

    /// <summary>
    /// Collects every mapping key written in the YAML text as a key path (see
    /// <see cref="ConfigurationOrigin"/>). The deserialized object cannot answer this, because
    /// a key the file omits and a key it writes with the default value look the same there.
    /// </summary>
    /// <param name="yaml">The YAML text, already known to deserialize.</param>
    /// <returns>The key paths, sections included.</returns>
    private static IReadOnlySet<string> ReadKeyPaths(string yaml)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode root)
        {
            CollectMapping(root, prefix: null, paths);
        }

        return paths;
    }

    /// <summary>
    /// Records each key of a mapping and descends into nested mappings and into the mapping
    /// items of sequences. A sequence of scalars stays a single path.
    /// </summary>
    private static void CollectMapping(YamlMappingNode mapping, string? prefix, HashSet<string> paths)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode { Value: { } key })
            {
                continue;
            }

            string path = prefix is null ? key : $"{prefix}.{key}";
            paths.Add(path);

            switch (entry.Value)
            {
                case YamlMappingNode child:
                    CollectMapping(child, path, paths);
                    break;

                case YamlSequenceNode sequence:
                    for (int i = 0; i < sequence.Children.Count; i++)
                    {
                        if (sequence.Children[i] is YamlMappingNode item)
                        {
                            string itemPath = $"{path}[{i}]";
                            paths.Add(itemPath);
                            CollectMapping(item, itemPath, paths);
                        }
                    }

                    break;
            }
        }
    }
}
