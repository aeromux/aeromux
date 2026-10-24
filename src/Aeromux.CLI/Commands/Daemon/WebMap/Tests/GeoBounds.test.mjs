// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the server subscription box. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { subscriptionBounds } from '../Services/GeoBounds.js';

// The server's own containment rule, so the tests check what the box selects
// rather than only its numbers.
function contains(box, lat, lon) {
    if (lat < box.south || lat > box.north) return false;
    return box.west > box.east
        ? lon >= box.west || lon <= box.east
        : lon >= box.west && lon <= box.east;
}

const inRange = (box) =>
    [box.west, box.east].every(l => l >= -180 && l <= 180) &&
    [box.south, box.north].every(l => l >= -90 && l <= 90);

test('a box already in range is returned unchanged', () => {
    const box = { south: 46, west: 18, north: 48, east: 20 };
    assert.deepEqual(subscriptionBounds(box), box);
});

test('the exact edges of the world are kept', () => {
    assert.deepEqual(
        subscriptionBounds({ south: -85, west: -180, north: 85, east: 180 }),
        { south: -85, west: -180, north: 85, east: 180 });
});

test('an east edge past 180 becomes a box across the meridian', () => {
    const box = subscriptionBounds({ south: -50, west: 170, north: -30, east: 190 });
    assert.deepEqual(box, { south: -50, west: 170, north: -30, east: -170 });
    assert.ok(contains(box, -40, 175));
    assert.ok(contains(box, -40, -175));
    assert.ok(!contains(box, -40, 0));
});

test('a west edge past -180 becomes a box across the meridian', () => {
    const box = subscriptionBounds({ south: -50, west: -190, north: -30, east: -170 });
    assert.deepEqual(box, { south: -50, west: 170, north: -30, east: -170 });
});

test('a view on a neighboring world copy folds back onto the real one', () => {
    const box = subscriptionBounds({ south: 46, west: 378, north: 48, east: 380 });
    assert.equal(box.west, 18);
    assert.equal(box.east, 20);
});

test('a view centered near Kecskemet at zoom 2 on a wide screen stays usable', () => {
    // Half of 337.5 degrees either side of 19.7 E, as a 1920 pixel map at zoom 2 reports.
    const box = subscriptionBounds({ south: -70, west: -149.05, north: 75, east: 188.45 });
    assert.ok(inRange(box));
    assert.ok(contains(box, 47, 19.7), 'the receiver itself');
    assert.ok(contains(box, 40, 175), 'east of the old 180 limit');
    assert.ok(contains(box, 40, -172), 'the wrapped-over strip');
});

test('a view 360 degrees or wider becomes the whole world', () => {
    for (const [west, east] of [[-180, 180], [-200, 160], [-400, 400]]) {
        assert.deepEqual(
            subscriptionBounds({ south: -80, west, north: 80, east }),
            { south: -80, west: -180, north: 80, east: 180 },
            `${west}..${east}`);
    }
});

test('latitudes are clamped into range', () => {
    const box = subscriptionBounds({ south: -95, west: 0, north: 95, east: 10 });
    assert.equal(box.south, -90);
    assert.equal(box.north, 90);
});

test('missing or non-finite input yields null', () => {
    assert.equal(subscriptionBounds(null), null);
    assert.equal(subscriptionBounds(undefined), null);
    assert.equal(subscriptionBounds({ south: NaN, west: 0, north: 1, east: 1 }), null);
    assert.equal(subscriptionBounds({ south: 0, west: 0, north: 1, east: Infinity }), null);
});

test('every output stays in range across a sweep of pans', () => {
    for (let center = -720; center <= 720; center += 7.5) {
        for (const halfWidth of [0.5, 10, 90, 170, 179.9]) {
            const box = subscriptionBounds({
                south: -10, west: center - halfWidth, north: 10, east: center + halfWidth
            });
            assert.ok(inRange(box), `center ${center}, half width ${halfWidth}`);
            // The center of the view, folded into range, is always selected.
            const folded = ((center + 180) % 360 + 360) % 360 - 180;
            assert.ok(contains(box, 0, folded), `center ${center} selected`);
        }
    }
});
