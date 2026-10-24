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
using System.Text.Json.Serialization;

namespace Aeromux.CLI.Commands.Daemon.Api;

/// <summary>
/// The JSON settings shared by everything the daemon's web server writes: REST responses,
/// SignalR pushes to the Web Map, and the push service's change detection.
/// </summary>
/// <remarks>
/// The Web Map reads the same models over REST and over SignalR, so the two must agree
/// on every setting; a field written differently on one path would look like a different
/// value to the client. Keeping a single definition makes that agreement structural.
/// The JSON broadcast stream (<c>JsonEncoder</c>) is a separate protocol and keeps its own.
/// </remarks>
internal static class DaemonJsonOptions
{
    /// <summary>
    /// A read-only instance with the shared settings, for serializing directly.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    /// <summary>
    /// Applies the shared settings to options the framework owns, which keep their other
    /// defaults: property names as declared (PascalCase), enums as their display strings,
    /// and null values always written, so every response shows its full schema.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = null;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions();
        Apply(options);

        // Read-only options must name their type resolver; this fills in the reflection-based
        // one that serialization would otherwise pick on first use.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
