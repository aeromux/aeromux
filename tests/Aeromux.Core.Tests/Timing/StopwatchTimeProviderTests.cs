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

using System.Diagnostics;
using Aeromux.Core.Timing;
using FluentAssertions;

namespace Aeromux.Core.Tests.Timing;

public class StopwatchTimeProviderTests
{
    [Fact]
    public void GetCurrentTimestamp_ReturnsTimeCloseToConstruction()
    {
        // Arrange
        DateTime before = DateTime.UtcNow;
        var provider = new StopwatchTimeProvider();

        // Act
        DateTime timestamp = provider.GetCurrentTimestamp();

        // Read after the call, so a thread paused before it widens the bound instead of failing the test
        DateTime after = DateTime.UtcNow;

        // Assert
        timestamp.Should().BeOnOrAfter(before);
        timestamp.Should().BeOnOrBefore(after.AddMilliseconds(10)); // Allow drift between DateTime.UtcNow and Stopwatch
    }

    [Fact]
    public void GetCurrentTimestamp_ProgressesMonotonically()
    {
        // Arrange
        var provider = new StopwatchTimeProvider();

        // Act
        DateTime timestamp1 = provider.GetCurrentTimestamp();
        Thread.Sleep(1); // Small delay to ensure progression
        DateTime timestamp2 = provider.GetCurrentTimestamp();
        Thread.Sleep(1);
        DateTime timestamp3 = provider.GetCurrentTimestamp();

        // Assert
        timestamp2.Should().BeAfter(timestamp1);
        timestamp3.Should().BeAfter(timestamp2);
    }

    [Fact]
    public void GetCurrentTimestamp_HasSubMillisecondPrecision()
    {
        // Arrange
        var provider = new StopwatchTimeProvider();

        // Act - Take rapid samples
        var timestamps = new List<DateTime>();
        for (int i = 0; i < 100; i++)
        {
            timestamps.Add(provider.GetCurrentTimestamp());
        }

        // Assert - Should have many sub-millisecond deltas
        var deltas = new List<double>();
        for (int i = 1; i < timestamps.Count; i++)
        {
            double deltaMs = (timestamps[i] - timestamps[i - 1]).TotalMilliseconds;
            if (deltaMs > 0) // Ignore zero deltas (can happen on fast CPUs)
            {
                deltas.Add(deltaMs);
            }
        }

        // At least some deltas should be sub-millisecond (< 1ms)
        deltas.Should().Contain(d => d < 1.0, "should have sub-millisecond precision");
    }

    [Fact]
    public void GetCurrentTimestamp_IsThreadSafe()
    {
        // Arrange
        var provider = new StopwatchTimeProvider();
        var timestamps = new System.Collections.Concurrent.ConcurrentBag<DateTime>();

        // Act - Call from multiple threads
        Parallel.For(0, 1000, _ =>
        {
            timestamps.Add(provider.GetCurrentTimestamp());
        });

        // Assert - All timestamps should be valid (no exceptions thrown)
        timestamps.Should().HaveCount(1000);

        // All timestamps should be monotonic or equal (thread scheduling may cause equal)
        var sorted = timestamps.OrderBy(t => t.Ticks).ToList();
        for (int i = 1; i < sorted.Count; i++)
        {
            sorted[i].Should().BeOnOrAfter(sorted[i - 1]);
        }
    }

    [Fact]
    public void GetCurrentTimestamp_AccuratelyTracksElapsedTime()
    {
        // Arrange
        var provider = new StopwatchTimeProvider();
        var reference = Stopwatch.StartNew();

        // Act - Nest each provider reading between two reference readings. The provider's interval
        // then always contains the inner reference interval and fits inside the outer one, so a
        // thread paused anywhere widens the bounds instead of failing the test.
        TimeSpan outerStart = reference.Elapsed;
        DateTime start = provider.GetCurrentTimestamp();
        TimeSpan innerStart = reference.Elapsed;

        // Busy-wait rather than sleep: the bounds are exact, so the window only has to dwarf the
        // call overhead and tick rounding, and spinning never yields the thread to the scheduler
        while (reference.Elapsed - innerStart < TimeSpan.FromMilliseconds(1))
        {
        }

        TimeSpan innerEnd = reference.Elapsed;
        DateTime end = provider.GetCurrentTimestamp();
        TimeSpan outerEnd = reference.Elapsed;

        // Assert - Each reading rounds raw timer ticks down to 100 ns TimeSpan ticks, so allow 1 µs
        TimeSpan elapsed = end - start;
        TimeSpan rounding = TimeSpan.FromMicroseconds(1);
        elapsed.Should().BeGreaterThanOrEqualTo(innerEnd - innerStart - rounding);
        elapsed.Should().BeLessThanOrEqualTo(outerEnd - outerStart + rounding);
    }
}
