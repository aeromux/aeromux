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

namespace Aeromux.Core.Configuration;

/// <summary>
/// Interface for YAML configuration loader to allow testability.
/// This interface abstracts the YAML loading implementation,
/// enabling ConfigurationBuilder to be tested with mock configurations.
/// </summary>
public interface IYamlConfigurationLoader
{
    /// <summary>
    /// Loads configuration from a YAML file.
    /// </summary>
    /// <param name="path">The absolute or relative path to the YAML configuration file.</param>
    /// <returns>The loaded configuration, or a configuration with null sections if not specified in YAML.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when YAML parsing fails.</exception>
    AeromuxConfig LoadFromFile(string path);
}
