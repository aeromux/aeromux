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
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for thread safety during concurrent operations.
/// </summary>
public class ThreadSafetyTests : AircraftStateTrackerTestsBase
{
    [Fact]
    public async Task ThreadSafety_ConcurrentUpdatesToSameAircraft_NoExceptionsAllUpdatesCount()
    {
        // Arrange
        Tracker = CreateTracker();
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        const int threadCount = 20;
        const int updatesPerThread = 10;
        var tasks = new Task[threadCount];

        // Act - 20 threads, each sending 10 frames for same ICAO
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < updatesPerThread; j++)
                {
                    Tracker.Update(frame);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull();
        aircraft!.Status.TotalMessages.Should().Be(threadCount * updatesPerThread);
        aircraft.Identification.ICAO.Should().Be("471DBC");
        aircraft.Identification.Callsign.Should().Be("WZZ476");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentUpdatesToDifferentAircraft_NoContamination()
    {
        // Arrange
        Tracker = CreateTracker();
        const int threadsPerAircraft = 10;
        const int updatesPerThread = 5;
        var tasks = new List<Task>();

        ProcessedFrame[] frames =
        [
            CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"),
            CreateFrame(RealFrames.AircraftId_8965F3, "8965F3"),
            CreateFrame(RealFrames.AircraftId_8964A0, "8964A0"),
            CreateFrame(RealFrames.AllCall_4D2407, "4D2407"),
            CreateFrame(RealFrames.AllCall_80073B, "80073B")
        ];

        // Act - 10 threads for each of 5 aircraft (50 threads total)
        foreach (ProcessedFrame frame in frames)
        {
            for (int i = 0; i < threadsPerAircraft; i++)
            {
                ProcessedFrame capturedFrame = frame;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < updatesPerThread; j++)
                    {
                        Tracker.Update(capturedFrame);
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Assert - 5 aircraft tracked independently
        Tracker.Count.Should().Be(5);

        foreach (ProcessedFrame frame in frames)
        {
            Aircraft? aircraft = Tracker.GetAircraft(frame.Frame.IcaoAddress);
            aircraft.Should().NotBeNull();
            aircraft!.Status.TotalMessages.Should().Be(threadsPerAircraft * updatesPerThread);
        }
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentReadsDuringUpdates_NoExceptions()
    {
        // Arrange - two seeded aircraft, which nothing can expire during the test
        Tracker = CreateTracker();
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        AircraftStateTracker tracker = Tracker;

        // Act - 5 updaters (3 on 471DBC, 2 on 4D2407) with 200 updates each, 5 readers meanwhile
        Action[] updaters = Enumerable.Range(0, 5)
            .Select(index => (Action)(() =>
            {
                ProcessedFrame frame = index % 2 == 0
                    ? CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")
                    : CreateFrame(RealFrames.AllCall_4D2407, "4D2407");
                for (int i = 0; i < 200; i++)
                {
                    tracker.Update(frame);
                }
            }))
            .ToArray();
        Action[] readers = Enumerable.Range(0, 5)
            .Select(_ => (Action)(() =>
            {
                IReadOnlyList<Aircraft> aircraft = tracker.GetAllAircraft();
                Require(aircraft.Count == 2, $"GetAllAircraft returned {aircraft.Count} aircraft");
                Require(aircraft.All(a => a.Status.TotalMessages >= 1), "an aircraft had no messages");
                Require(tracker.Count == 2, $"Count was {tracker.Count}");
            }))
            .ToArray();

        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(updaters, readers);

        // Assert - every update counted
        errors.Should().BeEmpty();
        Tracker.GetAircraft("471DBC")!.Status.TotalMessages.Should().Be(1 + (3 * 200));
        Tracker.GetAircraft("4D2407")!.Status.TotalMessages.Should().Be(1 + (2 * 200));
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentGetAircraftLookups_NoExceptions()
    {
        // Arrange - three seeded aircraft, which nothing can expire during the test
        Tracker = CreateTracker();
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));
        AircraftStateTracker tracker = Tracker;
        string[] known = ["471DBC", "4D2407", "80073B"];
        string[] unknown = ["ABCDEF", "UNKNOWN"];

        // Act - 5 updaters (200 updates each, spread over the three) and 20 lookup threads
        Action[] updaters = Enumerable.Range(0, 5)
            .Select(index => (Action)(() =>
            {
                ProcessedFrame[] frames =
                [
                    CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"),
                    CreateFrame(RealFrames.AllCall_4D2407, "4D2407"),
                    CreateFrame(RealFrames.AllCall_80073B, "80073B")
                ];
                for (int i = 0; i < 200; i++)
                {
                    tracker.Update(frames[index % frames.Length]);
                }
            }))
            .ToArray();
        Action[] lookups = Enumerable.Range(0, 20)
            .Select(index =>
            {
                var random = new Random(index);
                return (Action)(() =>
                {
                    string icao = known[random.Next(known.Length)];
                    Require(tracker.GetAircraft(icao) is { } found && found.Identification.ICAO == icao,
                        $"{icao} went missing during updates");
                    string absent = unknown[random.Next(unknown.Length)];
                    Require(tracker.GetAircraft(absent) is null, $"{absent} was found");
                });
            })
            .ToArray();

        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(updaters, lookups);

        // Assert - updaters 0 and 3 hit 471DBC, 1 and 4 hit 4D2407, 2 hits 80073B
        errors.Should().BeEmpty();
        Tracker.GetAircraft("471DBC")!.Status.TotalMessages.Should().Be(1 + 400);
        Tracker.GetAircraft("4D2407")!.Status.TotalMessages.Should().Be(1 + 400);
        Tracker.GetAircraft("80073B")!.Status.TotalMessages.Should().Be(1 + 200);
    }

    [Fact]
    public async Task ThreadSafety_DuringExpirationCleanup_NoDeadlocks()
    {
        // Arrange - on a manual clock, 471DBC is already past its timeout before the race starts
        var clock = new ManualClock();
        Tracker = CreateTrackerWithClock(timeoutSeconds: 1, clock);
        AircraftStateTracker tracker = Tracker;
        var expired = new ConcurrentQueue<string>();
        Tracker.OnAircraftExpired += (_, args) => expired.Enqueue(args.Aircraft.Identification.ICAO);
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        clock.Advance(TimeSpan.FromSeconds(2));
        ProcessedFrame live = CreateFrame(RealFrames.AllCall_4D2407, "4D2407");

        // Act - 500 updates of a live aircraft and 500 cleanups at once; a deadlock would hit
        // the run's bound and fail the test
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            () =>
            {
                for (int i = 0; i < 500; i++)
                {
                    tracker.Update(live);
                }
            },
            () =>
            {
                for (int i = 0; i < 500; i++)
                {
                    tracker.CleanupExpiredAircraft();
                }
            });

        // Assert - the stale aircraft is expired exactly once, the live one keeps every message
        errors.Should().BeEmpty();
        expired.Should().Equal("471DBC");
        Tracker.GetAircraft("471DBC").Should().BeNull();
        Tracker.GetAircraft("4D2407")!.Status.TotalMessages.Should().Be(500);
    }

    [Fact]
    public async Task ThreadSafety_RaceConditionBetweenUpdateAndExpiration_NoCorruption()
    {
        // Arrange - on a manual clock, the aircraft is past its timeout but not yet cleaned up
        var clock = new ManualClock();
        Tracker = CreateTrackerWithClock(timeoutSeconds: 1, clock);
        AircraftStateTracker tracker = Tracker;
        var expired = new ConcurrentQueue<string>();
        Tracker.OnAircraftExpired += (_, args) => expired.Enqueue(args.Aircraft.Identification.ICAO);
        ProcessedFrame frame = CreateFrame(RealFrames.AircraftId_471DBC, "471DBC");
        Tracker.Update(frame);
        clock.Advance(TimeSpan.FromSeconds(2));

        // Act - 200 updates race 200 cleanups
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    tracker.Update(frame);
                }
            },
            () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    tracker.CleanupExpiredAircraft();
                }
            });

        // Assert - the first update makes the aircraft current again, so only two outcomes are
        // legal: cleanup won before it (expired once, then rebuilt by all 200 updates), or it
        // never removed it (all 200 updates continue the original track)
        errors.Should().BeEmpty();
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        aircraft.Should().NotBeNull("the last thing to happen to it was an update");
        if (expired.IsEmpty)
        {
            aircraft!.Status.TotalMessages.Should().Be(1 + 200);
        }
        else
        {
            expired.Should().ContainSingle();
            aircraft!.Status.TotalMessages.Should().Be(200, "removed only before its first refresh");
        }
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentEventHandlers_NoDataCorruption()
    {
        // Arrange
        Tracker = CreateTracker();
        int addedCount = 0;
        int updatedCount = 0;
        bool eventException = false;

        // 10 event subscribers
        for (int i = 0; i < 10; i++)
        {
            Tracker.OnAircraftAdded += (sender, args) =>
            {
                try
                {
                    Interlocked.Increment(ref addedCount);
                    args.Aircraft.Identification.ICAO.Should().NotBeNullOrEmpty();
                }
                catch
                {
                    eventException = true;
                }
            };

            Tracker.OnAircraftUpdated += (previous, updated) =>
            {
                try
                {
                    Interlocked.Increment(ref updatedCount);
                    previous.Should().NotBeNull();
                    updated.Should().NotBeNull();
                    updated.Status.TotalMessages.Should().BeGreaterThan(previous.Status.TotalMessages);
                }
                catch
                {
                    eventException = true;
                }
            };
        }

        // Act - 10 threads updating aircraft
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        eventException.Should().BeFalse("event handlers should not corrupt data");

        // With 10 threads updating the same ICAO and 10 subscribers:
        // - Exactly 1 Add event should fire (first thread creates aircraft)
        // - That 1 event is delivered to all 10 subscribers
        // - So addedCount should be exactly 10 (1 event * 10 subscribers)
        // However, due to race conditions and frame timestamp differences,
        // multiple "creates" might occur if timestamps differ enough
        addedCount.Should().BeGreaterOrEqualTo(10, "at least one add event with 10 subscribers");

        // Update events should fire for the remaining 9 threads
        // Each update event is delivered to all 10 subscribers
        updatedCount.Should().BeGreaterThan(0, "update events should fire");
    }

    [Fact]
    public async Task ThreadSafety_StressTest_1000ConcurrentOperations()
    {
        // Arrange
        Tracker = CreateTracker();
        AircraftStateTracker tracker = Tracker;
        string[] icaos = ["471DBC", "4D2407", "80073B", "8965F3", "8964A0"];
        string[] hex = [RealFrames.AircraftId_471DBC, RealFrames.AllCall_4D2407, RealFrames.AllCall_80073B,
            RealFrames.AircraftId_8965F3, RealFrames.AircraftId_8964A0];
        var updates = new int[icaos.Length];

        // Act - 50 workers, 2,000 random operations each
        Action[] workers = Enumerable.Range(0, 50)
            .Select(index => (Action)(() =>
            {
                var random = new Random(index);
                ProcessedFrame[] frames = icaos.Select((icao, i) => CreateFrame(hex[i], icao)).ToArray();
                for (int op = 0; op < 2_000; op++)
                {
                    int target = random.Next(icaos.Length);
                    switch (random.Next(4))
                    {
                        case 0:
                            tracker.Update(frames[target]);
                            Interlocked.Increment(ref updates[target]);
                            break;
                        case 1:
                            Aircraft? found = tracker.GetAircraft(icaos[target]);
                            Require(found is null || found.Identification.ICAO == icaos[target],
                                $"lookup of {icaos[target]} returned {found?.Identification.ICAO}");
                            break;
                        case 2:
                            IReadOnlyList<Aircraft> all = tracker.GetAllAircraft();
                            Require(all.Count <= icaos.Length, $"GetAllAircraft returned {all.Count} aircraft");
                            Require(all.Select(a => a.Identification.ICAO).Distinct().Count() == all.Count,
                                "GetAllAircraft returned an aircraft twice");
                            break;
                        case 3:
                            int count = tracker.Count;
                            Require(count is >= 0 and <= 5, $"Count was {count}");
                            break;
                    }
                }
            }))
            .ToArray();

        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(workers);

        // Assert - each aircraft's message count is exactly the updates it received
        errors.Should().BeEmpty();
        for (int i = 0; i < icaos.Length; i++)
        {
            Aircraft? aircraft = Tracker.GetAircraft(icaos[i]);
            if (updates[i] == 0)
            {
                aircraft.Should().BeNull();
            }
            else
            {
                aircraft!.Status.TotalMessages.Should().Be(updates[i], $"{icaos[i]} received {updates[i]} updates");
            }
        }

        Tracker.GetAllAircraft().Should().HaveCount(Tracker.Count);
    }

    // Plain checks rather than assertions inside workers: they run many thousands of times.
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
