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
using Aeromux.Core.ModeS.ValueObjects;
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for the aircraft list <c>bounds</c> and <c>search</c> filters.
/// </summary>
public class AircraftListFilterTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(new List<Aircraft>
        {
            At("AAA001", 47.0, 19.0),     // Hungary
            At("AAA002", -40.0, 178.0),   // just west of the 180° meridian
            At("AAA003", -40.0, -178.0),  // just east of it
            At("AAA004", -40.0, 0.0),
            ApiTestFixture.CreateTestAircraft("AAA005", callsign: "BAW123") with
            {
                Position = new TrackedPosition()
            }
        });
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task Bounds_PlainBox_ReturnsAircraftInside()
    {
        (await IcaosAsync("/api/v1/aircraft?bounds=46,18,48,20")).Should().Equal("AAA001");
    }

    [Fact]
    public async Task Bounds_AcrossTheMeridian_ReturnsAircraftOnBothSides()
    {
        (await IcaosAsync("/api/v1/aircraft?bounds=-50,170,-30,-170"))
            .Should().BeEquivalentTo("AAA002", "AAA003");
    }

    [Theory]
    [InlineData("-50,190,-30,170")]
    [InlineData("-50,-170,-30,-200")]
    [InlineData("-50,170,-30,190")]
    public async Task Bounds_LongitudeOutOfRange_Returns400(string bounds)
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync($"/api/v1/aircraft?bounds={bounds}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).Should().Be("Invalid bounds: longitude must be between -180 and 180");
    }

    [Theory]
    [InlineData("NaN,170,-30,180")]
    [InlineData("-50,170,-30,NaN")]
    [InlineData("-50,170,-30,Infinity")]
    public async Task Bounds_NonFiniteValue_Returns400(string bounds)
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync($"/api/v1/aircraft?bounds={bounds}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).Should().Be("Invalid bounds values: all values must be finite numbers");
    }

    [Fact]
    public async Task Bounds_ExcludesAircraftWithoutPosition()
    {
        (await IcaosAsync("/api/v1/aircraft?bounds=-90,-180,90,180")).Should().NotContain("AAA005");
    }

    [Fact]
    public async Task Search_IncludesAircraftWithoutPosition()
    {
        (await IcaosAsync("/api/v1/aircraft?search=baw")).Should().Equal("AAA005");
    }

    [Fact]
    public async Task BoundsAndSearch_Returns400()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft?bounds=46,18,48,20&search=BAW");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(response)).Should().Be("Parameters 'bounds' and 'search' are mutually exclusive");
    }

    private static Aircraft At(string icao, double lat, double lon)
    {
        Aircraft aircraft = ApiTestFixture.CreateTestAircraft(icao);
        return aircraft with
        {
            Position = aircraft.Position with { Coordinate = new GeographicCoordinate(lat, lon) }
        };
    }

    private async Task<List<string>> IcaosAsync(string url)
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("Aircraft").EnumerateArray()
            .Select(a => a.GetProperty("ICAO").GetString()!)
            .ToList();
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("Error").GetString();
    }
}
