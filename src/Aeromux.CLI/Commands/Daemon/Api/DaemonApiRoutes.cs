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

using System.Text.Json;
using System.Text.RegularExpressions;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
    /// <param name="getStatistics">Function to get current stream statistics.</param>
    /// <param name="startTime">Daemon start time for uptime calculation.</param>
    /// <param name="config">Validated daemon configuration for receiver and device metadata.</param>
    /// <param name="jsonOptions">Shared JSON serializer options for dynamic response dictionaries.</param>
    public static void MapRoutes(
        WebApplication app,
        IAircraftStateTracker tracker,
        Func<StreamStatistics?> getStatistics,
        DateTime startTime,
        DaemonValidatedConfig config,
        JsonSerializerOptions jsonOptions)
    {
        // GET /api/v1/aircraft — Aircraft list (with optional bounds/search filtering)
        app.MapGet("/api/v1/aircraft", (HttpContext httpContext) =>
        {
            string? boundsParam = httpContext.Request.Query["bounds"].FirstOrDefault();
            string? searchParam = httpContext.Request.Query["search"].FirstOrDefault();

            // Mutual exclusivity check
            if (!string.IsNullOrEmpty(boundsParam) && !string.IsNullOrEmpty(searchParam))
            {
                return Results.BadRequest(new ErrorResponse("Parameters 'bounds' and 'search' are mutually exclusive"));
            }

            IReadOnlyList<Aircraft> allAircraft = tracker.GetAllAircraft();
            IEnumerable<Aircraft> filtered;

            if (!string.IsNullOrEmpty(boundsParam))
            {
                // Parse bounds=south,west,north,east
                string[] parts = boundsParam.Split(',');
                if (parts.Length != 4)
                {
                    return Results.BadRequest(new ErrorResponse(
                        $"Invalid bounds format: expected 4 comma-separated values (south,west,north,east), got {parts.Length}"));
                }

                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double south) ||
                    !double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double west) ||
                    !double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double north) ||
                    !double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double east))
                {
                    return Results.BadRequest(new ErrorResponse(
                        $"Invalid bounds values: all values must be valid numbers"));
                }

                if (south >= north)
                {
                    return Results.BadRequest(new ErrorResponse(
                        $"Invalid bounds: south ({south}) must be less than north ({north})"));
                }

                if (south < -90 || north > 90)
                {
                    return Results.BadRequest(new ErrorResponse(
                        $"Invalid bounds: latitude must be between -90 and 90"));
                }

                if (west < -180 || east > 180)
                {
                    return Results.BadRequest(new ErrorResponse(
                        $"Invalid bounds: longitude must be between -180 and 180"));
                }

                filtered = allAircraft.Where(a =>
                    a.Position.Coordinate is not null &&
                    a.Position.Coordinate.Latitude >= south &&
                    a.Position.Coordinate.Latitude <= north &&
                    a.Position.Coordinate.Longitude >= west &&
                    a.Position.Coordinate.Longitude <= east);
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

        // GET /api/v1/aircraft/{icao} — Aircraft detail
        app.MapGet("/api/v1/aircraft/{icao}", (string icao, HttpContext httpContext) =>
        {
            if (!IcaoPattern().IsMatch(icao))
            {
                return Results.BadRequest(new ErrorResponse($"Invalid ICAO address: {icao}"));
            }

            string normalizedIcao = icao.ToUpperInvariant();

            // Parse sections parameter
            string? sectionsParam = httpContext.Request.Query["sections"].FirstOrDefault();
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

        // GET /api/v1/aircraft/{icao}/history — Aircraft history
        app.MapGet("/api/v1/aircraft/{icao}/history", (string icao, HttpContext httpContext) =>
        {
            if (!IcaoPattern().IsMatch(icao))
            {
                return Results.BadRequest(new ErrorResponse($"Invalid ICAO address: {icao}"));
            }

            string normalizedIcao = icao.ToUpperInvariant();

            // Parse type parameter (single value only — comma-separated no longer supported)
            string? typeParam = httpContext.Request.Query["type"].FirstOrDefault();
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
            string? limitParam = httpContext.Request.Query["limit"].FirstOrDefault();
            int? limit = null;

            if (!string.IsNullOrEmpty(limitParam))
            {
                if (!int.TryParse(limitParam, out int parsedLimit) || parsedLimit <= 0)
                {
                    return Results.BadRequest(new ErrorResponse($"Invalid limit: {limitParam}. Must be a positive integer."));
                }

                limit = parsedLimit;
            }

            // Parse after parameter
            string? afterParam = httpContext.Request.Query["after"].FirstOrDefault();
            long? after = null;

            if (!string.IsNullOrEmpty(afterParam))
            {
                if (!long.TryParse(afterParam, out long parsedAfter) || parsedAfter < 0)
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

            if (includeAll || requestedType!.Equals("Position", StringComparison.OrdinalIgnoreCase))
            {
                response["Position"] = after.HasValue
                    ? DaemonApiMapper.ToPositionHistory(aircraft, after.Value)
                    : limit.HasValue
                        ? DaemonApiMapper.ToPositionHistory(aircraft, limit.Value)
                        : DaemonApiMapper.ToPositionHistory(aircraft);
            }

            if (includeAll || requestedType!.Equals("Altitude", StringComparison.OrdinalIgnoreCase))
            {
                response["Altitude"] = after.HasValue
                    ? DaemonApiMapper.ToAltitudeHistory(aircraft, after.Value)
                    : limit.HasValue
                        ? DaemonApiMapper.ToAltitudeHistory(aircraft, limit.Value)
                        : DaemonApiMapper.ToAltitudeHistory(aircraft);
            }

            if (includeAll || requestedType!.Equals("Velocity", StringComparison.OrdinalIgnoreCase))
            {
                response["Velocity"] = after.HasValue
                    ? DaemonApiMapper.ToVelocityHistory(aircraft, after.Value)
                    : limit.HasValue
                        ? DaemonApiMapper.ToVelocityHistory(aircraft, limit.Value)
                        : DaemonApiMapper.ToVelocityHistory(aircraft);
            }

            if (includeAll || requestedType!.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                response["State"] = after.HasValue
                    ? DaemonApiMapper.ToStateHistory(aircraft, after.Value)
                    : limit.HasValue
                        ? DaemonApiMapper.ToStateHistory(aircraft, limit.Value)
                        : DaemonApiMapper.ToStateHistory(aircraft);
            }

            return Results.Json(response, jsonOptions);
        });

        // GET /api/v1/stats — Statistics
        app.MapGet("/api/v1/stats", () =>
        {
            StreamStatistics? stats = getStatistics();
            return Results.Ok(DaemonApiMapper.ToStats(
                stats, tracker, config.EnabledSdrSources.Count, startTime, config.Config.Receiver));
        });

        // GET /api/v1/health — Health check
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
}
