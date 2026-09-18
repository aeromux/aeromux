// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the shared readout helpers. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fitChips, formatBearing } from '../Services/HudDom.js';

// Measured widths of the Map View's chips at the readout's type scale, least
// valuable first, which is the order they are given up in.
const MAP_ROW = [
    { key: 'range', width: 101 },
    { key: 'area', width: 109 },
    { key: 'center', width: 116 },
    { key: 'heading', width: 69 },
    { key: 'count', width: 93 },
    { key: 'span', width: 87 }
];

// The row's own spacing: --spacing-md between chips, and the same each side.
const METRICS = { gap: 16, padding: 32 };

const fit = (budget, chips = MAP_ROW) => [...fitChips(budget, chips, METRICS)];

// ---------- fitting ----------

test('a desktop row holds every chip', () => {
    assert.deepEqual(
        fit(836).sort(),
        ['area', 'center', 'count', 'heading', 'range', 'span'].sort()
    );
});

// The two reported cases: a tablet in portrait with 268px between the panels, and a
// phone in landscape with 192px.
test('a tablet in portrait keeps the span and the count', () => {
    assert.deepEqual(fit(268), ['span', 'count']);
});

test('a phone in landscape keeps the span alone', () => {
    assert.deepEqual(fit(192), ['span']);
});

// A phone in portrait stacks the panels instead of flanking, so the row has the whole
// width and can afford one more.
test('a phone in portrait keeps the heading as well', () => {
    assert.deepEqual(fit(358), ['span', 'count', 'heading']);
});

test('chips are given up in order, never skipped over', () => {
    // Enough for the span, the count and the heading, but not the center. The center
    // is not passed over in favor of something cheaper further down the row.
    const kept = fit(320);
    assert.ok(kept.includes('heading'), 'the heading fits');
    assert.ok(!kept.includes('center'), 'the center does not');
    assert.ok(!kept.includes('area') && !kept.includes('range'), 'nor anything below it');
});

test('a row that cannot hold even the span shows nothing', () => {
    assert.deepEqual(fit(118), [], 'one pixel short of the span and its padding');
    assert.deepEqual(fit(0), []);
});

test('the gap is only charged between chips', () => {
    // 32 padding + 87 span = 119 exactly, with no gap to pay for a single chip.
    assert.deepEqual(fit(119), ['span']);
    // 119 + 16 gap + 93 count = 228 for two.
    assert.deepEqual(fit(227), ['span']);
    assert.deepEqual(fit(228), ['span', 'count']);
});

test('an unmeasured chip is passed over rather than counted as free', () => {
    const chips = [{ key: 'a', width: 50 }, { key: 'b' }, { key: 'c', width: 50 }];
    const kept = [...fitChips(200, chips, METRICS)];
    assert.deepEqual(kept, ['c', 'a']);
});

test('nothing to fit is not an error', () => {
    assert.deepEqual([...fitChips(500, [], METRICS)], []);
    assert.deepEqual([...fitChips(500, null, METRICS)], []);
    assert.deepEqual([...fitChips(NaN, MAP_ROW, METRICS)], []);
});

// ---------- bearings ----------

test('bearings are three digits and zero-padded', () => {
    assert.equal(formatBearing(0), '000°');
    assert.equal(formatBearing(45), '045°');
    assert.equal(formatBearing(359.7), '360°');
    assert.equal(formatBearing(-45), '315°', 'a negative map bearing wraps');
});
