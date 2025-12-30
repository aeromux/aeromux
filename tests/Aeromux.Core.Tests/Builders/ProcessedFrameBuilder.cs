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

using Aeromux.Core.ModeS;

namespace Aeromux.Core.Tests.Builders;

/// <summary>
/// Fluent builder for creating ProcessedFrame instances from hex strings for testing.
/// Simplifies test setup by providing a clean API for constructing frames from real Mode-S data.
/// </summary>
public class ProcessedFrameBuilder
{
    private string? _hexData;
    private string? _icaoAddress;
    private byte _signalStrength = 128;
    private DateTime _timestamp = DateTime.UtcNow;
    private Aeromux.Core.ModeS.MessageParser? _parser;

    /// <summary>
    /// Sets the raw hex data for the Mode-S frame.
    /// </summary>
    /// <param name="hexData">Hex string (e.g., "8D4840D6202CC371C32CE0576098")</param>
    /// <returns>This builder for fluent chaining</returns>
    public ProcessedFrameBuilder WithHexData(string hexData)
    {
        _hexData = hexData;
        return this;
    }

    /// <summary>
    /// Sets the ICAO 24-bit address for the frame.
    /// </summary>
    /// <param name="icao">6-character hex ICAO address (e.g., "4840D6")</param>
    /// <returns>This builder for fluent chaining</returns>
    public ProcessedFrameBuilder WithIcaoAddress(string icao)
    {
        _icaoAddress = icao;
        return this;
    }

    /// <summary>
    /// Sets the signal strength (RSSI) for the frame.
    /// </summary>
    /// <param name="strength">Signal strength value (0-255)</param>
    /// <returns>This builder for fluent chaining</returns>
    public ProcessedFrameBuilder WithSignalStrength(byte strength)
    {
        _signalStrength = strength;
        return this;
    }

    /// <summary>
    /// Sets the timestamp for when the frame was received/processed.
    /// </summary>
    /// <param name="timestamp">UTC timestamp</param>
    /// <returns>This builder for fluent chaining</returns>
    public ProcessedFrameBuilder WithTimestamp(DateTime timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    /// <summary>
    /// Sets a shared MessageParser instance for CPR decoding.
    /// Use this when testing CPR position decoding that requires even/odd frame pairs.
    /// </summary>
    /// <param name="parser">Shared parser instance that maintains CPR state</param>
    /// <returns>This builder for fluent chaining</returns>
    public ProcessedFrameBuilder WithParser(Aeromux.Core.ModeS.MessageParser parser)
    {
        _parser = parser;
        return this;
    }

    /// <summary>
    /// Builds a ProcessedFrame by:
    /// 1. Creating a ValidatedFrame from hex data
    /// 2. Parsing the message using MessageParser
    /// 3. Combining into a ProcessedFrame
    /// </summary>
    /// <returns>Complete ProcessedFrame ready for testing</returns>
    /// <exception cref="InvalidOperationException">Thrown if hex data or ICAO not set</exception>
    public ProcessedFrame Build()
    {
        if (_hexData == null)
        {
            throw new InvalidOperationException("Hex data must be set before building ProcessedFrame");
        }

        if (_icaoAddress == null)
        {
            throw new InvalidOperationException("ICAO address must be set before building ProcessedFrame");
        }

        // Create ValidatedFrame using existing builder
        var validatedFrame = new ValidatedFrameBuilder()
            .WithHexData(_hexData)
            .WithIcaoAddress(_icaoAddress)
            .WithSignalStrength(_signalStrength)
            .WithTimestamp(_timestamp)
            .Build();

        // Parse message using shared parser (for CPR state) or create new one
        var parser = _parser ?? new Aeromux.Core.ModeS.MessageParser();
        ModeSMessage? message = parser.ParseMessage(validatedFrame);

        // Create and return ProcessedFrame
        return new ProcessedFrame(validatedFrame, message, _timestamp);
    }
}
