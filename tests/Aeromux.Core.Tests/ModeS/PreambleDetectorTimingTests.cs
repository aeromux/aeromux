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
using Aeromux.Core.ModeS;
using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for PreambleDetector sample-offset timestamp behavior.
/// </summary>
/// <remarks>
/// PreambleDetector no longer depends on ITimeProvider. Instead, it receives a buffer
/// timestamp via DetectAndExtract() and computes per-frame timestamps from sample position.
/// These tests verify the constructor API after the ITimeProvider removal.
/// </remarks>
public class PreambleDetectorTimingTests
{
    [Fact]
    public void PreambleDetector_DefaultConstructor_CreatesInstance()
    {
        // Act
        var detector = new PreambleDetector();

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void PreambleDetector_WithPreambleThreshold_CreatesInstance()
    {
        // Act
        var detector = new PreambleDetector(preambleThreshold: 2.0);

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void PreambleDetector_WithAllParameters_CreatesInstance()
    {
        // Arrange
        using var confidenceTracker = new IcaoConfidenceTracker(ConfidenceLevel.High, 60);

        // Act
        var detector = new PreambleDetector(
            preambleThreshold: 2.0,
            confidenceTracker: confidenceTracker);

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void PreambleDetector_InvalidThreshold_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        var act = () => new PreambleDetector(preambleThreshold: 0.5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
