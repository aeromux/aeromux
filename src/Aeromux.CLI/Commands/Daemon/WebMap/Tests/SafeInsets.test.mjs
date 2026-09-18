// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the view safe-area insets and the readout's width budget. Run with
// `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { computeInsets, MOBILE_TOP_BAND_PX, READOUT_MARGIN_PX } from '../Services/SafeInsets.js';

test('with no panel measured there are no insets', () => {
    assert.deepEqual(computeInsets({ mobile: false, panelRect: null, viewportHeight: 900 }), {});
    assert.deepEqual(computeInsets({ mobile: true, panelRect: null, viewportHeight: 900 }), {});
});

test('desktop insets the left edge by the panel right edge', () => {
    const insets = computeInsets({
        mobile: false,
        panelRect: { top: 16, right: 436, bottom: 868 },
        controlRect: { left: 1304 },
        viewportWidth: 1600,
        viewportHeight: 900
    });
    assert.equal(insets.left, 436);
    assert.equal(insets.bottom, undefined, 'nothing covers the bottom on a desktop');
});

// The readout sits between the aircraft list and the control panel, and the two
// reported cases are a tablet in portrait and a phone in landscape, where what is
// left between them is a fraction of what the row would take unbounded.
test('the readout is bounded by the gap between the panels', () => {
    const budget = (viewportWidth) => computeInsets({
        mobile: false,
        panelRect: { top: 16, right: 436 },
        controlRect: { left: viewportWidth - 296 },
        viewportWidth,
        viewportHeight: 900
    }).readoutMaxWidth;

    assert.equal(budget(1600), 836, 'desktop');
    assert.equal(budget(1032), 268, 'tablet, portrait');
    assert.equal(budget(956), 192, 'phone, landscape');
});

test('a control panel wider than the space left produces no width, never a negative one', () => {
    const insets = computeInsets({
        mobile: false,
        panelRect: { top: 16, right: 436 },
        controlRect: { left: 300 },
        viewportWidth: 600,
        viewportHeight: 900
    });
    assert.equal(insets.readoutMaxWidth, 0, 'clamped at zero, so the row withdraws');
});

// Mobile stacks the panels above and below instead of flanking, so the row has the
// whole width to itself.
test('mobile gives the readout the full width less its margins', () => {
    const insets = computeInsets({
        mobile: true,
        panelRect: { top: 600, right: 390 },
        viewportWidth: 390,
        viewportHeight: 900
    });
    assert.equal(insets.readoutMaxWidth, 390 - 2 * READOUT_MARGIN_PX);
});

// The control panel is measured from the live layout, so it can be missing on the
// first pass. Falling back to the screen edge is the behavior the row had before it
// was bounded at all, which is better than reporting nothing fits.
test('an unmeasured control panel bounds the readout by the screen edge', () => {
    const insets = computeInsets({
        mobile: false,
        panelRect: { top: 16, right: 436 },
        viewportWidth: 1600,
        viewportHeight: 900
    });
    assert.equal(insets.readoutMaxWidth, 1600 - 436 - 2 * READOUT_MARGIN_PX);
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
        mobile: false, panelRect: { top: 0, right: -50 }, viewportWidth: 1600, viewportHeight: 900
    });
    assert.equal(insets.left, 0, 'clamped at zero');
});

test('the mobile top band is overridable', () => {
    const insets = computeInsets({
        mobile: true, panelRect: { top: 600, right: 390 }, viewportHeight: 900, topBandPx: 0
    });
    assert.equal(insets.top, 0);
});
