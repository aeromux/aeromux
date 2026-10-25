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

namespace Aeromux.CLI.Commands.Daemon;

/// <summary>
/// Connected clients per TCP output, null for an output that is disabled.
/// </summary>
/// <param name="Beast">Beast output clients.</param>
/// <param name="Json">JSON output clients.</param>
/// <param name="Sbs">SBS output clients.</param>
public sealed record OutputClientCounts(int? Beast, int? Json, int? Sbs)
{
    /// <summary>
    /// Reads the client counts. The REST API starts before the broadcasters, so an
    /// enabled output whose broadcaster does not exist yet reports 0 rather than null,
    /// which would read as disabled.
    /// </summary>
    /// <param name="config">The validated configuration, which says which outputs are enabled.</param>
    /// <param name="broadcasters">The broadcasters, or null before they are created.</param>
    /// <returns>The counts.</returns>
    public static OutputClientCounts For(DaemonValidatedConfig config, DaemonBroadcasterCollection? broadcasters)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new OutputClientCounts(
            config.BeastEnabled ? broadcasters?.BeastClients ?? 0 : null,
            config.JsonEnabled ? broadcasters?.JsonClients ?? 0 : null,
            config.SbsEnabled ? broadcasters?.SbsClients ?? 0 : null);
    }
}
