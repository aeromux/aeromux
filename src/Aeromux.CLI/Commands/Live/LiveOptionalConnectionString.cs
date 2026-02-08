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

using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands.Live;

/// <summary>
/// Optional connection string that supports both flag usage (--connect) and value usage (--connect HOST:PORT).
/// </summary>
/// <remarks>
/// When used as flag, defaults to empty string which ParseConnectionString interprets as localhost:30005.
/// Implements IFlagValue to enable Spectre.Console.Cli's flag parsing behavior.
/// </remarks>
public sealed class LiveOptionalConnectionString : IFlagValue
{
    private string _value = string.Empty;
    private bool _isSet;

    /// <summary>
    /// Gets or sets the connection string value.
    /// </summary>
    public object? Value
    {
        get => _value;
        set => _value = value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets whether the flag was explicitly set by the user.
    /// </summary>
    public bool IsSet
    {
        get => _isSet;
        set => _isSet = value;
    }

    /// <summary>
    /// Gets the underlying type of the value (always string).
    /// </summary>
    public Type Type => typeof(string);

    /// <summary>
    /// Creates an instance for flag usage without a value (--connect).
    /// </summary>
    /// <returns>An OptionalConnectionString with empty value and IsSet=true.</returns>
    public static LiveOptionalConnectionString FromFlag() =>
        new() { _value = string.Empty, _isSet = true };

    /// <summary>
    /// Creates an instance with a specific connection string value (--connect HOST:PORT).
    /// </summary>
    /// <param name="value">The connection string value.</param>
    /// <returns>An OptionalConnectionString with the specified value and IsSet=true.</returns>
    public static LiveOptionalConnectionString FromValue(string value) =>
        new() { _value = value, _isSet = true };

    /// <summary>
    /// Implicitly converts OptionalConnectionString to string for convenient usage.
    /// </summary>
    /// <param name="connection">The OptionalConnectionString to convert.</param>
    /// <returns>The connection string value, or null if connection is null.</returns>
    public static implicit operator string?(LiveOptionalConnectionString? connection) =>
        connection?._value;
}
