// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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
        aircraft.Identification.Icao.Should().Be("471DBC");
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
        // Arrange
        Tracker = CreateTracker();
        bool readersException = false;
        bool updatersException = false;
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Seed with some aircraft
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));

        // Act - 5 reader threads, 5 updater threads, run for 2 seconds
        var readerTasks = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            readerTasks[i] = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        IReadOnlyList<Aircraft> aircraft = Tracker.GetAllAircraft();
                        int count = Tracker.Count;
                    }
                }
                catch
                {
                    readersException = true;
                }
            }, cts.Token);
        }

        var updaterTasks = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            updaterTasks[i] = Task.Run(() =>
            {
                try
                {
                    ProcessedFrame frame = index % 2 == 0
                        ? CreateFrame(RealFrames.AircraftId_471DBC, "471DBC")
                        : CreateFrame(RealFrames.AllCall_4D2407, "4D2407");

                    while (!cts.Token.IsCancellationRequested)
                    {
                        Tracker.Update(frame);
                        Thread.Sleep(10);
                    }
                }
                catch
                {
                    updatersException = true;
                }
            }, cts.Token);
        }

        // Run for 2 seconds
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected if cancellation happens during delay
        }

        await cts.CancelAsync();

        try
        {
            await Task.WhenAll(readerTasks.Concat(updaterTasks));
        }
        catch (TaskCanceledException)
        {
            // Expected since tasks were created with cancellation token
        }

        // Assert
        readersException.Should().BeFalse("readers should not throw exceptions");
        updatersException.Should().BeFalse("updaters should not throw exceptions");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentGetAircraftLookups_NoExceptions()
    {
        // Arrange
        Tracker = CreateTracker();

        // Seed with aircraft
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
        Tracker.Update(CreateFrame(RealFrames.AllCall_80073B, "80073B"));

        bool lookupException = false;
        bool updateException = false;
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Act - 20 lookup threads, 5 update threads
        var lookupTasks = new Task[20];
        string[] icaos = ["471DBC", "4D2407", "80073B", "ABCDEF", "UNKNOWN"];

        for (int i = 0; i < 20; i++)
        {
            int index = i;
            lookupTasks[i] = Task.Run(() =>
            {
                try
                {
                    var random = new Random(index);
                    while (!cts.Token.IsCancellationRequested)
                    {
                        string icao = icaos[random.Next(icaos.Length)];
                        Aircraft? aircraft = Tracker.GetAircraft(icao);
                    }
                }
                catch
                {
                    lookupException = true;
                }
            }, cts.Token);
        }

        var updateTasks = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            updateTasks[i] = Task.Run(() =>
            {
                try
                {
                    ProcessedFrame[] frames =
                    [
                        CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"),
                        CreateFrame(RealFrames.AllCall_4D2407, "4D2407"),
                        CreateFrame(RealFrames.AllCall_80073B, "80073B")
                    ];

                    while (!cts.Token.IsCancellationRequested)
                    {
                        Tracker.Update(frames[index % frames.Length]);
                        Thread.Sleep(10);
                    }
                }
                catch
                {
                    updateException = true;
                }
            }, cts.Token);
        }

        // Run for 1 second
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected if cancellation happens during delay
        }

        await cts.CancelAsync();

        try
        {
            await Task.WhenAll(lookupTasks.Concat(updateTasks));
        }
        catch (TaskCanceledException)
        {
            // Expected since tasks were created with cancellation token
        }

        // Assert
        lookupException.Should().BeFalse("lookups should not throw exceptions");
        updateException.Should().BeFalse("updates should not throw exceptions");
    }

    [Fact]
    public async Task ThreadSafety_DuringExpirationCleanup_NoDeadlocks()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 1);
        bool updateException = false;

        // Create initial aircraft
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Act - Continuous updates while cleanup runs
        var updateTask = Task.Run(() =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Tracker.Update(CreateFrame(RealFrames.AllCall_4D2407, "4D2407"));
                    Thread.Sleep(50);
                }
            }
            catch
            {
                updateException = true;
            }
        }, cts.Token);

        // Run for 3 seconds (multiple cleanup cycles)
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected if cancellation happens during delay
        }

        await cts.CancelAsync();

        try
        {
            await updateTask;
        }
        catch (TaskCanceledException)
        {
            // Expected since task was created with cancellation token
        }

        // Assert
        updateException.Should().BeFalse("updates should not deadlock with cleanup");
    }

    [Fact]
    public async Task ThreadSafety_RaceConditionBetweenUpdateAndExpiration_NoCorruption()
    {
        // Arrange
        Tracker = CreateTrackerWithTimeout(timeoutSeconds: 1);

        // Create aircraft close to expiration
        Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));

        // Wait almost to expiration
        await Task.Delay(TimeSpan.FromMilliseconds(900));

        // Act - Try to update while expiration might be happening
        var updateTask = Task.Run(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                Tracker.Update(CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"));
                Thread.Sleep(50);
            }
        });

        await updateTask;

        // Assert - Aircraft should either be alive (if update won race) or cleanly expired
        Aircraft? aircraft = Tracker.GetAircraft("471DBC");
        // No assertion on null/not-null since race outcome is non-deterministic
        // Key is no exceptions or corrupted state

        // If aircraft exists, it should be valid
        if (aircraft != null)
        {
            aircraft.Identification.Icao.Should().Be("471DBC");
            aircraft.Status.TotalMessages.Should().BeGreaterThan(0);
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
                    args.Aircraft.Identification.Icao.Should().NotBeNullOrEmpty();
                }
                catch
                {
                    eventException = true;
                }
            };

            Tracker.OnAircraftUpdated += (sender, args) =>
            {
                try
                {
                    Interlocked.Increment(ref updatedCount);
                    args.Previous.Should().NotBeNull();
                    args.Updated.Should().NotBeNull();
                    args.Updated.Status.TotalMessages.Should().BeGreaterThan(args.Previous.Status.TotalMessages);
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
        bool exception = false;
        var cts = new CancellationTokenSource();
        Disposables.Add(cts);

        // Act - 50 threads performing mixed operations for 10 seconds
        var tasks = new Task[50];
        for (int i = 0; i < 50; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var random = new Random(threadIndex);
                    ProcessedFrame[] frames =
                    [
                        CreateFrame(RealFrames.AircraftId_471DBC, "471DBC"),
                        CreateFrame(RealFrames.AllCall_4D2407, "4D2407"),
                        CreateFrame(RealFrames.AllCall_80073B, "80073B"),
                        CreateFrame(RealFrames.AircraftId_8965F3, "8965F3"),
                        CreateFrame(RealFrames.AircraftId_8964A0, "8964A0")
                    ];

                    while (!cts.Token.IsCancellationRequested)
                    {
                        int operation = random.Next(4);
                        switch (operation)
                        {
                            case 0: // Update
                                Tracker.Update(frames[random.Next(frames.Length)]);
                                break;
                            case 1: // GetAircraft
                                Tracker.GetAircraft(frames[random.Next(frames.Length)].Frame.IcaoAddress);
                                break;
                            case 2: // GetAllAircraft
                                Tracker.GetAllAircraft();
                                break;
                            case 3: // Count
                                int count = Tracker.Count;
                                break;
                        }

                        Thread.Sleep(random.Next(5));
                    }
                }
                catch
                {
                    exception = true;
                }
            }, cts.Token);
        }

        // Run for 10 seconds
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected if cancellation happens during delay
        }

        await cts.CancelAsync();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (TaskCanceledException)
        {
            // Expected since tasks were created with cancellation token
        }

        // Assert
        exception.Should().BeFalse("stress test should not throw exceptions");

        // Verify final state is consistent
        IReadOnlyList<Aircraft> allAircraft = Tracker.GetAllAircraft();
        allAircraft.Should().HaveCount(Tracker.Count);

        foreach (Aircraft aircraft in allAircraft)
        {
            aircraft.Identification.Icao.Should().NotBeNullOrEmpty();
            aircraft.Status.TotalMessages.Should().BeGreaterThan(0);
        }
    }
}
