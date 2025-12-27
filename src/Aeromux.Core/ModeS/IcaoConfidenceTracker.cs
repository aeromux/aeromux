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

namespace Aeromux.Core.ModeS;

/// <summary>
/// Tracks ICAO address detections to filter noise from real aircraft signals.
/// Only frames meeting the confidence threshold are passed to message parsing.
/// Implements automatic cleanup of expired ICAOs to prevent memory growth.
/// </summary>
/// <remarks>
/// Purpose: Separate real aircraft from noise/false positives
///
/// Why needed:
/// - AP mode frames have no direct validation (ICAO extracted from CRC)
/// - Noise can create "valid" AP frames with random ICAOs
/// - Real aircraft send hundreds of messages per minute
/// - Noise creates unique ICAOs appearing once or twice
///
/// Confidence tracking:
/// - Count detections per ICAO address
/// - Only pass frames from ICAOs seen N+ times (configurable threshold)
/// - Track PI vs AP mode (PI mode provides stronger validation)
///
/// Memory management:
/// - Lazy cleanup: Remove ICAOs not seen within timeout (default 30s)
/// - Cleanup happens automatically during TrackAndValidate (every 100 frames)
/// - Prevents unbounded dictionary growth
/// - Typical memory: ~100-200 active ICAOs in normal environment
///
/// References:
/// - Professional Mode S decoders use similar confidence tracking to filter noise
/// - Typical implementations require 3-10 detections before displaying aircraft
/// </remarks>
public sealed class IcaoConfidenceTracker
{
    private readonly Dictionary<string, IcaoRecord> _icaoRecords = new();
    private readonly ConfidenceLevel _requiredConfidence;
    private readonly TimeSpan _timeout;

    // Statistics (exposed as properties for DeviceWorker to log)
    private long _totalFrames;
    private long _confidentFrames;
    private long _unconfidentFrames;  // Frames that didn't meet confidence threshold
    private long _newConfirmedIcaos;
    private long _expiredIcaos;

