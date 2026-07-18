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

using Aeromux.Core.ModeS.ValueObjects;

namespace Aeromux.CLI.Commands.Daemon.WebMap;

/// <summary>
/// In-memory aggregator for the web-map traffic-density heatmap. Records each
/// observed aircraft position into a sparse 1 nm base grid keyed by cell, storing
/// the last-seen time per distinct aircraft, and answers per-client queries for
/// coloured display cells over a rolling time window. The grid is a fixed lattice
/// squared to an optional reference latitude (typically the receiver's, else 45°) —
/// it works with or without a receiver.
/// </summary>
public sealed class HeatmapTracker
{
    private const double BaseCellNm = 1.0;        // finest grid resolution
    private const double NmPerDegreeLat = 60.0;   // 1° latitude ≈ 60 nm
    private const double MinCosLat = 0.01;        // clamp cos(lat) near the poles
    private const int ScaleFloor = 2;             // log(1)=0 guard for the colour scale
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    // baseCellId -> (ICAO -> last-seen UTC). Sparse: only visited cells exist.
    // Guarded by a single lock; all access is from the one push-loop thread.
    private readonly Dictionary<long, Dictionary<string, DateTime>> _cells = new();
    private readonly object _lock = new();
    private readonly double _cosRefLat;

    /// <summary>
    /// Initializes the tracker. The optional reference latitude fixes a single longitude
    /// cell width for the whole grid, so columns line up across rows into a clean lattice;
    /// cells are exactly square on the ground at this latitude and drift only slightly
    /// rectangular away from it. Defaults to 45° when unset (e.g. no receiver configured).
    /// </summary>
    /// <param name="referenceLatitude">Latitude the grid is squared to, typically the receiver's.</param>
    public HeatmapTracker(double? referenceLatitude = null)
    {
        double refLat = referenceLatitude ?? 45.0;
        _cosRefLat = Math.Max(Math.Cos(refLat * Math.PI / 180.0), MinCosLat);
    }

    /// <summary>
    /// Records an aircraft position at the current UTC time, upserting the aircraft's
    /// last-seen timestamp in its 1 nm base cell.
    /// </summary>
    /// <param name="icao">The aircraft's ICAO 24-bit address.</param>
    /// <param name="position">The aircraft's decoded geographic position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="icao"/> or <paramref name="position"/> is null.</exception>
    public void RecordPosition(string icao, GeographicCoordinate position)
        => RecordPosition(icao, position, DateTime.UtcNow);

    /// <summary>
    /// Records an aircraft position at an explicit timestamp. Test seam; production
    /// callers use <see cref="RecordPosition(string, GeographicCoordinate)"/>.
    /// </summary>
    internal void RecordPosition(string icao, GeographicCoordinate position, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(icao);
        ArgumentNullException.ThrowIfNull(position);

        (int row, int col) = IndexOf(position.Latitude, position.Longitude, BaseCellNm);
        long id = Pack(row, col);

        lock (_lock)
        {
            if (!_cells.TryGetValue(id, out Dictionary<string, DateTime>? inner))
            {
                inner = new Dictionary<string, DateTime>();
                _cells[id] = inner;
            }
            inner[icao] = nowUtc; // upsert last-seen; distinctness is inherent
        }
    }

    /// <summary>
    /// Convenience over <see cref="BuildSnapshot"/> + <see cref="Project"/>: builds a
    /// snapshot for the configuration and projects it to the viewport in one call.
    /// Callers pushing to many clients build the snapshot once and project per client.
    /// </summary>
    /// <param name="cellSizeNm">Display cell size in nautical miles.</param>
    /// <param name="window">Rolling window; clamped to the 24 h retention.</param>
    /// <param name="viewport">Client viewport (south, west, north, east); null → empty.</param>
    /// <param name="previousScaleMax">The client's last colour anchor, for smoothing.</param>
    /// <returns>
    /// The coloured display cells intersecting the viewport plus the smoothed colour
    /// anchor; <see cref="HeatmapResult.Empty"/> when the viewport is null or no cells
    /// fall within the window.
    /// </returns>
    public HeatmapResult GetCells(
        int cellSizeNm,
        TimeSpan window,
        (double South, double West, double North, double East)? viewport,
        int previousScaleMax)
    {
        if (viewport is null)
        {
            return HeatmapResult.Empty;
        }
        return Project(BuildSnapshot(cellSizeNm, window), viewport, previousScaleMax);
    }

