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

// How much ground the Map view is showing, and how to write it down. Takes plain
// coordinates rather than a map, so it has no DOM and no MapLibre in it and runs
// directly under `node --test`.
//
// Everything is measured in nautical miles, the unit the rest of the application
// works in, and converted only at the point it is written into the readout.

import { haversineDistance, nmFromDisplayUnit, distanceUnitLabel, nmToKm } from './UnitConversion.js';
// The Earth radius, the bearing and the angle wrap live in the sky geometry, which
// is where they were needed first. Imported rather than restated so there is one of
// each, and re-exposed below in this module's own shape so the Map view has a single
// place to take its measurements from.
import { EARTH_RADIUS_M, bearingTo, wrap180 } from './SkyViewGeometry.js';

const DEG = Math.PI / 180;

export const EARTH_RADIUS_NM = EARTH_RADIUS_M / 1000 / nmToKm(1);

// Display units per nautical mile, derived from the conversion that already exists
// rather than restating its factors: nmFromDisplayUnit is exactly this inverted.
function unitScale(unit) {
    return 1 / nmFromDisplayUnit(1, unit);
}

// Great-circle distance between two coordinates. The readout measures three things
// with it: the span of the map, how far its center is from the receiver, and how far
// the receiver has heard.
export function distanceNm(from, to) {
    if (!isCoordinate(from) || !isCoordinate(to)) return 0;
    return haversineDistance(from.lat, from.lon, to.lat, to.lon) / nmToKm(1);
}

// Initial great-circle bearing, in the same coordinate shape as the rest of this
// module.
export function bearingDeg(from, to) {
    if (!isCoordinate(from) || !isCoordinate(to)) return 0;
    return bearingTo(from.lat, from.lon, to.lat, to.lon);
}

// Area of the quadrilateral the screen corners unproject to, as a spherical polygon.
//
// Taking the corners rather than the map's bounding box matters once the map is
// rotated: the box around a rotated viewport is larger than the viewport itself, by
// about 40% at 45°, and that is the one case where the user can see the number is
// wrong. North-up the two agree exactly, because a screen rectangle in Web Mercator
// is a latitude-longitude rectangle. Rotated, the edges are neither meridians,
// parallels nor great circles and this treats them as the last of those: an
// approximation, negligible at the scales the view is used at and growing with
// latitude and with how much of the world is on screen.
export function polygonAreaNm2(corners) {
    if (!Array.isArray(corners) || corners.length < 3) return 0;

    let total = 0;
    for (let i = 0; i < corners.length; i++) {
        const a = corners[i];
        const b = corners[(i + 1) % corners.length];
        if (!isCoordinate(a) || !isCoordinate(b)) return 0;
        // Per edge, so a viewport straddling the antimeridian contributes its own
        // width instead of the long way around the world.
        total += wrap180(b.lon - a.lon) * DEG * (2 + Math.sin(a.lat * DEG) + Math.sin(b.lat * DEG));
    }

    return Math.abs(total) * EARTH_RADIUS_NM * EARTH_RADIUS_NM / 2;
}

// Area of a latitude-longitude rectangle, in closed form. Exact, and so both the
// north-up answer and the oracle the polygon above is tested against.
export function boundsAreaNm2(bounds) {
    if (!bounds) return 0;
    const { south, west, north, east } = bounds;
    if (![south, west, north, east].every(Number.isFinite)) return 0;

    // Not wrapped into (-180, 180]: a bounds spanning the whole world is 360° wide,
    // which wrapping would collapse to nothing.
    const width = Math.min(Math.abs(east - west), 360) * DEG;
    const height = Math.abs(Math.sin(north * DEG) - Math.sin(south * DEG));

    return EARTH_RADIUS_NM * EARTH_RADIUS_NM * width * height;
}

// A distance for the readout. Values below ten carry a decimal, because a map zoomed
// in far enough to measure in hundreds of meters would otherwise read "0".
export function formatDistanceNm(nm, unit) {
    if (!Number.isFinite(nm) || nm < 0) return '';
    const value = nm * unitScale(unit);
    return `${ladder(value)} ${distanceUnitLabel(unit)}`;
}

// An area for the readout, in the square of the selected distance unit. Large values
// go to a suffix rather than to more digits: the exact number of square miles in a
// continent is not information, and a row of digits that changes width on every
// frame of a pan is a distraction.
export function formatAreaNm2(nm2, unit) {
    if (!Number.isFinite(nm2) || nm2 < 0) return '';
    const scale = unitScale(unit);
    const value = nm2 * scale * scale;
    const label = `${distanceUnitLabel(unit)}²`;

    if (value >= 1e6) return `${significant(value / 1e6)}M ${label}`;
    if (value >= 1e4) return `${significant(value / 1e3)}k ${label}`;
    return `${ladder(value)} ${label}`;
}

// Below ten, one decimal; above it, whole units with thousands grouping.
function ladder(value) {
    return value < 10
        ? (Math.round(value * 10) / 10).toFixed(1)
        : Math.round(value).toLocaleString();
}

// Three significant digits, so a suffixed value keeps a constant width as it grows:
// 1.85, 12.4, 124.
function significant(value) {
    if (value >= 100) return String(Math.round(value));
    if (value >= 10) return (Math.round(value * 10) / 10).toFixed(1);
    return (Math.round(value * 100) / 100).toFixed(2);
}

// Guards every entry point, because an unprojected corner above the horizon comes
// back as a non-finite pair rather than as an error.
function isCoordinate(point) {
    return !!point && Number.isFinite(point.lat) && Number.isFinite(point.lon);
}
