using Serilog;
using RtlSdrManager;
using RtlSdrManager.Modes;
using Aeromux.Core.Configuration;
using Aeromux.Core.SignalProcessing;

namespace Aeromux.Infrastructure.Sdr;

/// <summary>
/// Manages a single RTL-SDR device: opening, configuration, and sample reception.
/// Each DeviceWorker runs in its own task and processes samples from one device.
/// Uses Serilog for structured logging (ADR-007).
/// </summary>
public sealed class DeviceWorker(DeviceConfig config) : IDisposable
{
    private readonly DeviceConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly RtlSdrDeviceManager _deviceManager = RtlSdrDeviceManager.Instance;
    private readonly IQDemodulator _demodulator = new IQDemodulator();
    private RtlSdrManagedDevice? _device;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

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

        try
        {
            // Open device with friendly name
            _deviceManager.OpenManagedDevice((uint)_config.DeviceIndex, _config.Name);
            _device = _deviceManager[_config.Name];

            // Configure device - frequencies and sample rates
            _device.CenterFrequency = Frequency.FromMHz(_config.CenterFrequency);
            _device.SampleRate = Frequency.FromMHz(_config.SampleRate);
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

            Log.Information("Device {DeviceName} configured: Freq={Frequency}MHz, SR={SampleRate}MHz, Gain={Gain}, Mode={GainMode}",
                _config.Name,
                _config.CenterFrequency,
                _config.SampleRate,
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

        Log.Information("Starting sample reception for device {DeviceName}", _config.Name);

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

        Log.Information("Device {DeviceName} receiving samples", _config.Name);
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
    /// Retrieves samples from the device buffer and converts them to magnitude values.
    /// </summary>
    /// <param name="sender">Event source (RtlSdrManagedDevice).</param>
    /// <param name="args">Event arguments containing sample count available.</param>
    /// <remarks>
    /// This method runs on a background thread managed by RtlSdrManager.
    /// Passes samples to IQDemodulator for magnitude conversion (Phase 2).
    /// No pulse detection here - Phase 3 will use local noise estimation around preambles (readsb approach).
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
            // Note: Demodulator does NOT log - DeviceWorker logs all stats in StatisticsLoop
            // (ADR-009: Coordinator Pattern - zero overhead in hot path)
            _demodulator.ProcessSamples(samples);

            // TODO: Phase 3 will scan magnitude buffer for preambles with local noise estimation
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
                // Buffer position format: comma-separated thousands (e.g., 1,922,944)
                Log.Debug("Device '{DeviceName}' (index: {DeviceIndex}) demodulator: {SamplesProcessed:F3}M samples converted to magnitude, buffer position: {BufferPos:N0}",
                    _config.Name,
                    _config.DeviceIndex,
                    _demodulator.TotalSamplesProcessed / 1_000_000.0,
                    _demodulator.BufferPosition);

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
    /// Releases all resources used by the DeviceWorker.
    /// Stops sample reception, closes the device, and cancels background tasks.
    /// </summary>
    public void Dispose()
    {
        Stop();
        _demodulator.Dispose();
    }
}
