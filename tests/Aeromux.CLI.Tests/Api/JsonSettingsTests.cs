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
using Aeromux.CLI.Commands.Daemon.Api;
using Aeromux.Core.ModeS.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Aeromux.CLI.Tests.Api;

/// <summary>
/// The REST API and the SignalR hub must write the same models identically, because the
/// Web Map reads them over both. These tests compare what each actually produces.
/// </summary>
public class JsonSettingsTests : IAsyncLifetime
{
    private ApiTestFixture _fixture = null!;

    public async Task InitializeAsync() => _fixture = await ApiTestFixture.CreateAsync();

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    /// <summary>A PascalCase name, an enum with a display name, and a null.</summary>
    private sealed record Sample(EmergencyState EmergencyState, string? MissingValue);

    private static readonly Sample Value = new(EmergencyState.NoEmergency, null);

    private const string Expected = """{"EmergencyState":"No Emergency","MissingValue":null}""";

    [Fact]
    public void RestOptions_WriteTheSharedFormat()
    {
        JsonSerializerOptions rest = _fixture.Services.GetRequiredService<IOptions<HttpJsonOptions>>()
            .Value.SerializerOptions;

        JsonSerializer.Serialize(Value, rest).Should().Be(Expected);
    }

    [Fact]
    public void SignalROptions_WriteTheSameFormatAsRest()
    {
        JsonSerializerOptions hub = _fixture.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value.PayloadSerializerOptions;

        JsonSerializer.Serialize(Value, hub).Should().Be(Expected);
    }

    [Fact]
    public void DefaultOptions_WriteTheSameFormatAsRest()
    {
        JsonSerializer.Serialize(Value, DaemonJsonOptions.Default).Should().Be(Expected);
    }

    [Fact]
    public void DefaultOptions_CannotBeModified()
    {
        Action act = () => DaemonJsonOptions.Default.Converters.Add(new JsonStringEnumConverter());

        act.Should().Throw<InvalidOperationException>();
    }
}
