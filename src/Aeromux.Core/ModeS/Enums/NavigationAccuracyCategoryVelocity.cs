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

namespace Aeromux.Core.ModeS.Enums;

/// <summary>
/// Navigation Accuracy Category for Velocity (NACv) from Airborne Velocity message (TC 19).
/// Indicates the 95% accuracy of the reported horizontal and vertical velocity.
/// </summary>
/// <remarks>
/// Reference: RTCA DO-260B, Table 2-XX; https://mode-s.org/1090mhz/content/ads-b/7-uncertainty.html
/// NACv field is 3 bits (bits 43-45 in TC 19 messages).
/// Higher values indicate better velocity accuracy.
/// Values 5-7 are reserved for future use.
/// </remarks>
public enum NavigationAccuracyCategoryVelocity
{
    /// <summary>
    /// Unknown velocity accuracy or data not available.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Horizontal velocity error &lt; 10 m/s, vertical velocity error &lt; 15.2 m/s (50 fps).
    /// Basic velocity accuracy for general awareness.
    /// </summary>
    LessThan10MetersPerSecond = 1,

    /// <summary>
    /// Horizontal velocity error &lt; 3 m/s, vertical velocity error &lt; 4.5 m/s (15 fps).
    /// Good velocity accuracy for traffic monitoring.
    /// </summary>
    LessThan3MetersPerSecond = 2,

    /// <summary>
    /// Horizontal velocity error &lt; 1 m/s, vertical velocity error &lt; 1.5 m/s (5 fps).
    /// High velocity accuracy for ATC applications.
    /// </summary>
    LessThan1MeterPerSecond = 3,

    /// <summary>
    /// Horizontal velocity error &lt; 0.3 m/s, vertical velocity error &lt; 0.46 m/s (1.5 fps).
    /// Precision velocity for safety-critical operations.
    /// </summary>
    LessThan0Point3MetersPerSecond = 4,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    Reserved5 = 5,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    Reserved6 = 6,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    Reserved7 = 7
}
