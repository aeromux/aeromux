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

namespace Aeromux.Infrastructure.Sdr;

/// <summary>
/// Thrown when SDR sources are configured but no RTL-SDR hardware is present on the system.
/// Carries no library-specific detail; the CLI layer catches this type to render user-facing
/// guidance (USB connection, drivers, device detection). Derives from
/// <see cref="InvalidOperationException"/> so callers without a dedicated handler still treat
/// it as a startup precondition failure.
/// </summary>
public sealed class NoRtlSdrDeviceException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance with the default message.
    /// </summary>
    public NoRtlSdrDeviceException()
        : base("No supported RTL-SDR device found on the system.")
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NoRtlSdrDeviceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public NoRtlSdrDeviceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
