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
using Aeromux.Core.Configuration;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Database;
using FluentAssertions;
using Xunit;

namespace Aeromux.CLI.Tests.Commands;

/// <summary>
/// Tests for <see cref="DatabaseAutoUpdater"/>'s swap-decision logic, driven deterministically via the
/// internal single-check method and a scripted <see cref="IDatabaseUpdateService"/> fake. Branches that
/// require a valid on-disk database (a successful hot-swap) are covered by the manual/E2E path.
/// </summary>
public class DatabaseAutoUpdaterTests
{
    /// <summary>Scripted update service: returns a fixed result, or throws, and counts calls.</summary>
    private sealed class FakeUpdateService : IDatabaseUpdateService
    {
        private readonly DatabaseUpdateResult _result;
        private readonly Exception? _throw;
        public int Calls { get; private set; }

        public FakeUpdateService(DatabaseUpdateResult result) => _result = result;
        public FakeUpdateService(Exception toThrow) { _throw = toThrow; _result = new DatabaseUpdateResult(DatabaseUpdateStatus.Failed); }

        public Task<DatabaseUpdateResult> CheckAndUpdateAsync(
            string databaseDirectory, IProgress<DownloadProgress>? progress, Action<string>? status, CancellationToken cancellationToken)
        {
            Calls++;
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(_result);
        }
    }

    private static (DatabaseAutoUpdater updater, SwappableAircraftDatabaseLookup lookup, DatabaseConfig config) Build(
        IDatabaseUpdateService service, string path)
    {
        var config = new DatabaseConfig { Enabled = true, Path = path };
        var lookup = new SwappableAircraftDatabaseLookup(null, "v-initial");
        var cfg = DatabaseAutoUpdateConfig.Resolve(config.AutoUpdate);
        var updater = new DatabaseAutoUpdater(config, cfg, lookup, service);
        return (updater, lookup, config);
    }

    [Fact]
    public async Task UpToDate_DoesNotSwap()
    {
        var service = new FakeUpdateService(new DatabaseUpdateResult(DatabaseUpdateStatus.UpToDate, Version: "v2"));
        (DatabaseAutoUpdater updater, SwappableAircraftDatabaseLookup lookup, _) = Build(service, Path.GetTempPath());

        await using (updater)
        {
            await updater.CheckOnceAsync(CancellationToken.None);
        }

        service.Calls.Should().Be(1);
        lookup.CurrentVersion.Should().Be("v-initial", "an up-to-date result must not swap the live database");
    }

    [Fact]
    public async Task Updated_ButNoValidDatabaseOnDisk_DoesNotSwap()
    {
        // An empty temp directory: the post-install TryCreate finds no valid database, so the
        // updater must keep the current (empty) lookup rather than swapping in a broken one.
        string emptyDir = Path.Combine(Path.GetTempPath(), "aeromux-autoupdate-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new FakeUpdateService(new DatabaseUpdateResult(DatabaseUpdateStatus.Updated, Version: "v2", RecordCount: 5));
            (DatabaseAutoUpdater updater, SwappableAircraftDatabaseLookup lookup, _) = Build(service, emptyDir);

            await using (updater)
            {
                await updater.CheckOnceAsync(CancellationToken.None);
            }

            lookup.CurrentVersion.Should().Be("v-initial", "a freshly installed file that fails validation must not be swapped in");
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_DoesNotThrow_AndDoesNotSwap()
    {
        var service = new FakeUpdateService(new DatabaseUpdateResult(DatabaseUpdateStatus.Failed, Error: "network down"));
        (DatabaseAutoUpdater updater, SwappableAircraftDatabaseLookup lookup, _) = Build(service, Path.GetTempPath());

        await using (updater)
        {
            Func<Task> act = async () => await updater.CheckOnceAsync(CancellationToken.None);
            await act.Should().NotThrowAsync();
        }

        lookup.CurrentVersion.Should().Be("v-initial");
    }

    [Fact]
    public async Task UnexpectedException_IsSwallowed_LoopSurvives()
    {
        var service = new FakeUpdateService(new InvalidOperationException("boom"));
        (DatabaseAutoUpdater updater, SwappableAircraftDatabaseLookup lookup, _) = Build(service, Path.GetTempPath());

        await using (updater)
        {
            Func<Task> act = async () => await updater.CheckOnceAsync(CancellationToken.None);
            await act.Should().NotThrowAsync("an unexpected failure must be logged and swallowed so the loop survives");
        }

        lookup.CurrentVersion.Should().Be("v-initial");
    }
}
