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

// Pure receiver-centric sky geometry. No DOM, no framework, so it is directly
// testable under `node --test`.

const DEG = Math.PI / 180;

const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);

// Circumscribing latitude/longitude box around the receiver, used as the
// aircraft subscription region when the Sky View is active.
//
// Longitude is sized at the box's *poleward* edge, where a degree of longitude
// is shortest. Sizing at the receiver's own latitude under-covers the poleward
// corners — about 5% at 50 degrees north with a 150 nm range.
//
// Several degenerate cases cannot be expressed as a non-inverted box and fall
// back to full-width longitude. The antimeridian case is the important one: a
// receiver at 179 degrees east yields west = 176.5, east = -178.5, and the
// server's viewport test is a plain `lon >= west && lon <= east` range check
// with no wrap handling, so an inverted box would match nothing at all and the
// view would be silently empty.
export function receiverBox(lat, lon, rangeKm) {
    const dLat = rangeKm / 111.32;
    const south = lat - dLat;
    const north = lat + dLat;

    const fullWidth = {
        south: clamp(south, -85, 85),
        west: -180,
        north: clamp(north, -85, 85),
        east: 180
    };

    // A pole inside the box: every longitude is within range.
    if (north >= 85 || south <= -85) {
        return fullWidth;
    }

    const cosPoleward = Math.cos((Math.abs(lat) + dLat) * DEG);
    if (cosPoleward < 1e-6) {
        return fullWidth;
    }

    const dLon = rangeKm / (111.32 * cosPoleward);
    if (dLon >= 180) {
        return fullWidth;
    }

    const west = lon - dLon;
    const east = lon + dLon;
    if (west < -180 || east > 180) {
        return fullWidth;
    }

    return { south, west, north, east };
}
