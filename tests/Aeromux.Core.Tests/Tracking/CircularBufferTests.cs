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

using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for the CircularBuffer generic class.
/// Covers construction, add operations, retrieval methods, thread safety, and edge cases.
/// </summary>
public class CircularBufferTests
{
    #region Constructor Tests (6 tests)

    [Fact]
    public void Constructor_ValidCapacity_CreatesBuffer()
    {
        // Arrange & Act
        var buffer = new CircularBuffer<int>(10);

        // Assert
        buffer.Capacity.Should().Be(10);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_CapacityOne_CreatesBuffer()
    {
        // Arrange & Act
        var buffer = new CircularBuffer<int>(1);

        // Assert
        buffer.Capacity.Should().Be(1);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_LargeCapacity_CreatesBuffer()
    {
        // Arrange & Act
        var buffer = new CircularBuffer<int>(10000);

        // Assert
        buffer.Capacity.Should().Be(10000);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => { var _ = new CircularBuffer<int>(0); };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity must be positive*")
            .WithParameterName("capacity");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_NegativeCapacity_ThrowsArgumentException(int negativeCapacity)
    {
        // Arrange & Act
        Action act = () => { var _ = new CircularBuffer<int>(negativeCapacity); };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity must be positive*")
            .WithParameterName("capacity");
    }

    [Fact]
    public void Constructor_InitialState_CountZeroCapacitySet()
    {
        // Arrange & Act
        var buffer = new CircularBuffer<int>(5);

        // Assert
        buffer.Count.Should().Be(0);
        buffer.Capacity.Should().Be(5);
        buffer.GetAll().Should().BeEmpty();
    }

    #endregion

    #region Add Method Tests (8 tests)

    [Fact]
    public void Add_ToEmptyBuffer_IncreasesCount()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        buffer.Add(42);

        // Assert
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public void Add_UpToCapacity_CountIncreasesCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 5);

        // Assert
        buffer.Count.Should().Be(5);
        buffer.GetAll().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Add_BeyondCapacity_CountStaysAtCapacity()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 7);

        // Assert
        buffer.Count.Should().Be(5, "count should cap at capacity");
    }

