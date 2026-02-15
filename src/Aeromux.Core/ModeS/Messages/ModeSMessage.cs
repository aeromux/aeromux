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

namespace Aeromux.Core.ModeS.Messages;

/// <summary>
/// Abstract base record for all Mode S messages.
/// Provides common fields shared by all message types.
/// </summary>
/// <remarks>
/// <para><strong>Ephemeral Data Flow:</strong></para>
/// <para>This is an EPHEMERAL message type (fire-and-forget). Messages are:</para>
/// <list type="bullet">
/// <item>Created by MessageParser from ValidatedFrames</item>
/// <item>Broadcast via TCP to external tools</item>
/// <item>Fed to AircraftTracker to build persistent state</item>
/// <item>NOT stored directly - only AircraftState is persistent</item>
/// </list>
/// <para><strong>Design Decisions:</strong></para>
/// <list type="bullet">
/// <item>Abstract record (not interface) for shared implementation and value equality</item>
/// <item>Immutable by default (records)</item>
/// <item>Excellent pattern matching support (switch expressions)</item>
/// </list>
/// <para><strong>Usage Example - Pattern Matching:</strong></para>
/// <code>
/// ModeSMessage message = parser.ParseMessage(frame);
///
/// switch (message)
/// {
///     case AirbornePosition pos when pos.Position != null:
///         Console.WriteLine($"{pos.IcaoAddress} at {pos.Position} FL{pos.Altitude?.FlightLevel}");
///         break;
///
///     case AircraftIdentification id:
///         Console.WriteLine($"Callsign: {id.Callsign} ({id.IcaoAddress})");
///         break;
///
///     case AirborneVelocity vel when vel.Velocity != null:
///         Console.WriteLine($"{vel.IcaoAddress} traveling at {vel.Velocity.Knots} kts");
///         break;
/// }
/// </code>
/// </remarks>
/// <param name="IcaoAddress">ICAO aircraft address (hex string, e.g., "A12B3C").</param>
/// <param name="Timestamp">UTC timestamp when the message was received.</param>
/// <param name="DownlinkFormat">Downlink format (message type category).</param>
/// <param name="SignalStrength">Signal strength in RSSI (0-255).</param>
/// <param name="WasCorrected">True if error correction was applied during CRC validation.</param>
public abstract record ModeSMessage(
    string IcaoAddress,
    DateTime Timestamp,
    DownlinkFormat DownlinkFormat,
    double SignalStrength,
    bool WasCorrected);
