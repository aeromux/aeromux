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

using Aeromux.Core.Database;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Discovers installed aeromux-db database files in a configured directory.
/// Scans for files matching the <c>aeromux-db_*.sqlite</c> pattern and reads their metadata.
/// </summary>
public static class DatabaseDiscovery
{
    /// <summary>
    /// Result of database discovery, including the active database and whether multiple files were found.
    /// </summary>
    public class DiscoveryResult
    {
        /// <summary>Gets the installed database (latest version), or <c>null</c> if none found.</summary>
        public InstalledDatabase? Database { get; init; }

        /// <summary>Gets whether multiple database files were found in the directory.</summary>
        public bool MultipleFilesFound { get; init; }
    }

    /// <summary>
    /// Scans the specified directory for installed database files.
    /// If multiple files are found, the one with the latest version is selected.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    /// <returns>Discovery result with the active database and multiple-file flag.</returns>
    public static DiscoveryResult Discover(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Log.Debug("Database directory does not exist: {DirectoryPath}", directoryPath);
            return new DiscoveryResult();
        }

        string[] files = Directory.GetFiles(directoryPath, "aeromux-db_*.sqlite");
        Log.Debug("Found {Count} database file(s) in {DirectoryPath}", files.Length, directoryPath);

        if (files.Length == 0)
        {
            return new DiscoveryResult();
        }

        // Parse versions from filenames and sort to find the latest
        var candidates = new List<(string path, DatabaseVersion version)>();
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            if (DatabaseVersion.TryParseFromFilename(fileName, out DatabaseVersion? version) && version != null)
            {
                candidates.Add((file, version));
            }
            else
            {
                Log.Warning("Database file does not match expected naming pattern: {FileName}", fileName);
            }
        }

        if (candidates.Count == 0)
        {
            return new DiscoveryResult();
        }

        // Select the latest version
        candidates.Sort((a, b) => b.version.CompareTo(a.version));
        (string latestPath, DatabaseVersion latestVersion) = candidates[0];

        bool multipleFiles = candidates.Count > 1;
        if (multipleFiles)
        {
            Log.Warning("Multiple database files found in {DirectoryPath}. Using latest: {Version}",
                directoryPath, latestVersion);
        }

        // Read metadata from the database
        DatabaseMetadata? metadata = ReadMetadata(latestPath);

        // Validate filename version against metadata version if both are available
        if (metadata != null && metadata.DbVersion != latestVersion.VersionString)
        {
            Log.Warning("Database filename version ({FilenameVersion}) differs from metadata version ({MetadataVersion})",
                latestVersion, metadata.DbVersion);
        }

        var fileInfo = new FileInfo(latestPath);

        return new DiscoveryResult
        {
            Database = new InstalledDatabase
            {
                FilePath = latestPath,
                FileName = Path.GetFileName(latestPath),
                VersionFromFilename = latestVersion,
                FileSize = fileInfo.Length,
                Metadata = metadata
            },
            MultipleFilesFound = multipleFiles
        };
    }

    /// <summary>
    /// Reads metadata from the <c>metadata</c> table in a database file.
    /// </summary>
    /// <param name="filePath">Path to the SQLite database file.</param>
    /// <returns>The metadata, or <c>null</c> if the file cannot be read.</returns>
    public static DatabaseMetadata? ReadMetadata(string filePath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
            connection.Open();

            var values = new Dictionary<string, string>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT key, value FROM metadata";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string key = reader.GetString(0);
                string value = reader.GetString(1);
                values[key] = value;
            }

            Log.Debug("Read {Count} metadata entries from {FilePath}", values.Count, filePath);

            return new DatabaseMetadata
            {
                DbVersion = values.GetValueOrDefault("db_version", "unknown"),
                BuildTimestamp = values.GetValueOrDefault("build_timestamp", "unknown"),
                RecordCount = long.TryParse(values.GetValueOrDefault("record_count", "0"), out long count) ? count : 0,
                SchemaVersion = values.GetValueOrDefault("schema_version", "unknown"),
                ToolVersion = values.GetValueOrDefault("tool_version", "unknown")
            };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read metadata from {FilePath}", filePath);
            return null;
        }
    }
}
