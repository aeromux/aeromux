// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for AircraftIconResolver.resolveShape() and the module's
// load-time integrity assertion. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    resolveShape,
    TYPE_DESIGNATOR,
    TYPE_DESCRIPTION,
    TYPE_DESCRIPTION_FIRSTCHAR,
    CATEGORY,
} from '../Map/AircraftIconResolver.js';
import { SHAPES } from '../Map/AircraftShapes.js';

// -------------------- Layer 1: type designator --------------------

test('designator hit', () => {
    assert.deepEqual(
        resolveShape('A320', null, null, null),
        { shapeName: 'a320', scale: 1, resolvedVia: 'designator' }
    );
});

test('case-insensitive designator', () => {
    assert.deepEqual(
        resolveShape('a320', null, null, null),
        resolveShape('A320', null, null, null)
    );
});

test('B77W resolves to heavy_2e via designator', () => {
    assert.deepEqual(
        resolveShape('B77W', null, null, null),
        { shapeName: 'heavy_2e', scale: 1.04, resolvedVia: 'designator' }
    );
});

// -------------------- Layer 2: description + WTC --------------------

test('WTC-suffixed composite hit (heavy twinjet, L2J + H)', () => {
    assert.deepEqual(
        resolveShape(null, 'L2J', 'H', null),
        { shapeName: 'heavy_2e', scale: 0.96, resolvedVia: 'description-3-wtc' }
    );
});

test('WTC-suffixed composite hit (medium twinjet, L2J + M)', () => {
    assert.deepEqual(
        resolveShape(null, 'L2J', 'M', null),
        { shapeName: 'airliner', scale: 1, resolvedVia: 'description-3-wtc' }
    );
});

test('WTC-suffixed branch beats bare branch (L4T + H prefers 1.07 over bare 0.96)', () => {
    const r = resolveShape(null, 'L4T', 'H', null);
    assert.equal(r.shapeName, 'c130');
    assert.equal(r.scale, 1.07);
    assert.equal(r.resolvedVia, 'description-3-wtc');
});

// -------------------- Layer 2: bare description --------------------

test('bare description hit (L4T, no WTC)', () => {
    assert.deepEqual(
        resolveShape(null, 'L4T', null, null),
        { shapeName: 'c130', scale: 0.96, resolvedVia: 'description-3' }
    );
});

test('bare description fall-through when composite misses (L4T + J)', () => {
    // No L4T-J entry; falls through to bare L4T.
    assert.deepEqual(
        resolveShape(null, 'L4T', 'J', null),
        { shapeName: 'c130', scale: 0.96, resolvedVia: 'description-3' }
    );
});

test('unknown WTC value still allows bare fall-back (L4T + "X")', () => {
    assert.deepEqual(
        resolveShape(null, 'L4T', 'X', null),
        { shapeName: 'c130', scale: 0.96, resolvedVia: 'description-3' }
    );
});

// -------------------- Synthesised bare entries --------------------

test('synthesised bare fall-back for WTC-only class (L2J without WTC)', () => {
    // tar1090 has no bare L2J; we synthesise it as airliner.
    assert.deepEqual(
        resolveShape(null, 'L2J', null, null),
        { shapeName: 'airliner', scale: 1, resolvedVia: 'description-3' }
    );
});

test('synthesised bare fall-back for L3J', () => {
    assert.deepEqual(
        resolveShape(null, 'L3J', null, null),
        { shapeName: 'md11', scale: 1, resolvedVia: 'description-3' }
    );
});

test('WTC composite still preferred over synthesised bare (L2J + H -> heavy_2e, not airliner)', () => {
    const r = resolveShape(null, 'L2J', 'H', null);
    assert.equal(r.shapeName, 'heavy_2e');
    assert.notEqual(r.shapeName, 'airliner');
});

// -------------------- Layer 3: first-char description --------------------

test('first-char description hit (H1T -> helicopter)', () => {
    assert.deepEqual(
        resolveShape(null, 'H1T', null, null),
        { shapeName: 'helicopter', scale: 1, resolvedVia: 'description-1' }
    );
});

// -------------------- Layer 4: emitter category --------------------

test('category hit (Heavy -> heavy_2e)', () => {
    assert.deepEqual(
        resolveShape(null, null, null, 'Heavy'),
        { shapeName: 'heavy_2e', scale: 0.92, resolvedVia: 'category' }
    );
});

test('category hit (Light -> cessna)', () => {
    assert.deepEqual(
        resolveShape(null, null, null, 'Light'),
        { shapeName: 'cessna', scale: 1, resolvedVia: 'category' }
    );
});

test('category miss with non-mapped enum value (NoInformation -> unknown)', () => {
    assert.deepEqual(
        resolveShape(null, null, null, 'NoInformation'),
        { shapeName: 'unknown', scale: 1, resolvedVia: 'fallback' }
    );
});

// -------------------- Layer 5: fallback --------------------

test('all-null inputs -> unknown', () => {
    assert.deepEqual(
        resolveShape(null, null, null, null),
        { shapeName: 'unknown', scale: 1, resolvedVia: 'fallback' }
    );
});

// -------------------- Edge cases & contracts --------------------

test('empty-string TypeCode falls through to category', () => {
    const r = resolveShape('', null, null, 'Heavy');
    assert.equal(r.resolvedVia, 'category');
    assert.equal(r.shapeName, 'heavy_2e');
});

test('WTC alone is ignored without a class (category branch decides)', () => {
    const r = resolveShape(null, null, 'H', 'Heavy');
    assert.equal(r.resolvedVia, 'category');
    assert.equal(r.shapeName, 'heavy_2e');
});

test('pre-suffixed TypeIcaoClass is rejected by layer 2 (L2J-M does not hit)', () => {
    // The C# layer guarantees bare class strings. If a future change
    // ever forwards "L2J-M" verbatim, the length guard skips layer 2
    // entirely; with no category, the resolver lands at unknown.
    const r = resolveShape(null, 'L2J-M', 'M', null);
    assert.notEqual(r.resolvedVia, 'description-3-wtc');
    assert.notEqual(r.resolvedVia, 'description-3');
    assert.equal(r.shapeName, 'unknown');
});

// -------------------- Table referential integrity --------------------

test('every shape referenced from any table exists in SHAPES', () => {
    for (const [name, table] of [
        ['TYPE_DESIGNATOR',            TYPE_DESIGNATOR],
        ['TYPE_DESCRIPTION',           TYPE_DESCRIPTION],
        ['TYPE_DESCRIPTION_FIRSTCHAR', TYPE_DESCRIPTION_FIRSTCHAR],
        ['CATEGORY',                   CATEGORY],
    ]) {
        for (const [key, value] of Object.entries(table)) {
            assert.ok(
                value[0] in SHAPES,
                `${name}['${key}'] references missing shape '${value[0]}'`
            );
        }
    }
});

test('SHAPES.unknown exists', () => {
    assert.ok('unknown' in SHAPES, 'SHAPES.unknown is required as the universal fallback');
});
