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
using System.Text.Json;
using Serilog;

namespace Aeromux.Infrastructure.Photos;

/// <summary>
/// Wraps the Planespotters.net public photo lookup API
/// (<c>https://api.planespotters.net/pub/photos/...</c>). No API key required.
/// </summary>
public interface IPlanespottersApiClient
{
    /// <summary>
    /// Looks up the first available photo by 24-bit ICAO hex address.
    /// </summary>
    /// <param name="icao">24-bit ICAO address. Formatted as 6-character uppercase hex when constructing the upstream URL.</param>
    /// <param name="ct">Cancellation token; observed for both the request and the response-body read.</param>
    /// <returns>
    /// A <see cref="PhotoMetadata"/> for terminal results (positive: photo found;
    /// negative: empty array, 404, 410). <c>null</c> for transient failures
    /// (429, other 4xx, 5xx, network error, timeout) — caller must NOT cache.
    /// </returns>
    Task<PhotoMetadata?> GetByHexAsync(uint icao, CancellationToken ct);

    /// <summary>Looks up the first available photo by aircraft registration (tail number).</summary>
    /// <param name="registration">Aircraft registration / tail number (e.g. <c>"EI-DEO"</c>). URL-escaped before being placed in the upstream URL.</param>
    /// <param name="ct">Cancellation token; observed for both the request and the response-body read.</param>
    /// <returns>
    /// Same shape as <see cref="GetByHexAsync"/>: <see cref="PhotoMetadata"/> for terminal
    /// results, <c>null</c> for transient failures the caller must not cache.
    /// </returns>
    Task<PhotoMetadata?> GetByRegAsync(string registration, CancellationToken ct);
}

/// <summary>
/// Default <see cref="IPlanespottersApiClient"/> implementation. Each call creates
/// a fresh <see cref="HttpClient"/> with a 5-second timeout and disposes it after
/// the request — matches the existing <c>GitHubReleaseClient</c> pattern.
/// </summary>
public sealed class PlanespottersApiClient : IPlanespottersApiClient
{
    private const string HexUrlTemplate = "https://api.planespotters.net/pub/photos/hex/{0}";
    private const string RegUrlTemplate = "https://api.planespotters.net/pub/photos/reg/{0}";
    private const string LinkPrefix = "https://www.planespotters.net/";
    private const string CdnPrefix = "https://t.plnspttrs.net/";
    private const string UserAgent = "aeromux";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpMessageHandler? _handlerOverride;

    /// <summary>Creates a client that uses real outbound HTTP for every request.</summary>
    public PlanespottersApiClient()
    {
        _handlerOverride = null;
    }

    /// <summary>
    /// Constructs a client that routes every request through the supplied
    /// <see cref="HttpMessageHandler"/>. Exposed primarily for tests; the handler
    /// is reused across requests and is not disposed by this client.
    /// </summary>
    public PlanespottersApiClient(HttpMessageHandler handler)
    {
        _handlerOverride = handler;
    }

    /// <inheritdoc />
    public Task<PhotoMetadata?> GetByHexAsync(uint icao, CancellationToken ct)
    {
        string url = string.Format(HexUrlTemplate, icao.ToString("X6"));
        return FetchAsync(url, source: "hex", ct);
    }

    /// <inheritdoc />
    public Task<PhotoMetadata?> GetByRegAsync(string registration, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        string url = string.Format(RegUrlTemplate, Uri.EscapeDataString(registration));
        return FetchAsync(url, source: "reg", ct);
    }

