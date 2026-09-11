// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the receiver-centric sky geometry. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { receiverBox } from '../Services/SkyViewGeometry.js';
import { nmToKm } from '../Services/UnitConversion.js';

// -------------------- receiverBox --------------------

test('a normal box is non-inverted and circumscribes the range circle', () => {
    const box = receiverBox(50, 8, nmToKm(150));
    assert.ok(box.west < box.east, 'west of east');
    assert.ok(box.south < box.north, 'south of north');

    // Poleward sizing makes the span wider than naive cos(lat) sizing would.
    const naiveSpan = 2 * nmToKm(150) / (111.32 * Math.cos(50 * Math.PI / 180));
    assert.ok(box.east - box.west > naiveSpan, 'poleward edge widens the box');
});

test('a receiver near the antimeridian never yields an inverted box', () => {
    for (const lon of [179, -179, 180, -180]) {
        const box = receiverBox(-18, lon, nmToKm(150));
        assert.ok(box.west <= box.east, `longitude ${lon} produced an inverted box`);
        assert.equal(box.west, -180, `longitude ${lon} west`);
        assert.equal(box.east, 180, `longitude ${lon} east`);
    }
});

test('a pole inside the box falls back to full-width bounds', () => {
    const box = receiverBox(84, 20, nmToKm(300));
    assert.equal(box.west, -180);
    assert.equal(box.east, 180);
    assert.ok(box.north <= 85 && box.south >= -85, 'latitude clamped');
});

test('high latitude stays non-inverted', () => {
    const box = receiverBox(70, 20, nmToKm(150));
    assert.ok(box.west <= box.east);
    assert.ok(box.west >= -180 && box.east <= 180, 'within longitude range');
});
