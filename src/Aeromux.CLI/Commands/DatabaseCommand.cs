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

using Aeromux.CLI.Commands.Database;
using Aeromux.Core.Configuration;
using Aeromux.Infrastructure.Database;
using Serilog;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// CLI command for managing the aeromux aircraft metadata database.
/// Supports <c>update</c> and <c>info</c> actions.
/// </summary>
public class DatabaseCommand : AsyncCommand<DatabaseSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, DatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string action = settings.Action.ToLowerInvariant();

        return action switch
        {
            "update" => await ExecuteUpdateAsync(settings, cancellationToken),
            "info" => await ExecuteInfoAsync(settings, cancellationToken),
            _ => HandleInvalidAction(action)
        };
    }

    /// <summary>
    /// Resolves the database directory path from CLI option or YAML configuration.
    /// </summary>
    /// <returns>The resolved path, or <c>null</c> if not configured.</returns>
    private static string? ResolveDatabasePath(DatabaseSettings settings)
    {
        // CLI --database takes precedence
        if (!string.IsNullOrWhiteSpace(settings.DatabasePath))
        {
            return settings.DatabasePath;
        }

        // Fall back to YAML configuration
        try
        {
            AeromuxConfig config = ConfigurationProvider.Current;
            return config.Database?.Path;
        }
        catch (InvalidOperationException)
        {
            // Configuration not loaded (no --config and no default config file)
            return null;
        }
    }

    /// <summary>
    /// Checks whether the <c>--database</c> CLI option was explicitly provided.
    /// </summary>
    private static bool WasDatabaseCliProvided(DatabaseSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.DatabasePath);

    /// <summary>
    /// Checks whether <c>database.enabled</c> is <c>true</c> in the resolved configuration.
    /// </summary>
    private static bool IsDatabaseEnabled()
    {
        try
        {
            return ConfigurationProvider.Current.Database?.Enabled ?? false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that the resolved path is a directory and is writable.
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <returns>An error message, or <c>null</c> if validation passes.</returns>
    private static string? ValidateDirectory(string path)
    {
        // Check if the path points to an existing file (not a directory)
        if (File.Exists(path))
        {
            return $"The database path {path} is a file, not a directory.";
        }

        // Create directory if it doesn't exist
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            return $"Cannot create database directory {path}: {ex.Message}";
        }

        // Verify writability by testing a temp file
        try
        {
            string testFile = Path.Combine(path, $".aeromux-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
        }
        catch
        {
            return $"The database directory {path} is not writable.";
        }

        return null;
    }

    /// <summary>
    /// Reports an unknown action and returns a failure exit code.
    /// </summary>
    private static int HandleInvalidAction(string action)
    {
        Console.WriteLine($"Error: Unknown action '{action}'. Valid actions are: update, info");
        return 1;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  UPDATE action
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the <c>update</c> action: fetches the latest release, compares with the installed
    /// database, downloads if needed, verifies integrity, and installs atomically.
    /// </summary>
    private static async Task<int> ExecuteUpdateAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        // Resolve and validate path
        string? dbPath = ResolveDatabasePath(settings);
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            Console.WriteLine("Error: No database path configured.");
            Console.WriteLine("Provide --database <path> or set database.path in the configuration file (--config).");
            return 1;
        }

        string? dirError = ValidateDirectory(dbPath);
        if (dirError != null)
        {
            Console.WriteLine($"Error: {dirError}");
            return 1;
        }

        // Fetch latest release
        Console.WriteLine("Fetching latest release information...");
        GitHubReleaseClient.Result releaseResult = await GitHubReleaseClient.GetLatestReleaseAsync(cancellationToken);

        if (!releaseResult.Success)
        {
            Console.WriteLine($"Error: {releaseResult.Error}");
            return 1;
        }

        GitHubReleaseInfo release = releaseResult.Release!;
        Console.WriteLine($"Latest version: {release.TagName}");

        // Check installed database
        DatabaseDiscovery.DiscoveryResult discovery = DatabaseDiscovery.Discover(dbPath);
        InstalledDatabase? installed = discovery.Database;

        if (installed != null &&
            installed.VersionFromFilename.VersionString == release.TagName)
        {
            // Same version — verify integrity
            Console.WriteLine();
            Console.WriteLine("Installed version matches the latest release. Verifying integrity...");

            bool integrityOk = VerifyIntegrityWithOutput(installed.FilePath, release.AssetDigest, installed.Metadata);

            if (integrityOk)
            {
                Console.WriteLine();
                Console.WriteLine("Database is up-to-date.");
                return 0;
            }

            // Integrity failed — re-download
            Console.WriteLine();
            Console.WriteLine("Integrity check failed. Re-downloading...");
        }

        // Download
        Console.WriteLine();
        Console.WriteLine($"Downloading {release.AssetName}...");

        DatabaseDownloader.DownloadResult downloadResult =
            await DatabaseDownloader.DownloadToTempFileAsync(release.AssetUrl, release.AssetName, release.AssetSize, cancellationToken);

        if (downloadResult.Cancelled)
        {
            Console.WriteLine();
            Console.WriteLine("Download cancelled. No changes were made.");
            return 1;
        }

        if (!downloadResult.Success)
        {
            Console.WriteLine($"Error: {downloadResult.Error}");
            return 1;
        }

        string tempFile = downloadResult.FilePath!;

        try
        {
            // Verify downloaded file
            Console.WriteLine();
            Console.WriteLine("Verifying download integrity...");

            // Read metadata from downloaded file for record count check
            DatabaseMetadata? downloadedMetadata = DatabaseDiscovery.ReadMetadata(tempFile);

            bool downloadIntegrityOk = VerifyIntegrityWithOutput(tempFile, release.AssetDigest, downloadedMetadata);

            if (!downloadIntegrityOk)
            {
                Console.WriteLine();
                Console.WriteLine("Error: Download integrity check failed. The downloaded file has been discarded.");
                Console.WriteLine("The previous database (if any) is unchanged. Try running the command again.");
                DatabaseDownloader.CleanupTempFile(tempFile);
                return 1;
            }

            // Install atomically
            string installedPath = DatabaseDownloader.InstallDatabase(tempFile, dbPath, release.AssetName);

            Console.WriteLine();
            Console.WriteLine($"Database installed: {release.AssetName}");
            Console.WriteLine($"  Path: {Path.GetFullPath(installedPath)}");

            Log.Information("Database installed: {FileName} at {Path}", release.AssetName, installedPath);

            // Check for older database files
            string[] existingFiles = Directory.GetFiles(dbPath, "aeromux-db_*.sqlite");
            if (existingFiles.Length > 1)
            {
                Console.WriteLine();
                Console.WriteLine($"Note: Previous database files are still in {Path.GetFullPath(dbPath)} and can be removed manually.");
            }

            // Check if database.enabled is false and --database was not used
            if (!WasDatabaseCliProvided(settings) && !IsDatabaseEnabled())
            {
                Console.WriteLine();
                Console.WriteLine("Note: Database support is currently disabled. To use the database for aircraft enrichment,");
                Console.WriteLine("set database.enabled to true in the configuration file, or pass --database <path> to");
                Console.WriteLine("daemon/live commands (which implicitly enables database support).");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to install database");
            DatabaseDownloader.CleanupTempFile(tempFile);
            Console.WriteLine($"Error: Failed to install database: {ex.Message}");
            return 1;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  INFO action
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the <c>info</c> action: displays installed database metadata with integrity checks
    /// and latest release availability from GitHub.
    /// </summary>
    private static async Task<int> ExecuteInfoAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        string? dbPath = ResolveDatabasePath(settings);
        bool hasPath = !string.IsNullOrWhiteSpace(dbPath);

        // Fetch latest release (done early so we have it for both sections)
        GitHubReleaseClient.Result releaseResult = await GitHubReleaseClient.GetLatestReleaseAsync(cancellationToken);
        bool githubAvailable = releaseResult.Success;
        GitHubReleaseInfo? release = releaseResult.Release;

        // ── Installed database section ──
        if (hasPath)
        {
            DatabaseDiscovery.DiscoveryResult discovery = DatabaseDiscovery.Discover(dbPath!);

            if (discovery.MultipleFilesFound)
            {
                Console.WriteLine($"Warning: Multiple database files found in {Path.GetFullPath(dbPath!)}.");
                Console.WriteLine("Using the latest version. Older files can be removed manually.");
                Console.WriteLine();
            }

            InstalledDatabase? installed = discovery.Database;
            bool anyIntegrityFailed = false;

            if (installed == null)
            {
                Console.WriteLine("Installed database:");
                Console.WriteLine($"  No database found in {Path.GetFullPath(dbPath!)}");
            }
            else if (installed.Metadata == null)
            {
                // File exists but cannot be read — check if it's a permission issue
                Console.WriteLine("Installed database:");
                Console.WriteLine($"  Version:          {installed.VersionFromFilename} (from filename)");
                Console.WriteLine($"  Path:             {Path.GetFullPath(installed.FilePath)}");
                Console.WriteLine($"  File size:        {FormatBytes(installed.FileSize)}");

                if (!IsFileReadable(installed.FilePath))
                {
                    Console.WriteLine("  Error:            Permission denied — the database file is not readable.");
                    Console.WriteLine($"                    To fix, run: sudo chown aeromux:aeromux {Path.GetFullPath(installed.FilePath)}");
                }
                else
                {
                    Console.WriteLine("  Error:            Unable to read the database file. It may be corrupted.");
                }
            }
            else
            {
                Console.WriteLine("Installed database:");
                Console.WriteLine($"  Version:          {installed.Metadata.DbVersion}");
                Console.WriteLine($"  Build:            {installed.Metadata.BuildTimestamp}");
                Console.WriteLine($"  Records:          {installed.Metadata.RecordCount:N0}");
                Console.WriteLine($"  Path:             {Path.GetFullPath(installed.FilePath)}");
                Console.WriteLine($"  File size:        {FormatBytes(installed.FileSize)}");

                // Integrity checks
                // SHA-256: need the GitHub release for this version
                bool sha256Passed = true; // default to true if skipped
                if (!githubAvailable)
                {
                    Console.WriteLine("  SHA-256:          (skipped — GitHub unavailable)");
                }
                else if (release != null && release.TagName == installed.VersionFromFilename.VersionString)
                {
                    // Installed version matches latest release — use its digest
                    IntegrityChecker.CheckResult sha256Result =
                        IntegrityChecker.VerifySha256(installed.FilePath, release.AssetDigest);
                    PrintSha256Result(sha256Result);
                    sha256Passed = sha256Result.Passed;
                }
                else
                {
                    // Try to fetch the specific release by tag
                    GitHubReleaseClient.Result tagResult =
                        await GitHubReleaseClient.GetReleaseByTagAsync(installed.VersionFromFilename.VersionString, cancellationToken);

                    if (tagResult is { Success: true, Release: not null })
                    {
                        IntegrityChecker.CheckResult sha256Result =
                            IntegrityChecker.VerifySha256(installed.FilePath, tagResult.Release.AssetDigest);
                        PrintSha256Result(sha256Result);
                        sha256Passed = sha256Result.Passed;
                    }
                    else
                    {
                        Console.WriteLine("  SHA-256:          (skipped — release no longer available on GitHub)");
                    }
                }

                // SQLite integrity
                IntegrityChecker.CheckResult sqliteResult =
                    IntegrityChecker.VerifySqliteIntegrity(installed.FilePath);
                Console.WriteLine($"  SQLite integrity: {(sqliteResult.Passed ? "OK" : "FAILED")}");

                // Record count
                IntegrityChecker.CheckResult recordResult =
                    IntegrityChecker.VerifyRecordCount(installed.FilePath, installed.Metadata.RecordCount);
                Console.WriteLine(recordResult.Passed
                    ? $"  Record count:     OK ({installed.Metadata.RecordCount:N0})"
                    : $"  Record count:     FAILED (expected {recordResult.Expected}, found {recordResult.Actual})");

                // Track whether any integrity check failed (used for footer message)
                anyIntegrityFailed = !sha256Passed || !sqliteResult.Passed || !recordResult.Passed;
            }

            Console.WriteLine();

            // ── Latest release section ──
            if (!githubAvailable)
            {
                Console.WriteLine("Latest release:");
                Console.WriteLine("  Unable to reach the GitHub API. Update availability could not be checked.");
            }
            else if (release != null)
            {
                Console.WriteLine("Latest release:");
                Console.WriteLine($"  Version:          {release.TagName}");
                Console.WriteLine($"  Published:        {release.PublishedAt:yyyy-MM-dd}");
                Console.WriteLine($"  File size:        {FormatBytes(release.AssetSize)}");

                // Determine update message
                if (installed == null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Run 'aeromux database update' to install.");
                }
                else if (installed.Metadata == null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Run 'aeromux database update' to re-download the database.");
                }
                else if (installed.VersionFromFilename.VersionString == release.TagName)
                {
                    Console.WriteLine();
                    Console.WriteLine(anyIntegrityFailed
                        ? "Warning: Integrity check failed. Run 'aeromux database update' to re-download."
                        : "Database is up-to-date.");
                }
                else
                {
                    // Check if installed version is still on GitHub
                    GitHubReleaseClient.Result tagCheck =
                        await GitHubReleaseClient.GetReleaseByTagAsync(installed.VersionFromFilename.VersionString, cancellationToken);

                    Console.WriteLine();
                    if (!tagCheck.Success || tagCheck.Release == null)
                    {
                        Console.WriteLine("Warning: Installed version is outdated and no longer available on GitHub.");
                        Console.WriteLine("Run 'aeromux database update' to install the latest version.");
                    }
                    else
                    {
                        Console.WriteLine("Update available. Run 'aeromux database update' to install.");
                    }
                }
            }
        }
        else
        {
            // No path configured — show latest release only
            if (!githubAvailable)
            {
                Console.WriteLine("Error: No database path configured and unable to reach the GitHub API.");
                Console.WriteLine("Provide --database <path> or set database.path in the configuration file.");
                return 1;
            }

            if (release != null)
            {
                Console.WriteLine("Latest release:");
                Console.WriteLine($"  Version:          {release.TagName}");
                Console.WriteLine($"  Published:        {release.PublishedAt:yyyy-MM-dd}");
                Console.WriteLine($"  File size:        {FormatBytes(release.AssetSize)}");
                Console.WriteLine();
                Console.WriteLine("Note: Provide --database <path> or set database.path in the configuration file");
                Console.WriteLine("to show installed database details and integrity status.");
            }
        }

        return 0;
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs all three integrity checks with formatted console output.
    /// Short-circuits on first failure in update mode.
    /// </summary>
    /// <returns><c>true</c> if all checks pass.</returns>
    private static bool VerifyIntegrityWithOutput(string filePath, string expectedDigest, DatabaseMetadata? metadata)
    {
        // SHA-256
        if (!string.IsNullOrEmpty(expectedDigest))
        {
            IntegrityChecker.CheckResult sha256Result = IntegrityChecker.VerifySha256(filePath, expectedDigest);
            if (sha256Result.Passed)
            {
                Console.WriteLine("  SHA-256 checksum: OK");
            }
            else
            {
                Console.WriteLine("  SHA-256 checksum: FAILED");
                Console.WriteLine($"  Expected: {sha256Result.Expected}");
                Console.WriteLine($"  Actual:   {sha256Result.Actual}");
                Log.Information("SHA-256 verification failed for {FilePath}: expected={Expected}, actual={Actual}",
                    filePath, sha256Result.Expected, sha256Result.Actual);
                return false;
            }
        }

        // SQLite integrity
        IntegrityChecker.CheckResult sqliteResult = IntegrityChecker.VerifySqliteIntegrity(filePath);
        if (sqliteResult.Passed)
        {
            Console.WriteLine("  SQLite integrity: OK");
        }
        else
        {
            Console.WriteLine("  SQLite integrity: FAILED");
            Log.Information("SQLite integrity check failed for {FilePath}", filePath);
            return false;
        }

        // Record count
        if (metadata != null)
        {
            IntegrityChecker.CheckResult recordResult = IntegrityChecker.VerifyRecordCount(filePath, metadata.RecordCount);
            if (recordResult.Passed)
            {
                Console.WriteLine($"  Record count:     OK ({metadata.RecordCount:N0})");
            }
            else
            {
                Console.WriteLine($"  Record count:     FAILED (expected {recordResult.Expected}, found {recordResult.Actual})");
                Log.Information("Record count verification failed for {FilePath}: expected={Expected}, actual={Actual}",
                    filePath, recordResult.Expected, recordResult.Actual);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Prints the SHA-256 verification result as a single-line OK/FAILED status.
    /// </summary>
    private static void PrintSha256Result(IntegrityChecker.CheckResult result) =>
        Console.WriteLine(result.Passed ? "  SHA-256:          OK" : "  SHA-256:          FAILED");

    /// <summary>
    /// Checks whether a file can be opened for reading by the current process.
    /// </summary>
    /// <param name="filePath">Full path to the file to check.</param>
    /// <returns><c>true</c> if the file is readable; <c>false</c> if access is denied.</returns>
    private static bool IsFileReadable(string filePath)
    {
        try
        {
            using FileStream _ = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., <c>142.8 MB</c>).
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
