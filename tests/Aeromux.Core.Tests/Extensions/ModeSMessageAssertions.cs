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

namespace Aeromux.Core.Tests.Extensions;

/// <summary>
/// FluentAssertions extensions for Mode-S messages.
/// </summary>
public static class ModeSMessageAssertions
{
    extension(ModeSMessage? message)
    {
        /// <summary>
        /// Asserts that the message is an AircraftIdentification with the specified properties.
        /// </summary>
        public void BeAircraftIdentification(string expectedIcao,
            string expectedCallsign,
            string because = "")
        {
            message.Should().NotBeNull(because);
            message.Should().BeOfType<AircraftIdentification>(because);

            var identification = (AircraftIdentification)message!;
            identification.IcaoAddress.Should().Be(expectedIcao, because);
            identification.Callsign.Should().Be(expectedCallsign, because);
        }

        /// <summary>
        /// Asserts that the message is an AirbornePosition with the specified ICAO.
        /// </summary>
        public void BeAirbornePosition(string expectedIcao,
            string because = "")
        {
            message.Should().NotBeNull(because);
            message.Should().BeOfType<AirbornePosition>(because);

            var position = (AirbornePosition)message!;
            position.IcaoAddress.Should().Be(expectedIcao, because);
        }

        /// <summary>
        /// Asserts that the message is an AirborneVelocity with the specified ICAO.
        /// </summary>
        public void BeAirborneVelocity(string expectedIcao,
            string because = "")
        {
            message.Should().NotBeNull(because);
            message.Should().BeOfType<AirborneVelocity>(because);

            var velocity = (AirborneVelocity)message!;
            velocity.IcaoAddress.Should().Be(expectedIcao, because);
        }

        /// <summary>
        /// Asserts that the message has the specified ICAO address.
        /// </summary>
        public void HaveIcaoAddress(string expectedIcao,
            string because = "")
        {
            message.Should().NotBeNull(because);
            message!.IcaoAddress.Should().Be(expectedIcao, because);
        }
    }
}
