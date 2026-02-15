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

namespace Aeromux.Core.Timing;

/// <summary>
/// Provides high-precision timestamps for frame detection.
/// </summary>
/// <remarks>
/// This interface enables dependency injection of different timing strategies:
/// <list type="bullet">
/// <item>Production: StopwatchTimeProvider (sub-millisecond precision via Stopwatch)</item>
/// <item>Testing: FixedTimeProvider (deterministic timestamps for reproducible tests)</item>
/// </list>
/// </remarks>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current timestamp with sub-millisecond precision.
    /// </summary>
    /// <returns>Current UTC timestamp</returns>
    /// <remarks>
    /// Implementations should provide monotonically increasing timestamps
    /// (i.e., each call returns a time >= previous call).
    /// </remarks>
    DateTime GetCurrentTimestamp();
}
