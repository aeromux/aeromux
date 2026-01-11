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
        DateTime after = DateTime.UtcNow;

        // Act
        DateTime timestamp = provider.GetCurrentTimestamp();

        // Assert
        timestamp.Should().BeOnOrAfter(before);
        timestamp.Should().BeOnOrBefore(after.AddMilliseconds(10)); // Allow small processing time
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
        DateTime start = provider.GetCurrentTimestamp();

        // Act
        Thread.Sleep(100); // Sleep for 100ms
        DateTime end = provider.GetCurrentTimestamp();

        // Assert
        TimeSpan elapsed = end - start;
        elapsed.TotalMilliseconds.Should().BeInRange(90, 120); // Allow ±20ms for OS scheduling
    }
}
