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
using Aeromux.CLI.Commands.Daemon.WebMap;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
        ArgumentNullException.ThrowIfNull(config);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Suppress ASP.NET Core default logging — use existing Serilog configuration
        builder.Host.UseSerilog();

        // Reduce shutdown timeout from default 30s — SignalR WebSocket connections
        // would otherwise block StopAsync() until the full timeout expires
        builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(3);
        });

        // Configure JSON serialization for all endpoints
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.PropertyNamingPolicy = null; // PascalCase
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });

        // Configure SignalR and web map push service
        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = null; // PascalCase to match REST API
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddSingleton<IAircraftStateTracker>(tracker);
        builder.Services.AddSingleton<MapHubPushService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<MapHubPushService>());

        // Range outline tracker — only created when receiver location is configured
        if (config.Config.Receiver?.Latitude is not null && config.Config.Receiver?.Longitude is not null)
        {
            builder.Services.AddSingleton(new RangeOutlineTracker(
                config.Config.Receiver.Latitude.Value, config.Config.Receiver.Longitude.Value));
        }

        // Bind to configured address and port
        builder.WebHost.UseUrls($"http://{config.BindAddress}:{config.ApiPort}");

        WebApplication app = builder.Build();

        // Serve embedded web map static files from assembly resources
        ManifestEmbeddedFileProvider embeddedProvider = new(
            assembly: typeof(DaemonApiServer).Assembly);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedProvider });
        var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        contentTypes.Mappings[".woff2"] = "font/woff2";
        // Embedded assets are baked into the assembly — safe to cache for 24 hours
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = embeddedProvider,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "public, max-age=86400";
            }
        });

        // Map SignalR hub endpoint
        app.MapHub<MapHub>("/maphub");

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
