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

using Serilog;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Outcome category of a database update check.
/// </summary>
public enum DatabaseUpdateStatus
{
    /// <summary>The installed database already matches the latest release and passed integrity checks.</summary>
    UpToDate,

    /// <summary>A newer database was downloaded, verified, and installed.</summary>
    Updated,

    /// <summary>The operation was canceled (e.g. Ctrl+C or daemon shutdown). No changes were made.</summary>
    Cancelled,

    /// <summary>The check or update failed (network, rate limit, integrity, or IO error). No changes were made.</summary>
    Failed
}

/// <summary>
/// Result of a database update check.
/// </summary>
/// <param name="Status">The outcome category.</param>
/// <param name="Version">The latest release tag (for <see cref="DatabaseUpdateStatus.UpToDate"/> / <see cref="DatabaseUpdateStatus.Updated"/>).</param>
/// <param name="InstalledPath">The installed file path (for <see cref="DatabaseUpdateStatus.Updated"/>).</param>
/// <param name="RecordCount">The record count of the installed database (for <see cref="DatabaseUpdateStatus.Updated"/>).</param>
/// <param name="Error">A human-readable reason (for <see cref="DatabaseUpdateStatus.Failed"/>).</param>
public sealed record DatabaseUpdateResult(
    DatabaseUpdateStatus Status,
    string? Version = null,
    string? InstalledPath = null,
    long RecordCount = 0,
    string? Error = null);

