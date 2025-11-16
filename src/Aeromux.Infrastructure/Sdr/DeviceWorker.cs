using Serilog;
using RtlSdrManager;
using RtlSdrManager.Modes;
using Aeromux.Core.Configuration;
using Aeromux.Core.SignalProcessing;
using Aeromux.Core.ModeS;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Infrastructure.Sdr;

/// <summary>
/// Manages a single RTL-SDR device: opening, configuration, and sample reception.
/// Each DeviceWorker runs in its own task and processes samples from one device.
/// Uses Serilog for structured logging (ADR-007).
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

    private readonly DeviceConfig _config;
    private readonly TrackingConfig _trackingConfig;
    private readonly RtlSdrDeviceManager _deviceManager = RtlSdrDeviceManager.Instance;
    private readonly IQDemodulator _demodulator = new();
    private readonly PreambleDetector _preambleDetector;
    private readonly CrcValidator _crcValidator = new();
    private readonly IcaoConfidenceTracker _confidenceTracker;
    private readonly MessageParser _messageParser;
    private readonly Action<ValidatedFrame, ModeSMessage?>? _onDataParsed;
    private RtlSdrManagedDevice? _device;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public DeviceWorker(
        DeviceConfig deviceConfig,
        TrackingConfig trackingConfig,
        ReceiverConfig? receiverConfig,
        Action<ValidatedFrame, ModeSMessage?>? onDataParsed = null)
    {
        _config = deviceConfig ?? throw new ArgumentNullException(nameof(deviceConfig));
        _trackingConfig = trackingConfig ?? throw new ArgumentNullException(nameof(trackingConfig));
        _onDataParsed = onDataParsed;

        // Initialize confidence tracker first (needed by PreambleDetector for ICAO filtering)
        _confidenceTracker = new IcaoConfidenceTracker(
            trackingConfig.ConfidenceLevel,
            trackingConfig.IcaoTimeoutSeconds);

        // Pass confidence tracker to PreambleDetector for AP mode ICAO filtering
        _preambleDetector = new PreambleDetector(deviceConfig.PreambleThreshold, _confidenceTracker);

        // Initialize MessageParser with device context for logging
        _messageParser = new MessageParser(deviceConfig.Name, deviceConfig.DeviceIndex);

        // Configure MessageParser with receiver location if available (for TC 5-8 surface position)
        if (receiverConfig?.Latitude.HasValue == true && receiverConfig?.Longitude.HasValue == true)
        {
            var receiverLocation = new Core.ModeS.ValueObjects.GeographicCoordinate(
                receiverConfig.Latitude.Value,
                receiverConfig.Longitude.Value);
            _messageParser.SetReceiverLocation(receiverLocation);
            Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}): MessageParser configured with receiver location",
                deviceConfig.Name, deviceConfig.DeviceIndex);
        }
    }

    // Statistics tracking
    private long _totalSamplesReceived;        // Total IQ samples received since StartReceiving()
    private long _lastLoggedSampleCount;       // Sample count at last statistics log (for delta calculation)
    private DateTime _startTime;               // When StartReceiving() was called (for rate calculation)

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
            //   - At 2 MSPS: Each request = ~65ms, buffer holds ~131ms total
            _device.MaxAsyncBufferSize = 512 * 1024;    // 512 KB USB buffer (2x request size)
            _device.DropSamplesOnFullBuffer = true;     // Prevent blocking if processing falls behind
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
    /// <param name="cancellationToken">Token to signal shutdown. When cancelled, stops sample reception.</param>
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
        _startTime = DateTime.UtcNow;

        // Subscribe to sample availability events from RtlSdrManager
        _device.SamplesAvailable += OnSamplesAvailable;

        // Start async reading with 8 buffers of 16384 samples each (131,072 total)
        // At 2 MSPS, this represents ~65ms of buffering
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
    /// Phase 2: Converts IQ samples to magnitude using pre-computed lookup table.
    /// Phase 3: Detects Mode S preambles with local noise estimation and extracts raw frames.
    /// Frames are ready for CRC validation in Phase 4.
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
            // Get samples from device buffer
            List<IQData> samples = _device.GetSamplesFromAsyncBuffer(args.SampleCount);

            _totalSamplesReceived += samples.Count;

            // Convert samples to magnitude (Phase 2: IQ → Magnitude)
            // Fills a linear buffer with prefix from previous buffer for seamless preamble detection
            // Note: Demodulator does NOT log - DeviceWorker logs all stats in StatisticsLoop
            // (ADR-009: Coordinator Pattern - zero overhead in hot path)
            IQDemodulator.MagnitudeBuffer? magnitudeBuffer = _demodulator.ProcessSamples(samples);

            if (magnitudeBuffer == null)
            {
                // Buffer unavailable or invalid sample count
                return;
            }

            // Phase 3: Detect preambles and extract frames
            // Scan linear buffer from start (index 0) through new data region
            // The buffer layout is: [326 prefix samples][new data]
            // Scanning starts at index 0, allowing detector to access prefix for boundary detection
            List<RawFrame> frames = _preambleDetector.DetectAndExtract(magnitudeBuffer);

            // Phase 4: Validate frames with CRC and extract ICAO addresses
            foreach (RawFrame rawFrame in frames)
            {
                // Use peak magnitude from preamble as signal strength indicator
                // For now, use a placeholder value (will be improved in future phases)
                byte signalStrength = 128;  // TODO: Extract actual signal strength from preamble

                // Phase 4a: CRC validation
                ValidatedFrame? validatedFrame = _crcValidator.ValidateFrame(rawFrame, signalStrength);

                if (validatedFrame == null)
                {
                    continue;
                }

                // Phase 4b: Confidence tracking (filter noise from real aircraft)
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

                // Only pass confident frames to Phase 5+
                if (!isConfident)
                {
                    continue;
                }

                // Phase 5: Parse validated frame into structured message
                // Noise and unconfident ICAOs are filtered out above
                ModeSMessage? message = _messageParser.ParseMessage(validatedFrame);

                // Phase 6: Invoke callback with frame and message (even if message is null)
                // Beast format needs ALL frames, JSON/SBS will skip nulls
                _onDataParsed?.Invoke(validatedFrame, message);

                // Message may be null if:
                // - Unsupported message type (DF 24 Comm-D, rare formats)
                // - Parse error occurred (logged by MessageParser)
                // - Validation failure (invalid data in message fields)
                if (message != null)
                {
                    // TODO Phase 7: Feed message to AircraftTracker for state tracking
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing samples from device {DeviceName}", _config.Name);
        }
    }

    /// <summary>
    /// Background task that periodically logs statistics about sample reception and magnitude conversion.
    /// Logs every 10 seconds: total samples, samples/sec, uptime, and demodulator buffer status.
    /// </summary>
    /// <param name="cancellationToken">Token to signal task shutdown.</param>
    /// <remarks>
    /// Runs until cancellation is requested. OperationCanceledException is expected on shutdown.
    /// Calculates sample rate from start time to verify device is receiving at expected rate.
    /// Uses Coordinator Pattern (ADR-009) to log demodulator statistics without overhead in hot path.
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
                TimeSpan elapsed = DateTime.UtcNow - _startTime;
                double samplesPerSecond = currentTotal / elapsed.TotalSeconds;

                // Convert all statistics to millions for readability (3 decimal places)
                // Format: 19.923M samples, 19.923M increase, 1.992M samples/sec
                double totalInMillions = currentTotal / 1_000_000.0;
                double deltaInMillions = delta / 1_000_000.0;
                double samplesPerSecondInMillions = samplesPerSecond / 1_000_000.0;

                // Log device statistics (Phase 1: sample reception + Phase 2: magnitude conversion)
                Log.Information("Device '{DeviceName}' (index: {DeviceIndex}) stats: {TotalSamples:F3}M samples ({Delta:F3}M increase), {SamplesPerSec:F3}M samples/sec, running {Elapsed}",
                    _config.Name,
                    _config.DeviceIndex,
                    totalInMillions,
                    deltaInMillions,
                    samplesPerSecondInMillions,
                    elapsed.ToString(@"hh\:mm\:ss"));

                // Log demodulator buffer status (Phase 2) at Debug level
                // Note: Uses Coordinator Pattern (ADR-009) - DeviceWorker logs IQDemodulator's statistics
                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) demodulator: {SamplesProcessed:F3}M samples converted to magnitude",
                    _config.Name,
                    _config.DeviceIndex,
                    _demodulator.TotalSamplesProcessed / 1_000_000.0);

                // Log preamble detection statistics (Phase 3) at Debug level
                // Shows extraction rate to monitor threshold effectiveness
                long candidates = _preambleDetector.PreambleCandidates;
                long extracted = _preambleDetector.FramesExtracted;
                double extractedRate = candidates > 0 ? extracted * 100.0 / candidates : 0.0;

                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) preambles: {Candidates:N0} candidates, {Extracted:N0} frames extracted ({ExtractedRate:F1}%)",
                    _config.Name,
                    _config.DeviceIndex,
                    candidates,
                    extracted,
                    extractedRate);

                // Log CRC validation statistics (Phase 4) at Debug level
                // Shows validation and correction rates to monitor frame quality
                long crcChecked = _crcValidator.FramesChecked;
                long crcValid = _crcValidator.FramesValid;
                long crcCorrected = _crcValidator.FramesCorrected;
                long crcInvalid = _crcValidator.FramesInvalid;
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

                // Log confidence tracking statistics (Phase 4b) at Debug level
                // Shows how many frames pass confidence filter, active ICAOs, and cleanup stats
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

                // Log message parsing statistics (Phase 5) at Debug level
                // Shows how many frames were parsed, validation failures, unexpected errors, unsupported
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

                // Log DF (Downlink Format) breakdown
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

                // Log TC (Type Code) breakdown for Extended Squitter (DF 17/18)
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
        long crcChecked = _crcValidator.FramesChecked;
        long crcValid = _crcValidator.FramesValid;
        long crcCorrected = _crcValidator.FramesCorrected;
        long crcInvalid = _crcValidator.FramesInvalid;
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
                Log.Information("  - DF {DF,2}: {Count,8:N0} messages ({Percentage,5:F1}%)",
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
                Log.Information("  - TC {TC,2}: {Count,8:N0} messages ({Percentage,5:F1}%)",
                    kvp.Key, kvp.Value, percentage);
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
    }

    // Public properties for session summary (exposed to DaemonCommand for aggregation)
    public string DeviceName => _config.Name;
    public long TotalSamplesReceived => _totalSamplesReceived;
    public DateTime StartTime => _startTime;
    public IQDemodulator Demodulator => _demodulator;
    public PreambleDetector PreambleDetector => _preambleDetector;
    public CrcValidator CrcValidator => _crcValidator;
    public IcaoConfidenceTracker ConfidenceTracker => _confidenceTracker;
    public MessageParser MessageParser => _messageParser;
}
