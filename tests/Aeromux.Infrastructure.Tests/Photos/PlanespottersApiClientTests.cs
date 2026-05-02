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
using Aeromux.Infrastructure.Photos;

namespace Aeromux.Infrastructure.Tests.Photos;

public class PlanespottersApiClientTests
{
    // ─── Sample upstream JSON payloads ────────────────────────────────────────

    private const string PhotoFoundJson = """
        {
          "photos": [
            {
              "id": "1234567",
              "thumbnail": {"src": "https://t.plnspttrs.net/.../thumb.jpg", "size": {"width": 200, "height": 134}},
              "thumbnail_large": {"src": "https://t.plnspttrs.net/03699/1234567_abc_280.jpg", "size": {"width": 420, "height": 280}},
              "link": "https://www.planespotters.net/photo/1234567/foo?utm_source=api",
              "photographer": "Jane Doe"
            }
          ]
        }
        """;

    private const string EmptyPhotosJson = """{"photos":[]}""";

    // ─── 1. URL construction ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByHexAsync_FormatsUrlWith6CharUppercaseHex()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, EmptyPhotosJson);
        var sut = new PlanespottersApiClient(stub);

        await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        stub.ReceivedUrls.Should().ContainSingle()
            .Which.Should().Be("https://api.planespotters.net/pub/photos/hex/4CA87C");
    }

    [Fact]
    public async Task GetByHexAsync_PadsLeadingZeroesIn6CharHex()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, EmptyPhotosJson);
        var sut = new PlanespottersApiClient(stub);

        await sut.GetByHexAsync(0x000ABC, CancellationToken.None);

        stub.ReceivedUrls.Single().Should().Be("https://api.planespotters.net/pub/photos/hex/000ABC");
    }

    [Fact]
    public async Task GetByRegAsync_FormatsUrlAndUrlEncodesRegistration()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, EmptyPhotosJson);
        var sut = new PlanespottersApiClient(stub);

        await sut.GetByRegAsync("EI-DEO", CancellationToken.None);

        stub.ReceivedUrls.Single().Should().Be("https://api.planespotters.net/pub/photos/reg/EI-DEO");
    }

    // ─── 2 & 3. Positive results, source flag ─────────────────────────────────

    [Fact]
    public async Task GetByHexAsync_PhotoFound_ReturnsPositiveWithSourceHex()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, PhotoFoundJson);
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeTrue();
        result.ThumbnailUrl.Should().Be("https://t.plnspttrs.net/03699/1234567_abc_280.jpg");
        result.Photographer.Should().Be("Jane Doe");
        result.Link.Should().Be("https://www.planespotters.net/photo/1234567/foo?utm_source=api");
        result.Source.Should().Be("hex");
    }

    [Fact]
    public async Task GetByRegAsync_PhotoFound_ReturnsPositiveWithSourceReg()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, PhotoFoundJson);
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByRegAsync("EI-DEO", CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeTrue();
        result.Source.Should().Be("reg");
    }

    // ─── 4. Empty photos array → terminal negative ────────────────────────────

    [Fact]
    public async Task GetByHexAsync_EmptyPhotos_ReturnsNegative()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, EmptyPhotosJson);
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeFalse();
        result.ThumbnailUrl.Should().BeNull();
        result.Photographer.Should().BeNull();
        result.Link.Should().BeNull();
        result.Source.Should().BeNull();
    }

    // ─── 5. Defensive sanitization ─────────────────────────────────────────────

    [Fact]
    public async Task GetByHexAsync_LinkPrefixWrong_TreatsAsNegative()
    {
        const string body = """
            {"photos":[{
              "thumbnail_large": {"src": "https://t.plnspttrs.net/x/y_280.jpg"},
              "link": "https://evil.example.com/foo",
              "photographer": "Bad Actor"
            }]}
            """;
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, body);
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task GetByHexAsync_ThumbnailHostWrong_TreatsAsNegative()
    {
        const string body = """
            {"photos":[{
              "thumbnail_large": {"src": "https://evil.example.com/y_280.jpg"},
              "link": "https://www.planespotters.net/photo/1234/foo",
              "photographer": "Bad Actor"
            }]}
            """;
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, body);
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeFalse();
    }

    // ─── 6 & 7. 404 / 410 → terminal negative ─────────────────────────────────

    [Fact]
    public async Task GetByHexAsync_404_ReturnsNegative()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.NotFound, "");
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task GetByHexAsync_410_ReturnsNegative()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.Gone, "");
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeFalse();
    }

    // ─── 8 & 9. Transient 4xx → null (don't cache) ────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]    // 429
    [InlineData(HttpStatusCode.BadRequest)]          // 400
    [InlineData(HttpStatusCode.Unauthorized)]        // 401
    [InlineData(HttpStatusCode.Forbidden)]           // 403
    public async Task GetByHexAsync_Transient4xx_ReturnsNull(HttpStatusCode status)
    {
        using var stub = StubHttpMessageHandler.ForResponse(status, "");
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── 10. 5xx → null (don't cache) ──────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetByHexAsync_5xx_ReturnsNull(HttpStatusCode status)
    {
        using var stub = StubHttpMessageHandler.ForResponse(status, "");
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── 11. Network exception → null ──────────────────────────────────────────

    [Fact]
    public async Task GetByHexAsync_HttpRequestException_ReturnsNull()
    {
        using var stub = StubHttpMessageHandler.Throwing(new HttpRequestException("network down"));
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── 12. Timeout → null ────────────────────────────────────────────────────
    // (We use the test handler's TaskCanceledException path; the internal 5-sec
    // HttpClient.Timeout fires the same TaskCanceledException in production.)

    [Fact]
    public async Task GetByHexAsync_Timeout_ReturnsNull()
    {
        using var stub = StubHttpMessageHandler.Throwing(new TaskCanceledException("timed out"));
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().BeNull();
    }

    // External cancellation must propagate (vs. an internal timeout swallow).
    [Fact]
    public async Task GetByHexAsync_ExternalCancellation_PropagatesException()
    {
        using var stub = StubHttpMessageHandler.Throwing(new TaskCanceledException("cancelled"));
        var sut = new PlanespottersApiClient(stub);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => sut.GetByHexAsync(0x4CA87C, cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    // ─── 13. Malformed JSON → null ─────────────────────────────────────────────

    [Fact]
    public async Task GetByHexAsync_MalformedJson_ReturnsNull()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, "not json{");
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().BeNull();
    }

    // Missing `photos` field is treated as terminal negative (per design doc §10
    // "Planespotters API breaking change" row — permissive parsing).
    [Fact]
    public async Task GetByHexAsync_JsonMissingPhotosField_ReturnsNegative()
    {
        using var stub = StubHttpMessageHandler.ForResponse(HttpStatusCode.OK, """{"other":"value"}""");
        var sut = new PlanespottersApiClient(stub);

        PhotoMetadata? result = await sut.GetByHexAsync(0x4CA87C, CancellationToken.None);

        result.Should().NotBeNull();
        result!.HasPhoto.Should().BeFalse();
    }
}
