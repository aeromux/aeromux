// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for placing an aircraft at its real height on a tilted map. Run with
// `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    EARTH_CIRCUMFERENCE_M,
    TRANSFORM_TILE_SIZE,
    circumferenceAtLatitude,
    metersPerPixel,
    mercatorXFromLongitude,
    mercatorYFromLatitude,
    mercatorZFromAltitude,
    liftMeters,
    cameraAltitudeMeters,
    canPlace,
    projectWorld
} from '../Services/AltitudeProjection.js';

// MapLibre's default vertical field of view, in radians, which is what a custom
// layer is handed when nothing has changed it.
const DEFAULT_FOV = 0.6435011087932844;

// The latitude and tilt the figures below are quoted at. MAX_PITCH mirrors the map's
// own cap, so these describe the deepest tilt actually reachable.
const LAT = 47;
const MAX_PITCH = 45;

const FL350_M = 35000 * 0.3048;

const close = (actual, expected, tolerance, message) => {
    assert.ok(
        Math.abs(actual - expected) <= tolerance,
        `${message}: got ${actual}, expected ${expected} ± ${tolerance}`
    );
};

// ---------- ground scale ----------

// The trap this whole module exists to avoid. MapLibre's transform holds a 512-pixel
// tile whatever the source declares, so the familiar 256-tile constant of 156,543
// gives exactly twice the right answer, and an altitude drawn at half its height
// looks entirely plausible. If this test fails, every aircraft on the map is at the
// wrong height by a factor of two.
test('the transform tile size is 512, not the raster source 256', () => {
    assert.equal(TRANSFORM_TILE_SIZE, 512);
    close(EARTH_CIRCUMFERENCE_M / TRANSFORM_TILE_SIZE, 78184, 1, 'meters per pixel at zoom 0, equator');
});

test('meters per pixel halves with every zoom level', () => {
    close(metersPerPixel(0, 0), 78184, 1, 'equator, zoom 0');
    close(metersPerPixel(0, 1), 39092, 1, 'equator, zoom 1');
    close(metersPerPixel(LAT, 8), 208.3, 0.2, '47 degrees north, zoom 8');
    close(metersPerPixel(LAT, 12), 13.02, 0.02, '47 degrees north, zoom 12');
});

test('the circumference shrinks with the cosine of the latitude', () => {
    close(circumferenceAtLatitude(0), EARTH_CIRCUMFERENCE_M, 1, 'equator');
    close(circumferenceAtLatitude(60), EARTH_CIRCUMFERENCE_M / 2, 1, '60 degrees');
});

// ---------- mercator ----------

test('mercator x and y put the origin at the middle of the world', () => {
    close(mercatorXFromLongitude(0), 0.5, 1e-12, 'longitude 0');
    close(mercatorXFromLongitude(180), 1, 1e-12, 'longitude 180');
    close(mercatorYFromLatitude(0), 0.5, 1e-12, 'latitude 0');
    // The north edge of the projection, where the map runs out.
    close(mercatorYFromLatitude(85.051129), 0, 1e-6, 'the mercator limit');
});

// Altitude is measured against the circumference at its own latitude, so the same
// height is a larger mercator z the further north it is. Getting this wrong tilts
// the error with latitude, which would look like a receiver-specific bug.
test('a meter is a larger mercator z at higher latitude', () => {
    close(mercatorZFromAltitude(1, 0), 1 / EARTH_CIRCUMFERENCE_M, 1e-18, 'equator');
    close(mercatorZFromAltitude(1, 60), 2 / EARTH_CIRCUMFERENCE_M, 1e-18, '60 degrees');
    close(mercatorZFromAltitude(0, LAT), 0, 0, 'no altitude, no lift');
});

// ---------- lift ----------

test('lift converts feet to meters and applies the scale', () => {
    close(liftMeters(35000, 1), FL350_M, 1e-9, 'FL350 at 1x');
    close(liftMeters(35000, 5), FL350_M * 5, 1e-6, 'FL350 at 5x');
    close(liftMeters(0, 5), 0, 0, 'on the ground stays on the ground');
});

// Below-MSL readings are real and the icon layer already clamps them. A stalk
// pointing into the ground would be a mark that means nothing.
test('lift never goes below the ground', () => {
    close(liftMeters(-500, 1), 0, 0, 'below sea level');
    close(liftMeters(null, 1), 0, 0, 'no altitude reported');
    close(liftMeters(35000, undefined), FL350_M, 1e-9, 'scale defaults to 1x');
});

// ---------- the ceiling ----------

// Three camera altitudes computed by hand for a typical window. They are the anchor
// for everything about the ceiling, so if the formula moves, this says so.
test('camera altitude matches the worked examples in the spec', () => {
    close(cameraAltitudeMeters(DEFAULT_FOV, 800, MAX_PITCH, metersPerPixel(LAT, 10)), 44184, 40, 'zoom 10');
    close(cameraAltitudeMeters(DEFAULT_FOV, 800, MAX_PITCH, metersPerPixel(LAT, 11)), 22092, 20, 'zoom 11');
    close(cameraAltitudeMeters(DEFAULT_FOV, 800, MAX_PITCH, metersPerPixel(LAT, 12)), 11046, 10, 'zoom 12');
});

