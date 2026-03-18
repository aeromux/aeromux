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

using System.Reflection;
using Aeromux.CLI.Commands.Version;
using Spectre.Console.Cli;

namespace Aeromux.CLI.Commands;

/// <summary>
/// CLI command to display version information about Aeromux.
/// </summary>
public class VersionCommand : Command<VersionSettings>
{
    /// <summary>
    /// Executes the version command to display application version information.
    /// </summary>
    /// <param name="context">The command context from Spectre.Console.Cli.</param>
    /// <param name="settings">Version command settings including --details flag.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Exit code: always returns 0 for success.</returns>
    public override int Execute(CommandContext context, VersionSettings settings, CancellationToken cancellationToken)
    {
        // Validate settings parameter (required by CA1062)
        ArgumentNullException.ThrowIfNull(settings);

        // Read version from assembly metadata (set by Directory.Build.props).
        // InformationalVersion may include a "+commitHash" suffix appended by the .NET SDK.
        string fullVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        string version = fullVersion.Split('+')[0];

        if (settings.Details)
        {
            // Display detailed version information including commit hash for bug reports
            Console.WriteLine("Aeromux");
            Console.WriteLine("Multi-SDR Mode S and ADS-B Decoder");
            Console.WriteLine();
            Console.WriteLine($"Version:    Aeromux {fullVersion}");
            Console.WriteLine($"Runtime:    Microsoft .NET {Environment.Version}");
            Console.WriteLine("License:    GNU General Public License 3.0");
            Console.WriteLine("Website:    https://www.aeromux.com");
            Console.WriteLine("Repository: https://github.com/nandortoth/aeromux");
        }
        else
        {
            // Display only the clean version number
            Console.WriteLine(version);
        }

        return 0;
    }
}
