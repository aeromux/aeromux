// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the resizable mobile bottom-sheet geometry helpers.
// Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    clampSheetPx,
    pxToFraction,
    normalizeStoredFraction,
    MIN_SHEET_PX,
    MAX_SHEET_FRACTION,
    MIN_SHEET_FRACTION,
} from '../Services/SheetHeight.js';

// -------------------- clampSheetPx --------------------

test('mid-range height passes through unchanged', () => {
    assert.equal(clampSheetPx(400, 1000), 400);
});

test('clamps up to the pixel floor', () => {
    assert.equal(clampSheetPx(50, 1000), MIN_SHEET_PX);
});

test('clamps down to the fractional ceiling', () => {
    assert.equal(clampSheetPx(990, 1000), 1000 * MAX_SHEET_FRACTION);
});

test('on a very short viewport the ceiling wins over the pixel floor', () => {
    // 150px viewport: floor 160px would exceed the 85% ceiling (127.5px),
    // so the result must never exceed the ceiling.
    const maxPx = 150 * MAX_SHEET_FRACTION;
    assert.equal(clampSheetPx(160, 150), maxPx);
    assert.equal(clampSheetPx(10, 150), maxPx);
});

// -------------------- pxToFraction --------------------

test('converts pixels to a viewport fraction', () => {
    assert.equal(pxToFraction(500, 1000), 0.5);
});

test('fraction is clamped to the persisted range', () => {
    assert.equal(pxToFraction(950, 1000), MAX_SHEET_FRACTION);
    assert.equal(pxToFraction(10, 1000), MIN_SHEET_FRACTION);
});

test('non-positive viewport falls back to the fractional floor', () => {
    assert.equal(pxToFraction(500, 0), MIN_SHEET_FRACTION);
});

test('clamp/convert round-trips within range', () => {
    const viewport = 800;
    const px = clampSheetPx(360, viewport);
    const fraction = pxToFraction(px, viewport);
    assert.equal(fraction, px / viewport);
});

// -------------------- normalizeStoredFraction --------------------

test('accepts an in-range stored fraction', () => {
    assert.equal(normalizeStoredFraction(0.5), 0.5);
});

test('clamps an out-of-range stored fraction', () => {
    assert.equal(normalizeStoredFraction(0.99), MAX_SHEET_FRACTION);
    assert.equal(normalizeStoredFraction(0.01), MIN_SHEET_FRACTION);
});

test('rejects non-numbers and non-finite values', () => {
    assert.equal(normalizeStoredFraction('0.5'), null);
    assert.equal(normalizeStoredFraction(null), null);
    assert.equal(normalizeStoredFraction(undefined), null);
    assert.equal(normalizeStoredFraction(NaN), null);
    assert.equal(normalizeStoredFraction(Infinity), null);
});

test('rejects non-positive values', () => {
    assert.equal(normalizeStoredFraction(0), null);
    assert.equal(normalizeStoredFraction(-0.5), null);
});