    /// <summary>
    /// Issues the upstream request, parses the response, and maps it to a
    /// <see cref="PhotoMetadata"/> (positive/negative) or <c>null</c> (transient).
    /// </summary>
    private async Task<PhotoMetadata?> FetchAsync(string url, string source, CancellationToken ct)
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Log.Warning(ex, "Planespotters request timed out for {Url}", url);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Planespotters request failed for {Url}", url);
            return null;
        }

        using (response)
        {
            return await MapResponseAsync(response, url, source, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Maps an HTTP response to a metadata result per the per-status table in §10 of the design doc.</summary>
    private static async Task<PhotoMetadata?> MapResponseAsync(
        HttpResponseMessage response,
        string url,
        string source,
        CancellationToken ct)
    {
        // Terminal "no photo" — these are cacheable as negative.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PhotoMetadata.Negative();
        }

        // Anything other than 200 is a transient failure — do not cache.
        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("Planespotters returned HTTP {StatusCode} for {Url}",
                (int)response.StatusCode, url);
            return null;
        }

        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Failed to read Planespotters response body for {Url}", url);
            return null;
        }

        return ParseBody(body, source, url);
    }

    /// <summary>
    /// Parses a 200-OK response body. Returns a positive metadata when the first
    /// photo passes the link/host validation, a negative metadata for an empty
    /// <c>photos</c> array, or <c>null</c> if the JSON is malformed (treated as
    /// transient — the caller will retry on the next selection).
    /// </summary>
    private static PhotoMetadata? ParseBody(string body, string source, string url)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Planespotters returned malformed JSON for {Url}", url);
            return null;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("photos", out JsonElement photos) ||
                photos.ValueKind != JsonValueKind.Array)
            {
                // Missing or wrong-shape `photos` field — treat as no photo so we cache
                // the result and don't keep retrying. Per design doc §10 "Planespotters
                // API breaking change" row.
                Log.Warning("Planespotters response missing `photos` array for {Url}", url);
                return PhotoMetadata.Negative();
            }

            foreach (JsonElement photo in photos.EnumerateArray())
            {
                PhotoMetadata? candidate = TryBuildPhoto(photo, source);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return PhotoMetadata.Negative();
        }
    }

    /// <summary>
    /// Builds a positive <see cref="PhotoMetadata"/> from a single photo element if
    /// all required fields are present and pass URL prefix validation. Returns
    /// <c>null</c> (skip-and-try-next) when validation fails.
    /// </summary>
    private static PhotoMetadata? TryBuildPhoto(JsonElement photo, string source)
    {
        string? thumbnailUrl = TryReadString(photo, "thumbnail_large", "src");
        string? photographer = TryReadStringDirect(photo, "photographer");
        string? link = TryReadStringDirect(photo, "link");

        if (string.IsNullOrWhiteSpace(thumbnailUrl) ||
            string.IsNullOrWhiteSpace(photographer) ||
            string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        // Defensive sanitization per design doc §8.1: reject thumbnail URLs not on the
        // Planespotters CDN, and links not on planespotters.net. Cheap insurance against
        // upstream feed corruption being interpolated into our HTML.
        if (!thumbnailUrl.StartsWith(CdnPrefix, StringComparison.Ordinal) ||
            !link.StartsWith(LinkPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return source switch
        {
            "hex" => PhotoMetadata.FromHex(thumbnailUrl, photographer, link),
            "reg" => PhotoMetadata.FromReg(thumbnailUrl, photographer, link),
            _ => null,
        };
    }

    /// <summary>Reads a nested string property (e.g. <c>obj.outer.inner</c>); returns null if any step is missing.</summary>
    private static string? TryReadString(JsonElement obj, string outer, string inner)
    {
        if (!obj.TryGetProperty(outer, out JsonElement outerEl) ||
            outerEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return TryReadStringDirect(outerEl, inner);
    }

    /// <summary>Reads a top-level string property; returns null if missing or wrong type.</summary>
    private static string? TryReadStringDirect(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out JsonElement el) ||
            el.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return el.GetString();
    }

    /// <summary>Constructs a fresh <see cref="HttpClient"/> for one request (or wraps the test handler when injected).</summary>
    private HttpClient CreateClient()
    {
        HttpClient client = _handlerOverride is not null
            ? new HttpClient(_handlerOverride, disposeHandler: false)
            : new HttpClient();

        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }
}
