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

using System.Net;

namespace Aeromux.Core.Configuration;

/// <summary>
/// Network server configuration for output protocols.
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// Gets or sets the Beast binary protocol port number.
    /// Beast format: Raw binary Mode S frames with timestamps.
    /// Used for feeding other ADS-B software (e.g., tar1090, Virtual Radar Server).
    /// Default: 30005.
    /// </summary>
    public int BeastPort { get; set; } = 30005;

    /// <summary>
    /// Gets or sets the JSON streaming port number.
    /// JSON format: Line-delimited JSON objects with decoded aircraft data.
    /// Used for web applications and real-time streaming.
    /// Default: 30006 (web-friendly, non-standard port).
    /// </summary>
    public int JsonPort { get; set; } = 30006;

    /// <summary>
    /// Gets or sets the SBS BaseStation text protocol port number.
    /// SBS format: CSV text format compatible with Kinetic's BaseStation application.
    /// Used for compatibility with legacy ADS-B applications.
    /// Default: 30003.
    /// </summary>
    public int SbsPort { get; set; } = 30003;

    /// <summary>
    /// Gets or sets the REST API port number.
    /// Used for REST API endpoints providing aircraft tracking data.
    /// Default: 8080 (standard non-privileged HTTP port).
    /// </summary>
    public int ApiPort { get; set; } = 8080;

    /// <summary>
    /// Gets or sets whether the REST API is enabled.
    /// When disabled, the API server is not started and the port is not listened on.
    /// Default: true (REST API enabled).
    /// </summary>
    public bool ApiEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether Beast binary protocol output is enabled.
    /// When disabled, the Beast broadcaster is not started and the port is not listened on.
    /// Default: true (Beast output enabled).
    /// </summary>
    public bool BeastOutputEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether JSON streaming output is enabled.
    /// When disabled, the JSON broadcaster is not started and the port is not listened on.
    /// Default: false (JSON output disabled by default).
    /// </summary>
    public bool JsonOutputEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether SBS BaseStation protocol output is enabled.
    /// When disabled, the SBS broadcaster is not started and the port is not listened on.
    /// Default: false (SBS output disabled by default).
    /// </summary>
    public bool SbsOutputEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the network interface IP address to bind all servers to.
    /// IPAddress.Any (0.0.0.0): Bind to all network interfaces (accessible remotely).
    /// IPAddress.Loopback (127.0.0.1): Bind to localhost only (local access only).
    /// Specific IP (e.g., 192.168.1.100): Bind to specific network interface.
    /// Default: IPAddress.Any (0.0.0.0 - all interfaces).
    /// </summary>
    public IPAddress BindAddress { get; set; } = IPAddress.Any;
}