    /// <summary>
    /// Re-bins the whole base grid into display cells and captures the viewport-independent
    /// state: the distinct in-window aircraft count per display cell plus the whole-grid
    /// colour-scale inputs. Independent of any viewport, so one snapshot serves every client
    /// on the same configuration; the colour anchor is computed over the whole grid so colour
    /// is identical for every cell and stable while panning.
    /// </summary>
    /// <param name="cellSizeNm">Display cell size in nautical miles.</param>
    /// <param name="window">Rolling window; clamped to the 24 h retention.</param>
    /// <returns>A snapshot to hand to <see cref="Project"/>; empty when no cell is in-window.</returns>
    public HeatmapSnapshot BuildSnapshot(int cellSizeNm, TimeSpan window)
    {
        TimeSpan effWindow = window > Retention ? Retention : window;
        DateTime cutoff = DateTime.UtcNow - effWindow;

        // Re-bin base cells into display cells (whole grid), unioning in-window ICAOs.
        var display = new Dictionary<long, HashSet<string>>();
        lock (_lock)
        {
            foreach ((long baseId, Dictionary<string, DateTime> inner) in _cells)
            {
                List<string>? recent = null;
                foreach ((string icao, DateTime seen) in inner)
                {
                    if (seen >= cutoff)
                    {
                        (recent ??= new()).Add(icao);
                    }
                }
                if (recent is null)
                {
                    continue;
                }

                (int br, int bc) = Unpack(baseId);
                (double clat, double clon) = CentreOf(br, bc, BaseCellNm);
                (int dr, int dc) = IndexOf(clat, clon, cellSizeNm);
                long did = Pack(dr, dc);

                if (!display.TryGetValue(did, out HashSet<string>? set))
                {
                    set = new HashSet<string>();
                    display[did] = set;
                }
                set.UnionWith(recent);
            }
        }

        if (display.Count == 0)
        {
            return HeatmapSnapshot.Empty(cellSizeNm);
        }

        int[] counts = display.Values.Select(s => s.Count).ToArray();
        int rawScaleP99 = Percentile99(counts);
        int maxCount = counts.Max();

        var displayCounts = new Dictionary<long, int>(display.Count);
        foreach ((long did, HashSet<string> set) in display)
        {
            displayCounts[did] = set.Count;
        }
        return new HeatmapSnapshot(cellSizeNm, displayCounts, rawScaleP99, maxCount);
    }

