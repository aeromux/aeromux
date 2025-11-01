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

namespace Aeromux.Core.Configuration;

/// <summary>
/// Network server configuration for output protocols.
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// Gets or sets the Beast binary protocol port number.
    /// Used for feeding other ADS-B software (e.g., tar1090, VRS).
    /// Default: 30002
    /// </summary>
    public int BeastPort { get; set; } = 30002;

    /// <summary>
    /// Gets or sets the SBS BaseStation text protocol port number.
    /// Used for compatibility with legacy ADS-B applications.
    /// Default: 30003
    /// </summary>
    public int SbsPort { get; set; } = 30003;

    /// <summary>
    /// Gets or sets the HTTP API and web interface port number.
    /// Default: 8080
    /// </summary>
    public int HttpPort { get; set; } = 8080;

    /// <summary>
    /// Gets or sets the network interface IP address to bind to.
    /// "0.0.0.0" = all interfaces, "127.0.0.1" = localhost only.
    /// Default: "0.0.0.0"
    /// </summary>
    public string BindAddress { get; set; } = "0.0.0.0";
}
