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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.ModeS.Messages;

namespace Aeromux.Core.ModeS;

/// <summary>
/// Represents a processed Mode S frame containing both raw and parsed representations.
/// Created after a ValidatedFrame has been optionally parsed by MessageParser.
/// Enables format-specific encoding without redundant parsing.
/// </summary>
/// <remarks>
/// PARSE-ONCE ARCHITECTURE:
/// Each ValidatedFrame is processed exactly once in the pipeline (DeviceWorker callback).
/// The resulting ProcessedFrame contains both representations:
/// - Frame: Raw ValidatedFrame for binary protocols (Beast format)
/// - ParsedMessage: Decoded ModeSMessage for text protocols (JSON/SBS) and display (TUI)
/// - Timestamp: When the frame was processed (used for age calculation)
///
/// This eliminates redundant parsing when broadcasting to multiple output formats.
/// Beast encoder uses Frame (raw binary with timestamps).
/// JSON/SBS encoders use ParsedMessage (decoded aircraft data).
/// TUI displays use both (ICAO from frame, position/altitude from message).
///
/// ParsedMessage is null when:
/// - Frame format is not supported by MessageParser
/// - Frame data is malformed despite passing CRC validation
/// - Downlink Format is recognized but message parsing failed
///
/// CONSTRUCTION:
/// No factory needed - simple record construction at callback sites:
/// new ProcessedFrame(validatedFrame, parsedMessage, DateTime.UtcNow, FrameSource.Sdr)
///
/// This is intentionally lightweight compared to ValidatedFrameFactory which handles
/// complex CRC validation, error correction, and ICAO extraction.
///
/// FRAME SOURCE TRACKING:
/// The Source parameter identifies where the frame originated:
/// - Sdr: Local RTL-SDR device (default)
/// - Beast: Remote Beast-compatible server (--connect mode)
/// - Mlat: MLAT-computed position from mlat-client
/// This enables UI to distinguish MLAT aircraft and JSON output to include source metadata.
/// </remarks>
/// <param name="Frame">The validated frame (raw binary data with ICAO and signal strength)</param>
/// <param name="ParsedMessage">The parsed message (null if unparseable or unsupported format)</param>
/// <param name="Timestamp">When this frame was processed (UTC)</param>
/// <param name="Source">Origin of this frame (Sdr, Beast, or Mlat). Defaults to Sdr for backward compatibility.</param>
public record ProcessedFrame(
    ValidatedFrame Frame,
    ModeSMessage? ParsedMessage,
    DateTime Timestamp,
    FrameSource Source = FrameSource.Sdr
);