/// <summary>
/// Reusable orchestration of the database update sequence: fetch the latest release, compare with the
/// installed database, download and verify if needed, and install atomically. Used by the
/// <c>database update</c> command (with console reporters) and the daemon auto-updater (silent).
/// </summary>
public interface IDatabaseUpdateService
{
    /// <summary>
    /// Checks for a newer database and installs it if available.
    /// </summary>
    /// <param name="databaseDirectory">Directory where the database file is stored.</param>
    /// <param name="progress">Optional download-progress sink. <c>null</c> for a silent download.</param>
    /// <param name="status">Optional textual stage sink (one call per line). <c>null</c> to run silently.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    Task<DatabaseUpdateResult> CheckAndUpdateAsync(
        string databaseDirectory,
        IProgress<DownloadProgress>? progress = null,
        Action<string>? status = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDatabaseUpdateService"/> implementation over the existing
/// <see cref="GitHubReleaseClient"/>, <see cref="DatabaseDiscovery"/>, <see cref="DatabaseDownloader"/>,
/// and <see cref="IntegrityChecker"/> building blocks.
/// </summary>
public sealed class DatabaseUpdateService : IDatabaseUpdateService
{
    /// <inheritdoc />
    public async Task<DatabaseUpdateResult> CheckAndUpdateAsync(
        string databaseDirectory,
        IProgress<DownloadProgress>? progress = null,
        Action<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(databaseDirectory);

        void Report(string line = "") => status?.Invoke(line);

        try
        {
            // Resolve the latest published release — establishes the version and digest to compare against.
            Report("Fetching latest release information...");
            GitHubReleaseClient.Result releaseResult = await GitHubReleaseClient.GetLatestReleaseAsync(cancellationToken);

            if (!releaseResult.Success)
            {
                Report($"Error: {releaseResult.Error}");
                return new DatabaseUpdateResult(DatabaseUpdateStatus.Failed, Error: releaseResult.Error);
            }

            GitHubReleaseInfo release = releaseResult.Release!;
            Report($"Latest version: {release.TagName}");

            // Compare against the installed copy so an up-to-date database skips the (large) download.
            DatabaseDiscovery.DiscoveryResult discovery = DatabaseDiscovery.Discover(databaseDirectory);
            InstalledDatabase? installed = discovery.Database;

            if (installed != null &&
                installed.VersionFromFilename.VersionString == release.TagName)
            {
                // Same version — verify integrity
                Report();
                Report("Installed version matches the latest release. Verifying integrity...");

                bool integrityOk = VerifyIntegrity(installed.FilePath, release.AssetDigest, installed.Metadata, Report);

                if (integrityOk)
                {
                    Report();
                    Report("Database is up-to-date.");
                    return new DatabaseUpdateResult(DatabaseUpdateStatus.UpToDate, Version: release.TagName);
                }

                // Integrity failed — re-download
                Report();
                Report("Integrity check failed. Re-downloading...");
            }

            // A newer version (or a failed integrity re-check on the current one) — fetch to a temp
            // file first so a corrupt or interrupted download never replaces a good database.
            Report();
            Report($"Downloading {release.AssetName}...");

            DatabaseDownloader.DownloadResult downloadResult =
                await DatabaseDownloader.DownloadToTempFileAsync(release.AssetUrl, release.AssetName, release.AssetSize, progress, cancellationToken);

            if (downloadResult.Cancelled)
            {
                Report();
                Report("Download cancelled. No changes were made.");
                return new DatabaseUpdateResult(DatabaseUpdateStatus.Cancelled);
            }

            if (!downloadResult.Success)
            {
                Report($"Error: {downloadResult.Error}");
                return new DatabaseUpdateResult(DatabaseUpdateStatus.Failed, Error: downloadResult.Error);
            }

            string tempFile = downloadResult.FilePath!;

            try
            {
                // Verify downloaded file
                Report();
                Report("Verifying download integrity...");

                // Read metadata from downloaded file for record count check
                DatabaseMetadata? downloadedMetadata = DatabaseDiscovery.ReadMetadata(tempFile);

                bool downloadIntegrityOk = VerifyIntegrity(tempFile, release.AssetDigest, downloadedMetadata, Report);

                if (!downloadIntegrityOk)
                {
                    Report();
                    Report("Error: Download integrity check failed. The downloaded file has been discarded.");
                    Report("The previous database (if any) is unchanged. Try running the command again.");
                    DatabaseDownloader.CleanupTempFile(tempFile);
                    return new DatabaseUpdateResult(DatabaseUpdateStatus.Failed, Error: "Download integrity check failed");
                }

                // Validate asset filename — defense in depth against path traversal via compromised release metadata
                if (release.AssetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    release.AssetName.Contains("..") ||
                    release.AssetName != Path.GetFileName(release.AssetName))
                {
                    Report($"Error: Invalid asset filename: {release.AssetName}");
                    DatabaseDownloader.CleanupTempFile(tempFile);
                    return new DatabaseUpdateResult(DatabaseUpdateStatus.Failed, Error: $"Invalid asset filename: {release.AssetName}");
                }

                // Atomic move into place — the verified temp file replaces the previous database in
                // one step, so a reader never sees a partially written file.
                string installedPath = DatabaseDownloader.InstallDatabase(tempFile, databaseDirectory, release.AssetName);

                Report();
                Report($"Database installed: {release.AssetName}");
                Report($"  Path: {Path.GetFullPath(installedPath)}");

                Log.Information("Database installed: {FileName} at {Path}", release.AssetName, installedPath);

                // Check for older database files
                string[] existingFiles = Directory.GetFiles(databaseDirectory, "aeromux-db_*.sqlite");
                if (existingFiles.Length > 1)
                {
                    Report();
                    Report($"Note: Previous database files are still in {Path.GetFullPath(databaseDirectory)} and can be removed manually.");
                }

                long recordCount = downloadedMetadata?.RecordCount ?? 0;
                return new DatabaseUpdateResult(DatabaseUpdateStatus.Updated, Version: release.TagName, InstalledPath: installedPath, RecordCount: recordCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Debug(ex, "Failed to install database");
                DatabaseDownloader.CleanupTempFile(tempFile);
                Report($"Error: Failed to install database: {ex.Message}");
                return new DatabaseUpdateResult(DatabaseUpdateStatus.Failed, Error: $"Failed to install database: {ex.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation (Ctrl+C or daemon shutdown) — no changes were made.
            return new DatabaseUpdateResult(DatabaseUpdateStatus.Cancelled);
        }
    }

    /// <summary>
    /// Runs the SHA-256, SQLite-integrity, and record-count checks with formatted status output.
    /// Short-circuits on first failure.
    /// </summary>
    /// <returns><c>true</c> if all checks pass.</returns>
    private static bool VerifyIntegrity(string filePath, string expectedDigest, DatabaseMetadata? metadata, Action<string> report)
    {
        // SHA-256
        if (!string.IsNullOrEmpty(expectedDigest))
        {
            IntegrityChecker.CheckResult sha256Result = IntegrityChecker.VerifySha256(filePath, expectedDigest);
            if (sha256Result.Passed)
            {
                report("  SHA-256 checksum: OK");
            }
            else
            {
                report("  SHA-256 checksum: FAILED");
                report($"  Expected: {sha256Result.Expected}");
                report($"  Actual:   {sha256Result.Actual}");
                Log.Information("SHA-256 verification failed for {FilePath}: expected={Expected}, actual={Actual}",
                    filePath, sha256Result.Expected, sha256Result.Actual);
                return false;
            }
        }

        // SQLite integrity
        IntegrityChecker.CheckResult sqliteResult = IntegrityChecker.VerifySqliteIntegrity(filePath);
        if (sqliteResult.Passed)
        {
            report("  SQLite integrity: OK");
        }
        else
        {
            report("  SQLite integrity: FAILED");
            Log.Information("SQLite integrity check failed for {FilePath}", filePath);
            return false;
        }

        // Record count
        if (metadata != null)
        {
            IntegrityChecker.CheckResult recordResult = IntegrityChecker.VerifyRecordCount(filePath, metadata.RecordCount);
            if (recordResult.Passed)
            {
                report($"  Record count:     OK ({metadata.RecordCount:N0})");
            }
            else
            {
                report($"  Record count:     FAILED (expected {recordResult.Expected}, found {recordResult.Actual})");
                Log.Information("Record count verification failed for {FilePath}: expected={Expected}, actual={Actual}",
                    filePath, recordResult.Expected, recordResult.Actual);
                return false;
            }
        }

        return true;
    }
}
