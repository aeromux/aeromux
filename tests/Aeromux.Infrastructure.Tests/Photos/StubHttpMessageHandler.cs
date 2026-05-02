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

namespace Aeromux.Infrastructure.Tests.Photos;

/// <summary>
/// Hand-rolled <see cref="HttpMessageHandler"/> stub used in place of a mocking
/// framework (the project doesn't include Moq). Records every request and
/// returns whatever response delegate the test configured.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    /// <summary>Captured copies of every request URL the handler has seen.</summary>
    public List<string> ReceivedUrls { get; } = new();

    /// <summary>Number of times <see cref="SendAsync"/> has been invoked.</summary>
    public int CallCount { get; private set; }

    /// <summary>Convenience constructor for a fixed status + body response.</summary>
    public static StubHttpMessageHandler ForResponse(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body),
        }));

    /// <summary>Convenience constructor that throws a given exception on send.</summary>
    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Convenience constructor that delays before returning a response (simulates timeout when delay > timeout).</summary>
    public static StubHttpMessageHandler Delaying(TimeSpan delay, HttpStatusCode status, string body) =>
        new(async (_, ct) =>
        {
            await Task.Delay(delay, ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
        return responder(request, cancellationToken);
    }
}
