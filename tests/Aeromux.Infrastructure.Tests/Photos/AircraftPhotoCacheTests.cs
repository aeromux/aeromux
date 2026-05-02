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

using Aeromux.Infrastructure.Photos;

namespace Aeromux.Infrastructure.Tests.Photos;

public class AircraftPhotoCacheTests
{
    private static PhotoMetadata SamplePositive(string label = "X") =>
        PhotoMetadata.FromHex(
            thumbnailUrl: $"https://t.plnspttrs.net/{label}/photo.jpg",
            photographer: $"Photographer {label}",
            link: $"https://www.planespotters.net/photo/{label}/foo");

    // ─── 1. Insert + retrieve ──────────────────────────────────────────────────

    [Fact]
    public void Insert_ThenTryGet_ReturnsTheMetadata()
    {
        var cache = new AircraftPhotoCache();
        PhotoMetadata metadata = SamplePositive();

        cache.Insert(0x4CA87C, metadata);

        cache.TryGet(0x4CA87C, out PhotoMetadata fetched).Should().BeTrue();
        fetched.Should().BeSameAs(metadata);
    }

    // ─── 2. Miss for unknown ICAO ──────────────────────────────────────────────

    [Fact]
    public void TryGet_UnknownIcao_ReturnsFalse()
    {
        var cache = new AircraftPhotoCache();

        cache.TryGet(0xABCDEF, out PhotoMetadata fetched).Should().BeFalse();
        fetched.Should().BeNull();
    }

    // ─── 3. Evict removes ──────────────────────────────────────────────────────

    [Fact]
    public void Evict_RemovesTheEntry()
    {
        var cache = new AircraftPhotoCache();
        cache.Insert(0x4CA87C, SamplePositive());

        cache.Evict(0x4CA87C);

        cache.TryGet(0x4CA87C, out _).Should().BeFalse();
        cache.Count.Should().Be(0);
    }

    // ─── 4. Evict-unknown is a no-op ───────────────────────────────────────────

    [Fact]
    public void Evict_UnknownIcao_DoesNotThrow()
    {
        var cache = new AircraftPhotoCache();

        Action act = () => cache.Evict(0xABCDEF);

        act.Should().NotThrow();
        cache.Count.Should().Be(0);
    }

    // ─── 5. Negative entries work the same as positive ─────────────────────────

    [Fact]
    public void Insert_NegativeEntry_IsRetrievable()
    {
        var cache = new AircraftPhotoCache();
        var negative = PhotoMetadata.Negative();

        cache.Insert(0x4CA87C, negative);

        cache.TryGet(0x4CA87C, out PhotoMetadata fetched).Should().BeTrue();
        fetched.HasPhoto.Should().BeFalse();
    }

    // ─── 6. LastAccessTicks bumps on read ──────────────────────────────────────

    [Fact]
    public async Task TryGet_Hit_AdvancesLastAccessTicksOnHit()
    {
        var cache = new AircraftPhotoCache(capacity: 4);
        cache.Insert(0xAAA001, SamplePositive("A"));
        cache.Insert(0xAAA002, SamplePositive("B"));
        cache.Insert(0xAAA003, SamplePositive("C"));
        cache.Insert(0xAAA004, SamplePositive("D"));

        // Sleep enough for Environment.TickCount64 (millisecond resolution) to advance.
        await Task.Delay(20);

        // Re-read A so it becomes the most recently accessed.
        cache.TryGet(0xAAA001, out _).Should().BeTrue();

        // Insert a 5th entry — A must survive (recently read), and the oldest
        // *unread* entry (B) must be evicted.
        cache.Insert(0xAAA005, SamplePositive("E"));

        cache.TryGet(0xAAA001, out _).Should().BeTrue("A was just read and is the most-recently-used");
        cache.TryGet(0xAAA002, out _).Should().BeFalse("B is now the LRU candidate and should have been evicted");
        cache.TryGet(0xAAA003, out _).Should().BeTrue();
        cache.TryGet(0xAAA004, out _).Should().BeTrue();
        cache.TryGet(0xAAA005, out _).Should().BeTrue();
        cache.Count.Should().Be(4, "the cap was 4, and one entry was evicted before insert");
    }

    // ─── 7. LRU eviction at cap ────────────────────────────────────────────────

    [Fact]
    public async Task Insert_AtCapacity_EvictsTheOldestEntry()
    {
        var cache = new AircraftPhotoCache(capacity: 3);

        cache.Insert(0xAAA001, SamplePositive("A"));
        await Task.Delay(5);
        cache.Insert(0xAAA002, SamplePositive("B"));
        await Task.Delay(5);
        cache.Insert(0xAAA003, SamplePositive("C"));
        await Task.Delay(5);
        cache.Insert(0xAAA004, SamplePositive("D"));   // triggers sweep

        cache.Count.Should().Be(3);
        cache.TryGet(0xAAA001, out _).Should().BeFalse("A was the oldest");
        cache.TryGet(0xAAA002, out _).Should().BeTrue();
        cache.TryGet(0xAAA003, out _).Should().BeTrue();
        cache.TryGet(0xAAA004, out _).Should().BeTrue();
    }

    // ─── 8. Re-insert for an existing key replaces, doesn't grow count ─────────

    [Fact]
    public void Insert_ExistingKey_ReplacesAndDoesNotGrowCount()
    {
        var cache = new AircraftPhotoCache(capacity: 2);
        cache.Insert(0xAAA001, SamplePositive("A"));
        cache.Insert(0xAAA002, SamplePositive("B"));

        cache.Insert(0xAAA001, PhotoMetadata.Negative());

        cache.Count.Should().Be(2, "an existing-key insert must not push us over capacity");
        cache.TryGet(0xAAA001, out PhotoMetadata fetched).Should().BeTrue();
        fetched.HasPhoto.Should().BeFalse("the new value replaces the old");
        cache.TryGet(0xAAA002, out _).Should().BeTrue("B must not be evicted by an A re-insert");
    }

    // ─── 9. Concurrent inserts for different ICAOs ────────────────────────────

    [Fact]
    public async Task Insert_ConcurrentDifferentIcaos_AllSurviveUnderCap()
    {
        var cache = new AircraftPhotoCache(capacity: 1000);
        const int threadCount = 8;
        const int perThread = 50;

        Task[] tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < perThread; i++)
            {
                uint icao = (uint)((t * 1000) + i);
                cache.Insert(icao, SamplePositive($"{t}-{i}"));
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        cache.Count.Should().Be(threadCount * perThread);
    }

    // ─── 10. Capacity validation ───────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity)
    {
        Action act = () => _ = new AircraftPhotoCache(capacity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
