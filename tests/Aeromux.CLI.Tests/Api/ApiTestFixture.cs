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
using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Test fixture that builds an in-memory API server with mocked dependencies.
/// </summary>
public sealed class ApiTestFixture : IAsyncDisposable
{
    private WebApplication _app;
    private HttpClient _client;

    /// <summary>
    /// Mock tracker for controlling test data.
    /// </summary>
    public Mock<IAircraftStateTracker> TrackerMock { get; }

    /// <summary>
    /// Stream statistics to return from the stats endpoint.
    /// </summary>
    public StreamStatistics? Statistics { get; set; }

    /// <summary>
    /// HTTP client for making requests to the test API.
    /// </summary>
    public HttpClient Client => _client;

    /// <summary>
    /// Creates a new test fixture. Call InitializeAsync() to start the server.
    /// </summary>
    private ApiTestFixture(WebApplication app, HttpClient client, Mock<IAircraftStateTracker> trackerMock)
    {
        _app = app;
        _client = client;
        TrackerMock = trackerMock;
    }

    /// <summary>
    /// Creates and starts a new test fixture with an in-memory API server.
    /// </summary>
    public static async Task<ApiTestFixture> CreateAsync()
    {
        var trackerMock = new Mock<IAircraftStateTracker>();
        trackerMock.Setup(t => t.GetAllAircraft()).Returns(new List<Aircraft>());
        trackerMock.Setup(t => t.Count).Returns(0);

        var config = new DaemonValidatedConfig
        {
            Config = new AeromuxConfig
            {
                Tracking = new TrackingConfig { AircraftTimeoutSeconds = 60, MaxHistorySize = 1000 },
                Receiver = new ReceiverConfig
                {
                    Latitude = 46.907982,
                    Longitude = 19.693172,
                    Altitude = 120,
                    Name = "Test"
                }
            },
            BeastPort = 30005,
            JsonPort = 30006,
            SbsPort = 30003,
            ApiPort = 0,
            ApiEnabled = true,
            BindAddress = IPAddress.Loopback,
            ReceiverUuid = null,
            MlatConfig = MlatConfig.Validate(null, null, new MlatConfig()),
            BeastEnabled = true,
            JsonEnabled = false,
            SbsEnabled = false,
            EnabledDevices = []
        };

        DateTime startTime = DateTime.UtcNow;

        // Use a shared reference so the lambda captures the same fixture instance we return
        var fixture = new ApiTestFixture(null!, null!, trackerMock);

        WebApplication app = DaemonApiServer.Build(
            config,
            trackerMock.Object,
            () => fixture.Statistics,
            startTime);

        // Use random port for tests
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        // Get the actual bound URL and update the fixture in-place
        string url = app.Urls.First();
        fixture._app = app;
        fixture._client = new HttpClient { BaseAddress = new Uri(url) };

        return fixture;
    }

    /// <summary>
    /// Creates a test Aircraft record with reasonable defaults.
    /// </summary>
    public static Aircraft CreateTestAircraft(
        string icao = "407F19",
        string? callsign = "VIR359",
        bool databaseEnabled = true,
        TrackedAutopilot? autopilot = null,
        TrackedMeteo? meteo = null,
        TrackedAcas? acas = null,
        TrackedCapabilities? capabilities = null,
        TrackedDataQuality? dataQuality = null,
        TrackedOperationalMode? operationalMode = null)
    {
        return new Aircraft
        {
            Identification = new TrackedIdentification
            {
                ICAO = icao,
                Callsign = callsign,
                Squawk = "2646",
                Category = AircraftCategory.Heavy,
                EmergencyState = EmergencyState.NoEmergency,
                FlightStatus = FlightStatus.AirborneNormal,
                Version = AdsbVersion.DO260B
            },
            Status = new TrackedStatus
            {
                SignalStrength = 20.0,
                TotalMessages = 437,
                PositionMessages = 27,
                VelocityMessages = 37,
                IdentificationMessages = 2,
                FirstSeen = DateTime.UtcNow.AddMinutes(-5),
                LastSeen = DateTime.UtcNow,
                SeenSeconds = 300
            },
            Position = new TrackedPosition
            {
                Coordinate = new GeographicCoordinate(47.39753723144531, 18.523773193359375),
                BarometricAltitude = Altitude.FromFeet(38000, AltitudeType.Barometric),
                GeometricAltitude = Altitude.FromFeet(37725, AltitudeType.Geometric),
                GeometricBarometricDelta = -275,
                IsOnGround = false,
                PositionSource = FrameSource.Sdr,
                LastUpdate = DateTime.UtcNow
            },
            Velocity = new TrackedVelocity
            {
                Speed = Velocity.FromKnots(480, VelocityType.GroundSpeed),
                Track = 294.59,
                VerticalRate = 0,
                LastUpdate = DateTime.UtcNow
            },
            DatabaseEnabled = databaseEnabled,
            DatabaseRecord = databaseEnabled
                ? new AircraftDatabaseRecord
                {
                    Registration = "G-VWHO",
                    Country = "United Kingdom",
                    TypeCode = "A346",
                    TypeDescription = "Airbus A340-642",
                    OperatorName = "Virgin Atlantic"
                }
                : AircraftDatabaseRecord.Empty,
            Autopilot = autopilot,
            Meteo = meteo,
            Acas = acas,
            Capabilities = capabilities,
            DataQuality = dataQuality,
            OperationalMode = operationalMode
        };
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
