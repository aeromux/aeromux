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

using System.Collections.Concurrent;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Runs test workers truly concurrently, for thread-safety tests that must not depend on time.
/// </summary>
/// <remarks>
/// Each worker gets its own dedicated thread rather than a thread-pool one, so a test neither
/// starves the pool other tests share nor waits on it, and all workers are released together so
/// they really overlap. Work is a fixed amount, never "run for N seconds": the outcome is then
/// exact and can be asserted precisely. Exceptions are collected and returned, so a failing
/// assertion inside a worker is reported with its message instead of a bare flag.
/// </remarks>
internal static class Concurrently
{
    /// <summary>Upper bound on a run; reaching it means a worker hung, which fails the test.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Runs every worker to completion, all started at once.
    /// </summary>
    /// <param name="workers">The workers; each performs a fixed amount of work.</param>
    /// <returns>Every exception the workers threw.</returns>
    public static Task<IReadOnlyList<Exception>> RunAsync(params Action[] workers) =>
        RunAsync(workers, readers: []);

    /// <summary>
    /// Runs writers to completion while readers repeat, all started at once. Each reader action
    /// is one read; it runs at least once and keeps running until every writer has finished.
    /// </summary>
    /// <param name="writers">The writers; each performs a fixed amount of work.</param>
    /// <param name="readers">The readers; each performs one read per call.</param>
    /// <returns>Every exception the writers and readers threw.</returns>
    public static async Task<IReadOnlyList<Exception>> RunAsync(
        IReadOnlyList<Action> writers, IReadOnlyList<Action> readers)
    {
        var errors = new ConcurrentQueue<Exception>();
        using var start = new ManualResetEventSlim(false);
        int writersLeft = writers.Count;

        Task Spawn(Action body) => Task.Factory.StartNew(
            () =>
            {
                start.Wait();
                try
                {
                    body();
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        var tasks = new List<Task>();
        foreach (Action writer in writers)
        {
            tasks.Add(Spawn(() =>
            {
                try
                {
                    writer();
                }
                finally
                {
                    Interlocked.Decrement(ref writersLeft);
                }
            }));
        }

        foreach (Action reader in readers)
        {
            tasks.Add(Spawn(() =>
            {
                do
                {
                    reader();
                }
                while (Volatile.Read(ref writersLeft) > 0);
            }));
        }

        start.Set();
        await Task.WhenAll(tasks).WaitAsync(Bound);
        return errors.ToArray();
    }
}
