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

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Extension methods for reading human-readable display names from enum values
/// annotated with <see cref="JsonStringEnumMemberNameAttribute"/>.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Cache for enum display names to avoid repeated reflection lookups.
    /// </summary>
    private static readonly ConcurrentDictionary<Enum, string> DisplayNameCache = new();

    /// <summary>
    /// Returns the human-readable display name from <see cref="JsonStringEnumMemberNameAttribute"/>,
    /// or falls back to <see cref="Enum.ToString()"/> if no attribute is present.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The display name from the attribute, or the default ToString() representation.</returns>
    public static string ToDisplayString<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return DisplayNameCache.GetOrAdd(value, static v =>
        {
            FieldInfo? member = v.GetType().GetField(v.ToString());
            JsonStringEnumMemberNameAttribute? attr = member?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
            return attr?.Name ?? v.ToString();
        });
    }

    /// <summary>
    /// Returns the human-readable display name for a nullable enum value,
    /// or <c>null</c> if the value is <c>null</c>.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The nullable enum value.</param>
    /// <returns>The display name, or <c>null</c> if the input is <c>null</c>.</returns>
    public static string? ToDisplayString<TEnum>(this TEnum? value) where TEnum : struct, Enum =>
        value?.ToDisplayString();
}
