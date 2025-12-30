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
using Aeromux.Core.Tests.Builders;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Base class for AircraftStateTracker unit tests.
/// Provides shared helper methods, test configuration, and resource cleanup.
/// Test classes inherit from this base to access common functionality.
/// </summary>
public class AircraftStateTrackerTestsBase : IDisposable
{
    public AircraftStateTracker? _tracker;
    public readonly List<IDisposable> _disposables = [];

    /// <summary>
    /// Helper method to create a test configuration with sensible defaults.
    /// </summary>
    public static TrackingConfig CreateTestConfig() =>
        new TrackingConfigBuilder().Build();

    /// <summary>
    /// Helper method to create a ProcessedFrame from hex data and ICAO.
    /// </summary>
    public static ProcessedFrame CreateFrame(string hexData, string icao) =>
        new ProcessedFrameBuilder()
            .WithHexData(hexData)
            .WithIcaoAddress(icao)
            .Build();

    /// <summary>
    /// Helper method to create a ProcessedFrame with a shared parser for CPR decoding.
    /// Use this when testing CPR position decoding that requires even/odd frame pairs.
    /// </summary>
    public static ProcessedFrame CreateFrame(string hexData, string icao, Aeromux.Core.ModeS.MessageParser parser) =>
        new ProcessedFrameBuilder()
            .WithHexData(hexData)
            .WithIcaoAddress(icao)
            .WithParser(parser)
            .Build();

    /// <summary>
    /// Helper method to create a tracker with custom timeout (in seconds for fast tests).
    /// </summary>
    public AircraftStateTracker CreateTrackerWithTimeout(int timeoutSeconds)
    {
        TrackingConfig config = CreateTestConfig();
        var tracker = new AircraftStateTracker(config)
        {
            AircraftTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            CleanupInterval = TimeSpan.FromMilliseconds(100) // Fast cleanup for tests
        };
        _disposables.Add(tracker);
        return tracker;
    }

    /// <summary>
    /// Helper method to create a standard tracker and track it for disposal.
    /// </summary>
    public AircraftStateTracker CreateTracker()
    {
        var tracker = new AircraftStateTracker(CreateTestConfig());
        _disposables.Add(tracker);
        return tracker;
    }

    /// <summary>
    /// Cleanup resources after each test.
    /// </summary>
    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
        _tracker = null;

        GC.SuppressFinalize(this);
    }
}
