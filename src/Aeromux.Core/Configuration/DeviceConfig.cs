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
    /// Gets or sets the tuner gain in dB. Only used when GainMode is Manual.
    /// Default: 49.6 dB (maximum safe gain for R820T/R820T2, industry standard)
    /// Reduce if near airport and experiencing strong signal overload (artifacts, low message rate).
    /// Typical range: 0.0 to 49.6 dB in 0.1 dB steps.
    /// </summary>
    public double TunerGain { get; set; } = 49.6;

    /// <summary>
    /// Gets or sets the gain control mode (ADR-008: uses RtlSdrManager.Modes enum directly).
    /// Default: Manual (industry standard, maximum sensitivity with 49.6 dB)
    /// Set to AGC only if near airport and manual gain causes strong signal overload.
    /// Note: AGC is almost never optimal (dump1090-fa documentation).
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
    /// Controls sensitivity of Mode S preamble detection.
    /// Lower values = more sensitive (more frames detected, more false positives)
    /// Higher values = less sensitive (only strong signals detected)
    /// Valid range: 1.5 to 10.0
    /// Default: 1.8125 (58/32 ratio) - provides balanced sensitivity for typical environments
    /// Threshold is applied as: baseNoise * threshold
    /// </summary>
    public double PreambleThreshold { get; set; } = 1.8125;

    /// <summary>
    /// Gets or sets whether this device is enabled.
    /// Disabled devices are ignored during startup.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
}
