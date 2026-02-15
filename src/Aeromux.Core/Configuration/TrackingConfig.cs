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

namespace Aeromux.Core.Configuration;

/// <summary>
/// Aircraft tracking and state management configuration.
/// </summary>
public class TrackingConfig
{
    /// <summary>
    /// Gets or sets the ICAO confidence level (global setting for all devices).
    /// Determines how many detections required before frames are passed to parsing.
    /// Low=5, Medium=10, High=15 detections required.
    /// Default: Medium (10 detections)
    /// </summary>
    public ConfidenceLevel ConfidenceLevel { get; set; } = ConfidenceLevel.Medium;

    /// <summary>
    /// Gets or sets the ICAO timeout in seconds (global setting for all devices).
    /// ICAOs not seen within this time are removed from confidence tracking.
    /// Prevents memory growth and removes departed aircraft/noise.
    /// Based on ICAO Doc 9871 (Technical Provisions for Mode S Services and Extended Squitter).
    /// Default: 30 seconds
    /// </summary>
    public int IcaoTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the aircraft timeout in seconds.
    /// Aircraft that haven't sent messages within this time are removed from tracking.
    /// Default: 60 seconds
    /// </summary>
    public int AircraftTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Enable position history tracking (circular buffer).
    /// Default: true
    /// </summary>
    public bool EnablePositionHistory { get; set; } = true;

    /// <summary>
    /// Enable altitude history tracking (circular buffer).
    /// Default: true
    /// </summary>
    public bool EnableAltitudeHistory { get; set; } = true;

    /// <summary>
    /// Enable velocity history tracking (circular buffer).
    /// Default: true
    /// </summary>
    public bool EnableVelocityHistory { get; set; } = true;

    /// <summary>
    /// Maximum number of historical data points per buffer.
    /// Default: 1000 (per aircraft, per buffer)
    /// Memory: ~96 KB per aircraft with all histories enabled
    /// </summary>
    public int MaxHistorySize { get; set; } = 1000;
}

/// <summary>
/// ICAO confidence levels (global setting for all devices).
/// Determines how many detections required before passing frames to parsing.
/// Higher levels reduce false positives but require stronger signals.
/// Real aircraft transmit 5-10 messages/second, so thresholds are reached quickly (~2-8 seconds).
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// Low confidence: 5+ detections required.
    /// Use for indoor reception, weak signals, or high sensitivity.
    /// Confirmation time: ~2-3 seconds for real aircraft.
    /// </summary>
    Low = 5,

    /// <summary>
    /// Medium confidence: 10+ detections required (default).
    /// Balanced setting for normal conditions.
    /// Good trade-off between sensitivity and false positive rejection.
    /// Confirmation time: ~5 seconds for real aircraft.
    /// </summary>
    Medium = 10,

    /// <summary>
    /// High confidence: 15+ detections required.
    /// Use for outdoor reception, strong signals, or high-noise environments.
    /// Minimal false positives from noise.
    /// Confirmation time: ~7-8 seconds for real aircraft.
    /// </summary>
    High = 15
}
