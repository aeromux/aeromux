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

namespace Aeromux.Infrastructure.Network.Enums;

/// <summary>
/// Broadcast format for TCP streaming.
/// </summary>
public enum BroadcastFormat
{
    /// <summary>
    /// Beast binary format (dump1090/readsb compatible, port 30005).
    /// Transmits raw frames with timestamp and signal strength.
    /// </summary>
    Beast,

    /// <summary>
    /// JSON format (web-friendly, port 30006).
    /// Transmits parsed messages as line-delimited JSON.
    /// </summary>
    Json,

    /// <summary>
    /// SBS/BaseStation format (Virtual Radar Server compatible, port 30003).
    /// Transmits parsed messages as CSV.
    /// </summary>
    Sbs
}
