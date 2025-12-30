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

namespace Aeromux.Core.Tests.Builders;

/// <summary>
/// Fluent builder for creating TrackingConfig instances for testing.
/// Provides sensible defaults optimized for test performance (smaller buffers, faster timeouts).
/// </summary>
public class TrackingConfigBuilder
{
    private ConfidenceLevel _confidenceLevel = ConfidenceLevel.Medium;
    private int _icaoTimeoutSeconds = 30;
    private int _aircraftTimeoutMinutes = 60;
    private int _maxHistorySize = 100;  // Smaller than default (1000) for faster tests
    private bool _enablePositionHistory = true;
    private bool _enableAltitudeHistory = true;
    private bool _enableVelocityHistory = true;

    /// <summary>
    /// Sets the maximum size for history circular buffers.
    /// Default: 100 (smaller than production default of 1000 for test performance)
    /// </summary>
    /// <param name="size">Maximum number of historical snapshots per buffer</param>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithMaxHistorySize(int size)
    {
        _maxHistorySize = size;
        return this;
    }

    /// <summary>
    /// Sets the aircraft timeout in minutes.
    /// Aircraft not seen within this time will be expired and removed.
    /// </summary>
    /// <param name="minutes">Timeout in minutes (use small values like 1 for fast tests)</param>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithAircraftTimeout(int minutes)
    {
        _aircraftTimeoutMinutes = minutes;
        return this;
    }

    /// <summary>
    /// Sets the ICAO confidence level for frame validation.
    /// </summary>
    /// <param name="level">Confidence level (Low=5, Medium=10, High=15 detections)</param>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithConfidenceLevel(ConfidenceLevel level)
    {
        _confidenceLevel = level;
        return this;
    }

    /// <summary>
    /// Disables all history tracking to save memory in tests.
    /// Useful for tests that don't need historical data.
    /// </summary>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithHistoryDisabled()
    {
        _enablePositionHistory = false;
        _enableAltitudeHistory = false;
        _enableVelocityHistory = false;
        return this;
    }

    /// <summary>
    /// Disables position history only.
    /// </summary>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithPositionHistoryDisabled()
    {
        _enablePositionHistory = false;
        return this;
    }

    /// <summary>
    /// Disables altitude history only.
    /// </summary>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithAltitudeHistoryDisabled()
    {
        _enableAltitudeHistory = false;
        return this;
    }

    /// <summary>
    /// Disables velocity history only.
    /// </summary>
    /// <returns>This builder for fluent chaining</returns>
    public TrackingConfigBuilder WithVelocityHistoryDisabled()
    {
        _enableVelocityHistory = false;
        return this;
    }

    /// <summary>
    /// Builds a TrackingConfig instance with the configured settings.
    /// </summary>
    /// <returns>TrackingConfig ready for use in tests</returns>
    public TrackingConfig Build()
    {
        return new TrackingConfig
        {
            ConfidenceLevel = _confidenceLevel,
            IcaoTimeoutSeconds = _icaoTimeoutSeconds,
            AircraftTimeoutMinutes = _aircraftTimeoutMinutes,
            MaxHistorySize = _maxHistorySize,
            EnablePositionHistory = _enablePositionHistory,
            EnableAltitudeHistory = _enableAltitudeHistory,
            EnableVelocityHistory = _enableVelocityHistory
        };
    }
}
