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

using Aeromux.Core.Configuration;
using Aeromux.Core.Tests.TestData;
using Aeromux.Core.Tracking;

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for frames whose address is not an ICAO aircraft address (DF 18 with CF 1, 4, 5 or 7,
/// TIS-B/ADS-R with the IMF bit set, DF 19).
/// </summary>
/// <remarks>
/// Such an address is a TIS-B track number, an anonymous address or similar. Counting it as an
/// aircraft address would create a phantom aircraft or touch an unrelated one with the same
/// 24 bits, so these frames pass through (for Beast output) without confirming any address
/// and without reaching the aircraft tracker. The frames go through the real
/// ValidatedFrameFactory, as in production.
/// </remarks>
public class NonIcaoAddressTests : AircraftStateTrackerTestsBase
{
    // Made up from AirbornePos_80073B_Even: DF 18 with CF 1 (non-ICAO address), parity recomputed
    private const string Df18Cf1Position = "9180073B58CD7331C274978DD890";

    private readonly ValidatedFrameFactory _factory = new();
    private readonly MessageParser _parser = new();

    [Fact]
    public void ConfidenceTracker_NonIcaoFrames_PassWithoutConfirmingTheAddress()
    {
        // Arrange
        using var confidence = new IcaoConfidenceTracker(ConfidenceLevel.Low, 60);
        ValidatedFrame frame = Validate(Df18Cf1Position);

        for (int i = 0; i < 10; i++)
        {
            // Act
            bool passed = confidence.TrackAndValidate(frame, out bool isNewConfirmed);

            // Assert
            passed.Should().BeTrue(because: "the frame is CRC-valid and still goes to Beast output");
            isNewConfirmed.Should().BeFalse();
        }

        confidence.IsConfident(frame.IcaoRaw).Should().BeFalse(because: "a non-ICAO address never counts toward an aircraft");
    }

    [Fact]
    public void Tracker_NonIcaoFrame_CreatesNoAircraft()
    {
        // Arrange
        Tracker = CreateTracker();
        ValidatedFrame frame = Validate(Df18Cf1Position);

        // Act
        Tracker.Update(new ProcessedFrame(frame, _parser.ParseMessage(frame), DateTime.UtcNow));

        // Assert
        Tracker.GetAircraft(frame.IcaoAddress).Should().BeNull();
    }

    [Fact]
    public void Tracker_IcaoFrame_ThroughSamePath_CreatesAircraft()
    {
        // Arrange - the DF 17 frame the made-up one was derived from
        Tracker = CreateTracker();
        ValidatedFrame frame = Validate(RealFrames.AirbornePos_80073B_Even);

        // Act
        Tracker.Update(new ProcessedFrame(frame, _parser.ParseMessage(frame), DateTime.UtcNow));

        // Assert
        Tracker.GetAircraft("80073B").Should().NotBeNull();
    }

    private ValidatedFrame Validate(string hex)
    {
        ValidatedFrame? frame = _factory.ValidateFrame(new RawFrame(Convert.FromHexString(hex), DateTime.UtcNow, 0, 0.0), 0.0);
        frame.Should().NotBeNull(because: "the made-up frame has a valid parity");
        return frame!;
    }
}