    /// <summary>
    /// Projects a <see cref="BuildSnapshot"/> result to one client: emits the display cells
    /// intersecting the viewport and folds the client's previous anchor into the smoothed
    /// colour scale. Cheap and lock-free — the snapshot is immutable once built.
    /// </summary>
    /// <param name="snapshot">A snapshot from <see cref="BuildSnapshot"/>.</param>
    /// <param name="viewport">Client viewport (south, west, north, east); null → empty.</param>
    /// <param name="previousScaleMax">The client's last colour anchor, for smoothing.</param>
    /// <returns>
    /// The coloured cells intersecting the viewport plus the smoothed anchor;
    /// <see cref="HeatmapResult.Empty"/> when the viewport is null or the snapshot is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public HeatmapResult Project(
        HeatmapSnapshot snapshot,
        (double South, double West, double North, double East)? viewport,
        int previousScaleMax)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (viewport is null || snapshot.DisplayCounts.Count == 0)
        {
            return HeatmapResult.Empty;
        }

        int scaleMax = SmoothScaleMax(snapshot.RawScaleP99, previousScaleMax);

        var cells = new List<HeatmapCell>();
        (double vS, double vW, double vN, double vE) = viewport.Value;
        foreach ((long did, int count) in snapshot.DisplayCounts)
        {
            (int dr, int dc) = Unpack(did);
            (double s, double w, double n, double e) = BoundsOf(dr, dc, snapshot.CellSizeNm);
            if (n < vS || s > vN || e < vW || w > vE)
            {
                continue; // no overlap
            }
            cells.Add(new HeatmapCell(s, w, n, e, count));
        }

        return new HeatmapResult(scaleMax, snapshot.MaxCount, cells);
    }

    /// <summary>
    /// Drops per-cell aircraft entries older than the 24 h retention and removes
    /// cells that become empty. Bounds memory to the last 24 h of activity.
    /// </summary>
    public void Prune()
    {
        DateTime cutoff = DateTime.UtcNow - Retention;
        lock (_lock)
        {
            var emptyCells = new List<long>();
            foreach ((long id, Dictionary<string, DateTime> inner) in _cells)
            {
                List<string> stale = inner.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
                foreach (string icao in stale)
                {
                    inner.Remove(icao);
                }
                if (inner.Count == 0)
                {
                    emptyCells.Add(id);
                }
            }
            foreach (long id in emptyCells)
            {
                _cells.Remove(id);
            }
        }
    }

    /// <summary>Number of populated base cells currently held. Test/diagnostic hook.</summary>
    internal int PopulatedBaseCellCount
    {
        get { lock (_lock) { return _cells.Count; } }
    }

    // --- Geometry (parameterised by cell size; same formula for base and display grids) ---

    /// <summary>Row/col index of the cell containing (lat, lon) for a given cell size.</summary>
    private (int Row, int Col) IndexOf(double lat, double lon, double cellNm)
    {
        double degLat = cellNm / NmPerDegreeLat;
        int row = (int)Math.Floor(lat / degLat);
        int col = (int)Math.Floor(lon / DegLon(cellNm));
        return (row, col);
    }

    /// <summary>
    /// Longitude cell width, widened by 1/cos(reference latitude). Constant across all
    /// rows so column boundaries line up into a clean lattice instead of drifting.
    /// </summary>
    private double DegLon(double cellNm) => (cellNm / NmPerDegreeLat) / _cosRefLat;

    /// <summary>Geographic centre of a cell (used to re-bin base cells into display cells).</summary>
    private (double Lat, double Lon) CentreOf(int row, int col, double cellNm)
    {
        double degLat = cellNm / NmPerDegreeLat;
        double lat = (row + 0.5) * degLat;
        double lon = (col + 0.5) * DegLon(cellNm);
        return (lat, lon);
    }

    /// <summary>Rectangular lat/lon bounds of a display cell.</summary>
    private (double S, double W, double N, double E) BoundsOf(int row, int col, double cellNm)
    {
        double degLat = cellNm / NmPerDegreeLat;
        double dLon = DegLon(cellNm);
        return (row * degLat, col * dLon, (row + 1) * degLat, (col + 1) * dLon);
    }

    // Pack/unpack two ints into the dictionary key. int→uint→int round-trips negatives.
    private static long Pack(int row, int col) => ((long)row << 32) | (uint)col;

    private static (int Row, int Col) Unpack(long id) => ((int)(id >> 32), (int)(id & 0xFFFFFFFF));

    // --- Colour scale: relative logarithmic, anchored to a smoothed p99 ---

    /// <summary>Nearest-rank 99th percentile of the counts.</summary>
    private static int Percentile99(int[] counts)
    {
        Array.Sort(counts);
        int rank = (int)Math.Ceiling(0.99 * counts.Length); // 1-based
        return counts[Math.Clamp(rank - 1, 0, counts.Length - 1)];
    }

    /// <summary>Rounds up to the nearest 1-2-5 × 10ⁿ value; floored at <see cref="ScaleFloor"/>.</summary>
    private static int NiceCeil(double x)
    {
        if (x <= ScaleFloor)
        {
            return ScaleFloor;
        }
        double exp = Math.Floor(Math.Log10(x));
        double pow = Math.Pow(10, exp);
        double f = x / pow; // 1 ≤ f < 10
        double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
        return (int)Math.Round(nice * pow);
    }

    /// <summary>
    /// Anti-shimmer: keeps the previous anchor while the raw <paramref name="p99"/> stays
    /// within a dead-band around it, so a p99 hovering near a 1-2-5 bucket boundary does not
    /// flip the scale. The band is applied to the raw p99, not to the rounded candidate.
    /// </summary>
    private static int SmoothScaleMax(int p99, int previousScaleMax)
    {
        if (previousScaleMax >= ScaleFloor &&
            p99 >= previousScaleMax * 0.8 && p99 <= previousScaleMax * 1.25)
        {
            return previousScaleMax;
        }
        return Math.Max(ScaleFloor, NiceCeil(p99));
    }
}

