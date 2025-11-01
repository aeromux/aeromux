// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aeromux.Infrastructure.Configuration;

/// <summary>
/// Loads and validates Aeromux configuration from YAML files (ADR-003).
/// Uses YamlDotNet for deserialization with camelCase naming convention.
/// Uses Serilog for structured logging (ADR-007).
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
    public AeromuxConfig LoadFromFile(string path)
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

        // Deserialize YAML into strongly-typed configuration object
        // Enums are automatically converted from lowercase strings (e.g., "debug" -> LogEventLevel.Debug)
        AeromuxConfig config = deserializer.Deserialize<AeromuxConfig>(yaml);

        // Log successful load with device count if devices were specified
        int deviceCount = config.Devices?.Count ?? 0;
        Log.Information("Configuration loaded successfully: {DeviceCount} device(s)", deviceCount);

        return config;
    }
}
