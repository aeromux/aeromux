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
using Aeromux.Infrastructure.Photos;
using FluentAssertions;
using Moq;

namespace Aeromux.CLI.Tests.Api;

public class AircraftPhotoTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync() =>
        _fixture = await ApiTestFixture.CreateAsync();

    public async Task DisposeAsync() =>
        await _fixture.DisposeAsync();

    [Fact]
    public async Task Photo_HasPhoto_ReturnsAllFields()
    {
        var photo = PhotoMetadata.FromHex(
            "https://t.plnspttrs.net/x/y_280.jpg",
            "Jane Doe",
            "https://www.planespotters.net/photo/1234/foo");
        _fixture.PhotoServiceMock
            .Setup(s => s.GetAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoResult(PhotoOutcome.HasPhoto, photo));

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/4CA87C/photo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("HasPhoto").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("ThumbnailUrl").GetString().Should().Be(photo.ThumbnailUrl);
        doc.RootElement.GetProperty("Photographer").GetString().Should().Be(photo.Photographer);
        doc.RootElement.GetProperty("Link").GetString().Should().Be(photo.Link);
    }

    [Fact]
    public async Task Photo_NoPhoto_HasNullOptionalFields()
    {
        // Default fixture mock returns NoPhoto. Per docs/API.md "Response Format",
        // all fields are always present; missing values are null (not omitted).
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/ABCDEF/photo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("HasPhoto").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("ThumbnailUrl").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("Photographer").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("Link").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Photo_UpstreamFailure_Returns502()
    {
        _fixture.PhotoServiceMock
            .Setup(s => s.GetAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoResult(PhotoOutcome.UpstreamFailure, null));

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/4CA87C/photo");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Theory]
    [InlineData("XYZ123")]    // non-hex
    [InlineData("12345")]      // too short
    [InlineData("1234567")]    // too long
    public async Task Photo_InvalidIcao_Returns400(string icao)
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync($"/api/v1/aircraft/{icao}/photo");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Photo_LowercaseIcao_AcceptedAndNormalised()
    {
        // The route handler normalizes lowercase to uppercase before calling the service.
        // Verify the service was called with the uppercase-normalised uint value.
        uint? capturedIcao = null;
        _fixture.PhotoServiceMock
            .Setup(s => s.GetAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .Callback<uint, CancellationToken>((icao, _) => capturedIcao = icao)
            .ReturnsAsync(new PhotoResult(PhotoOutcome.NoPhoto, PhotoMetadata.Negative()));

        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/4ca87c/photo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedIcao.Should().Be(0x4CA87C);
    }

    [Fact]
    public async Task Photo_ContentType_IsJson()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/aircraft/4CA87C/photo");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
