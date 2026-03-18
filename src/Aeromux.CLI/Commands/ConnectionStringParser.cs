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

using System.Net;
using System.Text.RegularExpressions;
using Aeromux.Core.Configuration;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Parses connection strings into (host, port) tuples for Beast-compatible sources.
/// Supports: "HOST:PORT", ":PORT", "PORT", "HOST", "IP", or empty (defaults to localhost:30005).
/// Shared by both daemon and live commands.
/// </summary>
public static class ConnectionStringParser
{
    /// <summary>
    /// Parses a connection string into a (host, port) tuple.
    /// </summary>
    /// <param name="connectString">Connection string in format "HOST:PORT", ":PORT", "PORT", "HOST", "IP", or null for default.</param>
    /// <returns>Tuple of (host, port) parsed from connection string, or ("localhost", 30005) if null/empty.</returns>
    /// <exception cref="ArgumentException">Thrown when port number is invalid or format is incorrect.</exception>
    /// <remarks>
    /// Default port 30005 follows the Beast protocol convention used by readsb, dump1090, and dump1090-fa.
    /// Numeric-only input is interpreted as a port number (not a hostname), so "30005" resolves to localhost:30005.
    /// Host validation accepts IPv4, IPv6, and DNS hostnames via Uri.CheckHostName.
    /// </remarks>
    public static (string Host, int Port) Parse(string? connectString)
    {
        // Default if no value provided (e.g., bare --beast-source with no argument)
        if (string.IsNullOrWhiteSpace(connectString))
        {
            return ("localhost", 30005);
        }

        // Parse HOST:PORT or just PORT or just HOST/IP
        string[] parts = connectString.Split(':');

        switch (parts.Length)
        {
            case 1:
            {
                // Could be just port (30005) or just host (192.168.1.1 or example.com)
                string value = parts[0].TrimStart(':');

                // Try to parse as port number first
                if (int.TryParse(value, out int port) && port is > 0 and <= 65535)
                {
                    // It's a port number — use localhost
                    return ("localhost", port);
                }

                // It's a hostname or IP address — validate and use default port
                if (IsValidHost(value))
                {
                    return (value, 30005);
                }

                throw new ArgumentException($"Invalid hostname or IP address: {value}");
            }
            case 2:
            {
                // HOST:PORT format
                string host = parts[0];

                if (!IsValidHost(host))
                {
                    throw new ArgumentException($"Invalid hostname or IP address: {host}");
                }

                if (int.TryParse(parts[1], out int port) && port is > 0 and <= 65535)
                {
                    return (host, port);
                }

                throw new ArgumentException($"Invalid port number: {parts[1]}");
            }
            default:
                // Too many colons (e.g., host:port:extra)
                throw new ArgumentException($"Invalid connection string format: {connectString}");
        }
    }

    /// <summary>
    /// Parses multiple connection strings into a list of Beast source configurations.
    /// Validates each string and checks for duplicates.
    /// </summary>
    /// <param name="connectionStrings">Array of connection strings to parse.</param>
    /// <returns>List of parsed BeastSourceConfig instances.</returns>
    /// <exception cref="ArgumentException">Thrown when any string is invalid or duplicates are found.</exception>
    public static List<BeastSourceConfig> ParseMultiple(string[]? connectionStrings)
    {
        if (connectionStrings == null || connectionStrings.Length == 0)
        {
            return [];
        }

        var results = new List<BeastSourceConfig>();

        // Track seen host:port pairs for duplicate detection (case-insensitive)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string connectionString in connectionStrings)
        {
            (string host, int port) = Parse(connectionString);
            string key = $"{host.ToLowerInvariant()}:{port}";

            if (!seen.Add(key))
            {
                throw new ArgumentException($"Duplicate Beast source: {host}:{port}");
            }

            results.Add(new BeastSourceConfig { Host = host, Port = port });
        }

        return results;
    }

    /// <summary>
    /// RFC 1123 DNS hostname regex: each label is alphanumeric + hyphens, 1-63 chars,
    /// must start and end with alphanumeric. Labels separated by dots, total max 253 chars.
    /// </summary>
    private static readonly Regex DnsHostnameRegex = new(
        @"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates whether a string is a valid hostname or IP address.
    /// </summary>
    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // Check if it's a valid IP address (IPv4 or IPv6)
        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        // Check if it's a valid DNS hostname (RFC 1123)
        return host.Length <= 253 && DnsHostnameRegex.IsMatch(host);
    }
}
