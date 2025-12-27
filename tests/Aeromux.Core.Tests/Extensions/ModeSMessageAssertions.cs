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
