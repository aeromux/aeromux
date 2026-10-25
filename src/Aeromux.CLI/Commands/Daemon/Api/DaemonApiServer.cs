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

using Aeromux.CLI.Commands.Daemon.WebMap;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Photos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon.Api;

/// <summary>
/// Builds and configures the ASP.NET Core Minimal API server for the daemon REST API.
/// </summary>
public static class DaemonApiServer
{
    private const string ApiCorsPolicy = "Api";

    /// <summary>
    /// Builds a configured WebApplication for the REST API.
    /// </summary>
    /// <param name="config">Validated daemon configuration.</param>
    /// <param name="tracker">Aircraft state tracker for data queries.</param>
    /// <param name="photoService">Aircraft photo service used by the <c>/api/v1/aircraft/{icao}/photo</c> endpoint and registered as a DI singleton for the SignalR hub.</param>
    /// <param name="statistics">The statistics sampler, read by the stats and health endpoints.</param>
    /// <param name="getOutputClients">Returns the connected clients per TCP output.</param>
    /// <param name="startTime">Daemon start time for uptime calculation.</param>
    /// <param name="databaseLookup">Optional live enrichment lookup, used to report the active database version to the Web Map.</param>
    /// <returns>A configured WebApplication ready to be started.</returns>
    public static WebApplication Build(
        DaemonValidatedConfig config,
        IAircraftStateTracker tracker,
        IAircraftPhotoService photoService,
        DaemonStatisticsSampler statistics,
        Func<OutputClientCounts> getOutputClients,
        DateTime startTime,
        SwappableAircraftDatabaseLookup? databaseLookup = null)
    {
        ArgumentNullException.ThrowIfNull(photoService);
        ArgumentNullException.ThrowIfNull(config);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Suppress ASP.NET Core default logging and use the existing Serilog configuration
        builder.Host.UseSerilog();

        // Reduce shutdown timeout from the default 30s, because SignalR WebSocket connections
        // would otherwise block StopAsync() until the full timeout expires
        builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(3);
        });

        // Configure JSON serialization for all endpoints. Every result and error body is
        // written with these server-wide options, so no endpoint passes its own.
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            DaemonJsonOptions.Apply(options.SerializerOptions));

        // Configure SignalR and web map push service, with the same settings as the REST API
        // because the Web Map reads the same models over both
        builder.Services.AddSignalR()
            .AddJsonProtocol(options => DaemonJsonOptions.Apply(options.PayloadSerializerOptions));
        builder.Services.AddSingleton(tracker);
        builder.Services.AddSingleton(photoService);

        // Register the app-created wrapper instance (not a factory/type) so the DI container does
        // NOT own its lifetime; the orchestrator disposes it during ordered shutdown. A factory
        // registration would let the container dispose it at web-app shutdown, before the tracker's
        // consumer stops, silently killing enrichment mid-shutdown.
        if (databaseLookup is not null)
        {
            builder.Services.AddSingleton(databaseLookup);
        }

        builder.Services.AddSingleton<MapHubPushService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<MapHubPushService>());

        // Range outline tracker, only created when receiver location is configured
        if (config.Config.Receiver?.Latitude is not null && config.Config.Receiver?.Longitude is not null)
        {
            builder.Services.AddSingleton(new RangeOutlineTracker(
                config.Config.Receiver.Latitude.Value, config.Config.Receiver.Longitude.Value));
        }

        // Heatmap tracker, registered unless collection is explicitly disabled in config.
        // The receiver latitude (if configured) squares the grid to the coverage area and
        // aligns its columns; falls back to 45° when no receiver is set.
        if (config.Config.Heatmap?.Collect != false)
        {
            builder.Services.AddSingleton(new HeatmapTracker(config.Config.Receiver?.Latitude));
        }

        // Compress responses with the framework's own Brotli and gzip providers, Brotli first so
        // it wins when a client accepts both. Browsers offer Brotli only over HTTPS, so a Web Map
        // opened over plain HTTP on the local network gets gzip; Brotli serves HTTPS reverse
        // proxies that forward the browser's Accept-Encoding, and scripts. The defaults compress
        // JSON, HTML, CSS and JavaScript at the fastest level and leave fonts and images alone.
        builder.Services.AddResponseCompression(options =>
        {
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        // Browser access from other origins, only to the origins the operator listed. Nothing is
        // registered when the list is empty, so responses stay exactly as without CORS. No
        // credentials and only GET: every endpoint is a read, and none needs a cookie or a
        // non-safelisted request header.
        bool corsEnabled = config.ApiCorsOrigins.Count > 0;
        if (corsEnabled)
        {
            builder.Services.AddCors(options => options.AddPolicy(ApiCorsPolicy, policy =>
            {
                if (config.ApiCorsOrigins.Contains("*"))
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins([.. config.ApiCorsOrigins]);
                }

                policy.WithMethods(HttpMethods.Get);
            }));
        }

        // Bind to configured address and port
        builder.WebHost.UseUrls($"http://{config.BindAddress}:{config.ApiPort}");

        WebApplication app = builder.Build();

        // First in the pipeline, so error bodies are compressed too. The SignalR endpoint is
        // excluded: no transport, including the event-stream and long-polling fallbacks, may
        // be buffered by a compressor.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/maphub"),
            branch => branch.UseResponseCompression());

        // Live data must never be served from a cache, and photo metadata must not outlive the
        // daemon's own time limit in one. Set before the endpoint runs so errors carry it too.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            await next(context);
        });

        // CORS for the REST API only. The Web Map is served by the daemon itself, and the
        // SignalR hub would need credentials support from another origin. The middleware
        // answers a preflight OPTIONS itself, so it never reaches routing's 405.
        if (corsEnabled)
        {
            app.UseWhen(
                context => context.Request.Path.StartsWithSegments("/api"),
                branch => branch.UseCors(ApiCorsPolicy));
        }

        // Give bodiless API errors the same {"Error": "..."} shape as every other API error.
        // Routing answers an unknown /api path with an empty 404 and a wrong method with an
        // empty 405; the middleware only acts on responses that have no body, so replies that
        // already carry an ErrorResponse are left alone. Web Map static files keep the default.
        app.UseStatusCodePages(async context =>
        {
            HttpContext http = context.HttpContext;
            if (!http.Request.Path.StartsWithSegments("/api"))
            {
                return;
            }

            int statusCode = http.Response.StatusCode;
            string message = statusCode switch
            {
                StatusCodes.Status404NotFound => $"Not found: {http.Request.Path}",
                StatusCodes.Status405MethodNotAllowed => $"Method not allowed: {http.Request.Method}",
                _ => ReasonPhrases.GetReasonPhrase(statusCode)
            };

            await http.Response.WriteAsJsonAsync(new ErrorResponse(message));
        });

        // Serve embedded web map static files from assembly resources
        ManifestEmbeddedFileProvider embeddedProvider = new(
            assembly: typeof(DaemonApiServer).Assembly);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedProvider });
        var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider { Mappings =
        {
            [".woff2"] = "font/woff2"
        } };
        // Embedded assets are baked into the assembly, so they are safe to cache for 24 hours
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

        // Map all API routes
        DaemonApiRoutes.MapRoutes(app, tracker, photoService, statistics, getOutputClients, startTime, config, databaseLookup);

        return app;
    }
}
