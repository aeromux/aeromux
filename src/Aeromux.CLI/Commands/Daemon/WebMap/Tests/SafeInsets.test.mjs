// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the Sky View safe-area insets. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { computeInsets, MOBILE_TOP_BAND_PX } from '../Services/SafeInsets.js';

test('with no panel measured there are no insets', () => {
    assert.deepEqual(computeInsets({ mobile: false, panelRect: null, viewportHeight: 900 }), {});
    assert.deepEqual(computeInsets({ mobile: true, panelRect: null, viewportHeight: 900 }), {});
});

test('desktop insets the left edge by the panel right edge', () => {
    const insets = computeInsets({
        mobile: false,
        panelRect: { top: 16, right: 436, bottom: 868 },
        viewportHeight: 900
    });
    assert.deepEqual(insets, { left: 436 });
});

test('mobile insets the bottom by how much of the viewport the sheet covers', () => {
    const insets = computeInsets({
        mobile: true,
        panelRect: { top: 600, right: 390, bottom: 900 },
        viewportHeight: 900
    });
    assert.equal(insets.bottom, 300, 'sheet occupies the lower 300 px');
    assert.equal(insets.top, MOBILE_TOP_BAND_PX, 'control panel band reserved');
    assert.equal(insets.left, undefined, 'the sheet is full width, so no left inset');
});

test('a taller mobile sheet insets further, which is the selection case', () => {
    const asList = computeInsets({
        mobile: true, panelRect: { top: 700, right: 390 }, viewportHeight: 900
    });
    const asDetail = computeInsets({
        mobile: true, panelRect: { top: 450, right: 390 }, viewportHeight: 900
    });
    assert.ok(asDetail.bottom > asList.bottom, 'growing the sheet lifts the horizon further');
    assert.equal(asDetail.bottom, 450);
});

test('a sheet dragged past the viewport does not produce a negative inset', () => {
    const insets = computeInsets({
        mobile: true, panelRect: { top: 1200, right: 390 }, viewportHeight: 900
    });
    assert.equal(insets.bottom, 0, 'clamped at zero');
});

test('a panel measured off-screen left does not produce a negative inset', () => {
    const insets = computeInsets({
        mobile: false, panelRect: { top: 0, right: -50 }, viewportHeight: 900
    });
    assert.equal(insets.left, 0, 'clamped at zero');
});

test('the mobile top band is overridable', () => {
    const insets = computeInsets({
        mobile: true, panelRect: { top: 600, right: 390 }, viewportHeight: 900, topBandPx: 0
    });
    assert.equal(insets.top, 0);
});
