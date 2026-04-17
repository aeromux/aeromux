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
/// Tracks the farthest received aircraft position in each 5-degree bearing sector
/// from the receiver location. Produces a range outline polygon representing
/// receiver coverage over a sliding time window.
/// </summary>
public sealed class RangeOutlineTracker
{
    private const int SectorSize = 5;
    private const int SectorCount = 360 / SectorSize;
    private const double MaxDistanceNm = 300.0;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(4);

    private readonly GeographicCoordinate _receiver;
    private readonly RangeOutlineEntry?[] _sectors = new RangeOutlineEntry?[SectorCount];
    private readonly object _lock = new();

    /// <summary>
    /// Initializes the tracker with the receiver's geographic position.
    /// </summary>
    /// <param name="receiverLatitude">Receiver latitude in decimal degrees.</param>
    /// <param name="receiverLongitude">Receiver longitude in decimal degrees.</param>
    public RangeOutlineTracker(double receiverLatitude, double receiverLongitude)
    {
        _receiver = new GeographicCoordinate(receiverLatitude, receiverLongitude);
    }

    /// <summary>
    /// Records an aircraft position. Updates the bearing sector if this position
    /// is farther than the current entry or the current entry has expired.
    /// Positions beyond 300 nm are silently discarded.
    /// </summary>
    /// <param name="position">The aircraft's decoded geographic position.</param>
    public void RecordPosition(GeographicCoordinate position)
    {
        ArgumentNullException.ThrowIfNull(position);

        double distanceNm = _receiver.DistanceToNauticalMiles(position);
        if (distanceNm > MaxDistanceNm)
        {
            return;
        }

        double bearing = _receiver.BearingTo(position);
        int sector = (int)Math.Floor(bearing / SectorSize) % SectorCount;

        lock (_lock)
        {
            RangeOutlineEntry? existing = _sectors[sector];
            if (existing is null || distanceNm > existing.DistanceNm || IsExpired(existing))
            {
                _sectors[sector] = new RangeOutlineEntry(
                    position.Latitude, position.Longitude, distanceNm, DateTime.UtcNow);
            }
        }
    }

    /// <summary>
    /// Returns the range outline as a list of farthest positions in bearing order.
    /// Empty sectors are skipped — the polygon connects populated sectors directly,
    /// producing an irregular shape that reflects actual reception coverage.
    /// Prunes expired entries. Returns an empty list if fewer than 3 sectors are populated.
    /// </summary>
    public List<RangeOutlineCoordinate> GetOutline()
    {
        lock (_lock)
        {
            DateTime cutoff = DateTime.UtcNow - Retention;
            List<RangeOutlineCoordinate> outline = new();

            for (int i = 0; i < SectorCount; i++)
            {
                RangeOutlineEntry? entry = _sectors[i];
                if (entry is null)
                {
                    continue;
                }

                if (entry.Timestamp < cutoff)
                {
                    _sectors[i] = null;
                    continue;
                }

                outline.Add(new RangeOutlineCoordinate(entry.Latitude, entry.Longitude));
            }

            // Need at least 3 points to form a polygon
            return outline.Count >= 3 ? outline : [];
        }
    }

    /// <summary>
    /// Returns true if the entry's timestamp is older than the retention window.
    /// </summary>
    private bool IsExpired(RangeOutlineEntry entry)
        => entry.Timestamp < DateTime.UtcNow - Retention;
}

/// <summary>
/// Entry for a single bearing sector in the range outline tracker,
/// storing the farthest received position and its timestamp.
/// </summary>
public sealed record RangeOutlineEntry(
    double Latitude, double Longitude, double DistanceNm, DateTime Timestamp);

/// <summary>
/// A coordinate point in the range outline polygon, pushed to web map clients.
/// </summary>
public sealed record RangeOutlineCoordinate(double Latitude, double Longitude);
