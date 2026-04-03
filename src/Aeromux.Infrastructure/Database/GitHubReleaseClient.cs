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
using System.Security.Authentication;
using System.Text.Json;
using Serilog;

namespace Aeromux.Infrastructure.Database;

/// <summary>
/// Client for fetching release metadata from the aeromux-db GitHub repository.
/// Uses the public GitHub Releases API (no authentication required).
/// </summary>
public static class GitHubReleaseClient
{
    private const string RepoOwner = "aeromux";
    private const string RepoName = "aeromux-db";
    private const string BaseUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases";
    private const string UserAgent = "aeromux";

    /// <summary>
    /// Result of a GitHub API call — either a release or an error.
    /// </summary>
    public class Result
    {
        /// <summary>Gets the release info if the call succeeded.</summary>
        public GitHubReleaseInfo? Release { get; init; }

        /// <summary>Gets the error message if the call failed.</summary>
        public string? Error { get; init; }

        /// <summary>Gets whether the call succeeded.</summary>
        public bool Success => Release != null;
    }

    /// <summary>
    /// Fetches the latest release from GitHub.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing the latest release info or an error.</returns>
    public static async Task<Result> GetLatestReleaseAsync(CancellationToken cancellationToken = default) =>
        await FetchReleaseAsync($"{BaseUrl}/latest", cancellationToken);

    /// <summary>
    /// Fetches a specific release by tag name.
    /// </summary>
    /// <param name="tagName">The release tag (e.g., <c>2026.1.w08_r1</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing the release info or an error.</returns>
    public static async Task<Result> GetReleaseByTagAsync(string tagName, CancellationToken cancellationToken = default) =>
        await FetchReleaseAsync($"{BaseUrl}/tags/{tagName}", cancellationToken);

    /// <summary>
    /// Fetches and parses a single release from the given GitHub API URL.
    /// Handles rate limiting, network errors, and not-found responses.
    /// </summary>
    private static async Task<Result> FetchReleaseAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

        Log.Debug("Fetching GitHub release from {Url}", url);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            Log.Debug(ex, "SSL certificate validation failed for {Url}", url);
            return new Result { Error = "SSL certificate validation failed. Install ca-certificates: sudo apt install ca-certificates" };
        }
        catch (HttpRequestException ex)
        {
            Log.Debug(ex, "HTTP request failed for {Url}", url);
            return new Result { Error = "Unable to reach the GitHub API. Check your network connection and try again." };
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Log.Debug(ex, "HTTP request timed out for {Url}", url);
            return new Result { Error = "Unable to reach the GitHub API. Check your network connection and try again." };
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests:
                Log.Debug("GitHub API rate limit exceeded (HTTP {StatusCode})", (int)response.StatusCode);
                return new Result { Error = "GitHub API rate limit exceeded. Try again later." };
            case HttpStatusCode.NotFound:
                Log.Debug("GitHub release not found at {Url}", url);
                return new Result { Error = null, Release = null };
        }

        if (!response.IsSuccessStatusCode)
        {
            Log.Debug("GitHub API returned HTTP {StatusCode} for {Url}", (int)response.StatusCode, url);
            return new Result { Error = $"GitHub API returned HTTP {(int)response.StatusCode}." };
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        Log.Debug("GitHub API response: {ResponseLength} bytes", json.Length);

        return ParseRelease(json);
    }

    /// <summary>
    /// Parses a GitHub release JSON response and extracts the SQLite asset metadata.
    /// </summary>
    private static Result ParseRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string tagName = root.GetProperty("tag_name").GetString()!;
            DateTimeOffset publishedAt = root.GetProperty("published_at").GetDateTimeOffset();

            // Find the SQLite asset
            JsonElement assets = root.GetProperty("assets");
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString()!;
                if (!name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                long size = asset.GetProperty("size").GetInt64();
                string downloadUrl = asset.GetProperty("browser_download_url").GetString()!;

                // The digest field contains the SHA-256 hash (format: "sha256:<hex>")
                string digest = string.Empty;
                if (asset.TryGetProperty("digest", out JsonElement digestElement))
                {
                    digest = digestElement.GetString() ?? string.Empty;
                }

                Log.Debug("Parsed release: Tag={Tag}, Asset={Asset}, Size={Size}, Digest={Digest}",
                    tagName, name, size, digest);

                return new Result
                {
                    Release = new GitHubReleaseInfo
                    {
                        TagName = tagName,
                        PublishedAt = publishedAt,
                        AssetName = name,
                        AssetSize = size,
                        AssetUrl = downloadUrl,
                        AssetDigest = digest
                    }
                };
            }

            return new Result { Error = "No SQLite asset found in the release." };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to parse GitHub release JSON");
            return new Result { Error = "Failed to parse GitHub API response." };
        }
    }
}
