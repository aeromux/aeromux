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

using Aeromux.Core.Tracking;

// CA2000: these tests deliberately exercise the wrapper's ownership/disposal semantics; the
// wrapper takes ownership of the fake inners and disposal is the subject under test.
#pragma warning disable CA2000

namespace Aeromux.Core.Tests.Tracking;

/// <summary>
/// Tests for <see cref="SwappableAircraftDatabaseLookup"/>: delegation, hot-swap correctness,
/// old-inner disposal, null handling, and lookup/swap concurrency safety.
/// </summary>
public class SwappableAircraftDatabaseLookupTests
{
    /// <summary>Fake inner lookup that records disposal and returns a marker registration.</summary>
    private sealed class FakeLookup : IAircraftDatabaseLookup, IDisposable
    {
        private readonly string _registration;
        public bool Disposed { get; private set; }

        public FakeLookup(string registration) => _registration = registration;

        public AircraftDatabaseRecord LookupAircraft(string icaoAddress)
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(FakeLookup));
            }

            return new AircraftDatabaseRecord { Registration = _registration };
        }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void LookupAircraft_DelegatesToCurrentInner()
    {
        var inner = new FakeLookup("REG-1");
        var wrapper = new SwappableAircraftDatabaseLookup(inner, "v1");

        wrapper.LookupAircraft("ABCDEF").Registration.Should().Be("REG-1");
        wrapper.CurrentVersion.Should().Be("v1");
    }

    [Fact]
    public void NullInner_ReturnsEmpty_AndNullVersion()
    {
        var wrapper = new SwappableAircraftDatabaseLookup(null, null);

        wrapper.LookupAircraft("ABCDEF").Should().BeSameAs(AircraftDatabaseRecord.Empty);
        wrapper.CurrentVersion.Should().BeNull();
    }

    [Fact]
    public void Swap_UsesNewInner_DisposesOld_AndUpdatesVersion()
    {
        var oldInner = new FakeLookup("OLD");
        var newInner = new FakeLookup("NEW");
        var wrapper = new SwappableAircraftDatabaseLookup(oldInner, "v1");

        wrapper.Swap(newInner, "v2");

        wrapper.LookupAircraft("ABCDEF").Registration.Should().Be("NEW");
        wrapper.CurrentVersion.Should().Be("v2");
        oldInner.Disposed.Should().BeTrue();
        newInner.Disposed.Should().BeFalse();
    }

    [Fact]
    public void Swap_ToNull_ReturnsEmpty_AndNullVersion()
    {
        var oldInner = new FakeLookup("OLD");
        var wrapper = new SwappableAircraftDatabaseLookup(oldInner, "v1");

        wrapper.Swap(null, null);

        wrapper.LookupAircraft("ABCDEF").Should().BeSameAs(AircraftDatabaseRecord.Empty);
        wrapper.CurrentVersion.Should().BeNull();
        oldInner.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DisposesInner_AndIsIdempotent()
    {
        var inner = new FakeLookup("REG");
        var wrapper = new SwappableAircraftDatabaseLookup(inner, "v1");

        wrapper.Dispose();
        wrapper.Dispose(); // idempotent — must not throw

        inner.Disposed.Should().BeTrue();
        wrapper.LookupAircraft("ABCDEF").Should().BeSameAs(AircraftDatabaseRecord.Empty);
    }

    [Fact]
    public void Swap_AfterDispose_DisposesNewInner_AndDoesNotAdopt()
    {
        var inner = new FakeLookup("REG");
        var wrapper = new SwappableAircraftDatabaseLookup(inner, "v1");
        wrapper.Dispose();

        var lateInner = new FakeLookup("LATE");
        wrapper.Swap(lateInner, "v2");

        lateInner.Disposed.Should().BeTrue();
        wrapper.CurrentVersion.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentLookupAndSwap_NeverTouchesDisposedInner()
    {
        var wrapper = new SwappableAircraftDatabaseLookup(new FakeLookup("v0"), "v0");

        // Bounded, cooperative iterations (with frequent yields) rather than a busy-spin, kept small
        // so this test adds negligible CPU load to other (timing-sensitive) tests running in
        // parallel. Detecting a broken lock does not require high volume — the race window is
        // per-operation. Readers: if a disposed inner were ever hit, FakeLookup throws and fails.
        Task[] readers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 2_000; i++)
            {
                wrapper.LookupAircraft("ABCDEF");
                Thread.Yield();
            }
        })).ToArray();

        // Writer: swap in fresh inners, disposing the old under the lock each time.
        Task writer = Task.Run(() =>
        {
            for (int i = 1; i <= 2_000; i++)
            {
                wrapper.Swap(new FakeLookup($"v{i}"), $"v{i}");
                Thread.Yield();
            }
        });

        Func<Task> run = async () => await Task.WhenAll(readers.Append(writer));
        await run.Should().NotThrowAsync();
    }
}
