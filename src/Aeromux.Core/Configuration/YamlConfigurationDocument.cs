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

namespace Aeromux.Core.Configuration;

/// <summary>
/// A loaded YAML configuration file: its deserialized content and the key paths it writes.
/// </summary>
/// <param name="Config">The deserialized configuration; sections the file does not write are null.</param>
/// <param name="KeyPaths">Every key path written in the file, in the format of <see cref="ConfigurationOrigin"/>.</param>
public sealed record YamlConfigurationDocument(AeromuxConfig Config, IReadOnlySet<string> KeyPaths);
