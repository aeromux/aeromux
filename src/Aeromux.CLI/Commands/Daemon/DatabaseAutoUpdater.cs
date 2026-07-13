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
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Database;
using Serilog;

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Daemon background task that periodically checks the aeromux-db GitHub releases for a newer
/// database and, when one is installed, atomically hot-swaps the live enrichment connection over
/// to it. Runs a check shortly after start (optional) and then every configured interval.
/// Never propagates faults into the daemon: failures are logged and retried on the next tick.
/// </summary>
public sealed class DatabaseAutoUpdater : IAsyncDisposable
{
    private readonly DatabaseConfig _dbConfig;
    private readonly DatabaseAutoUpdateConfig _cfg;
    private readonly SwappableAircraftDatabaseLookup _lookup;
    private readonly IDatabaseUpdateService _updateService;
    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _linked;
    private Task? _loop;

    /// <summary>
    /// Creates the auto-updater.
    /// </summary>
    /// <param name="dbConfig">Database configuration (supplies the storage path and rebuild inputs).</param>
    /// <param name="cfg">Resolved and clamped auto-update configuration.</param>
    /// <param name="lookup">The live swappable lookup to hot-swap into.</param>
    /// <param name="updateService">The update orchestration service.</param>
    public DatabaseAutoUpdater(
        DatabaseConfig dbConfig,
        DatabaseAutoUpdateConfig cfg,
        SwappableAircraftDatabaseLookup lookup,
        IDatabaseUpdateService updateService)
    {
        ArgumentNullException.ThrowIfNull(dbConfig);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(updateService);

        _dbConfig = dbConfig;
        _cfg = cfg;
        _lookup = lookup;
        _updateService = updateService;
    }

    /// <summary>
    /// Starts the background loop. Runs off the caller's thread so it never delays daemon startup.
    /// </summary>
    /// <param name="daemonToken">The daemon's cancellation token.</param>
    public void Start(CancellationToken daemonToken)
    {
        _linked = CancellationTokenSource.CreateLinkedTokenSource(daemonToken, _cts.Token);
        _loop = Task.Run(() => RunAsync(_linked.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_cfg.CheckOnStartup)
        {
            Log.Information("Database auto-update enabled: checking on startup, then every {IntervalHours} h.", _cfg.CheckIntervalHours);
        }
        else
        {
            Log.Information("Database auto-update enabled: checking every {IntervalHours} h.", _cfg.CheckIntervalHours);
        }

        try
        {
            if (_cfg.CheckOnStartup)
            {
                await CheckOnceAsync(cancellationToken);
            }

            using var timer = new PeriodicTimer(TimeSpan.FromHours(_cfg.CheckIntervalHours));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Daemon shutdown — exit quietly.
        }
    }

    /// <summary>
    /// Runs a single check-and-swap cycle. Internal for deterministic testing; normally driven by the loop.
    /// </summary>
    internal async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            DatabaseUpdateResult result = await _updateService.CheckAndUpdateAsync(
                _dbConfig.Path!, progress: null, status: null, cancellationToken);

            switch (result.Status)
            {
                case DatabaseUpdateStatus.UpToDate:
                    Log.Debug("Database auto-update: already at latest version {Tag}.", result.Version);
                    break;

                case DatabaseUpdateStatus.Updated:
                    AircraftDatabaseLookupService? newInner =
                        DatabaseLookupFactory.TryCreate(_dbConfig, out string? version);
                    if (newInner is not null)
                    {
                        _lookup.Swap(newInner, version);
                        Log.Information(
                            "Database auto-update: installed {Tag} ({RecordCount} records) and swapped the live database.",
                            result.Version, result.RecordCount);

                        // Remove superseded files now that the swap has disposed the old inner service.
                        // Safe on the POSIX targets even if a pooled handle lingers (unlink detaches it).
                        if (_cfg.PruneOldDatabases && result.InstalledPath is not null)
                        {
                            int removed = DatabaseDownloader.DeleteSupersededDatabases(_dbConfig.Path!, result.InstalledPath);
                            if (removed > 0)
                            {
                                Log.Information("Database auto-update: removed {Count} superseded database file(s).", removed);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning(
                            "Database auto-update installed {Tag} but it failed validation; keeping the current database.",
                            result.Version);
                    }
                    break;

                case DatabaseUpdateStatus.Cancelled:
                    // Shutdown in progress — exit quietly on the next loop iteration.
                    break;

                case DatabaseUpdateStatus.Failed:
                    Log.Warning("Database auto-update check failed: {Reason}. Will retry at the next interval.", result.Error);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database auto-update check failed unexpectedly; will retry at the next interval.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch
            {
                // Loop faults are already logged; disposal must not throw.
            }
        }

        _linked?.Dispose();
        _cts.Dispose();
    }
}