/// <summary>
/// Viewport-independent heatmap state for one (cellSizeNm, window): the distinct
/// in-window aircraft count per display cell plus the whole-grid colour-scale inputs.
/// Built once by <see cref="HeatmapTracker.BuildSnapshot"/> and shared across every
/// client on the same configuration, then turned into per-client results by
/// <see cref="HeatmapTracker.Project"/>.
/// </summary>
public sealed class HeatmapSnapshot
{
    internal HeatmapSnapshot(int cellSizeNm, Dictionary<long, int> displayCounts, int rawScaleP99, int maxCount)
    {
        CellSizeNm = cellSizeNm;
        DisplayCounts = displayCounts;
        RawScaleP99 = rawScaleP99;
        MaxCount = maxCount;
    }

    /// <summary>Display cell size in nautical miles the snapshot was binned to.</summary>
    internal int CellSizeNm { get; }

    /// <summary>Distinct in-window aircraft count keyed by packed display cell id.</summary>
    internal Dictionary<long, int> DisplayCounts { get; }

    /// <summary>Whole-grid 99th-percentile count: the un-smoothed colour anchor.</summary>
    internal int RawScaleP99 { get; }

    /// <summary>Whole-grid busiest-cell count, for the legend's peak note.</summary>
    internal int MaxCount { get; }

    /// <summary>Empty snapshot: no cells. RawScaleP99 is unused — Project short-circuits on empty.</summary>
    internal static HeatmapSnapshot Empty(int cellSizeNm) => new(cellSizeNm, new(), 0, 0);
}

/// <summary>Result of a heatmap query: coloured cells plus the colour anchor.</summary>
/// <param name="ScaleMax">Colour anchor: the smoothed, nice-rounded 99th-percentile count that maps to red.</param>
/// <param name="MaxCount">The busiest cell's distinct-aircraft count (for the legend's peak note).</param>
/// <param name="Cells">The coloured display cells within the client's viewport.</param>
public sealed record HeatmapResult(int ScaleMax, int MaxCount, IReadOnlyList<HeatmapCell> Cells)
{
    /// <summary>Empty result: floor anchor, no cells.</summary>
    public static readonly HeatmapResult Empty = new(2, 0, []); // 2 == ScaleFloor
}

/// <summary>One coloured display cell pushed to web-map clients.</summary>
/// <param name="South">Southern latitude bound in degrees.</param>
/// <param name="West">Western longitude bound in degrees.</param>
/// <param name="North">Northern latitude bound in degrees.</param>
/// <param name="East">Eastern longitude bound in degrees.</param>
/// <param name="Count">Distinct aircraft observed in the cell within the window.</param>
public sealed record HeatmapCell(double South, double West, double North, double East, int Count);
