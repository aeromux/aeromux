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

using Aeromux.Core.Tracking;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Provides aircraft enrichment data by querying the aeromux-db SQLite database.
/// Opens a read-only connection at construction and keeps it open for the lifetime of the service.
/// Designed for single-threaded access from the tracker's consumer task.
/// </summary>
public sealed class AircraftDatabaseLookupService : IAircraftDatabaseLookup, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteCommand _lookupCommand;
    private readonly SqliteParameter _icaoParameter;
    private readonly int _schemaVersion;
    private bool _disposed;

    /// <summary>
    /// Creates a new lookup service with an open read-only connection to the database.
    /// </summary>
    /// <param name="databaseFilePath">Full path to the aeromux-db SQLite file.</param>
    /// <param name="schemaVersion">The database schema version, used to adapt queries and mapping for different schema versions.</param>
    public AircraftDatabaseLookupService(string databaseFilePath, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(databaseFilePath);

        _connection = new SqliteConnection($"Data Source={databaseFilePath};Mode=ReadOnly");
        _connection.Open();
        _schemaVersion = schemaVersion;

        // Pre-create parameterized lookup command — reused across all lookups
        _lookupCommand = _connection.CreateCommand();
        _lookupCommand.CommandText = "SELECT * FROM aircraft_view WHERE aircraft_icao_address = @icao";
        _icaoParameter = _lookupCommand.Parameters.Add("@icao", SqliteType.Text);

        Log.Debug("Database lookup service opened: {FilePath}, schema version {SchemaVersion}",
            databaseFilePath, schemaVersion);
    }

    /// <inheritdoc />
    public AircraftDatabaseRecord LookupAircraft(string icaoAddress)
    {
        ArgumentNullException.ThrowIfNull(icaoAddress);

        try
        {
            _icaoParameter.Value = icaoAddress;
            using SqliteDataReader reader = _lookupCommand.ExecuteReader();
            return !reader.Read() ? AircraftDatabaseRecord.Empty : MapToRecord(reader, _schemaVersion);
        }
        catch (SqliteException ex)
        {
            Log.Error(ex, "Database lookup failed for ICAO {IcaoAddress}", icaoAddress);
            return AircraftDatabaseRecord.Empty;
        }
    }

    /// <summary>
    /// Maps a database row from the <c>aircraft_view</c> to an <see cref="AircraftDatabaseRecord"/>.
    /// Adapts to the database schema version — newer schemas may provide additional fields.
    /// </summary>
    /// <param name="reader">The data reader positioned on a row.</param>
    /// <param name="schemaVersion">The database schema version for conditional field mapping.</param>
    private static AircraftDatabaseRecord MapToRecord(SqliteDataReader reader, int schemaVersion)
    {
        return new AircraftDatabaseRecord
        {
            Registration = GetStringOrNull(reader, "aircraft_registration"),
            Country = GetStringOrNull(reader, "aircraft_country"),
            TypeCode = GetStringOrNull(reader, "aircraft_type_code"),
            TypeDescription = GetStringOrNull(reader, "type_description"),
            TypeIcaoClass = GetStringOrNull(reader, "type_icao_class"),
            Model = GetStringOrNull(reader, "model"),
            ManufacturerIcao = GetStringOrNull(reader, "aircraft_manufacturer_icao"),
            ManufacturerName = GetStringOrNull(reader, "manufacturer_name"),
            OperatorName = GetStringOrNull(reader, "operator_name"),
            Pia = GetBoolOrNull(reader, "faa_pia"),
            Ladd = GetBoolOrNull(reader, "faa_ladd"),
            Military = GetBoolOrNull(reader, "military")
        };
    }

    /// <summary>
    /// Gets a string column value, or null if the column is DBNull or not present.
    /// </summary>
    private static string? GetStringOrNull(SqliteDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Gets a boolean column value (from SQLite integer 0/1), or null if the column is DBNull or not present.
    /// </summary>
    private static bool? GetBoolOrNull(SqliteDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal) != 0;
    }

    /// <summary>
    /// Disposes the cached lookup command and closes the database connection.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lookupCommand.Dispose();
        _connection.Dispose();
        Log.Debug("Database lookup service closed");
    }
}