    /// <summary>
    /// Initializes ICAO confidence tracker with global settings.
    /// </summary>
    /// <param name="requiredConfidence">Confidence level (Low=5, Medium=10, High=15 detections)</param>
    /// <param name="timeoutSeconds">Remove ICAOs not seen within this many seconds</param>
    public IcaoConfidenceTracker(ConfidenceLevel requiredConfidence, int timeoutSeconds)
    {
        if (!Enum.IsDefined(typeof(ConfidenceLevel), requiredConfidence))
        {
            throw new ArgumentOutOfRangeException(nameof(requiredConfidence),
                $"Invalid confidence level: {requiredConfidence}");
        }

        if (timeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds),
                $"Timeout must be positive (got {timeoutSeconds})");
        }

        _requiredConfidence = requiredConfidence;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    /// <summary>
    /// Checks if an ICAO address is already confident (reached detection threshold).
    /// Used by PreambleDetector to filter AP mode messages from unknown aircraft.
    /// </summary>
    /// <param name="icaoAddress">ICAO address to check (e.g., "4BC889")</param>
    /// <returns>True if ICAO has reached confidence threshold</returns>
    public bool IsConfident(string icaoAddress) =>
        _icaoRecords.TryGetValue(icaoAddress, out IcaoRecord? record) && record.IsConfident;

    /// <summary>
    /// Tracks a validated frame and determines if it meets confidence threshold.
    /// Automatically performs lazy cleanup of expired ICAOs every 100 frames.
    /// </summary>
    /// <param name="frame">Validated frame from CRC validation</param>
    /// <param name="isNewConfirmedIcao">True if this ICAO just reached confidence threshold</param>
    /// <returns>True if frame meets confidence threshold and should be passed to parsing</returns>
    public bool TrackAndValidate(ValidatedFrame frame, out bool isNewConfirmedIcao)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _totalFrames++;

        // Lazy cleanup: Remove expired ICAOs every 100 frames
        // Spreads cleanup work over time without blocking or requiring separate task
        // At typical rates: 2-5 frames/second = cleanup every 20-50 seconds
        if (_totalFrames % 100 == 0)
        {
            CleanupExpired(frame.Timestamp);
        }

        string icao = frame.IcaoAddress;

        // Get or create ICAO record
        if (!_icaoRecords.TryGetValue(icao, out IcaoRecord? record))
        {
            record = new IcaoRecord(icao, frame.Timestamp, (int)_requiredConfidence);
            _icaoRecords[icao] = record;
        }

        // Track detection
        bool wasConfident = record.IsConfident;
        record.IncrementDetections(frame);

        // Check if just became confident (crossed threshold)
        isNewConfirmedIcao = !wasConfident && record.IsConfident;
        if (isNewConfirmedIcao)
        {
            _newConfirmedIcaos++;
        }

        // Only pass frames that meet confidence threshold
        if (record.IsConfident)
        {
            _confidentFrames++;
            return true;
        }

        // Track frames that didn't meet confidence threshold
        _unconfidentFrames++;
        return false;
    }

    /// <summary>
    /// Removes ICAOs not seen within timeout period.
    /// Called automatically during TrackAndValidate (every 100 frames).
    /// </summary>
    /// <param name="currentTime">Current timestamp for expiry calculation</param>
    private void CleanupExpired(DateTime currentTime)
    {
        // Find expired ICAOs
        var expiredIcaos = _icaoRecords
            .Where(kvp => currentTime - kvp.Value.LastSeen > _timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        // Remove them
        foreach (string icao in expiredIcaos)
        {
            _icaoRecords.Remove(icao);
        }

        _expiredIcaos += expiredIcaos.Count;
    }

    // Statistics properties for Coordinator Pattern (ADR-009)
    // DeviceWorker reads these for logging, tracker never logs itself

    /// <summary>Total frames processed (both confident and non-confident)</summary>
    public long TotalFrames => _totalFrames;

    /// <summary>Frames meeting confidence threshold</summary>
    public long ConfidentFrames => _confidentFrames;

    /// <summary>Frames that didn't meet confidence threshold (rejected after extraction)</summary>
    public long UnconfidentFrames => _unconfidentFrames;

    /// <summary>Number of ICAOs that reached confidence threshold</summary>
    public long NewConfirmedIcaos => _newConfirmedIcaos;

    /// <summary>Total ICAOs removed due to timeout (cumulative)</summary>
    public long ExpiredIcaos => _expiredIcaos;

    /// <summary>Currently tracked ICAOs (active + unconfident)</summary>
    public int TrackedIcaos => _icaoRecords.Count;

    /// <summary>ICAOs meeting confidence threshold (subset of TrackedIcaos)</summary>
    public int ConfirmedIcaos => _icaoRecords.Values.Count(r => r.IsConfident);

    /// <summary>Gets the set of confirmed ICAO addresses for deduplication across devices</summary>
    public IEnumerable<string> GetConfirmedIcaoAddresses() =>
        _icaoRecords.Where(kvp => kvp.Value.IsConfident).Select(kvp => kvp.Key);

    /// <summary>Gets the set of all tracked ICAO addresses for deduplication across devices</summary>
    public IEnumerable<string> GetTrackedIcaoAddresses() => _icaoRecords.Keys;
}

/// <summary>
/// Tracks detection statistics for a single ICAO address.
/// </summary>
internal sealed class IcaoRecord
{
    private readonly int _confidenceThreshold;

    public string Icao { get; }
    public int DetectionCount { get; private set; }
    public DateTime FirstSeen { get; }
    public DateTime LastSeen { get; private set; }

    /// <summary>Number of PI mode detections (DF 11, 17, 18, 19) - strong validation</summary>
    public int PICount { get; private set; }

    /// <summary>Number of AP mode detections (DF 0, 4, 5, 16, 20, 21) - weak validation</summary>
    public int APCount { get; private set; }

    /// <summary>True if this ICAO has reached the confidence threshold</summary>
    public bool IsConfident => DetectionCount >= _confidenceThreshold;

    public IcaoRecord(string icao, DateTime firstSeen, int confidenceThreshold)
    {
        Icao = icao;
        FirstSeen = firstSeen;
        LastSeen = firstSeen;
        _confidenceThreshold = confidenceThreshold;
    }

    public void IncrementDetections(ValidatedFrame frame)
    {
        DetectionCount++;
        LastSeen = frame.Timestamp;

        // Track PI vs AP mode for future cross-validation enhancements
        if (frame.UsesPIMode)
        {
            PICount++;
        }
        else
        {
            APCount++;
        }
    }
}
