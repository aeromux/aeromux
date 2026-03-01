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

using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Performs integrity verification checks on aeromux-db database files.
/// Supports SHA-256 checksum verification, SQLite structural integrity, and record count validation.
/// </summary>
public static class IntegrityChecker
{
    /// <summary>
    /// Result of a single integrity check.
    /// </summary>
    public class CheckResult
    {
        /// <summary>Gets whether the check passed.</summary>
        public required bool Passed { get; init; }

        /// <summary>Gets the expected value (for display on failure).</summary>
        public string? Expected { get; init; }

        /// <summary>Gets the actual value (for display on failure).</summary>
        public string? Actual { get; init; }
    }

    /// <summary>
    /// Verifies the SHA-256 checksum of a database file against the expected GitHub digest.
    /// </summary>
    /// <param name="filePath">Path to the database file.</param>
    /// <param name="expectedDigest">Expected digest in <c>sha256:&lt;hex&gt;</c> format.</param>
    /// <returns>The check result with pass/fail and expected/actual values.</returns>
    public static CheckResult VerifySha256(string filePath, string expectedDigest)
    {
        ArgumentNullException.ThrowIfNull(expectedDigest);

        // Parse expected hex from "sha256:<hex>" format
        string expectedHex = expectedDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? expectedDigest["sha256:".Length..]
            : expectedDigest;

        Log.Debug("Computing SHA-256 for {FilePath}", filePath);

        using FileStream stream = File.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);
        string actualHex = Convert.ToHexString(hash).ToLowerInvariant();

        Log.Debug("SHA-256 computed: {ActualHash}, expected: {ExpectedHash}", actualHex, expectedHex);

        bool passed = string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase);
        return new CheckResult
        {
            Passed = passed,
            Expected = expectedHex,
            Actual = actualHex
        };
    }

    /// <summary>
    /// Runs <c>PRAGMA integrity_check</c> on the database file to detect structural corruption.
    /// </summary>
    /// <param name="filePath">Path to the database file.</param>
    /// <returns>The check result.</returns>
    public static CheckResult VerifySqliteIntegrity(string filePath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";

            string? result = command.ExecuteScalar()?.ToString();
            Log.Debug("PRAGMA integrity_check result: {Result}", result);

            bool passed = string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            return new CheckResult
            {
                Passed = passed,
                Actual = passed ? null : result
            };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "SQLite integrity check failed for {FilePath}", filePath);
            return new CheckResult
            {
                Passed = false,
                Actual = ex.Message
            };
        }
    }

    /// <summary>
    /// Verifies the record count in the <c>aircrafts</c> table against the expected count from metadata.
    /// </summary>
    /// <param name="filePath">Path to the database file.</param>
    /// <param name="expectedCount">Expected record count from the <c>record_count</c> metadata key.</param>
    /// <returns>The check result with expected/actual counts on failure.</returns>
    public static CheckResult VerifyRecordCount(string filePath, long expectedCount)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM aircrafts";

            long actualCount = (long)command.ExecuteScalar()!;
            Log.Debug("Record count: expected={Expected}, actual={Actual}", expectedCount, actualCount);

            bool passed = actualCount == expectedCount;
            return new CheckResult
            {
                Passed = passed,
                Expected = expectedCount.ToString("N0"),
                Actual = actualCount.ToString("N0")
            };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Record count verification failed for {FilePath}", filePath);
            return new CheckResult
            {
                Passed = false,
                Expected = expectedCount.ToString("N0"),
                Actual = ex.Message
            };
        }
    }
}
