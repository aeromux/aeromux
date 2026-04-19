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

using FluentAssertions;

namespace Aeromux.Core.Tests.ModeS;

/// <summary>
/// Tests for ValidatedFrameFactory ICAO string cache eviction.
/// Verifies that the cache clears when it exceeds capacity to prevent unbounded memory growth
/// from noise ICAOs in AP mode (DF 0/4/5/16/20/21).
/// </summary>
public class ValidatedFrameFactoryCacheTests
{
    private readonly ValidatedFrameFactory _factory = new();

    /// <summary>
    /// Feeds more unique AP-mode frames than the cache capacity and verifies the cache was cleared.
    /// AP mode (DF 0) always "validates" and caches the derived ICAO string.
    /// Each unique last-3-byte combination produces a different ICAO via CRC XOR.
    /// </summary>
    [Fact]
    public void ValidateFrame_APMode_ClearsCacheAtCapacity()
    {
        // Arrange — generate 70,000 unique DF 0 frames (exceeds 65,536 cache limit)
        int frameCount = 70_000;

        // Act
        for (int i = 0; i < frameCount; i++)
        {
            // DF 0 = 0b00000_xxx → byte 0 top 5 bits = 0 → 0x00-0x07
            // Vary last 3 bytes to produce unique ICAOs via CRC XOR
            byte[] data =
            [
                0x02, 0x00, 0x00, 0x00,
                (byte)(i >> 16), (byte)(i >> 8), (byte)i
            ];
            var rawFrame = new RawFrame(data, DateTime.UtcNow, 0, 0.0);
            _factory.ValidateFrame(rawFrame, 0.0);
        }

        // Assert
        _factory.CacheClears.Should().BeGreaterThan(0,
            "cache should have been cleared after exceeding 65,536 entries");
    }

    /// <summary>
    /// Verifies that ICAO strings remain correctly formatted after a cache clear.
    /// </summary>
    [Fact]
    public void ValidateFrame_APMode_ReturnsCorrectIcaoAfterCacheClear()
    {
        // Arrange — fill cache past capacity
        for (int i = 0; i < 70_000; i++)
        {
            byte[] data =
            [
                0x02, 0x00, 0x00, 0x00,
                (byte)(i >> 16), (byte)(i >> 8), (byte)i
            ];
            var rawFrame = new RawFrame(data, DateTime.UtcNow, 0, 0.0);
            _factory.ValidateFrame(rawFrame, 0.0);
        }

        _factory.CacheClears.Should().BeGreaterThan(0);

        // Act — validate one more frame and check the ICAO format
        byte[] testData = [0x02, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC];
        var testFrame = new RawFrame(testData, DateTime.UtcNow, 0, 0.0);
        ValidatedFrame? result = _factory.ValidateFrame(testFrame, 10.0);

        // Assert — frame should validate (AP mode always succeeds) with correctly formatted ICAO
        result.Should().NotBeNull();
        result!.IcaoAddress.Should().MatchRegex("^[0-9A-F]{6}$",
            "ICAO should be a 6-character uppercase hex string after cache clear");
    }
}
