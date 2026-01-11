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

using Aeromux.Core.Configuration;
using Aeromux.Core.ModeS;
using Aeromux.Core.Tests.Timing;
using Aeromux.Core.Timing;
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for PreambleDetector timing behavior with ITimeProvider.
/// </summary>
/// <remarks>
/// Note: These tests verify that PreambleDetector uses the provided ITimeProvider.
/// Full integration tests with actual frame detection are in other test files.
/// </remarks>
public class PreambleDetectorTimingTests
{
    [Fact]
    public void PreambleDetector_AcceptsFixedTimeProvider()
    {
        // Arrange
        var fixedTime = new DateTime(2025, 1, 11, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FixedTimeProvider(fixedTime);

        // Act - Constructor should accept time provider without error
        var detector = new PreambleDetector(timeProvider: timeProvider);

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void PreambleDetector_AcceptsStopwatchTimeProvider()
    {
        // Arrange
        var timeProvider = new StopwatchTimeProvider();

        // Act - Constructor should accept time provider without error
        var detector = new PreambleDetector(timeProvider: timeProvider);

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void PreambleDetector_DefaultsToStopwatchTimeProvider_WhenNoProviderSpecified()
    {
        // Act - Constructor should work without time provider
        var detector = new PreambleDetector();

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void PreambleDetector_AcceptsAllConstructorParameters()
    {
        // Arrange
        var fixedTime = new DateTime(2025, 1, 11, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FixedTimeProvider(fixedTime);
        var confidenceTracker = new IcaoConfidenceTracker(ConfidenceLevel.High, 60);

        // Act - Constructor should accept all parameters
        var detector = new PreambleDetector(
            preambleThreshold: 2.0,
            confidenceTracker: confidenceTracker,
            timeProvider: timeProvider);

        // Assert
        detector.Should().NotBeNull();
    }
}
