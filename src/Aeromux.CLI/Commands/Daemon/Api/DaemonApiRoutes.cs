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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Photos;
using Aeromux.Infrastructure.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Aeromux.CLI.Commands.Daemon.Api;

/// <summary>
/// Maps all REST API route handlers for the daemon API.
/// </summary>
public static partial class DaemonApiRoutes
{
    /// <summary>
    /// Valid 6-character hexadecimal ICAO address pattern.
    /// </summary>
    [GeneratedRegex(@"^[0-9A-Fa-f]{6}$")]
    private static partial Regex IcaoPattern();

    /// <summary>
    /// Valid section names for the detail endpoint (case-insensitive lookup).
    /// </summary>
    private static readonly HashSet<string> ValidSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Identification", "DatabaseRecord", "Status", "Position", "VelocityAndDynamics",
        "Autopilot", "Meteorology", "Acas", "Capabilities", "DataQuality"
    };

    /// <summary>
    /// Valid history type names (case-insensitive lookup).
    /// </summary>
    private static readonly HashSet<string> ValidHistoryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Position", "Altitude", "Velocity", "State"
    };

    /// <summary>
    /// Maps all API routes to the WebApplication.
    /// </summary>
    /// <param name="app">The web application to register routes on.</param>
    /// <param name="tracker">Aircraft state tracker for data queries.</param>
    /// <param name="photoService">Aircraft photo service backing the <c>/api/v1/aircraft/{icao}/photo</c> endpoint.</param>
    /// <param name="getStatistics">Function to get current stream statistics.</param>
    /// <param name="startTime">Daemon start time for uptime calculation.</param>
    /// <param name="config">Validated daemon configuration for receiver and device metadata.</param>
    /// <param name="jsonOptions">Shared JSON serializer options for dynamic response dictionaries.</param>
    public static void MapRoutes(
        WebApplication app,
        IAircraftStateTracker tracker,
        IAircraftPhotoService photoService,
        Func<StreamStatistics?> getStatistics,
        DateTime startTime,
        DaemonValidatedConfig config,
        JsonSerializerOptions jsonOptions)
    {
        // GET /api/v1/aircraft: aircraft list (with optional bounds/search filtering)
        app.MapGet("/api/v1/aircraft", (HttpContext httpContext) =>
        {
            if (!TryGetSingleQueryValue(httpContext, "bounds", out string? boundsParam, out IResult? error) ||
                !TryGetSingleQueryValue(httpContext, "search", out string? searchParam, out error))
            {
                return error;
            }

            // Mutual exclusivity check
            if (!string.IsNullOrEmpty(boundsParam) && !string.IsNullOrEmpty(searchParam))
            {
                return Results.BadRequest(new ErrorResponse("Parameters 'bounds' and 'search' are mutually exclusive"));
            }

            IReadOnlyList<Aircraft> allAircraft = tracker.GetAllAircraft();
            IEnumerable<Aircraft> filtered;

            if (!string.IsNullOrEmpty(boundsParam))
            {
                // Parse bounds=south,west,north,east (west > east crosses the 180° meridian)
                if (!TryParseBounds(boundsParam, out GeographicBounds bounds, out string? boundsError))
                {
                    return Results.BadRequest(new ErrorResponse(boundsError));
                }

                filtered = allAircraft.Where(a =>
                    a.Position.Coordinate is not null && bounds.Contains(a.Position.Coordinate));
            }
            else if (!string.IsNullOrEmpty(searchParam))
            {
                // Case-insensitive substring match against ICAO, callsign, squawk, registration
                string query = searchParam.Trim();
                filtered = allAircraft.Where(a =>
                    a.Identification.ICAO.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (a.Identification.Callsign?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.Identification.Squawk?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.DatabaseEnabled && a.DatabaseRecord.Registration?.Contains(query, StringComparison.OrdinalIgnoreCase) == true));
            }
            else
            {
                filtered = allAircraft;
            }

            AircraftListItem[] items = filtered.Select(DaemonApiMapper.ToListItem).ToArray();

            return Results.Ok(new AircraftListResponse(
                Count: items.Length,
                Timestamp: DateTime.UtcNow,
                Aircraft: items));
        });

        // GET /api/v1/aircraft/{icao}: aircraft detail
        app.MapGet("/api/v1/aircraft/{icao}", (string icao, HttpContext httpContext) =>
        {
            if (!IcaoPattern().IsMatch(icao))
            {
                return Results.BadRequest(new ErrorResponse($"Invalid ICAO address: {icao}"));
            }

            string normalizedIcao = icao.ToUpperInvariant();

            // Parse sections parameter
            if (!TryGetSingleQueryValue(httpContext, "sections", out string? sectionsParam, out IResult? error))
            {
                return error;
            }

            HashSet<string>? requestedSections = null;

            if (!string.IsNullOrEmpty(sectionsParam))
            {
                requestedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string section in sectionsParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (section.Equals("History", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.BadRequest(new ErrorResponse($"Unknown section: {section}. Use /api/v1/aircraft/{{icao}}/history for history data."));
                    }

                    if (!ValidSections.Contains(section))
                    {
                        return Results.BadRequest(new ErrorResponse($"Unknown section: {section}"));
                    }

                    requestedSections.Add(section);
                }

                // A list with nothing in it after splitting (e.g. "sections=,") means all
                // sections, the same as an empty parameter.
                if (requestedSections.Count == 0)
                {
                    requestedSections = null;
                }
            }

            Aircraft? aircraft = tracker.GetAircraft(normalizedIcao);
            if (aircraft == null)
            {
                return Results.NotFound(new ErrorResponse($"Aircraft not found: {normalizedIcao}"));
            }

            // Build dynamic response dictionary
            var response = new Dictionary<string, object?>
            {
                ["Timestamp"] = DateTime.UtcNow
            };

            bool includeAll = requestedSections == null;

            if (includeAll || requestedSections!.Contains("Identification"))
            {
                response["Identification"] = DaemonApiMapper.ToIdentification(aircraft);
            }

            if (includeAll || requestedSections!.Contains("DatabaseRecord"))
            {
                response["DatabaseRecord"] = DaemonApiMapper.ToDatabaseRecord(aircraft);
            }

            if (includeAll || requestedSections!.Contains("Status"))
            {
                response["Status"] = DaemonApiMapper.ToStatus(aircraft);
            }

            if (includeAll || requestedSections!.Contains("Position"))
            {
                response["Position"] = DaemonApiMapper.ToPosition(aircraft);
            }

            if (includeAll || requestedSections!.Contains("VelocityAndDynamics"))
            {
                response["VelocityAndDynamics"] = DaemonApiMapper.ToVelocityAndDynamics(aircraft);
            }

            if (includeAll || requestedSections!.Contains("Autopilot"))
            {
                response["Autopilot"] = DaemonApiMapper.ToAutopilot(aircraft);
            }

            if (includeAll || requestedSections!.Contains("Meteorology"))
            {
                response["Meteorology"] = DaemonApiMapper.ToMeteorology(aircraft);
            }

            if (includeAll || requestedSections!.Contains("Acas"))
            {
                response["Acas"] = DaemonApiMapper.ToAcas(aircraft);
            }

            if (includeAll || requestedSections!.Contains("Capabilities"))
            {
                response["Capabilities"] = DaemonApiMapper.ToCapabilities(aircraft);
            }

            if (includeAll || requestedSections!.Contains("DataQuality"))
            {
                response["DataQuality"] = DaemonApiMapper.ToDataQuality(aircraft);
            }

            return Results.Json(response, jsonOptions);
        });

        // GET /api/v1/aircraft/{icao}/photo: aircraft photo metadata (Planespotters.net)
        app.MapGet("/api/v1/aircraft/{icao}/photo", async (string icao, HttpContext httpContext) =>
        {
            if (!IcaoPattern().IsMatch(icao))
            {
                return Results.BadRequest(new ErrorResponse($"Invalid ICAO address: {icao}"));
            }

            // Only tracked aircraft are looked up. Anything else would turn the daemon into a
            // general Planespotters lookup service for whoever can reach it, with an upstream
            // request (and a cache entry) per arbitrary ICAO.
            string normalizedIcao = icao.ToUpperInvariant();
            if (tracker.GetAircraft(normalizedIcao) == null)
            {
                return Results.NotFound(new ErrorResponse($"Aircraft not found: {normalizedIcao}"));
            }

            uint icaoUint = uint.Parse(icao, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            PhotoResult result = await photoService.GetAsync(icaoUint, httpContext.RequestAborted);

            // Per docs/API.md "Response Format": all fields are always present;
            // ThumbnailUrl/Photographer/Link are null (not omitted) when HasPhoto is false.
            // result.Metadata is non-null for both HasPhoto and NoPhoto outcomes (positive
            // entry in the first case, PhotoMetadata.Negative() in the second).
            return result.Outcome switch
            {
                PhotoOutcome.HasPhoto or PhotoOutcome.NoPhoto => Results.Json(new
                {
                    result.Metadata!.HasPhoto,
                    result.Metadata.ThumbnailUrl,
                    result.Metadata.Photographer,
                    result.Metadata.Link,
                }, jsonOptions),

                PhotoOutcome.UpstreamFailure => Results.Json(
                    new ErrorResponse("Upstream photo lookup failed; try again."),
                    statusCode: StatusCodes.Status502BadGateway),

                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        });

        // GET /api/v1/aircraft/{icao}/history: aircraft history
        app.MapGet("/api/v1/aircraft/{icao}/history", (string icao, HttpContext httpContext) =>
        {
            if (!IcaoPattern().IsMatch(icao))
            {
                return Results.BadRequest(new ErrorResponse($"Invalid ICAO address: {icao}"));
            }

            string normalizedIcao = icao.ToUpperInvariant();

            if (!TryGetSingleQueryValue(httpContext, "type", out string? typeParam, out IResult? error) ||
                !TryGetSingleQueryValue(httpContext, "limit", out string? limitParam, out error) ||
                !TryGetSingleQueryValue(httpContext, "after", out string? afterParam, out error))
            {
                return error;
            }

            // Parse type parameter (a single type; a comma-separated list is rejected)
            string? requestedType = null;

            if (!string.IsNullOrEmpty(typeParam))
            {
                if (typeParam.Contains(','))
                {
                    return Results.BadRequest(new ErrorResponse($"Invalid type: {typeParam}"));
                }

                if (!ValidHistoryTypes.Contains(typeParam))
                {
                    return Results.BadRequest(new ErrorResponse($"Unknown history type: {typeParam}"));
                }

                requestedType = typeParam;
            }

            // Parse limit parameter
            int? limit = null;

            if (!string.IsNullOrEmpty(limitParam))
            {
                if (!int.TryParse(limitParam, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLimit) ||
                    parsedLimit <= 0)
                {
                    return Results.BadRequest(new ErrorResponse($"Invalid limit: {limitParam}. Must be a positive integer."));
                }

                const int maxLimit = 10000;
                limit = Math.Min(parsedLimit, maxLimit);
            }

            // Parse after parameter
            long? after = null;

            if (!string.IsNullOrEmpty(afterParam))
            {
                if (!long.TryParse(afterParam, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedAfter) ||
                    parsedAfter < 0)
                {
                    return Results.BadRequest(new ErrorResponse($"Invalid after: {afterParam}"));
                }

                if (requestedType == null)
                {
                    return Results.BadRequest(new ErrorResponse("Parameter 'after' requires a type"));
                }

                after = parsedAfter;
            }

            Aircraft? aircraft = tracker.GetAircraft(normalizedIcao);
            if (aircraft == null)
            {
                return Results.NotFound(new ErrorResponse($"Aircraft not found: {normalizedIcao}"));
            }

            // Build dynamic response dictionary
            var response = new Dictionary<string, object?>
            {
                ["ICAO"] = normalizedIcao,
                ["Timestamp"] = DateTime.UtcNow
            };

            bool includeAll = requestedType == null;
            var query = new HistoryQuery(limit, after);

            if (includeAll || requestedType!.Equals("Position", StringComparison.OrdinalIgnoreCase))
            {
                response["Position"] = DaemonApiMapper.ToPositionHistory(aircraft, query);
            }

            if (includeAll || requestedType!.Equals("Altitude", StringComparison.OrdinalIgnoreCase))
            {
                response["Altitude"] = DaemonApiMapper.ToAltitudeHistory(aircraft, query);
            }

            if (includeAll || requestedType!.Equals("Velocity", StringComparison.OrdinalIgnoreCase))
            {
                response["Velocity"] = DaemonApiMapper.ToVelocityHistory(aircraft, query);
            }

            if (includeAll || requestedType!.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                response["State"] = DaemonApiMapper.ToStateHistory(aircraft, query);
            }

            return Results.Json(response, jsonOptions);
        });

        // GET /api/v1/stats: statistics
        app.MapGet("/api/v1/stats", () =>
        {
            StreamStatistics? stats = getStatistics();
            return Results.Ok(DaemonApiMapper.ToStats(
                stats, tracker, config.EnabledSdrSources.Count, startTime, config.Config.Receiver,
                config.Config.Heatmap?.Collect != false));
        });

        // GET /api/v1/health: health check
        app.MapGet("/api/v1/health", () =>
        {
            int uptime = (int)(DateTime.UtcNow - startTime).TotalSeconds;

            return Results.Ok(new HealthResponse(
                Status: "OK",
                Uptime: uptime,
                AircraftCount: tracker.Count,
                Timestamp: DateTime.UtcNow));
        });
    }

    /// <summary>
    /// Parses the <c>bounds</c> query parameter, written <c>south,west,north,east</c> in
    /// decimal degrees with an invariant-culture decimal point. The edges are then
    /// validated by <see cref="GeographicBounds.TryCreate"/>, whose messages are passed on.
    /// </summary>
    /// <param name="text">The parameter value.</param>
    /// <param name="bounds">The parsed box when parsing and validation succeed; default otherwise.</param>
    /// <param name="error">A human-readable reason when parsing or validation fails; null otherwise.</param>
    /// <returns><c>true</c> if the value holds four valid edges.</returns>
    internal static bool TryParseBounds(
        string? text,
        out GeographicBounds bounds,
        [NotNullWhen(false)] out string? error)
    {
        bounds = default;
        string[] parts = (text ?? string.Empty).Split(',');
        if (parts.Length != 4)
        {
            error = string.Create(CultureInfo.InvariantCulture,
                $"Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got {parts.Length}");
            return false;
        }

        double[] values = new double[4];
        for (int i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                error = "Invalid bounds values: all values must be valid numbers";
                return false;
            }
        }

        if (!GeographicBounds.TryCreate(values[0], values[1], values[2], values[3], out bounds, out string? invalid))
        {
            error = invalid!;
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Reads a query parameter that may appear at most once. A repeated parameter is an
    /// error rather than silently reduced to its first value, which would answer a
    /// different question from the one the client asked.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="name">The query parameter name.</param>
    /// <param name="value">The parameter value, or null when absent.</param>
    /// <param name="error">A 400 result when the parameter is repeated; null otherwise.</param>
    /// <returns><c>true</c> if the parameter is absent or appears once.</returns>
    private static bool TryGetSingleQueryValue(
        HttpContext httpContext,
        string name,
        out string? value,
        [NotNullWhen(false)] out IResult? error)
    {
        StringValues values = httpContext.Request.Query[name];
        if (values.Count > 1)
        {
            value = null;
            error = Results.BadRequest(new ErrorResponse($"Parameter '{name}' must not be repeated"));
            return false;
        }

        value = values.FirstOrDefault();
        error = null;
        return true;
    }
}
