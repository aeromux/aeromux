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

using Aeromux.CLI.Commands.Daemon;
using Aeromux.CLI.Commands.Live;
using Aeromux.Core.Configuration;
using Serilog;

namespace Aeromux.CLI.Commands;

/// <summary>
/// Prints the startup summary listing active sources and outputs.
/// Shared by both daemon and live commands.
/// </summary>
public static class StartupSummaryPrinter
{
    /// <summary>
    /// Prints the daemon startup summary with sources and outputs.
    /// </summary>
    /// <param name="config">Validated daemon configuration containing resolved sources and output settings.</param>
    public static void PrintDaemonSummary(DaemonValidatedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Console.WriteLine();
        Console.WriteLine("  Source(s):");

        foreach (SdrSourceConfig sdr in config.EnabledSdrSources)
        {
            string line = $"    SDR    {sdr.Name}";
            Console.WriteLine(line);
            Log.Information("Source: SDR {Name}", sdr.Name);
        }

        foreach (BeastSourceConfig beast in config.BeastSources)
        {
            string line = $"    Beast  {beast.Host}:{beast.Port}";
            Console.WriteLine(line);
            Log.Information("Source: Beast {Host}:{Port}", beast.Host, beast.Port);
        }

        if (config.MlatConfig.Enabled)
        {
            string line = $"    MLAT   port {config.MlatConfig.InputPort}";
            Console.WriteLine(line);
            Log.Information("Source: MLAT port {Port}", config.MlatConfig.InputPort);
        }

        Console.WriteLine();
        Console.WriteLine("  Output(s):");

        if (config.BeastEnabled)
        {
            Console.WriteLine($"    Beast  port {config.BeastOutputPort}");
        }

        if (config.JsonEnabled)
        {
            Console.WriteLine($"    JSON   port {config.JsonOutputPort}");
        }

        if (config.SbsEnabled)
        {
            Console.WriteLine($"    SBS    port {config.SbsOutputPort}");
        }

        if (config.ApiEnabled)
        {
            Console.WriteLine($"    API    port {config.ApiPort}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints the live startup summary with sources only (output is always TUI).
    /// </summary>
    /// <param name="config">Validated live configuration containing resolved input sources.</param>
    public static void PrintLiveSummary(LiveValidatedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Console.WriteLine();
        Console.WriteLine("  Source(s):");

        foreach (SdrSourceConfig sdr in config.EnabledSdrSources)
        {
            string line = $"    SDR    {sdr.Name}";
            Console.WriteLine(line);
            Log.Information("Source: SDR {Name}", sdr.Name);
        }

        foreach (BeastSourceConfig beast in config.BeastSources)
        {
            string line = $"    Beast  {beast.Host}:{beast.Port}";
            Console.WriteLine(line);
            Log.Information("Source: Beast {Host}:{Port}", beast.Host, beast.Port);
        }

        Console.WriteLine();
    }
}
