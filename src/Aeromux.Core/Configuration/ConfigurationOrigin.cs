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

using Aeromux.Core.Configuration.Enums;

namespace Aeromux.Core.Configuration;

/// <summary>
/// Records where the resolved configuration came from: which YAML file was loaded, which
/// keys it writes, and which keys command-line options set.
/// </summary>
/// <remarks>
/// Keys are paths written with the YAML key names (camelCase), joined by <c>.</c>, with
/// <c>[i]</c> for sequence items: <c>network.apiPort</c>, <c>sdrSources[0].tunerGain</c>.
/// A key whose value is a sequence of scalars, such as <c>network.apiCorsOrigins</c>, is a
/// single path. Whether a key is written is recorded from the YAML structure, because a
/// deserialized object cannot tell a written value from its initializer.
/// </remarks>
/// <param name="FilePath">Absolute path of the loaded YAML file, or null when none was loaded.</param>
/// <param name="YamlKeys">Every key path written in the YAML file, sections included.</param>
/// <param name="CliKeys">Every key path a command-line option set.</param>
public sealed record ConfigurationOrigin(
    string? FilePath,
    IReadOnlySet<string> YamlKeys,
    IReadOnlySet<string> CliKeys)
{
    /// <summary>
    /// An origin with no file and no keys: every value is a default.
    /// </summary>
    public static ConfigurationOrigin None { get; } = new(
        null,
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Determines which layer the value at a key path came from. A command-line option wins
    /// when it set the path or any list or section containing it (<c>--beast-source</c> sets
    /// <c>beastSources</c>, which covers <c>beastSources[1].host</c>); otherwise the YAML file
    /// wins when it writes the path; otherwise the value is a default.
    /// </summary>
    /// <param name="path">The key path, for example <c>network.apiPort</c>.</param>
    /// <returns>The layer that supplied the value.</returns>
    public ConfigurationSource SourceOf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        for (string? current = path; current != null; current = Parent(current))
        {
            if (CliKeys.Contains(current))
            {
                return ConfigurationSource.Cli;
            }
        }

        return YamlKeys.Contains(path) ? ConfigurationSource.Yaml : ConfigurationSource.Default;
    }

    /// <summary>
    /// Returns a copy with additional command-line keys.
    /// </summary>
    /// <param name="keys">Key paths set by command-line options.</param>
    /// <returns>A new origin whose <see cref="CliKeys"/> include <paramref name="keys"/>.</returns>
    public ConfigurationOrigin WithCliKeys(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var merged = new HashSet<string>(CliKeys, StringComparer.Ordinal);
        merged.UnionWith(keys);
        return this with { CliKeys = merged };
    }

    /// <summary>
    /// Removes the last <c>.segment</c> or <c>[i]</c> from a key path, or returns null for a
    /// top-level key.
    /// </summary>
    private static string? Parent(string path)
    {
        int cut = path.LastIndexOfAny(['.', '[']);
        return cut > 0 ? path[..cut] : null;
    }
}
