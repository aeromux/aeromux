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
using FluentAssertions;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for CORS on the REST API: listed origins may read <c>/api</c> from a browser,
/// others may not, and with no origins configured nothing about a response changes.
/// </summary>
public class CorsTests
{
    private const string Allowed = "http://dashboard.local:3000";
    private const string Other = "http://evil.example";
    private const string AllowOrigin = "Access-Control-Allow-Origin";

    // === Listed origins ===

    [Fact]
    public async Task AllowedOrigin_MayReadTheResponse()
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed);

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/api/v1/stats", Allowed);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(AllowOrigin).Should().Equal(Allowed);
    }

    [Theory]
    [InlineData(Allowed)]
    [InlineData("https://grafana.example.net")]
    public async Task SeveralOrigins_EachGetsItsOwnEchoed(string origin)
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed, "https://grafana.example.net");

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/api/v1/stats", origin);

        response.Headers.GetValues(AllowOrigin).Should().Equal(origin);
        response.Headers.Vary.Should().Contain("Origin");
    }

    [Fact]
    public async Task OtherOrigin_GetsTheSameResponseWithoutPermission()
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed);

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/api/v1/stats", Other);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains(AllowOrigin).Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"Frames\"");
    }

    [Fact]
    public async Task AllowedOrigin_MayReadErrorResponsesToo()
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed);

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/api/v1/nothing-here", Allowed);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.GetValues(AllowOrigin).Should().Equal(Allowed);
    }

    [Fact]
    public async Task Preflight_FromAllowedOrigin_IsAnsweredWith204()
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed);

        HttpResponseMessage response = await PreflightAsync(fixture, "/api/v1/aircraft", Allowed);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues(AllowOrigin).Should().Equal(Allowed);
        response.Headers.GetValues("Access-Control-Allow-Methods").Should().Equal("GET");
        response.Content.Headers.ContentEncoding.Should().BeEmpty("there is no body to compress");
    }

    [Fact]
    public async Task Preflight_FromOtherOrigin_GrantsNothing()
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed);

        HttpResponseMessage response = await PreflightAsync(fixture, "/api/v1/aircraft", Other);

        response.Headers.Contains(AllowOrigin).Should().BeFalse();
    }

    [Fact]
    public async Task Wildcard_AllowsEveryOrigin()
    {
        await using ApiTestFixture fixture = await CreateAsync("*");

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/api/v1/health", Other);

        response.Headers.GetValues(AllowOrigin).Should().Equal("*");
    }

    [Fact]
    public async Task WebMap_GetsNoCorsHeaders()
    {
        await using ApiTestFixture fixture = await CreateAsync(Allowed);

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/", Allowed);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains(AllowOrigin).Should().BeFalse();
    }

    // === No origins ===

    [Fact]
    public async Task NoOrigins_NoCorsHeaders()
    {
        await using ApiTestFixture fixture = await ApiTestFixture.CreateAsync();

        HttpResponseMessage response = await SendAsync(fixture, HttpMethod.Get, "/api/v1/stats", Allowed);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains(AllowOrigin).Should().BeFalse();
    }

    [Fact]
    public async Task NoOrigins_PreflightIsNotAnswered()
    {
        await using ApiTestFixture fixture = await ApiTestFixture.CreateAsync();

        HttpResponseMessage response = await PreflightAsync(fixture, "/api/v1/aircraft", Allowed);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Headers.Contains(AllowOrigin).Should().BeFalse();
    }

    // === Helpers ===

    private static Task<ApiTestFixture> CreateAsync(params string[] origins) =>
        ApiTestFixture.CreateAsync(ApiTestFixture.CreateDefaultConfig() with { ApiCorsOrigins = origins });

    private static async Task<HttpResponseMessage> SendAsync(
        ApiTestFixture fixture, HttpMethod method, string path, string origin)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PreflightAsync(ApiTestFixture fixture, string path, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Offered so the test sees that the compressor leaves the bodiless answer alone
        request.Headers.Add("Accept-Encoding", "gzip, br");
        return await fixture.Client.SendAsync(request);
    }
}
