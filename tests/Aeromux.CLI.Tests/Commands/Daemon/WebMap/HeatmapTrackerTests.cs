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

using Aeromux.CLI.Commands.Daemon.WebMap;
using Aeromux.Core.ModeS.ValueObjects;
using FluentAssertions;

namespace Aeromux.CLI.Tests.Commands.Daemon.WebMap;

/// <summary>
/// Unit tests for <see cref="HeatmapTracker"/>: grid indexing, distinct counting,
/// window filtering, base→display re-binning, colour-anchor maths, viewport filtering,
/// and pruning. Exercised through the public/internal API (geometry helpers are private).
/// </summary>
public class HeatmapTrackerTests
{
    // Whole-world viewport used by most tests.
    private static readonly (double, double, double, double) World = (-90, -180, 90, 180);
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    private static GeographicCoordinate At(double lat, double lon) => new(lat, lon);

    [Fact]
    public void DistinctCounting_SameAircraftManyTimes_CountsOnce()
    {
        var t = new HeatmapTracker();
        for (int i = 0; i < 10; i++)
        {
            t.RecordPosition("AAAAAA", At(47.0, 19.0));
        }
        t.RecordPosition("BBBBBB", At(47.0, 19.0));
        t.RecordPosition("CCCCCC", At(47.0, 19.0));

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.Cells.Should().ContainSingle();
        r.Cells[0].Count.Should().Be(3);
    }

