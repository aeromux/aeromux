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

using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Tests.Api;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands.Daemon;

/// <summary>
/// Tests for <see cref="OutputClientCounts.For"/>: a disabled output is null, an enabled
/// one is a count, also before its broadcaster exists.
/// </summary>
public class OutputClientCountsTests
{
    [Fact]
    public void BeforeBroadcastersExist_EnabledOutputsAreZero()
    {
        DaemonValidatedConfig config = ApiTestFixture.CreateDefaultConfig() with
        {
            BeastEnabled = true, JsonEnabled = true, SbsEnabled = false
        };

        OutputClientCounts.For(config, null).Should().Be(new OutputClientCounts(0, 0, null));
    }

    [Fact]
    public async Task BroadcastersNotStartedYet_EnabledOutputsAreZero()
    {
        // The collection is assigned before its broadcasters are created
        await using var broadcasters = new DaemonBroadcasterCollection();
        DaemonValidatedConfig config = ApiTestFixture.CreateDefaultConfig() with
        {
            BeastEnabled = false, JsonEnabled = true, SbsEnabled = true
        };

        OutputClientCounts.For(config, broadcasters).Should().Be(new OutputClientCounts(null, 0, 0));
    }
}
