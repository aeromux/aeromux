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
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon.Api;

/// <summary>
/// Builds and configures the ASP.NET Core Minimal API server for the daemon REST API.
/// </summary>
public static class DaemonApiServer
{
    /// <summary>
    /// Builds a configured WebApplication for the REST API.
    /// </summary>
    /// <param name="config">Validated daemon configuration.</param>
    /// <param name="tracker">Aircraft state tracker for data queries.</param>
    /// <param name="getStatistics">Function to get stream statistics.</param>
    /// <param name="startTime">Daemon start time for uptime calculation.</param>
    /// <returns>A configured WebApplication ready to be started.</returns>
    public static WebApplication Build(
        DaemonValidatedConfig config,
        IAircraftStateTracker tracker,
        Func<StreamStatistics?> getStatistics,
        DateTime startTime)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Suppress ASP.NET Core default logging — use existing Serilog configuration
        builder.Host.UseSerilog();

        // Configure JSON serialization for all endpoints
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.PropertyNamingPolicy = null; // PascalCase
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });

        // Configure rate limiting policies
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            // Per client IP — shared limit for the aircraft list endpoint
            options.AddPolicy("aircraft-list", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMilliseconds(500),
                        PermitLimit = 1
                    }));

            // Per client IP + ICAO — independent limit per aircraft
            options.AddPolicy("per-aircraft", httpContext =>
            {
                string icao = httpContext.Request.RouteValues["icao"]?.ToString() ?? "";
                string clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{clientIp}:{icao}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMilliseconds(500),
                        PermitLimit = 1
                    });
            });
        });

        // Bind to configured address and port
        builder.WebHost.UseUrls($"http://{config.BindAddress}:{config.ApiPort}");

        WebApplication app = builder.Build();

        app.UseRateLimiter();

        // Build shared JsonSerializerOptions for Results.Json() calls
        JsonSerializerOptions jsonOptions = new()
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() }
        };

        // Map all API routes
        DaemonApiRoutes.MapRoutes(app, tracker, getStatistics, startTime, config, jsonOptions);

        return app;
    }
}
