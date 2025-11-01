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
/// Provides global access to the loaded application configuration.
/// Configuration is set once during application startup by ConfigurationInterceptor.
/// This allows any class in the application to access configuration without parameter passing.
/// </summary>
public static class ConfigurationProvider
{
    private static AeromuxConfig? _config;

    /// <summary>
    /// Gets or sets the current application configuration.
    /// The setter should only be called once during application startup by ConfigurationInterceptor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration has not been loaded yet.</exception>
    public static AeromuxConfig Current
    {
        get => _config ?? throw new InvalidOperationException(
            "Configuration not loaded. Ensure ConfigurationInterceptor has run before accessing configuration.");
        set => _config = value;
    }

    /// <summary>
    /// Clears the loaded configuration.
    /// Primarily used for testing to reset state between test runs.
    /// </summary>
    internal static void Reset() => _config = null;
}