    [Fact]
    public void Add_BeyondCapacity_OverwritesOldest()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 7);

        // Assert
        buffer.GetAll().Should().Equal(3, 4, 5, 6, 7);
    }

    [Fact]
    public void Add_MultipleWraparounds_MaintainsCorrectOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        FillBuffer(buffer, 10);

        // Assert
        buffer.Count.Should().Be(3);
        buffer.GetAll().Should().Equal(8, 9, 10);
    }

    [Fact]
    public void Add_SingleItemBuffer_AlwaysOverwrites()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Assert
        buffer.Count.Should().Be(1);
        buffer.GetAll().Should().Equal(3);
    }

    [Fact]
    public void Add_ReferenceTypes_OverwritesCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(3);

        // Act
        buffer.Add("a");
        buffer.Add("b");
        buffer.Add("c");
        buffer.Add("d");
        buffer.Add("e");
        buffer.Add("f");

        // Assert
        buffer.GetAll().Should().Equal("d", "e", "f");
    }

    [Fact]
    public void Add_NullableValueType_HandlesNulls()
    {
        // Arrange
        var buffer = new CircularBuffer<int?>(3);

        // Act
        buffer.Add(1);
        buffer.Add(null);
        buffer.Add(2);
        buffer.Add(null);
        buffer.Add(3);

        // Assert
        buffer.Count.Should().Be(3);
        buffer.GetAll().Should().Equal(2, null, 3);
    }

    #endregion

    #region GetAll Method Tests (7 tests)

    [Fact]
    public void GetAll_EmptyBuffer_ReturnsEmptyArray()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        int[] result = buffer.GetAll();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_PartiallyFilled_ReturnsInChronologicalOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        FillBuffer(buffer, 3);
        int[] result = buffer.GetAll();

        // Assert
        result.Should().Equal(1, 2, 3);
        buffer.Count.Should().Be(3);
    }

    [Fact]
    public void GetAll_FullBuffer_ReturnsInChronologicalOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 5);
        int[] result = buffer.GetAll();

        // Assert
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void GetAll_AfterWraparound_OldestFirst()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(4);

        // Act
        FillBuffer(buffer, 6);
        int[] result = buffer.GetAll();

        // Assert
        result.Should().Equal(3, 4, 5, 6).And.HaveCount(4);
    }

    [Fact]
    public void GetAll_MultipleWraparounds_CorrectOldestToNewest()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        FillBuffer(buffer, 15);
        int[] result = buffer.GetAll();

        // Assert
        result.Should().Equal(13, 14, 15);
    }

    [Fact]
    public void GetAll_ReturnsIndependentCopy_ModificationDoesNotAffectBuffer()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 3);

        // Act
        int[] first = buffer.GetAll();
        first[0] = 999;
        int[] second = buffer.GetAll();

        // Assert
        second[0].Should().Be(1, "modification to returned array shouldn't affect buffer");
        second.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GetAll_CalledTwice_ReturnsSameContent()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 3);

        // Act
        int[] first = buffer.GetAll();
        int[] second = buffer.GetAll();

        // Assert
        first.Should().Equal(second, "consecutive calls should return same data");
    }

    #endregion

    #region GetRecent Method Tests (10 tests)

    [Fact]
    public void GetRecent_EmptyBuffer_ReturnsEmptyArray()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        int[] result = buffer.GetRecent(5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetRecent_RequestMoreThanAvailable_ReturnsAll()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 3);

        // Act
        int[] result = buffer.GetRecent(10);

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void GetRecent_RequestFewerThanAvailable_ReturnsRecentN()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 5);

        // Act
        int[] result = buffer.GetRecent(3);

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void GetRecent_RequestExactCount_ReturnsAll()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 5);

        // Act
        int[] result = buffer.GetRecent(5);

        // Assert
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void GetRecent_AfterWraparound_ReturnsRecentInOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 7);

        // Act
        int[] result = buffer.GetRecent(3);

        // Assert
        result.Should().Equal(5, 6, 7);
    }

    [Fact]
    public void GetRecent_RequestZero_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 3);

        // Act
        int[] result = buffer.GetRecent(0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetRecent_RequestOne_ReturnsMostRecent()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 5);

        // Act
        int[] result = buffer.GetRecent(1);

        // Assert
        result.Should().Equal(5);
    }

    [Fact]
    public void GetRecent_AfterMultipleWraparounds_CorrectRecent()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(4);
        FillBuffer(buffer, 20);

        // Act
        int[] result = buffer.GetRecent(2);

        // Assert
        result.Should().Equal(19, 20);
    }

    [Fact]
    public void GetRecent_FullBuffer_RequestAll_ChronologicalOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 5);

        // Act
        int[] result = buffer.GetRecent(5);

        // Assert
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void GetRecent_ReturnsIndependentCopy_ModificationDoesNotAffect()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 5);

        // Act
        int[] first = buffer.GetRecent(3);
        first[0] = 999;
        int[] second = buffer.GetRecent(3);

        // Assert
        second[0].Should().Be(3, "modification to returned array shouldn't affect buffer");
        second.Should().Equal(3, 4, 5);
    }

    #endregion

    #region Count and Capacity Properties (5 tests)

    [Fact]
    public void Count_InitiallyZero()
    {
        // Arrange & Act
        var buffer = new CircularBuffer<int>(10);

        // Assert
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Count_IncreasesWithAdd_UpToCapacity()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act & Assert
        buffer.Count.Should().Be(0);

        buffer.Add(1);
        buffer.Count.Should().Be(1);

        buffer.Add(2);
        buffer.Count.Should().Be(2);

        buffer.Add(3);
        buffer.Count.Should().Be(3);

        buffer.Add(4);
        buffer.Count.Should().Be(4);

        buffer.Add(5);
        buffer.Count.Should().Be(5);
    }

    [Fact]
    public void Count_StaysAtCapacity_AfterWraparound()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 10);

        // Assert
        buffer.Count.Should().Be(5, "count should stay at capacity after wraparound");
    }

    [Fact]
    public void Capacity_NeverChanges()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        int initialCapacity = buffer.Capacity;
        FillBuffer(buffer, 10);
        int finalCapacity = buffer.Capacity;

        // Assert
        initialCapacity.Should().Be(5);
        finalCapacity.Should().Be(5, "capacity should never change");
    }

    [Fact]
    public void Capacity_IndependentProperty_NotAffectedByOperations()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        int beforeAdd = buffer.Capacity;
        buffer.Add(1);
        int afterAdd = buffer.Capacity;
        buffer.GetAll();
        int afterGetAll = buffer.Capacity;
        buffer.GetRecent(3);
        int afterGetRecent = buffer.Capacity;

        // Assert
        beforeAdd.Should().Be(5);
        afterAdd.Should().Be(5);
        afterGetAll.Should().Be(5);
        afterGetRecent.Should().Be(5, "capacity should remain constant");
    }

    #endregion

    #region Thread Safety Tests (6 tests)

    [Fact]
    public async Task ThreadSafety_ConcurrentAdds_NoDataLoss()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1000);
        const int threadCount = 10;
        const int addsPerThread = 100;
        var tasks = new Task[threadCount];

        // Act - 10 threads adding 100 items each
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < addsPerThread; j++)
                {
                    buffer.Add((threadId * 1000) + j);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        buffer.Count.Should().Be(threadCount * addsPerThread);
        int[] result = buffer.GetAll();
        result.Should().HaveCount(threadCount * addsPerThread);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentGetAllDuringAdd_NoCrashes()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        bool readerException = false;
        bool writerException = false;

        // Pre-fill buffer
        FillBuffer(buffer, 50);

        using (var cts = new CancellationTokenSource())
        {
            CancellationToken token = cts.Token;

            // Act - 20 readers, 5 writers, run for 2 seconds
            var readerTasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                readerTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            int[] data = buffer.GetAll();
                            data.Should().NotBeNull();
                        }
                    }
                    catch
                    {
                        readerException = true;
                    }
                }, token);
            }

            var writerTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                writerTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            buffer.Add(threadId);
                            Thread.Sleep(10);
                        }
                    }
                    catch
                    {
                        writerException = true;
                    }
                }, token);
            }

            // Run for 2 seconds
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
            catch (TaskCanceledException)
            {
                // Expected if cancellation happens during delay
            }

            await cts.CancelAsync();

            try
            {
                await Task.WhenAll(readerTasks.Concat(writerTasks));
            }
            catch (TaskCanceledException)
            {
                // Expected since tasks were created with cancellation token
            }
        }

        // Assert
        readerException.Should().BeFalse("readers should not throw exceptions");
        writerException.Should().BeFalse("writers should not throw exceptions");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentGetRecentDuringAdd_NoCrashes()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        bool readerException = false;
        bool writerException = false;

        // Pre-fill buffer
        FillBuffer(buffer, 50);

        using (var cts = new CancellationTokenSource())
        {
            CancellationToken token = cts.Token;

            // Act - 20 readers calling GetRecent, 5 writers
            var readerTasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                int threadId = i;
                readerTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        var random = new Random(threadId);
                        while (!token.IsCancellationRequested)
                        {
                            int count = random.Next(1, 50);
                            int[] data = buffer.GetRecent(count);
                            data.Should().NotBeNull();
                        }
                    }
                    catch
                    {
                        readerException = true;
                    }
                }, token);
            }

            var writerTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                writerTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            buffer.Add(threadId);
                            Thread.Sleep(10);
                        }
                    }
                    catch
                    {
                        writerException = true;
                    }
                }, token);
            }

            // Run for 2 seconds
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
            catch (TaskCanceledException)
            {
                // Expected if cancellation happens during delay
            }

            await cts.CancelAsync();

            try
            {
                await Task.WhenAll(readerTasks.Concat(writerTasks));
            }
            catch (TaskCanceledException)
            {
                // Expected since tasks were created with cancellation token
            }
        }

        // Assert
        readerException.Should().BeFalse("readers should not throw exceptions");
        writerException.Should().BeFalse("writers should not throw exceptions");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentCountAccess_ReturnsValidValues()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        bool readerException = false;
        bool writerException = false;
        bool invalidCount = false;

        // Pre-fill buffer
        FillBuffer(buffer, 50);

        using (var cts = new CancellationTokenSource())
        {
            CancellationToken token = cts.Token;

            // Act - 20 threads reading Count, 5 threads adding
            var readerTasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                readerTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            int count = buffer.Count;
                            if (count < 0 || count > buffer.Capacity)
                            {
                                invalidCount = true;
                            }
                        }
                    }
                    catch
                    {
                        readerException = true;
                    }
                }, token);
            }

            var writerTasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                writerTasks[i] = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            buffer.Add(threadId);
                            Thread.Sleep(10);
                        }
                    }
                    catch
                    {
                        writerException = true;
                    }
                }, token);
            }

            // Run for 2 seconds
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
            catch (TaskCanceledException)
            {
                // Expected if cancellation happens during delay
            }

            await cts.CancelAsync();

            try
            {
                await Task.WhenAll(readerTasks.Concat(writerTasks));
            }
            catch (TaskCanceledException)
            {
                // Expected since tasks were created with cancellation token
            }
        }

        // Assert
        readerException.Should().BeFalse("readers should not throw exceptions");
        writerException.Should().BeFalse("writers should not throw exceptions");
        invalidCount.Should().BeFalse("count should always be valid (0 to Capacity)");
    }

    [Fact]
    public async Task ThreadSafety_MixedOperations_DataIntegrity()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        bool exception = false;

        // Pre-fill buffer
        FillBuffer(buffer, 50);

        using (var cts = new CancellationTokenSource())
        {
            CancellationToken token = cts.Token;

            // Act - 50 threads performing random operations for 2 seconds
            var tasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                int threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        var random = new Random(threadId);
                        while (!token.IsCancellationRequested)
                        {
                            int operation = random.Next(4);
                            switch (operation)
                            {
                                case 0: // Add (40%)
                                    buffer.Add(threadId);
                                    break;
                                case 1: // GetAll (30%)
                                    int[] all = buffer.GetAll();
                                    all.Should().NotBeNull();
                                    break;
                                case 2: // GetRecent (20%)
                                    int[] recent = buffer.GetRecent(random.Next(1, 20));
                                    recent.Should().NotBeNull();
                                    break;
                                case 3: // Count (10%)
                                    int count = buffer.Count;
                                    count.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(buffer.Capacity);
                                    break;
                            }

                            Thread.Sleep(random.Next(5));
                        }
                    }
                    catch
                    {
                        exception = true;
                    }
                }, token);
            }

            // Run for 2 seconds
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
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
        }

        // Assert
        exception.Should().BeFalse("mixed operations should not throw exceptions");

        // Verify final state is consistent
        int[] finalData = buffer.GetAll();
        finalData.Should().HaveCount(buffer.Count);
    }

    [Fact]
    public async Task ThreadSafety_StressTest_1000ConcurrentOperations()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(200);
        bool exception = false;

        using (var cts = new CancellationTokenSource())
        {
            CancellationToken token = cts.Token;

            // Act - 50 threads for 10 seconds
            var tasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                int threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        var random = new Random(threadId);
                        while (!token.IsCancellationRequested)
                        {
                            int operation = random.Next(4);
                            switch (operation)
                            {
                                case 0:
                                    buffer.Add((threadId * 1000) + random.Next(1000));
                                    break;
                                case 1:
                                    buffer.GetAll();
                                    break;
                                case 2:
                                    buffer.GetRecent(random.Next(1, 50));
                                    break;
                                case 3:
                                    int count = buffer.Count;
                                    break;
                            }

                            Thread.Sleep(random.Next(5));
                        }
                    }
                    catch
                    {
                        exception = true;
                    }
                }, token);
            }

            // Run for 10 seconds
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
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
        }

        // Assert
        exception.Should().BeFalse("stress test should not throw exceptions");

        // Verify final state is consistent
        int[] finalData = buffer.GetAll();
        finalData.Should().HaveCount(buffer.Count);
        buffer.Count.Should().BeGreaterThan(0).And.BeLessOrEqualTo(buffer.Capacity);
    }

    #endregion

    #region Edge Cases (8 tests)

    [Fact]
    public void EdgeCase_CapacityOne_AddMultiple()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1);

        // Act
        FillBuffer(buffer, 5);

        // Assert
        buffer.Count.Should().Be(1);
        buffer.GetAll().Should().Equal(5);
    }

    [Fact]
    public void EdgeCase_CapacityOne_GetRecent()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1);
        FillBuffer(buffer, 5);

        // Act
        int[] result = buffer.GetRecent(10);

        // Assert
        result.Should().Equal(5);
    }

    [Fact]
    public void EdgeCase_AlternatingAddAndGet_ConsistentState()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act & Assert - Alternate adding and getting
        buffer.Add(1);
        buffer.GetAll().Should().Equal(1);

        buffer.Add(2);
        buffer.GetAll().Should().Equal(1, 2);

        buffer.Add(3);
        buffer.GetAll().Should().Equal(1, 2, 3);

        buffer.Add(4);
        buffer.GetAll().Should().Equal(1, 2, 3, 4);

        buffer.Add(5);
        buffer.GetAll().Should().Equal(1, 2, 3, 4, 5);

        buffer.Add(6);
        buffer.GetAll().Should().Equal(2, 3, 4, 5, 6);
    }

    [Fact]
    public void EdgeCase_GetRecentAfterPartialFill_Boundary()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 5);

        // Act
        int[] firstRecent = buffer.GetRecent(5);

        // Add more
        buffer.Add(6);
        buffer.Add(7);
        buffer.Add(8);

        int[] secondRecent = buffer.GetRecent(8);

        // Assert
        firstRecent.Should().Equal(1, 2, 3, 4, 5);
        secondRecent.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void EdgeCase_LargeCapacity_SmallUsage()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10000);

        // Act
        FillBuffer(buffer, 5);

        // Assert
        buffer.Count.Should().Be(5);
        buffer.Capacity.Should().Be(10000);
        buffer.GetAll().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void EdgeCase_ReferenceType_NullItems()
    {
        // Arrange
        var buffer = new CircularBuffer<string?>(5);

        // Act
        buffer.Add("a");
        buffer.Add(null);
        buffer.Add("c");
        buffer.Add(null);
        buffer.Add("e");

        // Assert
        buffer.GetAll().Should().Equal("a", null, "c", null, "e");
    }

    [Fact]
    public void EdgeCase_GetAllAfterExactCapacityFill_BeforeWraparound()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 5);
        int[] result = buffer.GetAll();

        // Assert
        buffer.Count.Should().Be(5);
        result.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void EdgeCase_WraparoundVerification_CompleteScenario()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        FillBuffer(buffer, 8);

        // Assert - Multi-faceted verification
        buffer.Count.Should().Be(5, "count should cap at capacity");
        buffer.Capacity.Should().Be(5, "capacity never changes");

        int[] all = buffer.GetAll();
        all.Should().Equal(4, 5, 6, 7, 8);
        all.Should().HaveCount(5);

        buffer.GetRecent(3).Should().Equal(6, 7, 8);
        buffer.GetRecent(1).Should().Equal(8);
        buffer.GetRecent(10).Should().Equal(4, 5, 6, 7, 8);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Helper to fill buffer with sequential integers starting from 1.
    /// </summary>
    private static void FillBuffer(CircularBuffer<int> buffer, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            buffer.Add(i);
        }
    }

    #endregion
}