    [Fact]
    public void CellSeparation_PointsFarApart_LandInDifferentCells()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0));
        t.RecordPosition("BBBBBB", At(47.0, 20.0)); // ~1° lon ≈ 40 nm apart at 47°N

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.Cells.Should().HaveCount(2);
        r.Cells.Should().OnlyContain(c => c.Count == 1);
    }

    [Fact]
    public void ColumnsAlignAcrossRows()
    {
        // With a single reference latitude, every row shares one column width, so cells in
        // different rows at the same longitude have identical west/east bounds — a clean
        // lattice rather than a staggered grid.
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0));
        t.RecordPosition("BBBBBB", At(47.5, 19.0)); // several rows north, same longitude

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.Cells.Should().HaveCount(2);
        r.Cells[0].West.Should().BeApproximately(r.Cells[1].West, 1e-9);
        r.Cells[0].East.Should().BeApproximately(r.Cells[1].East, 1e-9);
        r.Cells[0].South.Should().NotBe(r.Cells[1].South); // genuinely different rows
    }

    [Fact]
    public void WindowFiltering_ExcludesOldSightings_AndClampsToRetention()
    {
        var t = new HeatmapTracker();
        DateTime now = DateTime.UtcNow;
        t.RecordPosition("AAAAAA", At(47.0, 19.0), now - TimeSpan.FromHours(25));

        // 25 h old is outside the 24 h window.
        t.GetCells(5, Day, World, 0).Cells.Should().BeEmpty();

        // A 48 h request is clamped to 24 h retention, so it is still excluded.
        t.GetCells(5, TimeSpan.FromHours(48), World, 0).Cells.Should().BeEmpty();

        // A 23 h old sighting is inside the window.
        t.RecordPosition("BBBBBB", At(47.0, 19.0), now - TimeSpan.FromHours(23));
        HeatmapResult r = t.GetCells(5, Day, World, 0);
        r.Cells.Should().ContainSingle();
        r.Cells[0].Count.Should().Be(1); // only BBBBBB; AAAAAA aged out
    }

    [Fact]
    public void BaseToDisplayAggregation_UnionAcrossBaseCells_CountsDistinct()
    {
        var t = new HeatmapTracker();
        // 47.00 and 47.02 are different 1 nm base rows but the same 5 nm display row.
        t.RecordPosition("AAAAAA", At(47.00, 19.0));
        t.RecordPosition("AAAAAA", At(47.02, 19.0)); // same aircraft, adjacent base cell
        t.RecordPosition("BBBBBB", At(47.00, 19.0));

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.Cells.Should().ContainSingle();
        r.Cells[0].Count.Should().Be(2); // {AAAAAA, BBBBBB}, AAAAAA counted once
    }

    [Fact]
    public void ScaleMax_LoneAircraft_IsFlooredAtTwo()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0));

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.MaxCount.Should().Be(1);
        r.ScaleMax.Should().Be(2); // log(1)=0 floor
    }

    [Fact]
    public void ScaleMax_NiceRoundsThePercentile()
    {
        var t = new HeatmapTracker();
        for (int i = 0; i < 3; i++)
        {
            t.RecordPosition($"AC{i:0000}", At(47.0, 19.0));
        }

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.MaxCount.Should().Be(3);
        r.ScaleMax.Should().Be(5); // NiceCeil(3) == 5
    }

    [Fact]
    public void ScaleMax_P99_IgnoresSingleOutlierCell()
    {
        var t = new HeatmapTracker();
        // 100 cells with a single aircraft each, spread out in longitude.
        for (int i = 0; i < 100; i++)
        {
            t.RecordPosition($"LO{i:0000}", At(0.0, i * 1.0));
        }
        // One hot cell with 10 distinct aircraft.
        for (int i = 0; i < 10; i++)
        {
            t.RecordPosition($"HOT{i:000}", At(-40.0, -100.0));
        }

        HeatmapResult r = t.GetCells(5, Day, World, 0);

        r.MaxCount.Should().Be(10);  // the hot cell
        r.ScaleMax.Should().Be(2);   // p99 is 1 (the 99th-percentile cell), NiceCeil→floor 2
    }

    [Fact]
    public void ScaleMax_Hysteresis_HoldsPreviousAnchorInsideDeadBand()
    {
        var t = new HeatmapTracker();
        for (int i = 0; i < 10; i++)
        {
            t.RecordPosition($"AC{i:0000}", At(47.0, 19.0)); // single cell, count 10 → p99 = 10
        }

        // Fresh derivation.
        t.GetCells(5, Day, World, 0).ScaleMax.Should().Be(10); // NiceCeil(10) == 10

        // p99 (10) is inside the dead-band of a previous anchor of 12 → held.
        t.GetCells(5, Day, World, 12).ScaleMax.Should().Be(12);

        // p99 (10) is outside the dead-band of 50 → re-derived to NiceCeil(10) == 10.
        t.GetCells(5, Day, World, 50).ScaleMax.Should().Be(10);
    }

    [Fact]
    public void ViewportFilter_ReturnsOnlyVisibleCells_ButAnchorReflectsWholeGrid()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("VIS001", At(47.0, 19.0)); // in the viewport, count 1
        for (int i = 0; i < 10; i++)
        {
            t.RecordPosition($"FAR{i:000}", At(-40.0, -100.0)); // out of viewport, count 10
        }

        var viewport = (46.0, 18.0, 48.0, 20.0);
        HeatmapResult r = t.GetCells(5, Day, viewport, 0);

        r.Cells.Should().ContainSingle();
        r.Cells[0].Count.Should().Be(1);
        r.MaxCount.Should().Be(10); // whole-grid maximum, including the off-screen cell
    }

    [Fact]
    public void NullViewport_ReturnsEmpty()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0));

        HeatmapResult r = t.GetCells(5, Day, null, 0);

        r.Cells.Should().BeEmpty();
    }

    [Fact]
    public void Prune_RemovesStaleCells()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0), DateTime.UtcNow - TimeSpan.FromHours(25));
        t.PopulatedBaseCellCount.Should().Be(1);

        t.Prune();

        t.PopulatedBaseCellCount.Should().Be(0);
    }

    [Fact]
    public void NoReceiverRequired_AggregatesAndAnswers()
    {
        // The tracker has no receiver dependency; construct and use it directly.
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(51.5, -0.13));

        HeatmapResult r = t.GetCells(10, Day, World, 0);

        r.Cells.Should().ContainSingle();
    }

    [Fact]
    public void BuildSnapshotThenProject_MatchesGetCells()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0));
        t.RecordPosition("BBBBBB", At(47.0, 19.0));
        t.RecordPosition("CCCCCC", At(47.0, 20.0));

        HeatmapResult viaGetCells = t.GetCells(5, Day, World, 0);
        HeatmapResult viaProject = t.Project(t.BuildSnapshot(5, Day), World, 0);

        viaProject.ScaleMax.Should().Be(viaGetCells.ScaleMax);
        viaProject.MaxCount.Should().Be(viaGetCells.MaxCount);
        viaProject.Cells.Should().HaveCount(viaGetCells.Cells.Count);
    }

    [Fact]
    public void SharedSnapshot_ProjectsPerViewport_WithWholeGridAnchor()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("VIS001", At(47.0, 19.0));             // in viewport A, count 1
        for (int i = 0; i < 10; i++)
        {
            t.RecordPosition($"FAR{i:000}", At(-40.0, -100.0)); // in viewport B, count 10
        }

        // One shared build, projected to two different viewports.
        HeatmapSnapshot snapshot = t.BuildSnapshot(5, Day);
        HeatmapResult a = t.Project(snapshot, (46.0, 18.0, 48.0, 20.0), 0);
        HeatmapResult b = t.Project(snapshot, (-41.0, -101.0, -39.0, -99.0), 0);

        a.Cells.Should().ContainSingle();
        a.Cells[0].Count.Should().Be(1);
        b.Cells.Should().ContainSingle();
        b.Cells[0].Count.Should().Be(10);
        // MaxCount is a whole-grid quantity, identical from the one snapshot regardless of viewport.
        a.MaxCount.Should().Be(10);
        b.MaxCount.Should().Be(10);
    }

    [Fact]
    public void Project_NullViewportOrEmptySnapshot_ReturnsEmpty()
    {
        var t = new HeatmapTracker();
        t.RecordPosition("AAAAAA", At(47.0, 19.0));
        t.Project(t.BuildSnapshot(5, Day), null, 0).Cells.Should().BeEmpty();

        var empty = new HeatmapTracker();
        empty.Project(empty.BuildSnapshot(5, Day), World, 0).Cells.Should().BeEmpty();
    }
}