// The camera is directly overhead at zero pitch and leans away as the map tilts, so
// the ceiling falls with the tilt as well as with the zoom.
test('camera altitude falls as the map tilts', () => {
    const level = cameraAltitudeMeters(DEFAULT_FOV, 800, 0, metersPerPixel(LAT, 12));
    const tilted = cameraAltitudeMeters(DEFAULT_FOV, 800, 60, metersPerPixel(LAT, 12));
    close(tilted, level / 2, 1, 'cosine of 60 degrees');
});

test('a taller window raises the ceiling', () => {
    const short = cameraAltitudeMeters(DEFAULT_FOV, 800, 60, metersPerPixel(LAT, 12));
    const tall = cameraAltitudeMeters(DEFAULT_FOV, 1600, 60, metersPerPixel(LAT, 12));
    close(tall, short * 2, 1, 'twice the canvas, twice the camera height');
});

// Where the ceiling actually bites, asserted rather than trusted. At the 45 degree
// cap cruise traffic clears zoom 12 by about 400 meters and not zoom 13, and each
// step of exaggeration costs a zoom level.
test('the ceiling bites one zoom level per step of exaggeration', () => {
    const ceiling = (zoom) => cameraAltitudeMeters(DEFAULT_FOV, 800, MAX_PITCH, metersPerPixel(LAT, zoom));

    assert.equal(canPlace(FL350_M, ceiling(12)), true, 'FL350 at 1x, zoom 12');
    assert.equal(canPlace(FL350_M, ceiling(13)), false, 'FL350 at 1x, zoom 13');
    assert.equal(canPlace(FL350_M * 2, ceiling(11)), true, 'FL350 at 2x, zoom 11');
    assert.equal(canPlace(FL350_M * 2, ceiling(12)), false, 'FL350 at 2x, zoom 12');
    assert.equal(canPlace(FL350_M * 5, ceiling(10)), false, 'FL350 at 5x, zoom 10');
    assert.equal(canPlace(FL350_M * 5, ceiling(9)), true, 'FL350 at 5x, zoom 9');
});

test('nothing can be placed without a camera above the ground', () => {
    assert.equal(canPlace(1000, 0), false, 'no camera height');
    assert.equal(canPlace(1000, NaN), false, 'no camera height to speak of');
    assert.equal(canPlace(NaN, 10000), false, 'no lift to speak of');
    assert.equal(canPlace(0, 10000), true, 'on the ground is always placeable');
});

// ---------- projection ----------

// The identity matrix leaves clip space alone, so this is really a test of the
// viewport mapping: the middle of clip space is the middle of the canvas, and y is
// flipped on the way to pixels.
test('the middle of clip space is the middle of the canvas', () => {
    const identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
    const middle = projectWorld(identity, [0, 0, 0], 800, 600);
    close(middle.x, 400, 1e-9, 'x');
    close(middle.y, 300, 1e-9, 'y');

    const corner = projectWorld(identity, [1, 1, 0], 800, 600);
    close(corner.x, 800, 1e-9, 'right edge');
    close(corner.y, 0, 1e-9, 'top edge, because screen y runs downward');
});

// Column-major, as WebGL matrices are. A row-major reading of this matrix would put
// the point somewhere else entirely, which is the mistake being guarded against.
test('the matrix is read column-major', () => {
    const translate = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0.5, -0.25, 0, 1];
    const point = projectWorld(translate, [0, 0, 0], 800, 600);
    close(point.x, ((0.5) + 1) / 2 * 800, 1e-9, 'translated in x');
    close(point.y, (1 - (-0.25)) / 2 * 600, 1e-9, 'translated in y');
});

// The perspective divide is what makes a lifted point behave differently from a
// ground one, since lifting also moves it toward the camera.
test('the perspective divide is applied', () => {
    // w comes from z: clip w = z, so the same x is half as far off center at twice
    // the depth.
    const perspective = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0];
    const near = projectWorld(perspective, [0.5, 0, 1], 800, 600);
    const far = projectWorld(perspective, [0.5, 0, 2], 800, 600);
    close(near.x - 400, 200, 1e-9, 'half a unit off center at depth 1');
    close(far.x - 400, 100, 1e-9, 'half as far off center at depth 2');
});

test('nothing behind the camera is projected', () => {
    const behind = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0];
    assert.equal(projectWorld(behind, [0.5, 0, -1], 800, 600), null, 'negative w');
    assert.equal(projectWorld(behind, [0.5, 0, 0], 800, 600), null, 'w of zero');
    assert.equal(projectWorld(null, [0, 0, 0], 800, 600), null, 'no matrix yet');
});
