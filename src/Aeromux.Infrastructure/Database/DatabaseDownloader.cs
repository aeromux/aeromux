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
/// Progress update for an in-flight database download.
/// </summary>
/// <param name="BytesRead">Bytes downloaded so far.</param>
/// <param name="TotalBytes">Total expected bytes (from the response, or the caller-supplied size).</param>
public readonly record struct DownloadProgress(long BytesRead, long TotalBytes);

/// <summary>
/// Downloads database assets from GitHub releases with progress reporting and atomic installation.
/// Uses a temporary file for download, then moves to the target path on success.
/// </summary>
public static class DatabaseDownloader
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Result of a download operation.
    /// </summary>
    public class DownloadResult
    {
        /// <summary>Gets whether the download succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Gets the path where the file was saved (on success).</summary>
        public string? FilePath { get; init; }

        /// <summary>Gets the error message (on failure).</summary>
        public string? Error { get; init; }

        /// <summary>Gets whether the download was canceled (by the user, or by a shutdown/cancellation token).</summary>
        public bool Cancelled { get; init; }
    }

    /// <summary>
    /// Downloads a database asset to a temporary file, displaying progress.
    /// The caller is responsible for moving the temp file to the final location.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from.</param>
    /// <param name="assetName">The asset filename (for display).</param>
    /// <param name="totalSize">The expected total size in bytes (used when the response omits a length).</param>
    /// <param name="progress">Optional progress sink. When <c>null</c> the download is silent (daemon path).</param>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation.</param>
    /// <returns>The download result with the temp file path on success.</returns>
    public static async Task<DownloadResult> DownloadToTempFileAsync(
        string downloadUrl,
        string assetName,
        long totalSize,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempFile = Path.GetTempFileName();
        Log.Debug("Downloading {AssetName} to temp file {TempFile}", assetName, tempFile);

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "aeromux");

            using HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long contentLength = response.Content.Headers.ContentLength ?? totalSize;

            await using Stream downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream fileStream = new(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[BufferSize];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await downloadStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                progress?.Report(new DownloadProgress(totalBytesRead, contentLength));
            }

            Log.Debug("Download complete: {TotalBytes} bytes written to {TempFile}", totalBytesRead, tempFile);

            return new DownloadResult
            {
                Success = true,
                FilePath = tempFile
            };
        }
        catch (OperationCanceledException)
        {
            CleanupTempFile(tempFile);
            return new DownloadResult { Cancelled = true };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Download failed for {Url}", downloadUrl);
            CleanupTempFile(tempFile);
            return new DownloadResult { Error = $"Download failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Moves a downloaded file to the target directory with the specified filename.
    /// Creates the target directory if it does not exist.
    /// </summary>
    /// <param name="tempFilePath">Path to the temporary downloaded file.</param>
    /// <param name="targetDirectory">The target directory.</param>
    /// <param name="targetFileName">The target filename.</param>
    /// <returns>The full path of the installed file.</returns>
    public static string InstallDatabase(string tempFilePath, string targetDirectory, string targetFileName)
    {
        Directory.CreateDirectory(targetDirectory);
        string targetPath = Path.Combine(targetDirectory, targetFileName);

        Log.Debug("Installing database: {TempFile} -> {TargetPath}", tempFilePath, targetPath);
        File.Move(tempFilePath, targetPath, overwrite: true);

        return targetPath;
    }

    /// <summary>
    /// Cleans up a temporary file, logging any errors but not throwing.
    /// </summary>
    /// <param name="tempFilePath">Path to the temporary file to delete.</param>
    public static void CleanupTempFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
                Log.Debug("Cleaned up temp file: {TempFile}", tempFilePath);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to clean up temp file: {TempFile}", tempFilePath);
        }
    }

    /// <summary>
    /// Deletes every <c>aeromux-db_*.sqlite</c> file in the directory except the kept file.
    /// Best-effort: per-file failures are logged and ignored. Used by the daemon auto-updater to
    /// remove superseded databases after a successful swap.
    /// </summary>
    /// <param name="directory">The database directory.</param>
    /// <param name="keepFilePath">Path of the file to keep (the just-installed database).</param>
    /// <returns>The number of files actually deleted (for the caller's summary log).</returns>
    public static int DeleteSupersededDatabases(string directory, string keepFilePath)
    {
        string keepName = Path.GetFileName(keepFilePath);

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "aeromux-db_*.sqlite");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to enumerate database files in {Directory} for pruning", directory);
            return 0;
        }

        int deleted = 0;
        foreach (string file in files)
        {
            if (string.Equals(Path.GetFileName(file), keepName, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
                Log.Debug("Removed superseded database file {File}", file);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to remove superseded database file {File}", file);
            }
        }

        return deleted;
    }
}
