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

using Aeromux.Core.ModeS.Enums;
using Aeromux.Core.Tracking;
using Aeromux.Infrastructure.Photos;

namespace Aeromux.Infrastructure.Tests.Photos;

public class AircraftPhotoServiceTests
{
    // ─── 1. Cache hit short-circuits the upstream call ────────────────────────

    [Fact]
    public async Task GetAsync_CacheHit_DoesNotCallUpstream()
    {
        var planespotters = new FakePlanespottersClient();
        var cache = new AircraftPhotoCache();
        cache.Insert(0x4CA87C, PhotoMetadata.FromHex(
            "https://t.plnspttrs.net/x.jpg", "Pre-Cached", "https://www.planespotters.net/photo/1/x"));
        var tracker = new FakeTracker();
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0x4CA87C, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.HasPhoto);
        result.Metadata!.Photographer.Should().Be("Pre-Cached");
        planespotters.HexCalls.Should().Be(0);
        planespotters.RegCalls.Should().Be(0);
    }

    // ─── 2. Cache miss + positive hex result → cached and returned ────────────

    [Fact]
    public async Task GetAsync_CacheMissPositiveHex_PopulatesCacheAndReturns()
    {
        var photo = PhotoMetadata.FromHex(
            "https://t.plnspttrs.net/y.jpg", "Photographer Y", "https://www.planespotters.net/photo/2/y");
        var planespotters = new FakePlanespottersClient { HexResult = photo };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker();
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0xAAA111, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.HasPhoto);
        result.Metadata.Should().Be(photo);
        cache.TryGet(0xAAA111, out PhotoMetadata cached).Should().BeTrue();
        cached.Should().Be(photo);
    }

    // ─── 3. Hex returns negative + reg returns positive → cached as Source=reg ─

    [Fact]
    public async Task GetAsync_HexNegativeRegPositive_FallsBackAndCaches()
    {
        var regPhoto = PhotoMetadata.FromReg(
            "https://t.plnspttrs.net/r.jpg", "Reg Photographer", "https://www.planespotters.net/photo/3/r");
        var planespotters = new FakePlanespottersClient
        {
            HexResult = PhotoMetadata.Negative(),
            RegResult = regPhoto,
        };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker { Registration = "EI-DEO" };
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0xAAA222, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.HasPhoto);
        result.Metadata!.Source.Should().Be("reg");
        planespotters.HexCalls.Should().Be(1);
        planespotters.RegCalls.Should().Be(1);
        planespotters.LastRegArg.Should().Be("EI-DEO");
    }

    // ─── 4. Hex negative + reg negative → cache negative, return NoPhoto ─────

    [Fact]
    public async Task GetAsync_HexAndRegBothNegative_CachesNegative()
    {
        var planespotters = new FakePlanespottersClient
        {
            HexResult = PhotoMetadata.Negative(),
            RegResult = PhotoMetadata.Negative(),
        };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker { Registration = "EI-DEO" };
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0xAAA333, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.NoPhoto);
        cache.TryGet(0xAAA333, out PhotoMetadata cached).Should().BeTrue();
        cached.HasPhoto.Should().BeFalse();
    }

    // ─── 5. No registration available → no reg fallback attempted ─────────────

    [Fact]
    public async Task GetAsync_HexNegativeNoRegistration_SkipsRegLookup()
    {
        var planespotters = new FakePlanespottersClient { HexResult = PhotoMetadata.Negative() };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker { Registration = null };  // DB disabled or no record
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0xAAA444, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.NoPhoto);
        planespotters.HexCalls.Should().Be(1);
        planespotters.RegCalls.Should().Be(0);
    }

    // ─── 6. Hex transient → UpstreamFailure, no caching ────────────────────────

    [Fact]
    public async Task GetAsync_HexTransient_ReturnsUpstreamFailureAndDoesNotCache()
    {
        var planespotters = new FakePlanespottersClient { HexResult = null };  // null = transient
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker();
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0xAAA555, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.UpstreamFailure);
        result.Metadata.Should().BeNull();
        cache.TryGet(0xAAA555, out _).Should().BeFalse();
    }

    // ─── 7. Reg transient → UpstreamFailure, no caching ──────────────────────

    [Fact]
    public async Task GetAsync_HexNegativeRegTransient_ReturnsUpstreamFailure()
    {
        var planespotters = new FakePlanespottersClient
        {
            HexResult = PhotoMetadata.Negative(),
            RegResult = null,  // transient
        };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker { Registration = "EI-DEO" };
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        PhotoResult result = await sut.GetAsync(0xAAA666, CancellationToken.None);

        result.Outcome.Should().Be(PhotoOutcome.UpstreamFailure);
        cache.TryGet(0xAAA666, out _).Should().BeFalse();
    }

    // ─── 8. Single-flight: N concurrent calls → 1 upstream call ───────────────

    [Fact]
    public async Task GetAsync_ConcurrentCallsForSameIcao_TriggerExactlyOneUpstreamCall()
    {
        var photo = PhotoMetadata.FromHex(
            "https://t.plnspttrs.net/sf.jpg", "SF", "https://www.planespotters.net/photo/9/sf");
        var planespotters = new FakePlanespottersClient
        {
            HexResult = photo,
            HexDelay = TimeSpan.FromMilliseconds(100),  // ensure overlap
        };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker();
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        Task<PhotoResult>[] tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.GetAsync(0xBBB001, CancellationToken.None))
            .ToArray();
        PhotoResult[] results = await Task.WhenAll(tasks);

        planespotters.HexCalls.Should().Be(1, "single-flight collapses concurrent calls into one upstream lookup");
        results.Should().AllSatisfy(r => r.Outcome.Should().Be(PhotoOutcome.HasPhoto));
    }

    // ─── 9. Tracker eviction during in-flight insert: race-safe via semaphore ─

    [Fact]
    public async Task TrackerEviction_DuringInFlightInsert_LeavesCacheConsistent()
    {
        var photo = PhotoMetadata.FromHex(
            "https://t.plnspttrs.net/race.jpg", "Race", "https://www.planespotters.net/photo/10/race");
        var planespotters = new FakePlanespottersClient
        {
            HexResult = photo,
            HexDelay = TimeSpan.FromMilliseconds(100),
        };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker();
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);

        // Start the slow upstream call.
        Task<PhotoResult> getTask = sut.GetAsync(0xCCC001, CancellationToken.None);
        await Task.Delay(20);  // give the GetAsync time to acquire the semaphore

        // Fire OnAircraftExpired while the GetAsync is mid-flight. The handler
        // will block on the per-ICAO semaphore until the insert completes.
        var expireTask = Task.Run(() => tracker.FireExpired("CCC001"));

        await Task.WhenAll(getTask, expireTask);
        PhotoResult getResult = await getTask;

        // Final state: cache must be empty — the eviction ran *after* the
        // insert and removed the entry it just inserted.
        cache.TryGet(0xCCC001, out _).Should().BeFalse();
        getResult.Outcome.Should().Be(PhotoOutcome.HasPhoto, "the GetAsync itself succeeded — only the cache was evicted afterwards");
    }

    // ─── 10. Dispose unsubscribes from tracker ────────────────────────────────

    [Fact]
    public void Dispose_UnsubscribesFromTracker()
    {
        var planespotters = new FakePlanespottersClient { HexResult = PhotoMetadata.Negative() };
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker();
        var sut = new AircraftPhotoService(planespotters, cache, tracker);
        cache.Insert(0xDDD001, PhotoMetadata.Negative());

        sut.Dispose();

        // After Dispose, firing the event must not invoke the handler — so the
        // entry we manually inserted should still be there.
        tracker.FireExpired("DDD001");

        cache.TryGet(0xDDD001, out _).Should().BeTrue("Dispose should have unsubscribed the eviction handler");
    }

    // ─── Tracker eviction also evicts under the same semaphore (basic) ────────

    [Fact]
    public void TrackerEviction_RemovesCachedEntry()
    {
        var planespotters = new FakePlanespottersClient();
        var cache = new AircraftPhotoCache();
        var tracker = new FakeTracker();
        using var sut = new AircraftPhotoService(planespotters, cache, tracker);
        cache.Insert(0xEEE001, PhotoMetadata.FromHex(
            "https://t.plnspttrs.net/e.jpg", "E", "https://www.planespotters.net/photo/e/e"));

        tracker.FireExpired("EEE001");

        cache.TryGet(0xEEE001, out _).Should().BeFalse();
    }

    // ─── Helper test doubles ───────────────────────────────────────────────────

    /// <summary>
    /// Hand-rolled <see cref="IPlanespottersApiClient"/> fake — records call counts,
    /// returns whatever <see cref="HexResult"/> / <see cref="RegResult"/> the test set.
    /// <c>null</c> means transient failure, matching the real client's contract.
    /// </summary>
    private sealed class FakePlanespottersClient : IPlanespottersApiClient
    {
        public PhotoMetadata? HexResult { get; set; }
        public PhotoMetadata? RegResult { get; set; }
        public TimeSpan HexDelay { get; set; } = TimeSpan.Zero;
        public int HexCalls;
        public int RegCalls;
        public string? LastRegArg;

        public async Task<PhotoMetadata?> GetByHexAsync(uint icao, CancellationToken ct)
        {
            Interlocked.Increment(ref HexCalls);
            if (HexDelay > TimeSpan.Zero)
            {
                await Task.Delay(HexDelay, ct);
            }
            return HexResult;
        }

        public Task<PhotoMetadata?> GetByRegAsync(string registration, CancellationToken ct)
        {
            Interlocked.Increment(ref RegCalls);
            LastRegArg = registration;
            return Task.FromResult(RegResult);
        }
    }

    /// <summary>
    /// Hand-rolled <see cref="IAircraftStateTracker"/> fake. Only implements the
    /// surface the service actually uses: <see cref="GetAircraft"/> and the
    /// <see cref="OnAircraftExpired"/> event. <see cref="FireExpired"/> lets tests
    /// trigger evictions on demand.
    /// </summary>
    private sealed class FakeTracker : IAircraftStateTracker
    {
        public string? Registration { get; set; }

        public Aircraft? GetAircraft(string icao)
        {
            // Return an Aircraft with the configured registration so the service
            // can read aircraft.DatabaseRecord.Registration.
            AircraftDatabaseRecord record = Registration is null
                ? AircraftDatabaseRecord.Empty
                : new AircraftDatabaseRecord { Registration = Registration };
            return new Aircraft
            {
                Identification = new TrackedIdentification
                {
                    ICAO = icao,
                    Callsign = null,
                    Squawk = null,
                    Category = null,
                    EmergencyState = EmergencyState.NoEmergency,
                    FlightStatus = null,
                },
                Status = new TrackedStatus
                {
                    SignalStrength = 0,
                    TotalMessages = 1,
                    PositionMessages = 0,
                    VelocityMessages = 0,
                    IdentificationMessages = 0,
                    FirstSeen = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    SeenSeconds = 0,
                    SeenPosSeconds = null,
                    NextJsonOutput = DateTime.UtcNow,
                },
                DatabaseEnabled = Registration is not null,
                DatabaseRecord = record,
            };
        }

        public void FireExpired(string icao)
        {
            Aircraft? aircraft = GetAircraft(icao);
            OnAircraftExpired?.Invoke(this, new AircraftEventArgs { Aircraft = aircraft! });
        }

        // ─── Unused interface members (throw to make accidental use loud) ──

        public void Update(ProcessedFrame frame) => throw new NotSupportedException();
        public IReadOnlyList<Aircraft> GetAllAircraft() => throw new NotSupportedException();
        public int Count => throw new NotSupportedException();
        public TimeSpan AircraftTimeout { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(10);

        public event Action<Aircraft, Aircraft>? OnAircraftUpdated;
        public event EventHandler<AircraftEventArgs>? OnAircraftAdded;
        public event EventHandler<AircraftEventArgs>? OnAircraftExpired;

        // Suppress "unused event" warnings — these are part of the interface contract.
        private void _suppressWarnings()
        {
            OnAircraftUpdated?.Invoke(default!, default!);
            OnAircraftAdded?.Invoke(this, default!);
        }
    }
}
