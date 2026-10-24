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

using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Aeromux.Core.Tracking;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// Tests for the web server's caching headers and response compression.
/// </summary>
/// <remarks>
/// The test client does not decompress on its own, so these see the encoding the server chose.
/// Web Map static files are not covered here: they are embedded only when the Web Map has been
/// built before the CLI, which a test run cannot assume.
/// </remarks>
public class HttpBehaviorTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
        _fixture.TrackerMock.Setup(t => t.GetAllAircraft()).Returns(
            Enumerable.Range(1, 20).Select(i => ApiTestFixture.CreateTestAircraft($"AAA{i:000}")).ToList());
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    // --- Caching ---

    [Theory]
    [InlineData("/api/v1/health", HttpStatusCode.OK)]
    [InlineData("/api/v1/aircraft", HttpStatusCode.OK)]
    [InlineData("/api/v1/aircraft/ZZZZZZ", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/aircraft/AAAAAA", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/nope", HttpStatusCode.NotFound)]
    public async Task ApiResponses_AreNeverCached(string url, HttpStatusCode status)
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync(url);

        response.StatusCode.Should().Be(status);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task NonApiResponses_GetNoCachingInstructionFromTheApi()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/no-such-file.html");

        response.Headers.CacheControl.Should().BeNull();
    }

    // --- Compression ---

    [Fact]
    public async Task NoAcceptEncoding_IsNotCompressed()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft");

        response.Content.Headers.ContentEncoding.Should().BeEmpty();
    }

    [Theory]
    [InlineData("gzip", "gzip")]
    [InlineData("br", "br")]
    [InlineData("gzip, deflate, br", "br")] // Brotli preferred when both are offered
    public async Task AcceptedEncoding_IsAppliedAndDecodesToTheResponse(string accept, string expected)
    {
        HttpResponseMessage response = await GetAsync("/api/v1/aircraft", accept);

        response.Content.Headers.ContentEncoding.Should().Equal(expected);
        response.Headers.Vary.Should().Contain("Accept-Encoding");
        using JsonDocument doc = await DecodeJsonAsync(response);
        doc.RootElement.GetProperty("Count").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task ErrorBodies_AreCompressedToo()
    {
        HttpResponseMessage response = await GetAsync("/api/v1/nope", "gzip");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentEncoding.Should().Equal("gzip");
        using JsonDocument doc = await DecodeJsonAsync(response);
        doc.RootElement.GetProperty("Error").GetString().Should().Be("Not found: /api/v1/nope");
    }

    [Fact]
    public async Task SignalREndpoint_IsNeverCompressed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/maphub/negotiate?negotiateVersion=1");
        request.Headers.AcceptEncoding.ParseAdd("gzip, br");

        HttpResponseMessage response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().BeEmpty();
    }

    // --- Helpers ---

    private async Task<HttpResponseMessage> GetAsync(string url, string acceptEncoding)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.AcceptEncoding.ParseAdd(acceptEncoding);
        return await _fixture.Client.SendAsync(request);
    }

    private static async Task<JsonDocument> DecodeJsonAsync(HttpResponseMessage response)
    {
        using var body = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        switch (response.Content.Headers.ContentEncoding.SingleOrDefault())
        {
            case "gzip":
                await using (var gzip = new GZipStream(body, CompressionMode.Decompress))
                {
                    return await JsonDocument.ParseAsync(gzip);
                }
            case "br":
                await using (var brotli = new BrotliStream(body, CompressionMode.Decompress))
                {
                    return await JsonDocument.ParseAsync(brotli);
                }
            default:
                return await JsonDocument.ParseAsync(body);
        }
    }
}
