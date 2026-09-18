// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the Map View readout's measurements. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    distanceNm,
    bearingDeg,
    polygonAreaNm2,
    boundsAreaNm2,
    formatDistanceNm,
    formatAreaNm2,
    EARTH_RADIUS_NM
} from '../Services/ViewportMetrics.js';

// Square nautical miles to square kilometres, for the anchors below, which are
// quoted in the unit the external references use.
const NM2_TO_KM2 = 1.852 * 1.852;

// Corners of a latitude-longitude box in screen order, north-up.
function box(south, west, north, east) {
    return [
        { lat: south, lon: west },
        { lat: south, lon: east },
        { lat: north, lon: east },
        { lat: north, lon: west }
    ];
}

const close = (actual, expected, tolerance, message) => {
    assert.ok(
        Math.abs(actual - expected) <= tolerance,
        `${message}: got ${actual}, expected ${expected} ± ${tolerance}`
    );
};

// ---------- area ----------

// The external anchor. A one-degree box on the equator is a little over 12 300 km²
// on a sphere of this radius, which is the figure quoted for a degree square at the
// equator. A transposed sine or a radius in the wrong unit misses it by orders of
// magnitude, which is the failure this catches.
test('a one-degree box at the equator is about 12 360 km²', () => {
    const km2 = boundsAreaNm2({ south: -0.5, west: -0.5, north: 0.5, east: 0.5 }) * NM2_TO_KM2;
    close(km2, 12363, 20, 'equatorial degree square');
});

// Area between two parallels goes with the difference of their sines, so the same
// box at 60° covers half the ground. Independent of the radius, so it checks the
// shape of the formula rather than its scale.
test('the same box at 60° north covers half the ground', () => {
    const equator = boundsAreaNm2({ south: -0.5, west: -0.5, north: 0.5, east: 0.5 });
    const high = boundsAreaNm2({ south: 59.5, west: -0.5, north: 60.5, east: 0.5 });
    close(high / equator, 0.5, 0.001, 'ratio at 60° north');
});

test('the whole world is the surface of the sphere', () => {
    const whole = boundsAreaNm2({ south: -90, west: -180, north: 90, east: 180 });
    const sphere = 4 * Math.PI * EARTH_RADIUS_NM * EARTH_RADIUS_NM;
    close(whole / sphere, 1, 1e-9, 'full-world bounds');
});

// North-up is the only case where the polygon and the closed form should agree
// exactly: a screen rectangle in Web Mercator is a latitude-longitude rectangle.
test('the corner polygon agrees with the closed form north-up', () => {
    const bounds = { south: 47.2, west: 18.7, north: 47.8, east: 19.4 };
    const polygon = polygonAreaNm2(box(bounds.south, bounds.west, bounds.north, bounds.east));
    close(polygon / boundsAreaNm2(bounds), 1, 1e-9, 'polygon against closed form');
});

test('winding direction does not change the area', () => {
    const corners = box(47.2, 18.7, 47.8, 19.4);
    close(polygonAreaNm2([...corners].reverse()) / polygonAreaNm2(corners), 1, 1e-9, 'reversed winding');
});

// The antimeridian case: the same viewport, once at longitude 0 and once straddling
// ±180. Without the per-edge wrap the second one would come out as most of the world.
test('a viewport straddling the antimeridian is not world-sized', () => {
    const home = polygonAreaNm2(box(-0.5, -0.5, 0.5, 0.5));
    const straddling = polygonAreaNm2(box(-0.5, 179.5, 0.5, -179.5));
    close(straddling / home, 1, 1e-9, 'antimeridian straddle');
});

test('a corner above the horizon yields no area rather than a wrong one', () => {
    const corners = box(47.2, 18.7, 47.8, 19.4);
    corners[2] = { lat: NaN, lon: NaN };
    assert.equal(polygonAreaNm2(corners), 0);
    assert.equal(polygonAreaNm2(null), 0);
    assert.equal(boundsAreaNm2({ south: 0, west: 0, north: NaN, east: 1 }), 0);
});

// ---------- distance and bearing ----------

// A degree of latitude is 60 nautical miles by definition, give or take the
// difference between the sphere this measures on and the real Earth.
test('a degree of latitude spans about 60 nm', () => {
    close(distanceNm({ lat: 47, lon: 19 }, { lat: 48, lon: 19 }), 60, 0.3, 'one degree of latitude');
});

test('a degree of longitude shortens with the cosine of latitude', () => {
    const equator = distanceNm({ lat: 0, lon: 0 }, { lat: 0, lon: 1 });
    const sixty = distanceNm({ lat: 60, lon: 0 }, { lat: 60, lon: 1 });
    close(sixty / equator, 0.5, 0.001, 'longitude at 60° north');
});

test('distance guards a coordinate it cannot use', () => {
    assert.equal(distanceNm({ lat: 47, lon: 19 }, { lat: NaN, lon: 19 }), 0);
    assert.equal(distanceNm(null, { lat: 47, lon: 19 }), 0);
});

test('bearings point the way they are spoken', () => {
    const home = { lat: 47.5, lon: 19 };
    close(bearingDeg(home, { lat: 48.5, lon: 19 }), 0, 0.001, 'due north');
    close(bearingDeg(home, { lat: 47.5, lon: 20 }), 90, 0.5, 'due east');
    close(bearingDeg(home, { lat: 46.5, lon: 19 }), 180, 0.001, 'due south');
    assert.equal(bearingDeg(home, null), 0);
});

// ---------- formatting ----------

test('distances carry a decimal below ten and none above it', () => {
    assert.equal(formatDistanceNm(0.82, 'nm'), '0.8 nm');
    assert.equal(formatDistanceNm(4.24, 'nm'), '4.2 nm');
    assert.equal(formatDistanceNm(46.4, 'nm'), '46 nm');
    assert.equal(formatDistanceNm(9.96, 'nm'), '10.0 nm');
});

test('distances follow the selected unit', () => {
    assert.equal(formatDistanceNm(100, 'nm'), '100 nm');
    assert.equal(formatDistanceNm(100, 'km'), '185 km');
    assert.equal(formatDistanceNm(100, 'mi'), '115 mi');
});

test('a distance that is not a distance formats to nothing', () => {
    assert.equal(formatDistanceNm(NaN, 'nm'), '');
    assert.equal(formatDistanceNm(-1, 'nm'), '');
});

test('areas step from decimals through grouping to suffixes', () => {
    assert.equal(formatAreaNm2(4.24, 'nm'), '4.2 nm²');
    assert.equal(formatAreaNm2(1840, 'nm'), '1,840 nm²');
    assert.equal(formatAreaNm2(12400, 'nm'), '12.4k nm²');
    assert.equal(formatAreaNm2(124000, 'nm'), '124k nm²');
    assert.equal(formatAreaNm2(1850000, 'nm'), '1.85M nm²');
});

// The square of the distance factor, not the factor: 100 nm² is 343 km², not 185.
test('areas convert by the square of the unit', () => {
    assert.equal(formatAreaNm2(1000, 'nm'), '1,000 nm²');
    assert.equal(formatAreaNm2(1000, 'km'), '3,430 km²');
    assert.equal(formatAreaNm2(1000, 'mi'), '1,324 mi²');
});

test('an area that is not an area formats to nothing', () => {
    assert.equal(formatAreaNm2(NaN, 'nm'), '');
    assert.equal(formatAreaNm2(-1, 'km'), '');
});
