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
using FluentAssertions;

namespace Aeromux.CLI.Tests.Api;

public class HealthTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await ApiTestFixture.CreateAsync();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task Health_ReturnsExpectedFields()
    {
        string json = await (await _fixture.Client.GetAsync("/api/v1/health")).Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Status").GetString().Should().Be("OK");
        doc.RootElement.TryGetProperty("Uptime", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("AircraftCount", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("Timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Health_ContentType_IsJson()
    {
        HttpResponseMessage response = await _fixture.Client.GetAsync("/api/v1/health");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
