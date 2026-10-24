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

    // The tests below run fixed work on dedicated threads (see Concurrently) rather than for a
    // length of time, so they check exact outcomes. Writer w (numbered from 1) adds
    // w * WriterStride + k for k = 0, 1, 2, ..., so every snapshot must show each writer's values
    // in increasing order; FillBuffer's 1..n read as writer 0's, already in order.

    private const int WriterStride = 1_000_000;

    [Fact]
    public async Task ThreadSafety_ConcurrentGetAllDuringAdd_NoCrashes()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        FillBuffer(buffer, 50);

        // Act - 5 writers adding 1,000 values each while 20 readers take snapshots
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            Writers(buffer, writerCount: 5, addsPerWriter: 1_000),
            Readers(20, _ => () =>
            {
                int[] data = buffer.GetAll();
                Require(data.Length is >= 50 and <= 100, $"GetAll returned {data.Length} items");
                RequireEachWriterInOrder(data);
            }));

        // Assert
        errors.Should().BeEmpty();
        RequireContiguousHistory(buffer, totalAdds: 50 + (5 * 1_000));
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentGetRecentDuringAdd_NoCrashes()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        FillBuffer(buffer, 50);

        // Act - the buffer never holds fewer than 50, so asking for 1..50 always gets that many
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            Writers(buffer, writerCount: 5, addsPerWriter: 1_000),
            Readers(20, readerId =>
            {
                var random = new Random(readerId);
                return () =>
                {
                    int count = random.Next(1, 51);
                    int[] data = buffer.GetRecent(count);
                    Require(data.Length == count, $"GetRecent({count}) returned {data.Length} items");
                    RequireEachWriterInOrder(data);
                };
            }));

        // Assert
        errors.Should().BeEmpty();
        RequireContiguousHistory(buffer, totalAdds: 50 + (5 * 1_000));
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentCountAccess_ReturnsValidValues()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        FillBuffer(buffer, 50);

        // Act - nothing removes items, so each reader must see Count in range and never falling
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            Writers(buffer, writerCount: 5, addsPerWriter: 1_000),
            Readers(20, _ =>
            {
                int last = 0;
                return () =>
                {
                    int count = buffer.Count;
                    Require(count is >= 50 and <= 100, $"Count was {count}");
                    Require(count >= last, $"Count fell from {last} to {count}");
                    last = count;
                };
            }));

        // Assert
        errors.Should().BeEmpty();
        buffer.Count.Should().Be(100);
    }

    [Fact]
    public async Task ThreadSafety_MixedOperations_DataIntegrity()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);
        FillBuffer(buffer, 50);
        int adds = 0;

        // Act - 50 workers, 200 random operations each
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            Workers(buffer, workerCount: 50, operationsPerWorker: 200, () => Interlocked.Increment(ref adds)));

        // Assert
        errors.Should().BeEmpty();
        RequireContiguousHistory(buffer, totalAdds: 50 + adds);
    }

    [Fact]
    public async Task ThreadSafety_StressTest_1000ConcurrentOperations()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(200);
        int adds = 0;

        // Act - 50 workers, 2,000 random operations each
        IReadOnlyList<Exception> errors = await Concurrently.RunAsync(
            Workers(buffer, workerCount: 50, operationsPerWorker: 2_000, () => Interlocked.Increment(ref adds)));

        // Assert - exact, because the work is fixed: every add is accounted for
        errors.Should().BeEmpty();
        adds.Should().BeGreaterThan(0);
        RequireContiguousHistory(buffer, totalAdds: adds);
    }

    /// <summary>Writers 1..n, each adding its own increasing values.</summary>
    private static Action[] Writers(CircularBuffer<int> buffer, int writerCount, int addsPerWriter) =>
        Enumerable.Range(1, writerCount)
            .Select(writer => (Action)(() =>
            {
                for (int k = 0; k < addsPerWriter; k++)
                {
                    buffer.Add((writer * WriterStride) + k);
                }
            }))
            .ToArray();

    /// <summary>Readers built per reader index, so each can keep its own state.</summary>
    private static Action[] Readers(int readerCount, Func<int, Action> create) =>
        Enumerable.Range(0, readerCount).Select(create).ToArray();

    /// <summary>
    /// Workers performing a fixed number of random operations, adds included, checking every
    /// read against the buffer's invariants as they go.
    /// </summary>
    private static Action[] Workers(CircularBuffer<int> buffer, int workerCount, int operationsPerWorker, Action onAdd) =>
        Enumerable.Range(1, workerCount)
            .Select(worker => (Action)(() =>
            {
                var random = new Random(worker);
                int next = 0;
                for (int i = 0; i < operationsPerWorker; i++)
                {
                    switch (random.Next(5))
                    {
                        case 0:
                            buffer.Add((worker * WriterStride) + next++);
                            onAdd();
                            break;
                        case 1:
                            int[] all = buffer.GetAll();
                            Require(all.Length <= buffer.Capacity, $"GetAll returned {all.Length} items");
                            RequireEachWriterInOrder(all);
                            break;
                        case 2:
                            int[] recent = buffer.GetRecent(random.Next(1, 51));
                            RequireEachWriterInOrder(recent);
                            break;
                        case 3:
                            int count = buffer.Count;
                            Require(count >= 0 && count <= buffer.Capacity, $"Count was {count}");
                            break;
                        case 4:
                            BufferSnapshot<int> snapshot = buffer.GetSnapshot();
                            Require(snapshot.Entries.Count == snapshot.Count,
                                $"snapshot holds {snapshot.Entries.Count} entries but reports {snapshot.Count}");
                            RequireConsecutive(snapshot);
                            break;
                    }
                }
            }))
            .ToArray();

    /// <summary>
    /// After all adds: the buffer holds the newest min(total, capacity) entries, and their sequence
    /// IDs are exactly the run ending at the total, so no add was lost or counted twice.
    /// </summary>
    private static void RequireContiguousHistory(CircularBuffer<int> buffer, int totalAdds)
    {
        BufferSnapshot<int> snapshot = buffer.GetSnapshot();
        int expectedCount = Math.Min(totalAdds, buffer.Capacity);

        snapshot.Count.Should().Be(expectedCount);
        snapshot.Entries.Should().HaveCount(expectedCount);
        snapshot.MaxSequenceId.Should().Be(totalAdds);
        snapshot.MinSequenceId.Should().Be(totalAdds - expectedCount + 1);
        RequireConsecutive(snapshot);
        RequireEachWriterInOrder(snapshot.Entries.Select(e => e.Item).ToArray());
        buffer.GetAll().Should().HaveCount(expectedCount);
    }

    private static void RequireConsecutive(BufferSnapshot<int> snapshot)
    {
        for (int i = 1; i < snapshot.Entries.Count; i++)
        {
            Require(snapshot.Entries[i].SequenceId == snapshot.Entries[i - 1].SequenceId + 1,
                $"sequence IDs jump from {snapshot.Entries[i - 1].SequenceId} to {snapshot.Entries[i].SequenceId}");
        }
    }

    private static void RequireEachWriterInOrder(int[] values)
    {
        var last = new Dictionary<int, int>();
        foreach (int value in values)
        {
            int writer = value / WriterStride;
            int k = value % WriterStride;
            Require(!last.TryGetValue(writer, out int previous) || k > previous,
                $"writer {writer}: {k} appears after {previous}");
            last[writer] = k;
        }
    }

    // Plain checks rather than assertions inside workers: they run millions of times.
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
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

    #region Sequence ID Tests (7 tests)

    [Fact]
    public void SequenceId_EmptyBuffer_BothNull()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act & Assert
        buffer.MinSequenceId.Should().BeNull();
        buffer.MaxSequenceId.Should().BeNull();
    }

    [Fact]
    public void Add_FirstEntry_SequenceIdIsOne()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        buffer.Add(42);

        // Assert
        buffer.MinSequenceId.Should().Be(1);
        buffer.MaxSequenceId.Should().Be(1);
    }

    [Fact]
    public void Add_SecondEntry_SequenceIdIsTwo()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        buffer.Add(42);
        buffer.Add(99);

        // Assert
        buffer.MinSequenceId.Should().Be(1);
        buffer.MaxSequenceId.Should().Be(2);
    }

    [Fact]
    public void Add_MultipleEntries_SequenceIdsAreSequential()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        FillBuffer(buffer, 5);

        // Assert
        buffer.MinSequenceId.Should().Be(1);
        buffer.MaxSequenceId.Should().Be(5);
    }

    [Fact]
    public void SequenceId_AfterWraparound_MinIncreases()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        FillBuffer(buffer, 5);

        // Assert
        buffer.MinSequenceId.Should().Be(3);
        buffer.MaxSequenceId.Should().Be(5);
    }

    [Fact]
    public void SequenceId_AfterMultipleWraparounds_CorrectRange()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        FillBuffer(buffer, 15);

        // Assert
        buffer.MinSequenceId.Should().Be(13);
        buffer.MaxSequenceId.Should().Be(15);
    }

    [Fact]
    public void SequenceId_SingleCapacity_MinEqualsMax()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(1);

        // Act
        FillBuffer(buffer, 5);

        // Assert
        buffer.MinSequenceId.Should().Be(5);
        buffer.MaxSequenceId.Should().Be(5);
    }

    #endregion

    #region GetAfter Method Tests (8 tests)

    [Fact]
    public void GetAfter_Zero_ReturnsAllEntries()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 3);

        // Act
        var result = buffer.GetAfter(0);

        // Assert
        result.Should().HaveCount(3);
        result[0].Item.Should().Be(1);
        result[0].SequenceId.Should().Be(1);
        result[1].Item.Should().Be(2);
        result[1].SequenceId.Should().Be(2);
        result[2].Item.Should().Be(3);
        result[2].SequenceId.Should().Be(3);
    }

    [Fact]
    public void GetAfter_One_ReturnsEntriesTwoAndThree()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 3);

        // Act
        var result = buffer.GetAfter(1);

        // Assert
        result.Should().HaveCount(2);
        result[0].Item.Should().Be(2);
        result[0].SequenceId.Should().Be(2);
        result[1].Item.Should().Be(3);
        result[1].SequenceId.Should().Be(3);
    }

    [Fact]
    public void GetAfter_MaxSequenceId_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 3);

        // Act
        var result = buffer.GetAfter(3);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAfter_BeyondMaxSequenceId_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 3);

        // Act
        var result = buffer.GetAfter(100);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAfter_WrappedBuffer_BelowMin_ReturnsAll()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);
        FillBuffer(buffer, 5);

        // Act
        var result = buffer.GetAfter(0);

        // Assert
        result.Should().HaveCount(3);
        result[0].Item.Should().Be(3);
        result[0].SequenceId.Should().Be(3);
        result[1].Item.Should().Be(4);
        result[1].SequenceId.Should().Be(4);
        result[2].Item.Should().Be(5);
        result[2].SequenceId.Should().Be(5);
    }

    [Fact]
    public void GetAfter_WrappedBuffer_BetweenMinAndMax_ReturnsSubset()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);
        FillBuffer(buffer, 5);

        // Act
        var result = buffer.GetAfter(3);

        // Assert
        result.Should().HaveCount(2);
        result[0].Item.Should().Be(4);
        result[0].SequenceId.Should().Be(4);
        result[1].Item.Should().Be(5);
        result[1].SequenceId.Should().Be(5);
    }

    [Fact]
    public void GetAfter_EmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        var result = buffer.GetAfter(0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAfter_ResultsOrderedChronologically()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        FillBuffer(buffer, 7);

        // Act
        var result = buffer.GetAfter(3);

        // Assert
        result.Should().HaveCount(4);
        for (int i = 1; i < result.Length; i++)
        {
            result[i].SequenceId.Should().BeGreaterThan(result[i - 1].SequenceId,
                "items should be ordered oldest-first by sequence ID");
            result[i].Item.Should().BeGreaterThan(result[i - 1].Item,
                "items should match sequence ID order");
        }
    }

    #endregion

    #region GetAllWithSequenceIds and GetRecentWithSequenceIds Tests (4 tests)

    [Fact]
    public void GetAllWithSequenceIds_ReturnsItemsAndSequenceIds()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 3);

        // Act
        var result = buffer.GetAllWithSequenceIds();

        // Assert
        result.Should().HaveCount(3);
        result[0].Item.Should().Be(1);
        result[0].SequenceId.Should().Be(1);
        result[1].Item.Should().Be(2);
        result[1].SequenceId.Should().Be(2);
        result[2].Item.Should().Be(3);
        result[2].SequenceId.Should().Be(3);
    }

    [Fact]
    public void GetAllWithSequenceIds_AfterWraparound_CorrectIds()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);
        FillBuffer(buffer, 5);

        // Act
        var result = buffer.GetAllWithSequenceIds();

        // Assert
        result.Should().HaveCount(3);
        result[0].Item.Should().Be(3);
        result[0].SequenceId.Should().Be(3);
        result[1].Item.Should().Be(4);
        result[1].SequenceId.Should().Be(4);
        result[2].Item.Should().Be(5);
        result[2].SequenceId.Should().Be(5);
    }

    [Fact]
    public void GetRecentWithSequenceIds_ReturnsSubsetWithIds()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        FillBuffer(buffer, 5);

        // Act
        var result = buffer.GetRecentWithSequenceIds(2);

        // Assert
        result.Should().HaveCount(2);
        result[0].Item.Should().Be(4);
        result[0].SequenceId.Should().Be(4);
        result[1].Item.Should().Be(5);
        result[1].SequenceId.Should().Be(5);
    }

    [Fact]
    public void GetAllWithSequenceIds_EmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        var result = buffer.GetAllWithSequenceIds();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Duplicate Check

    [Fact]
    public void Add_WithDuplicateCheck_SkipsConsecutiveDuplicate()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10, (a, b) => a == b);

        // Act
        buffer.Add(42);
        buffer.Add(42);

        // Assert
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public void Add_WithDuplicateCheck_AllowsDifferentItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10, (a, b) => a == b);

        // Act
        buffer.Add(1);
        buffer.Add(2);

        // Assert
        buffer.Count.Should().Be(2);
        buffer.GetAll().Should().Equal(1, 2);
    }

    [Fact]
    public void Add_WithDuplicateCheck_AllowsSameItemAfterDifferent()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10, (a, b) => a == b);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(1);

        // Assert - Non-consecutive duplicates are allowed
        buffer.Count.Should().Be(3);
        buffer.GetAll().Should().Equal(1, 2, 1);
    }

    [Fact]
    public void Add_WithoutDuplicateCheck_AllowsDuplicates()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        buffer.Add(42);
        buffer.Add(42);

        // Assert - No dedup without a check function
        buffer.Count.Should().Be(2);
        buffer.GetAll().Should().Equal(42, 42);
    }

    [Fact]
    public void Add_WithDuplicateCheck_SequenceIdsOnlyIncrementOnInsert()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10, (a, b) => a == b);

        // Act
        buffer.Add(1);
        buffer.Add(1);  // Skipped
        buffer.Add(2);

        // Assert - Sequence IDs should be 1 and 2 (no gap from the skipped duplicate)
        buffer.Count.Should().Be(2);
        var entries = buffer.GetAllWithSequenceIds();
        entries.Should().HaveCount(2);
        entries[0].SequenceId.Should().Be(1);
        entries[1].SequenceId.Should().Be(2);
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

    #region Snapshot Tests

    [Fact]
    public void GetSnapshot_EmptyBuffer_HasNullSequenceRange()
    {
        var buffer = new CircularBuffer<int>(5);

        BufferSnapshot<int> snapshot = buffer.GetSnapshot();

        snapshot.Count.Should().Be(0);
        snapshot.MinSequenceId.Should().BeNull();
        snapshot.MaxSequenceId.Should().BeNull();
        snapshot.Entries.Should().BeEmpty();
    }

    [Fact]
    public void GetSnapshot_AfterWrap_MetadataMatchesEntries()
    {
        var buffer = new CircularBuffer<int>(3);
        for (int i = 1; i <= 5; i++)
        {
            buffer.Add(i * 10);
        }

        BufferSnapshot<int> snapshot = buffer.GetSnapshot();

        snapshot.Count.Should().Be(3);
        snapshot.MinSequenceId.Should().Be(3);
        snapshot.MaxSequenceId.Should().Be(5);
        snapshot.Entries.Should().Equal((30, 3L), (40, 4L), (50, 5L));
    }

    [Fact]
    public void GetRecentSnapshot_ReturnsNewestEntriesWithWholeBufferMetadata()
    {
        var buffer = new CircularBuffer<int>(10);
        for (int i = 1; i <= 6; i++)
        {
            buffer.Add(i);
        }

        BufferSnapshot<int> snapshot = buffer.GetRecentSnapshot(2);

        snapshot.Count.Should().Be(6);
        snapshot.MinSequenceId.Should().Be(1);
        snapshot.MaxSequenceId.Should().Be(6);
        snapshot.Entries.Should().Equal((5, 5L), (6, 6L));
    }

    [Theory]
    [InlineData(0L, new long[] { 3, 4, 5 })]  // below the minimum: everything left in the buffer
    [InlineData(3L, new long[] { 4, 5 })]
    [InlineData(5L, new long[0])]              // at the maximum: nothing new
    public void GetSnapshotAfter_AfterWrap_ReturnsEntriesPastTheCursor(long after, long[] expectedIds)
    {
        var buffer = new CircularBuffer<int>(3);
        for (int i = 1; i <= 5; i++)
        {
            buffer.Add(i);
        }

        BufferSnapshot<int> snapshot = buffer.GetSnapshotAfter(after);

        snapshot.Count.Should().Be(3);
        snapshot.MinSequenceId.Should().Be(3);
        snapshot.MaxSequenceId.Should().Be(5);
        snapshot.Entries.Select(e => e.SequenceId).Should().Equal(expectedIds);
    }

    [Fact]
    public async Task GetSnapshot_UnderConcurrentAdds_NeverReportsAnOlderMaximumThanItsEntries()
    {
        var buffer = new CircularBuffer<int>(50);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        Task writer = Task.Run(() =>
        {
            int i = 0;
            while (!cts.IsCancellationRequested)
            {
                buffer.Add(i++);
            }
        });

        bool consistent = true;
        while (!cts.IsCancellationRequested)
        {
            BufferSnapshot<int> snapshot = buffer.GetSnapshotAfter(0);
            if (snapshot.Entries.Count > 0 &&
                (snapshot.Entries[^1].SequenceId != snapshot.MaxSequenceId ||
                 snapshot.Entries[0].SequenceId != snapshot.MinSequenceId ||
                 snapshot.Entries.Count != snapshot.Count))
            {
                consistent = false;
                break;
            }
        }

        await writer;
        consistent.Should().BeTrue();
    }

    #endregion
}
