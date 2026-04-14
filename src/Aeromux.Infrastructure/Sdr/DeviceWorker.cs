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

using Serilog;
using RtlSdrManager;
using RtlSdrManager.Modes;
using Aeromux.Core.Configuration;
using Aeromux.Core.SignalProcessing;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;
using Aeromux.Core.Timing;

namespace Aeromux.Infrastructure.Sdr;

/// <summary>
/// Manages a single RTL-SDR device: opening, configuration, and sample reception.
/// Each DeviceWorker runs in its own task and processes samples from one device.
/// Uses Serilog for structured logging.
/// </summary>
public sealed class DeviceWorker : IDisposable
{
    /// <summary>
    /// Mode S / ADS-B center frequency in MHz (industry standard).
    /// This frequency is hardcoded as Aeromux is a specialized Mode S/ADS-B decoder.
    /// </summary>
    private const double CenterFrequency = 1090.0;

    /// <summary>
    /// Sample rate in MSPS (industry standard for Mode S/ADS-B).
    /// This sample rate is hardcoded as signal processing algorithms (phase correlation)
    /// are specifically tuned for 2.4 MSPS.
    /// </summary>
    private const double SampleRate = 2.4;

    private readonly SdrSourceConfig _config;
    private readonly TrackingConfig _trackingConfig;
    private readonly RtlSdrDeviceManager _deviceManager = RtlSdrDeviceManager.Instance;
    private readonly ITimeProvider _timeProvider;
    private readonly IQDemodulator _demodulator = new();
    private readonly PreambleDetector _preambleDetector;
    private readonly ValidatedFrameFactory _validatedFrameFactory = new();
    private readonly IcaoConfidenceTracker _confidenceTracker;
    private readonly bool _ownsConfidenceTracker;  // True when tracker is self-created (not shared), responsible for disposal
    private readonly FrameDeduplicator _frameDeduplicator;
    private readonly MessageParser _messageParser;
    private readonly Action<ValidatedFrame, ModeSMessage?>? _onDataParsed;
    private RtlSdrManagedDevice? _device;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public DeviceWorker(
        SdrSourceConfig sourceConfig,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig,
        IcaoConfidenceTracker? confidenceTracker = null,
        Action<ValidatedFrame, ModeSMessage?>? onDataParsed = null)
    {
        // RtlSdrManager handles console output suppression automatically during device operations
        // using scoped suppression with reference counting (fixed in RtlSdrManager v0.5.2+).
        // Default is false (show librtlsdr messages). Applications can set
        // RtlSdrDeviceManager.SuppressLibraryConsoleOutput = true to suppress if needed.
        RtlSdrDeviceManager.SuppressLibraryConsoleOutput = true;

        _config = sourceConfig ?? throw new ArgumentNullException(nameof(sourceConfig));
        _trackingConfig = trackingConfig ?? throw new ArgumentNullException(nameof(trackingConfig));
        _onDataParsed = onDataParsed;

        // Initialize high-precision time provider for this device
        _timeProvider = new StopwatchTimeProvider();

        // Initialize confidence tracker (shared across devices + MLAT, or create new for standalone)
        // Shared tracker enables MLAT to mark ICAOs as confident for all SDR workers
        _ownsConfidenceTracker = confidenceTracker == null;
        _confidenceTracker = confidenceTracker ?? new IcaoConfidenceTracker(
            trackingConfig.ConfidenceLevel,
            trackingConfig.IcaoTimeoutSeconds);

        // Pass confidence tracker to PreambleDetector for AP mode ICAO filtering
        _preambleDetector = new PreambleDetector(sourceConfig.PreambleThreshold, _confidenceTracker);

        // Initialize frame deduplicator with config values
        _frameDeduplicator = new FrameDeduplicator(
            sourceConfig.DeduplicationWindow,
            sourceConfig.MaxTrackedFrames);

        // Initialize MessageParser with device context for logging
        _messageParser = new MessageParser(sourceConfig.Name, sourceConfig.DeviceIndex);

        // Configure MessageParser with receiver location if available (for TC 5-8 surface position)
        if (receiverConfig?.Latitude.HasValue == true && receiverConfig?.Longitude.HasValue == true)
        {
            var receiverLocation = new Core.ModeS.ValueObjects.GeographicCoordinate(
                receiverConfig.Latitude.Value,
                receiverConfig.Longitude.Value);
            _messageParser.SetReceiverLocation(receiverLocation);
            Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}): MessageParser configured with receiver location",
                sourceConfig.Name, sourceConfig.DeviceIndex);
        }
    }

    // Statistics tracking
    private long _totalSamplesReceived;        // Total IQ samples received since StartReceiving()
    private long _lastLoggedSampleCount;       // Sample count at last statistics log (for delta calculation)
    private DateTime _receptionStartTime;      // When StartReceiving() was called (tracked via time provider)

    /// <summary>
    /// Opens the RTL-SDR device and configures it according to the config.
    /// Sets center frequency, sample rate, gain mode, and USB buffer parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when device cannot be opened or configured.</exception>
    /// <remarks>
    /// Must be called before StartReceiving(). USB buffer configuration is critical for stable operation.
    /// </remarks>
    public void OpenDevice()
    {
        Log.Information("Opening RTL-SDR device: {DeviceName} (index={DeviceIndex})",
            _config.Name, _config.DeviceIndex);

        // Validate device index
        if (_config.DeviceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_config.DeviceIndex),
                $"Device '{_config.Name}': Device index must be non-negative (got {_config.DeviceIndex})");
        }

        // Validate gain mode enum
        if (!Enum.IsDefined(_config.GainMode))
        {
            throw new ArgumentOutOfRangeException(nameof(_config.GainMode),
                $"Device '{_config.Name}': Invalid gain mode: {_config.GainMode}");
        }

        // Validate tuner gain (only if in Manual mode)
        if (_config is
            { GainMode: TunerGainModes.Manual, TunerGain: < 0 or > 50 })
        {
            throw new ArgumentOutOfRangeException(nameof(_config.TunerGain),
                $"Device '{_config.Name}': Tuner gain must be between 0 and 50 dB (got {_config.TunerGain})");
        }

        // Validate PPM correction (typical range)
        if (_config.PpmCorrection is < -1000 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(_config.PpmCorrection),
                $"Device '{_config.Name}': PPM correction must be between -1000 and +1000 (got {_config.PpmCorrection})");
        }

        try
        {
            // Open device with friendly name
            // Console output suppression is enabled by default in RtlSdrManager v0.5.0+
            _deviceManager.OpenManagedDevice((uint)_config.DeviceIndex, _config.Name);
            _device = _deviceManager[_config.Name];

            // Configure device - frequencies and sample rates (hardcoded for Mode S/ADS-B)
            _device.CenterFrequency = Frequency.FromMHz(CenterFrequency);
            _device.SampleRate = Frequency.FromMHz(SampleRate);
            _device.TunerGainMode = _config.GainMode;

            // Gain configuration - conditional based on gain mode
            // AGC (Automatic Gain Control) mode: Hardware automatically adjusts gain
            // Manual mode: Software sets specific gain value
            // IMPORTANT: Attempting to set TunerGain while in AGC mode throws an exception
            if (_config.GainMode == TunerGainModes.Manual)
            {
                _device.TunerGain = _config.TunerGain;
            }

            _device.FrequencyCorrection = _config.PpmCorrection;

            // Format gain info for logging (show specific dB value in Manual mode, "auto" for AGC)
            string gainInfo = _config.GainMode == TunerGainModes.Manual
                ? $"{_config.TunerGain}dB"
                : "auto";

            // USB buffer configuration - critical for stable RTL-SDR operation
            // Without proper buffering, USB transfers fail with "Failed to submit transfer" errors
            //
            // Buffer sizing:
            //   - MaxAsyncBufferSize: 512 KB (internal USB buffer)
            //   - requestedSamples: 131,072 samples = 256 KB per callback
            //   - Relationship: Buffer is 2x request size to provide headroom for USB transfers
            _device.MaxAsyncBufferSize = 512 * 1024;    // 512 KB USB buffer (2x request size)
            _device.DropSamplesOnFullBuffer = true;     // Prevent blocking if processing falls behind
            _device.UseRawBufferMode = true;            // Zero-copy raw buffer mode (eliminates per-sample IQData allocation)
            _device.ResetDeviceBuffer();                // Clear any stale data from previous operations

            Log.Information("Device '{DeviceName}' (index: {DeviceIndex}) configured: Freq={Frequency}MHz, SR={SampleRate}MHz, Gain={Gain}, Mode={GainMode}",
                _config.Name,
                _config.DeviceIndex,
                CenterFrequency,
                SampleRate,
                gainInfo,
                _config.GainMode);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open device {DeviceName} (index={DeviceIndex})",
                _config.Name, _config.DeviceIndex);
            throw;
        }
    }

    /// <summary>
    /// Starts receiving IQ samples asynchronously from the RTL-SDR device.
    /// Subscribes to sample events and starts a background statistics logging task.
    /// </summary>
    /// <param name="cancellationToken">Token to signal shutdown. When canceled, stops sample reception.</param>
    /// <exception cref="InvalidOperationException">Thrown when OpenDevice() has not been called first.</exception>
    /// <remarks>
    /// This method starts async sample reading with 131,072 samples (8 buffers × 16384).
    /// Samples are delivered via the OnSamplesAvailable event handler.
    /// Statistics are logged every 10 seconds via the background StatisticsLoop task.
    /// </remarks>
    public void StartReceiving(CancellationToken cancellationToken)
    {
        if (_device == null)
        {
            throw new InvalidOperationException($"Device {_config.Name} not opened. Call OpenDevice() first.");
        }

        Log.Information("Starting sample reception for device '{DeviceName}' (index: {DeviceIndex})", _config.Name, _config.DeviceIndex);

        // Create linked token source so we can cancel independently if needed
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receptionStartTime = _timeProvider.GetCurrentTimestamp();

        // Subscribe to sample availability events from RtlSdrManager
        _device.SamplesAvailable += OnSamplesAvailable;

        // Start async reading with 8 buffers of 16384 samples each (131,072 total)
        _device.StartReadSamplesAsync(requestedSamples: 8 * 16384);

        // Start background task for periodic statistics logging (every 10 seconds)
        _workerTask = Task.Run(() => StatisticsLoop(_cts.Token), _cts.Token);

        Log.Information("Device '{DeviceName}' (index: {DeviceIndex}) receiving samples", _config.Name, _config.DeviceIndex);
    }

    /// <summary>
    /// Stops receiving samples and closes the device.
    /// Called automatically by Dispose(). Cancels background tasks and releases all resources.
    /// </summary>
    /// <remarks>
    /// Waits up to 5 seconds for the statistics task to complete gracefully.
    /// Unsubscribes from sample events and closes the RTL-SDR device.
    /// </remarks>
    private void Stop()
    {
        Log.Information("Stopping device '{DeviceName}' (index: {DeviceIndex})", _config.Name, _config.DeviceIndex);

        // Log final statistics summary
        LogFinalStatistics();

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_device != null)
        {
            _device.SamplesAvailable -= OnSamplesAvailable;
            _device.StopReadSamplesAsync();
            _deviceManager.CloseManagedDevice(_config.Name);
            _device = null;
        }

        if (_workerTask != null)
        {
            // Wait up to 5 seconds for statistics task to complete (should exit quickly after cancellation)
            _workerTask.Wait(TimeSpan.FromSeconds(5));
            _workerTask = null;
        }

        double totalInMillions = _totalSamplesReceived / 1_000_000.0;
        Log.Information("Device '{DeviceName}' (index: {DeviceIndex}) stopped. Total samples: {TotalSamples:F3}M",
            _config.Name, _config.DeviceIndex, totalInMillions);
    }

    /// <summary>
    /// Event handler called when IQ samples are available from the RTL-SDR device.
    /// Retrieves samples from the device buffer, converts to magnitude, and detects Mode S frames.
    /// </summary>
    /// <param name="sender">Event source (RtlSdrManagedDevice).</param>
    /// <param name="args">Event arguments containing sample count available.</param>
    /// <remarks>
    /// This method runs on a background thread managed by RtlSdrManager.
    /// Converts IQ samples to magnitude using pre-computed lookup table.
    /// Detects Mode S preambles with local noise estimation and extracts raw frames.
    /// Frames are ready for CRC validation.
    /// </remarks>
    private void OnSamplesAvailable(object? sender, SamplesAvailableEventArgs args)
    {
        // Race condition check: device may be closing while events are still firing
        if (_device == null)
        {
            return;
        }

        try
        {
            // Get raw buffer from device channel (zero-copy from native callback)
            RawSampleBuffer? rawBuffer = _device.GetRawSamplesFromAsyncBuffer();
            if (rawBuffer == null)
            {
                return;
            }

            try
            {
                _totalSamplesReceived += rawBuffer.SampleCount;

                // Capture wall-clock anchor ONCE per buffer for sample-offset timestamps.
                // All frames detected in this buffer derive their timestamps from this anchor
                // plus their sample position, providing deterministic sub-microsecond precision.
                DateTime bufferTimestamp = _timeProvider.GetCurrentTimestamp();

                // Convert raw I/Q bytes to magnitude (direct byte access, no IQData intermediary)
                // Fills a linear buffer with prefix from previous buffer for seamless preamble detection
                // Note: Demodulator does NOT log - DeviceWorker logs all stats in StatisticsLoop
                IQDemodulator.MagnitudeBuffer? magnitudeBuffer =
                    _demodulator.ProcessSamples(rawBuffer.Data.AsSpan(0, rawBuffer.ByteLength));

                if (magnitudeBuffer == null)
                {
                    // Buffer unavailable or invalid sample count
                    return;
                }

                // Detect preambles and extract frames
                // Scan linear buffer from start (index 0) through new data region
                // The buffer layout is: [326 prefix samples][new data]
                // Scanning starts at index 0, allowing detector to access prefix for boundary detection
                List<RawFrame> frames = _preambleDetector.DetectAndExtract(magnitudeBuffer, bufferTimestamp);

                // Validate frames with CRC and extract ICAO addresses
                foreach (RawFrame rawFrame in frames)
                {
                    // Extract pre-calculated signal strength from frame for validation and tracking
                    double signalStrength = rawFrame.SignalStrength;

                    // CRC validation
                    ValidatedFrame? validatedFrame = _validatedFrameFactory.ValidateFrame(rawFrame, signalStrength);

                    if (validatedFrame == null)
                    {
                        continue;
                    }

                    // Confidence tracking (filter noise from real aircraft)
                    bool isConfident = _confidenceTracker.TrackAndValidate(validatedFrame, out bool isNewConfirmedIcao);

                    // Log when ICAO reaches confidence threshold
                    if (isNewConfirmedIcao)
                    {
                        Log.Information("Device '{DeviceName}' (index: {DeviceIndex}) confirmed aircraft: {IcaoAddress} (confidence {Level}, seen {Count}+ times, DF {DownlinkFormat}, {Mode} mode)",
                            _config.Name,
                            _config.DeviceIndex,
                            validatedFrame.IcaoAddress,
                            _trackingConfig.ConfidenceLevel,
                            (int)_trackingConfig.ConfidenceLevel,
                            (int)validatedFrame.DownlinkFormat,
                            validatedFrame.UsesPIMode ? "PI" : "AP");
                    }

                    // Only pass confident frames to next steps
                    if (!isConfident)
                    {
                        continue;
                    }

                    // Deduplication: filter frames seen within deduplication window (FRUIT, multipath, multiple interrogators)
                    // This prevents expensive parsing of duplicate frames (~30% CPU savings)
                    if (_frameDeduplicator.IsDuplicate(validatedFrame.Data, validatedFrame.Timestamp))
                    {
                        continue;
                    }

                    // Parse validated frame into structured message
                    // Noise and unconfident ICAOs are filtered out above
                    ModeSMessage? message = _messageParser.ParseMessage(validatedFrame);

                    // Invoke callback with frame and message (even if message is null)
                    // Beast format needs ALL frames, JSON/SBS will skip nulls
                    _onDataParsed?.Invoke(validatedFrame, message);

                    // Message may be null if:
                    // - Unsupported message type (DF 24 Comm-D, rare formats)
                    // - Parse error occurred (logged by MessageParser)
                    // - Validation failure (invalid data in message fields)
                }
            }
            finally
            {
                // Always return the pooled buffer, even if processing throws
                rawBuffer.Return();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing samples from device {DeviceName}", _config.Name);
        }
    }

    /// <summary>
    /// Background task that periodically logs comprehensive statistics from all processing stages.
    /// Logs every 10 seconds: sample reception, demodulation, preamble detection, CRC validation,
    /// confidence tracking, deduplication, and message parsing metrics.
    /// </summary>
    /// <param name="cancellationToken">Token to signal task shutdown.</param>
    /// <remarks>
    /// Runs until cancellation is requested. OperationCanceledException is expected on shutdown.
    /// DeviceWorker aggregates and logs statistics from all processing components (IQDemodulator,
    /// PreambleDetector, ValidatedFrameFactory, etc.) to provide comprehensive device health monitoring
    /// without overhead in hot path.
    /// </remarks>
    private async Task StatisticsLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                // Calculate statistics for this interval
                long currentTotal = _totalSamplesReceived;
                long delta = currentTotal - _lastLoggedSampleCount;
                TimeSpan elapsed = _timeProvider.GetCurrentTimestamp() - _receptionStartTime;
                double samplesPerSecond = currentTotal / elapsed.TotalSeconds;

                // Convert all statistics to millions for readability (3 decimal places)
                // Format: 19.923M samples, 19.923M increase, 1.992M samples/sec
                double totalInMillions = currentTotal / 1_000_000.0;
                double deltaInMillions = delta / 1_000_000.0;
                double samplesPerSecondInMillions = samplesPerSecond / 1_000_000.0;

                // === Sample Reception & Demodulation Statistics ===
                // Aggregate statistics from IQDemodulator and device buffer
                // to monitor overall sample flow health and verify expected 2.4 MSPS rate
                Log.Information("Device '{DeviceName}' (index: {DeviceIndex}) stats: {TotalSamples:F3}M samples ({Delta:F3}M increase), {SamplesPerSec:F3}M samples/sec, running {Elapsed}",
                    _config.Name,
                    _config.DeviceIndex,
                    totalInMillions,
                    deltaInMillions,
                    samplesPerSecondInMillions,
                    elapsed.ToString(@"hh\:mm\:ss"));

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) demodulator: {SamplesProcessed:F3}M samples converted to magnitude",
                    _config.Name,
                    _config.DeviceIndex,
                    _demodulator.TotalSamplesProcessed / 1_000_000.0);

                // === Preamble Detection Statistics ===
                // Monitor threshold effectiveness: extraction rate indicates if preambleThreshold is optimal
                long candidates = _preambleDetector.PreambleCandidates;
                long extracted = _preambleDetector.FramesExtracted;
                double extractedRate = candidates > 0 ? extracted * 100.0 / candidates : 0.0;

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) preambles: {Candidates:N0} candidates, {Extracted:N0} frames extracted ({ExtractedRate:F1}%)",
                    _config.Name,
                    _config.DeviceIndex,
                    candidates,
                    extracted,
                    extractedRate);

                // === CRC Validation Statistics ===
                // Monitor frame quality: high invalid rate suggests RF interference or weak signal
                long crcChecked = _validatedFrameFactory.FramesChecked;
                long crcValid = _validatedFrameFactory.FramesValid;
                long crcCorrected = _validatedFrameFactory.FramesCorrected;
                long crcInvalid = _validatedFrameFactory.FramesInvalid;
                double crcValidRate = crcChecked > 0 ? crcValid * 100.0 / crcChecked : 0.0;
                double crcCorrectedRate = crcChecked > 0 ? crcCorrected * 100.0 / crcChecked : 0.0;

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) CRC: {Checked:N0} checked, {Valid:N0} valid ({ValidRate:F1}%), {Corrected:N0} corrected ({CorrectedRate:F1}%), {Invalid:N0} invalid",
                    _config.Name,
                    _config.DeviceIndex,
                    crcChecked,
                    crcValid,
                    crcValidRate,
                    crcCorrected,
                    crcCorrectedRate,
                    crcInvalid);

                // === Confidence Tracking Statistics ===
                // Monitor noise filtering effectiveness: confident rate shows how well we reject random ICAOs
                long confTotal = _confidenceTracker.TotalFrames;
                long confConfident = _confidenceTracker.ConfidentFrames;
                double confConfidentRate = confTotal > 0 ? confConfident * 100.0 / confTotal : 0.0;
                int confTracked = _confidenceTracker.TrackedIcaos;
                int confConfirmed = _confidenceTracker.ConfirmedIcaos;
                long confExpired = _confidenceTracker.ExpiredIcaos;

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) confidence: {Total:N0} frames, {Confident:N0} confident ({ConfidentRate:F1}%), {Tracked:N0} active ICAOs, {Confirmed:N0} confirmed, {Expired:N0} expired total",
                    _config.Name,
                    _config.DeviceIndex,
                    confTotal,
                    confConfident,
                    confConfidentRate,
                    confTracked,
                    confConfirmed,
                    confExpired);

                // === Deduplication Statistics ===
                // Monitor CPU savings: filtered rate shows effectiveness of FRUIT/multipath/multi-interrogator detection
                long dedupTotal = _frameDeduplicator.TotalFramesProcessed;
                long dedupFiltered = _frameDeduplicator.DuplicatesFiltered;
                double dedupFilteredRate = dedupTotal > 0 ? dedupFiltered * 100.0 / dedupTotal : 0.0;
                int dedupCacheSize = _frameDeduplicator.CurrentCacheSize;
                long dedupEvictions = _frameDeduplicator.CacheEvictions;

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) deduplication: {Total:N0} frames, {Filtered:N0} duplicates filtered ({FilteredRate:F1}%), cache: {CacheSize:N0}/{MaxCache:N0} frames, {Evictions:N0} evictions",
                    _config.Name,
                    _config.DeviceIndex,
                    dedupTotal,
                    dedupFiltered,
                    dedupFilteredRate,
                    dedupCacheSize,
                    _config.MaxTrackedFrames,
                    dedupEvictions);

                // === Message Parsing Statistics ===
                // Monitor parser health: unexpected errors indicate bugs, validation failures are normal in noisy RF
                long msgParsed = _messageParser.MessagesParsed;
                long msgValidationFailures = _messageParser.ValidationFailures;
                long msgUnexpectedErrors = _messageParser.UnexpectedErrors;
                long msgUnsupported = _messageParser.UnsupportedMessages;
                double msgValidationRate = msgParsed > 0 ? msgValidationFailures * 100.0 / msgParsed : 0.0;
                double msgUnexpectedRate = msgParsed > 0 ? msgUnexpectedErrors * 100.0 / msgParsed : 0.0;
                double msgUnsupportedRate = msgParsed > 0 ? msgUnsupported * 100.0 / msgParsed : 0.0;

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) parser: {Parsed:N0} parsed, {ValidationFailures:N0} validation failures ({ValidationRate:F1}%), {UnexpectedErrors:N0} unexpected errors ({UnexpectedRate:F1}%), {Unsupported:N0} unsupported ({UnsupportedRate:F1}%)",
                    _config.Name,
                    _config.DeviceIndex,
                    msgParsed,
                    msgValidationFailures,
                    msgValidationRate,
                    msgUnexpectedErrors,
                    msgUnexpectedRate,
                    msgUnsupported,
                    msgUnsupportedRate);

                // === DF (Downlink Format) Breakdown ===
                // Shows distribution of message types to understand traffic composition
                var dfBreakdown = _messageParser.MessagesByDF
                    .Where(kvp => kvp.Value > 0)
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => $"DF {(int)kvp.Key}: {kvp.Value:N0} ({kvp.Value * 100.0 / msgParsed:F1}%)")
                    .ToList();

                if (dfBreakdown.Any())
                {
                    Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) DF breakdown: {DFBreakdown}",
                        _config.Name,
                        _config.DeviceIndex,
                        string.Join(", ", dfBreakdown));
                }

                // === TC (Type Code) Breakdown ===
                // Shows Extended Squitter (DF 17/18) message type distribution
                var tcBreakdown = _messageParser.MessagesByTC
                    .Where(kvp => kvp.Value > 0)
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => $"TC {kvp.Key}: {kvp.Value:N0} ({kvp.Value * 100.0 / msgParsed:F1}%)")
                    .ToList();

                if (tcBreakdown.Any())
                {
                    Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) TC breakdown: {TCBreakdown}",
                        _config.Name,
                        _config.DeviceIndex,
                        string.Join(", ", tcBreakdown));
                }

                // === BDS (Binary Data Subsystem) Breakdown ===
                // Shows Comm-B (DF 20/21) register type distribution
                var bdsBreakdown = _messageParser.MessagesByBDS
                    .Where(kvp => kvp.Value > 0)
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => $"{kvp.Key.ToFormattedString()}: {kvp.Value:N0} ({kvp.Value * 100.0 / msgParsed:F1}%)")
                    .ToList();

                if (bdsBreakdown.Any())
                {
                    Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) BDS breakdown: {BDSBreakdown}",
                        _config.Name,
                        _config.DeviceIndex,
                        string.Join(", ", bdsBreakdown));
                }

                // Update last logged count for next delta calculation
                _lastLoggedSampleCount = currentTotal;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in statistics loop for device {DeviceName}", _config.Name);
            }
        }
    }

    /// <summary>
    /// Logs comprehensive final statistics when the device is stopped.
    /// Includes message parsing breakdown by DF/TC with counts and percentages.
    /// </summary>
    private void LogFinalStatistics()
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Final Statistics for Device '{DeviceName}' (index: {DeviceIndex})",
            _config.Name, _config.DeviceIndex);
        Log.Information("═══════════════════════════════════════════════════════════════");

        // CRC Validation statistics
        long crcChecked = _validatedFrameFactory.FramesChecked;
        long crcValid = _validatedFrameFactory.FramesValid;
        long crcCorrected = _validatedFrameFactory.FramesCorrected;
        long crcInvalid = _validatedFrameFactory.FramesInvalid;
        double crcValidRate = crcChecked > 0 ? crcValid * 100.0 / crcChecked : 0.0;
        double crcCorrectedRate = crcChecked > 0 ? crcCorrected * 100.0 / crcChecked : 0.0;

        Log.Information("CRC Validation: {Checked:N0} frames checked",
            crcChecked);
        Log.Information("  - Valid: {Valid:N0} ({ValidRate:F1}%)",
            crcValid, crcValidRate);
        Log.Information("  - Corrected: {Corrected:N0} ({CorrectedRate:F1}%)",
            crcCorrected, crcCorrectedRate);
        Log.Information("  - Invalid: {Invalid:N0}",
            crcInvalid);

        // Confidence tracking statistics
        long confTotal = _confidenceTracker.TotalFrames;
        long confConfident = _confidenceTracker.ConfidentFrames;
        double confConfidentRate = confTotal > 0 ? confConfident * 100.0 / confTotal : 0.0;
        int confTracked = _confidenceTracker.TrackedIcaos;
        int confConfirmed = _confidenceTracker.ConfirmedIcaos;
        long confExpired = _confidenceTracker.ExpiredIcaos;

        Log.Information("Confidence Tracking: {Total:N0} frames processed",
            confTotal);
        Log.Information("  - Confident: {Confident:N0} ({ConfidentRate:F1}%)",
            confConfident, confConfidentRate);
        Log.Information("  - Active ICAOs: {Tracked:N0} tracked, {Confirmed:N0} confirmed",
            confTracked, confConfirmed);
        Log.Information("  - Expired ICAOs: {Expired:N0} (lifetime total)",
            confExpired);

        // Deduplication statistics
        long dedupTotal = _frameDeduplicator.TotalFramesProcessed;
        long dedupFiltered = _frameDeduplicator.DuplicatesFiltered;
        double dedupFilteredRate = dedupTotal > 0 ? dedupFiltered * 100.0 / dedupTotal : 0.0;
        long dedupEvictions = _frameDeduplicator.CacheEvictions;

        Log.Information("Frame Deduplication: {Total:N0} frames processed",
            dedupTotal);
        Log.Information("  - Duplicates filtered: {Filtered:N0} ({FilteredRate:F1}%) [FRUIT/multipath/multiple interrogators]",
            dedupFiltered, dedupFilteredRate);
        Log.Information("  - Cache evictions: {Evictions:N0} (LRU)",
            dedupEvictions);

        // Message parser statistics
        long msgParsed = _messageParser.MessagesParsed;
        long msgValidationFailures = _messageParser.ValidationFailures;
        long msgUnexpectedErrors = _messageParser.UnexpectedErrors;
        long msgUnsupported = _messageParser.UnsupportedMessages;
        double msgValidationRate = msgParsed > 0 ? msgValidationFailures * 100.0 / msgParsed : 0.0;
        double msgUnexpectedRate = msgParsed > 0 ? msgUnexpectedErrors * 100.0 / msgParsed : 0.0;
        double msgUnsupportedRate = msgParsed > 0 ? msgUnsupported * 100.0 / msgParsed : 0.0;

        Log.Information("Message Parser: {Parsed:N0} messages parsed",
            msgParsed);
        Log.Information("  - Validation failures: {ValidationFailures:N0} ({ValidationRate:F1}%) [expected in noisy RF]",
            msgValidationFailures, msgValidationRate);
        Log.Information("  - Unexpected errors: {UnexpectedErrors:N0} ({UnexpectedRate:F1}%) [bugs if >0]",
            msgUnexpectedErrors, msgUnexpectedRate);
        Log.Information("  - Unsupported: {Unsupported:N0} ({UnsupportedRate:F1}%)",
            msgUnsupported, msgUnsupportedRate);

        // DF (Downlink Format) breakdown
        var dfBreakdown = _messageParser.MessagesByDF
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        if (dfBreakdown.Any())
        {
            Log.Information("Downlink Format (DF) Breakdown:");
            foreach (KeyValuePair<DownlinkFormat, long> kvp in dfBreakdown)
            {
                double percentage = msgParsed > 0 ? kvp.Value * 100.0 / msgParsed : 0.0;
                Log.Information("  - DF {DF,-5}: {Count,8:N0} messages ({Percentage,5:F1}%)",
                    (int)kvp.Key, kvp.Value, percentage);
            }
        }

        // TC (Type Code) breakdown for Extended Squitter (DF 17/18)
        var tcBreakdown = _messageParser.MessagesByTC
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        if (tcBreakdown.Any())
        {
            long totalTcMessages = tcBreakdown.Sum(kvp => kvp.Value);
            Log.Information("Type Code (TC) Breakdown (DF 17/18 only):");
            foreach (KeyValuePair<int, long> kvp in tcBreakdown)
            {
                double percentage = totalTcMessages > 0 ? kvp.Value * 100.0 / totalTcMessages : 0.0;
                Log.Information("  - TC {TC,-5}: {Count,8:N0} messages ({Percentage,5:F1}%)",
                    kvp.Key, kvp.Value, percentage);
            }
        }

        // BDS (Binary Data Subsystem) breakdown for Comm-B messages (DF 20/21)
        var bdsBreakdown = _messageParser.MessagesByBDS
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        if (bdsBreakdown.Any())
        {
            long totalBdsMessages = bdsBreakdown.Sum(kvp => kvp.Value);
            Log.Information("BDS Code Breakdown (DF 20/21 Comm-B only):");
            foreach (KeyValuePair<BdsCode, long> kvp in bdsBreakdown)
            {
                double percentage = totalBdsMessages > 0 ? kvp.Value * 100.0 / totalBdsMessages : 0.0;
                Log.Information("  - {BDS,-8}: {Count,8:N0} messages ({Percentage,5:F1}%)",
                    kvp.Key.ToFormattedString(), kvp.Value, percentage);
            }
        }

        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Releases all resources used by the DeviceWorker.
    /// Stops sample reception, closes the device, and cancels background tasks.
    /// </summary>
    public void Dispose()
    {
        Stop();
        _demodulator.Dispose();

        if (_ownsConfidenceTracker)
        {
            _confidenceTracker.Dispose();
        }
    }

    /// <summary>Gets the configured device name.</summary>
    public string DeviceName => _config.Name;

    /// <summary>Gets the total number of I/Q samples received since <see cref="StartReceiving"/>.</summary>
    public long TotalSamplesReceived => _totalSamplesReceived;

    /// <summary>Gets the timestamp when sample reception started.</summary>
    public DateTime StartTime => _receptionStartTime;

    /// <summary>Gets the I/Q-to-magnitude demodulator for this device.</summary>
    public IQDemodulator Demodulator => _demodulator;

    /// <summary>Gets the Mode S preamble detector for this device.</summary>
    public PreambleDetector PreambleDetector => _preambleDetector;

    /// <summary>Gets the CRC validation and frame factory for this device.</summary>
    public ValidatedFrameFactory ValidatedFrameFactory => _validatedFrameFactory;

    /// <summary>Gets the ICAO confidence tracker (may be shared across devices for MLAT).</summary>
    public IcaoConfidenceTracker ConfidenceTracker => _confidenceTracker;

    /// <summary>Gets the frame deduplicator for FRUIT/multipath filtering.</summary>
    public FrameDeduplicator FrameDeduplicator => _frameDeduplicator;

    /// <summary>Gets the Mode S/ADS-B message parser for this device.</summary>
    public MessageParser MessageParser => _messageParser;
}
