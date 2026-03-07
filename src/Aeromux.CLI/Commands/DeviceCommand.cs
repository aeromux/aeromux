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

using System.Linq;
using Aeromux.CLI.Commands.Device;
using RtlSdrManager;
using RtlSdrManager.Exceptions;
using RtlSdrManager.Modes;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// CLI command to list RTL-SDR devices connected to the system.
/// Displays basic device information, or detailed tuner parameters with <c>--verbose</c>.
/// </summary>
public class DeviceCommand : Command<DeviceSettings>
{
    /// <summary>
    /// Mode S / ADS-B center frequency in MHz.
    /// Used when opening devices in verbose mode to read tuner parameters.
    /// </summary>
    private const double CenterFrequency = 1090.0;

    /// <summary>
    /// Sample rate in MSPS. Matches the Aeromux receiver pipeline default.
    /// Used when opening devices in verbose mode to read tuner parameters.
    /// </summary>
    private const double SampleRate = 2.4;

    /// <inheritdoc />
    public override int Execute(CommandContext context, DeviceSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Suppress native librtlsdr console output (e.g., "Found 1 device(s)")
        RtlSdrDeviceManager.SuppressLibraryConsoleOutput = true;

        // Try to access the device manager — fails if librtlsdr is not installed
        RtlSdrDeviceManager manager;
        try
        {
            manager = RtlSdrDeviceManager.Instance;
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("Error: RTL-SDR library not found. Install librtlsdr and ensure it is available on the system path.");
            return 1;
        }

        // Check for available devices
        if (manager.Devices.Count == 0)
        {
            Console.WriteLine("No RTL-SDR devices found.");
            return 0;
        }

        if (settings.Verbose)
        {
            return ExecuteVerbose(manager);
        }

        return ExecuteBasic(manager);
    }

    /// <summary>
    /// Lists devices with basic identification info (serial, name).
    /// Does not open any devices.
    /// </summary>
    private static int ExecuteBasic(RtlSdrDeviceManager manager)
    {
        Console.WriteLine($"Found {manager.Devices.Count} RTL-SDR device(s):");

        foreach (DeviceInfo device in manager.Devices.Values)
        {
            Console.WriteLine();
            Console.WriteLine($"  Device #{device.Index}: {device.Manufacturer} {device.ProductType}");
            Console.WriteLine($"    Serial : {device.Serial}");
            Console.WriteLine($"    Name   : {device.Name}");
        }

        Console.WriteLine();
        Console.WriteLine("Tip: Use the device index as 'deviceIndex' in aeromux.yaml.");
        Console.WriteLine("Run with --verbose to see supported tuner gains for 'tunerGain' configuration.");

        return 0;
    }

    /// <summary>
    /// Lists devices with detailed tuner parameters by temporarily opening each device.
    /// Continues to the next device if one fails to open.
    /// </summary>
    private static int ExecuteVerbose(RtlSdrDeviceManager manager)
    {
        Console.WriteLine($"Found {manager.Devices.Count} RTL-SDR device(s).");
        Console.WriteLine("Opening devices for detailed info...");

        try
        {
            foreach (DeviceInfo device in manager.Devices.Values)
            {
                Console.WriteLine();
                Console.WriteLine($"  Device #{device.Index}: {device.Manufacturer} {device.ProductType}");

                string friendlyName = $"aeromux-probe-{device.Index}";
                try
                {
                    manager.OpenManagedDevice(device.Index, friendlyName);
                    RtlSdrManagedDevice managed = manager[friendlyName];

                    // Configure with Aeromux defaults
                    managed.CenterFrequency = Frequency.FromMHz(CenterFrequency);
                    managed.SampleRate = Frequency.FromMHz(SampleRate);
                    managed.TunerGainMode = TunerGainModes.AGC;
                    managed.ResetDeviceBuffer();

                    // Print all parameters aligned to the widest label (22 chars)
                    Console.WriteLine($"    {"Serial",-22}: {device.Serial}");
                    Console.WriteLine($"    {"Name",-22}: {device.Name}");
                    Console.WriteLine($"    {"Tuner type",-22}: {managed.TunerType}");
                    Console.WriteLine($"    {"Center frequency",-22}: {managed.CenterFrequency.MHz} MHz");
                    Console.WriteLine($"    {"Crystal frequency",-22}: {managed.CrystalFrequency}");
                    Console.WriteLine($"    {"Frequency correction",-22}: {managed.FrequencyCorrection} ppm");
                    Console.WriteLine($"    {"Bandwidth selection",-22}: {managed.TunerBandwidthSelectionMode}");
                    Console.WriteLine($"    {"Sample rate",-22}: {managed.SampleRate.MHz} MHz");
                    Console.WriteLine($"    {"Direct sampling mode",-22}: {managed.DirectSamplingMode}");
                    Console.WriteLine($"    {"AGC mode",-22}: {managed.AGCMode}");
                    Console.WriteLine($"    {"Tuner gain mode",-22}: {managed.TunerGainMode}");
                    Console.WriteLine($"    {"Offset tuning mode",-22}: {managed.OffsetTuningMode}");
                    Console.WriteLine($"    {"KerberosSDR mode",-22}: {managed.KerberosSDRMode}");
                    Console.WriteLine($"    {"Frequency dithering",-22}: {managed.FrequencyDitheringMode}");
                    Console.WriteLine($"    {"Test mode",-22}: {managed.TestMode}");

                    // Supported tuner gains — 5 per row
                    List<double> gains = managed.SupportedTunerGains;
                    for (int i = 0; i < gains.Count; i++)
                    {
                        if (i % 5 == 0)
                        {
                            Console.Write(i == 0 ? $"    {"Supported tuner gains",-22}: " : $"    {"",-22}: ");
                        }

                        Console.Write($"{gains.ElementAt(i),4:F1} dB  ");

                        if ((i + 1) % 5 == 0 || i == gains.Count - 1)
                        {
                            Console.WriteLine();
                        }
                    }
                }
                catch (RtlSdrDeviceException)
                {
                    Console.WriteLine($"    {"Serial",-22}: {device.Serial}");
                    Console.WriteLine($"    {"Name",-22}: {device.Name}");
                    Console.WriteLine($"    Error: Failed to open the device. It may be in use by another application.");
                }
            }
        }
        finally
        {
            manager.CloseAllManagedDevice();
        }

        Console.WriteLine();
        Console.WriteLine("Tip: Use the device index as 'deviceIndex' and a supported tuner gain as 'tunerGain' in aeromux.yaml.");

        return 0;
    }
}
