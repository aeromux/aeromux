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

namespace Aeromux.Infrastructure.Photos;

/// <summary>
/// Internal DTO for aircraft photo metadata. Stored in the in-memory cache
/// and returned by <see cref="IPlanespottersApiClient"/>. The REST response
/// DTO is a separate type that omits the <see cref="Source"/> field.
/// </summary>
/// <remarks>
/// Two valid shapes:
/// <list type="bullet">
///   <item><description><b>Positive</b>: <see cref="HasPhoto"/> is true; all other fields non-null.</description></item>
///   <item><description><b>Negative</b>: <see cref="HasPhoto"/> is false; all other fields null.</description></item>
/// </list>
/// Transient upstream failures are not represented as a third shape — they're
/// signalled by returning <c>null</c> from the API client.
/// </remarks>
public sealed record PhotoMetadata
{
    /// <summary>True when a photo was found, false for terminal "no photo" results.</summary>
    public required bool HasPhoto { get; init; }

    /// <summary>Direct URL to the thumbnail JPEG on Planespotters' CDN. Non-null iff <see cref="HasPhoto"/> is true.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Photographer's display name. Non-null iff <see cref="HasPhoto"/> is true.</summary>
    public string? Photographer { get; init; }

    /// <summary>URL to the photo's page on planespotters.net (used as the attribution link). Non-null iff <see cref="HasPhoto"/> is true.</summary>
    public string? Link { get; init; }

    /// <summary>
    /// Internal diagnostic only — which lookup path produced this entry: <c>"hex"</c>,
    /// <c>"reg"</c>, or <c>null</c> for negative entries. Logged when investigating
    /// cache misses by lookup path; omitted from the public REST DTO.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>Returns a negative entry (no photo found).</summary>
    /// <returns>A <see cref="PhotoMetadata"/> with <see cref="HasPhoto"/> set to false and all other fields null.</returns>
    public static PhotoMetadata Negative() => new() { HasPhoto = false };

    /// <summary>Returns a positive entry sourced from the hex lookup endpoint.</summary>
    /// <param name="thumbnailUrl">Direct URL to the thumbnail JPEG on Planespotters' CDN.</param>
    /// <param name="photographer">Photographer's display name.</param>
    /// <param name="link">URL to the photo's page on planespotters.net (used as the attribution link).</param>
    /// <returns>A positive <see cref="PhotoMetadata"/> with <see cref="Source"/> set to <c>"hex"</c>.</returns>
    public static PhotoMetadata FromHex(string thumbnailUrl, string photographer, string link) =>
        new()
        {
            HasPhoto = true,
            ThumbnailUrl = thumbnailUrl,
            Photographer = photographer,
            Link = link,
            Source = "hex",
        };

    /// <summary>Returns a positive entry sourced from the registration lookup endpoint.</summary>
    /// <param name="thumbnailUrl">Direct URL to the thumbnail JPEG on Planespotters' CDN.</param>
    /// <param name="photographer">Photographer's display name.</param>
    /// <param name="link">URL to the photo's page on planespotters.net (used as the attribution link).</param>
    /// <returns>A positive <see cref="PhotoMetadata"/> with <see cref="Source"/> set to <c>"reg"</c>.</returns>
    public static PhotoMetadata FromReg(string thumbnailUrl, string photographer, string link) =>
        new()
        {
            HasPhoto = true,
            ThumbnailUrl = thumbnailUrl,
            Photographer = photographer,
            Link = link,
            Source = "reg",
        };
}
