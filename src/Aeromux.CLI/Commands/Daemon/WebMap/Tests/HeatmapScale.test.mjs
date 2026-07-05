// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the heatmap colour-scale helpers. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { colourT, legendTicks, payloadToFeatures, RDYLGN_STOPS } from '../Services/HeatmapScale.js';
import { formatCellSizeLabel, distanceUnitLabel } from '../Services/UnitConversion.js';

// -------------------- colourT --------------------

test('count of 1 maps to the green end (t = 0)', () => {
    assert.equal(colourT(1, 200), 0);
});

test('count at scaleMax maps to the red end (t = 1)', () => {
    assert.equal(colourT(200, 200), 1);
});

test('count above scaleMax clamps to 1', () => {
    assert.equal(colourT(500, 200), 1);
});

test('the log midpoint sqrt(scaleMax) sits near t = 0.5', () => {
    assert.ok(Math.abs(colourT(14, 200) - 0.5) < 0.02);
});

test('a skewed set spreads strictly across the palette (log), not collapsed', () => {
    const scaleMax = 300;
    const ts = [3, 8, 15, 40, 120, 300].map((c) => colourT(c, scaleMax));
    for (let i = 1; i < ts.length; i++) {
        assert.ok(ts[i] > ts[i - 1], `t should increase: ${ts[i - 1]} -> ${ts[i]}`);
    }
    assert.ok(ts[0] > 0, 'low end should not collapse to pure green');
    assert.equal(ts[ts.length - 1], 1);
});

test('degenerate scaleMax <= 1 renders green (avoids log divide-by-zero)', () => {
    assert.equal(colourT(5, 1), 0);
});

// -------------------- legendTicks --------------------

test('legend ticks are [1, round(sqrt(scaleMax)), scaleMax]', () => {
    assert.deepEqual(legendTicks(200), [1, 14, 200]);
});

// -------------------- payloadToFeatures --------------------

test('payload (PascalCase) converts to closed polygon features with count + t', () => {
    const payload = {
        ScaleMax: 100,
        Cells: [
            { South: 47.3, West: 19.0, North: 47.383, East: 19.11, Count: 12 },
            { South: 47.0, West: 18.0, North: 47.083, East: 18.11, Count: 100 },
        ],
    };

    const features = payloadToFeatures(payload);

    assert.equal(features.length, 2);
    assert.equal(features[0].type, 'Feature');
    assert.equal(features[0].properties.count, 12);
    assert.ok(Math.abs(features[0].properties.t - colourT(12, 100)) < 1e-9);

    const ring = features[0].geometry.coordinates[0];
    assert.equal(ring.length, 5); // closed ring
    assert.deepEqual(ring[0], [19.0, 47.3]); // [lon, lat] order
    assert.deepEqual(ring[0], ring[4]);      // first == last

    assert.equal(features[1].properties.t, 1); // count == scaleMax → red
});

test('missing Cells yields an empty feature list', () => {
    assert.deepEqual(payloadToFeatures({ ScaleMax: 100 }), []);
});

// -------------------- palette --------------------

test('palette has seven RdYlGn stops from green to red', () => {
    assert.equal(RDYLGN_STOPS.length, 7);
    assert.equal(RDYLGN_STOPS[0][1], '#1a9850');
    assert.equal(RDYLGN_STOPS[6][1], '#d73027');
});

// -------------------- cell-size unit labels --------------------

test('cell size labels stay as nm in nautical miles', () => {
    assert.equal(formatCellSizeLabel(2, 'nm'), '2');
    assert.equal(formatCellSizeLabel(40, 'nm'), '40');
});

test('cell size labels convert to km/mi to one decimal, trailing .0 stripped', () => {
    assert.equal(formatCellSizeLabel(2, 'km'), '3.7');   // 2 × 1.852
    assert.equal(formatCellSizeLabel(20, 'km'), '37');   // 37.04 → 37
    assert.equal(formatCellSizeLabel(40, 'km'), '74.1');
    assert.equal(formatCellSizeLabel(2, 'mi'), '2.3');   // 2 × 1.15078
    assert.equal(formatCellSizeLabel(10, 'mi'), '11.5');
});

test('distanceUnitLabel maps the three units', () => {
    assert.equal(distanceUnitLabel('nm'), 'nm');
    assert.equal(distanceUnitLabel('km'), 'km');
    assert.equal(distanceUnitLabel('mi'), 'mi');
});
