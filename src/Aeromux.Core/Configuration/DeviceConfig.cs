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

using RtlSdrManager.Modes;

namespace Aeromux.Core.Configuration;

/// <summary>
/// RTL-SDR device configuration for ADS-B/Mode S reception.
/// </summary>
public class DeviceConfig
{
    /// <summary>
    /// Gets or sets a friendly name for this device.
    /// Used in logs and statistics.
    /// Default: "default"
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    /// Gets or sets the RTL-SDR device index.
    /// 0 = first device, 1 = second device, etc.
    /// Default: 0
    /// </summary>
    public int DeviceIndex { get; set; }

    /// <summary>
    /// Gets or sets the center frequency in MHz.
    /// ADS-B operates at 1090 MHz.
    /// Default: 1090 MHz
    /// </summary>
    public double CenterFrequency { get; set; } = 1090.0;

    /// <summary>
    /// Gets or sets the sample rate in MHz.
    /// Higher sample rates provide better signal quality but require more CPU.
    /// Industry standard for ADS-B is 2.4 MHz (2.4 MSPS), aligned with readsb.
    /// Validated at device open - only 2.4 MSPS is supported (±0.01 MHz tolerance).
    /// Default: 2.4 MHz
    /// </summary>
    public double SampleRate { get; set; } = 2.4;

    /// <summary>
    /// Gets or sets the tuner gain in dB.
    /// Valid range depends on tuner hardware (typically 0-50 dB).
    /// Higher gain increases sensitivity but may cause overload in strong signal environments.
    /// Note: Only used when GainMode is Manual. Ignored in AGC mode.
    /// Default: 40.0 dB
    /// </summary>
    public double TunerGain { get; set; } = 40.0;

    /// <summary>
    /// Gets or sets the gain control mode (ADR-008: uses RtlSdrManager.Modes enum directly).
    /// Manual: Use fixed TunerGain value specified above
    /// AGC: Let the tuner hardware automatically adjust gain (TunerGain ignored)
    /// Default: Manual
    /// </summary>
    public TunerGainModes GainMode { get; set; } = TunerGainModes.Manual;

    /// <summary>
    /// Gets or sets the frequency correction in parts per million (PPM).
    /// Compensates for crystal oscillator inaccuracies in the RTL-SDR.
    /// Typical range: -50 to +50 PPM
    /// Default: 0 PPM
    /// </summary>
    public int PpmCorrection { get; set; }

    /// <summary>
    /// Gets or sets the preamble detection threshold ratio.
    /// Controls sensitivity of Mode S preamble detection (Phase 3).
    /// Lower values = more sensitive (more frames detected, more false positives)
    /// Higher values = less sensitive (only strong signals detected)
    /// Valid range: 1.5 to 10.0
    /// Default: 1.8125 (readsb's 58/32) - industry standard, balanced sensitivity
    /// Threshold is applied as: baseNoise * threshold (matches readsb exactly)
    /// </summary>
    public double PreambleThreshold { get; set; } = 1.8125;

    /// <summary>
    /// Gets or sets whether this device is enabled.
    /// Disabled devices are ignored during startup.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
}
