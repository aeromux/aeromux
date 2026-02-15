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

using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Aeromux.CLI;

/// <summary>
/// Custom help provider that adds Aeromux header before the standard help output.
/// </summary>
public sealed class AeromuxHelpProvider : IHelpProvider
{
    private readonly IHelpProvider _defaultProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AeromuxHelpProvider"/> class.
    /// Configures plain help styles (no colors).
    /// </summary>
    /// <param name="settings">Command application settings.</param>
    internal AeromuxHelpProvider(ICommandAppSettings? settings)
    {
        // Configure plain help styles (no colors)
        settings!.HelpProviderStyles = new HelpProviderStyle
        {
            Description = new DescriptionStyle(),
            Commands = new CommandStyle()
        };

        _defaultProvider = new HelpProvider(settings);
    }

    public AeromuxHelpProvider(IHelpProvider defaultProvider)
    {
        _defaultProvider = defaultProvider;
    }

    /// <summary>
    /// Writes the help output with custom Aeromux header.
    /// </summary>
    /// <param name="model">Command model containing help information.</param>
    /// <param name="command">Optional specific command to get help for.</param>
    /// <returns>Enumerable of renderable help items.</returns>
    public IEnumerable<IRenderable> Write(ICommandModel model, ICommandInfo? command)
    {
        // Write header to console directly
        Console.WriteLine("Aeromux");
        Console.WriteLine("Multi-SDR Mode S and ADS-B Decoder");
        Console.WriteLine();

        // Return default help renderables
        foreach (IRenderable renderable in _defaultProvider.Write(model, command))
        {
            yield return renderable;
        }
    }
}
