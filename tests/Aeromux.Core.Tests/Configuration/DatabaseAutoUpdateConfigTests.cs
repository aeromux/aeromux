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

using Aeromux.Core.Configuration;

namespace Aeromux.Core.Tests.Configuration;

/// <summary>
/// Tests for <see cref="DatabaseAutoUpdateConfig"/> defaults and <see cref="DatabaseAutoUpdateConfig.Resolve"/>
/// (null → default, interval clamping, non-mutation).
/// </summary>
public class DatabaseAutoUpdateConfigTests
{
    [Fact]
    public void Defaults_AreOnStartupAnd24h()
    {
        var config = new DatabaseAutoUpdateConfig();

        config.Enabled.Should().BeTrue();
        config.CheckOnStartup.Should().BeTrue();
        config.CheckIntervalHours.Should().Be(24);
        config.PruneOldDatabases.Should().BeTrue();
    }

    [Fact]
    public void Resolve_PreservesPruneOldDatabases()
    {
        DatabaseAutoUpdateConfig.Resolve(new DatabaseAutoUpdateConfig { PruneOldDatabases = false })
            .PruneOldDatabases.Should().BeFalse();

        DatabaseAutoUpdateConfig.Resolve(null).PruneOldDatabases.Should().BeTrue();
    }

    [Fact]
    public void Resolve_Null_ReturnsDefaultsEnabled()
    {
        DatabaseAutoUpdateConfig resolved = DatabaseAutoUpdateConfig.Resolve(null);

        resolved.Enabled.Should().BeTrue();
        resolved.CheckOnStartup.Should().BeTrue();
        resolved.CheckIntervalHours.Should().Be(24);
    }

    [Fact]
    public void Resolve_PreservesFlags()
    {
        var source = new DatabaseAutoUpdateConfig { Enabled = false, CheckOnStartup = false, CheckIntervalHours = 12 };

        DatabaseAutoUpdateConfig resolved = DatabaseAutoUpdateConfig.Resolve(source);

        resolved.Enabled.Should().BeFalse();
        resolved.CheckOnStartup.Should().BeFalse();
        resolved.CheckIntervalHours.Should().Be(12);
    }

    [Theory]
    [InlineData(0, DatabaseAutoUpdateConfig.MinCheckIntervalHours)]
    [InlineData(-5, DatabaseAutoUpdateConfig.MinCheckIntervalHours)]
    [InlineData(1, 1)]
    [InlineData(48, 48)]
    [InlineData(int.MaxValue, DatabaseAutoUpdateConfig.MaxCheckIntervalHours)]
    public void Resolve_ClampsInterval(int input, int expected)
    {
        var source = new DatabaseAutoUpdateConfig { CheckIntervalHours = input };

        DatabaseAutoUpdateConfig resolved = DatabaseAutoUpdateConfig.Resolve(source);

        resolved.CheckIntervalHours.Should().Be(expected);
    }

    [Fact]
    public void Resolve_DoesNotMutateInput()
    {
        var source = new DatabaseAutoUpdateConfig { CheckIntervalHours = 0 };

        DatabaseAutoUpdateConfig resolved = DatabaseAutoUpdateConfig.Resolve(source);

        source.CheckIntervalHours.Should().Be(0, "Resolve must return a copy, not mutate the shared config");
        resolved.CheckIntervalHours.Should().Be(DatabaseAutoUpdateConfig.MinCheckIntervalHours);
        resolved.Should().NotBeSameAs(source);
    }
}
