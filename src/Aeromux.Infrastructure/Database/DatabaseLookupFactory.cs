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

using Aeromux.Core.Configuration;
using Serilog;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Factory for creating <see cref="AircraftDatabaseLookupService"/> instances with startup validation.
/// Encapsulates discovery, integrity checking, and schema version verification.
/// </summary>
public static class DatabaseLookupFactory
{
    /// <summary>
    /// Maximum database schema version that this version of aeromux can handle.
    /// If the database has a higher schema version, enrichment is disabled.
    /// </summary>
    public const int MaxSupportedSchemaVersion = 1;

    /// <summary>
    /// Attempts to create a database lookup service after validating the database.
    /// Returns null if the database is not configured, not found, not readable, corrupt, or has an unsupported schema version.
    /// </summary>
    /// <param name="databaseConfig">Database configuration from the application config.</param>
    /// <returns>A ready-to-use lookup service, or null if validation fails.</returns>
    public static AircraftDatabaseLookupService? TryCreate(DatabaseConfig? databaseConfig)
    {
        // Check if database is configured
        if (databaseConfig == null || !databaseConfig.Enabled || string.IsNullOrEmpty(databaseConfig.Path))
        {
            Log.Debug("Database path not configured, enrichment disabled");
            return null;
        }

        // Discover database file
        DatabaseDiscovery.DiscoveryResult discovery = DatabaseDiscovery.Discover(databaseConfig.Path);
        if (discovery.Database == null)
        {
            Log.Warning("Database file not found at {Path}, enrichment disabled", databaseConfig.Path);
            return null;
        }

        InstalledDatabase db = discovery.Database;

        // Verify file is readable before running further checks
        try
        {
            using FileStream _ = File.Open(db.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (UnauthorizedAccessException)
        {
            string fullPath = Path.GetFullPath(db.FilePath);
            Log.Warning(
                "Database file {FilePath} is not readable — check file ownership. " +
                "Enrichment disabled. To fix, run: sudo chown aeromux:aeromux {FilePath}",
                fullPath, fullPath);
            return null;
        }

        // Verify SQLite integrity
        IntegrityChecker.CheckResult integrityResult = IntegrityChecker.VerifySqliteIntegrity(db.FilePath);
        if (!integrityResult.Passed)
        {
            Log.Warning("Database integrity check failed, enrichment disabled");
            return null;
        }

        // Verify schema version
        if (db.Metadata == null)
        {
            Log.Warning("Database metadata not available, enrichment disabled");
            return null;
        }

        if (!int.TryParse(db.Metadata.SchemaVersion, out int schemaVersion))
        {
            Log.Warning("Database schema version '{SchemaVersion}' is not a valid integer, enrichment disabled",
                db.Metadata.SchemaVersion);
            return null;
        }

        if (schemaVersion > MaxSupportedSchemaVersion)
        {
            Log.Warning(
                "Database schema version {SchemaVersion} not supported (max supported: {MaxSupported}), enrichment disabled — update aeromux",
                schemaVersion, MaxSupportedSchemaVersion);
            return null;
        }

        // All checks passed — create the lookup service
        try
        {
            Log.Information("Database loaded: version {Version}, {RecordCount} records",
                db.Metadata.DbVersion, db.Metadata.RecordCount);

            return new AircraftDatabaseLookupService(db.FilePath, schemaVersion);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open database {FilePath}, enrichment disabled", db.FilePath);
            return null;
        }
    }
}
