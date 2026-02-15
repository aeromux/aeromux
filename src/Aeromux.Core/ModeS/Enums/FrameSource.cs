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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Identifies the origin of a Mode S frame for downstream consumers.
/// Used to distinguish between local SDR reception, remote Beast sources, and MLAT-computed positions.
/// </summary>
/// <remarks>
/// Frame sources have different characteristics:
/// - Sdr: Direct RF reception from local RTL-SDR devices (raw signal processing)
/// - Beast: Frames from remote Beast-compatible server via TCP (readsb, dump1090, aeromux daemon)
/// - Mlat: MLAT-computed positions from mlat-client (pre-validated, high-quality positions)
///
/// This enum enables:
/// - UI to display different indicators for MLAT vs SDR aircraft
/// - JSON/SBS output to include source metadata
/// - Analytics to distinguish coverage (SDR) vs computed positions (MLAT)
/// </remarks>
public enum FrameSource
{
    /// <summary>
    /// Frame received from local RTL-SDR device through signal processing pipeline.
    /// Includes: IQ demodulation, preamble detection, CRC validation, confidence tracking.
    /// </summary>
    Sdr,

    /// <summary>
    /// Frame received from remote Beast-compatible TCP server.
    /// Used in 'aeromux live --connect' mode to receive frames from remote sources.
    /// Includes frames from remote SDR devices and potentially remote MLAT positions.
    /// </summary>
    Beast,

    /// <summary>
    /// Frame received from mlat-client containing MLAT-computed aircraft position.
    /// MLAT frames are pre-validated by MLAT network and represent high-quality positions
    /// derived from time-difference-of-arrival calculations across multiple receivers.
    /// These frames bypass deduplication and confidence tracking.
    /// </summary>
    Mlat
}
