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

// The latitude-longitude box the server is asked to send aircraft for. Pure, with
// no imports, so it runs directly under `node --test` and anything can use it.
//
// The server takes longitudes in [-180, 180] and reads a box whose west edge is
// east of its east edge as one that crosses the 180° meridian. MapLibre does not
// wrap the longitudes of its bounds, so a map panned across the meridian reports
// an edge such as 188°, and a map zoomed out far enough to show the world more
// than once reports edges beyond ±180 on both sides. Sending either unchanged gets
// the request rejected.

// Into [-180, 180]. Values already in range are returned as they are, so 180 and
// -180 both survive; only out-of-range values are folded.
function wrapLongitude(lon) {
    if (lon >= -180 && lon <= 180) return lon;
    const folded = ((lon + 180) % 360 + 360) % 360 - 180;
    // A value 180° past the range folds onto -180; as an east edge that would read as
    // the far west end of the map, so the equivalent 180 is used instead.
    return folded === -180 ? 180 : folded;
}

// `{ south, west, north, east }` as the server accepts it, or null when the input
// is missing or not finite. A view at least 360° wide becomes the whole world;
// otherwise each edge is wrapped on its own, which leaves either an ordinary box
// or one with west > east crossing the meridian.
export function subscriptionBounds(bounds) {
    if (!bounds) return null;
    const { south, west, north, east } = bounds;
    if (![south, west, north, east].every(Number.isFinite)) return null;

    const s = Math.max(-90, Math.min(90, south));
    const n = Math.max(-90, Math.min(90, north));

    if (east - west >= 360) {
        return { south: s, west: -180, north: n, east: 180 };
    }

    return { south: s, west: wrapLongitude(west), north: n, east: wrapLongitude(east) };
}
