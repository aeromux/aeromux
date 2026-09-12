// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the aircraft-list windowing arithmetic. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { visibleWindow, OVERSCAN_ROWS, UNMEASURED_BATCH } from '../Services/ListWindow.js';

const ROW = 48;
const PANEL = 900;

test('an empty list yields an empty window rather than dividing by zero', () => {
    assert.deepEqual(
        visibleWindow({ total: 0, rowHeight: ROW, viewportHeight: PANEL, scrollTop: 0 }),
        { start: 0, end: 0, padTop: 0, padBottom: 0 }
    );
});

test('a row height not yet measured renders a first batch, not nothing', () => {
    // The height comes from measuring a rendered row. Returning an empty window
    // here would mean nothing renders, so nothing can be measured, so the list
    // stays empty for ever.
    const w = visibleWindow({ total: 1500, rowHeight: 0, viewportHeight: PANEL, scrollTop: 0 });
    assert.equal(w.start, 0);
    assert.equal(w.end, UNMEASURED_BATCH);
    assert.ok(w.end > 0, 'something is rendered, so a height can be taken from it');
});

test('a short list is returned whole, with no padding', () => {
    const w = visibleWindow({ total: 5, rowHeight: ROW, viewportHeight: PANEL, scrollTop: 0 });
    assert.equal(w.start, 0);
    assert.equal(w.end, 5);
    assert.equal(w.padTop, 0);
    assert.equal(w.padBottom, 0);
});

test('at the top there is no padding above', () => {
    const w = visibleWindow({ total: 1000, rowHeight: ROW, viewportHeight: PANEL, scrollTop: 0 });
    assert.equal(w.start, 0);
    assert.equal(w.padTop, 0);
    assert.ok(w.padBottom > 0);
});

test('at the bottom there is no padding below', () => {
    const total = 1000;
    const w = visibleWindow({
        total, rowHeight: ROW, viewportHeight: PANEL, scrollTop: total * ROW
    });
    assert.equal(w.end, total);
    assert.equal(w.padBottom, 0);
});

test('the scrollbar keeps its full length at every scroll position', () => {
    // This is what stops the list jumping under the user as the window moves.
    for (const total of [1, 30, 200, 1500]) {
        for (const scrollTop of [0, 137, 5000, total * ROW, total * ROW * 2]) {
            const w = visibleWindow({ total, rowHeight: ROW, viewportHeight: PANEL, scrollTop });
            const rendered = (w.end - w.start) * ROW;
            assert.equal(
                w.padTop + rendered + w.padBottom, total * ROW,
                `total ${total} at ${scrollTop}`
            );
        }
    }
});

test('the window never runs outside the list', () => {
    for (const total of [1, 7, 200, 1500]) {
        for (const scrollTop of [-500, 0, 999, 1e7]) {
            const w = visibleWindow({ total, rowHeight: ROW, viewportHeight: PANEL, scrollTop });
            assert.ok(w.start >= 0, 'start is not negative');
            assert.ok(w.end <= total, 'end is within the list');
            assert.ok(w.end >= w.start, 'the window is not inverted');
            assert.ok(w.padTop >= 0 && w.padBottom >= 0, 'padding is not negative');
        }
    }
});

test('rows are rendered beyond the viewport on both sides', () => {
    const w = visibleWindow({
        total: 1000, rowHeight: ROW, viewportHeight: PANEL, scrollTop: 20 * ROW
    });
    assert.equal(w.start, 20 - OVERSCAN_ROWS, 'overscan above');
    const fits = Math.ceil(PANEL / ROW);
    assert.ok(w.end >= 20 + fits + OVERSCAN_ROWS, 'overscan below');
});

test('rendered rows track the panel height, not the aircraft count', () => {
    // The property the whole change is bought for: a busy sky costs no more to
    // display than a quiet one.
    const counts = [200, 600, 1500].map((total) => {
        const w = visibleWindow({ total, rowHeight: ROW, viewportHeight: PANEL, scrollTop: 0 });
        return w.end - w.start;
    });
    assert.equal(new Set(counts).size, 1, `same row count at every list size (${counts})`);

    // And it does rise with a taller panel — it is bounded, not constant.
    const short = visibleWindow({ total: 1500, rowHeight: ROW, viewportHeight: 300, scrollTop: 0 });
    const tall = visibleWindow({ total: 1500, rowHeight: ROW, viewportHeight: 1400, scrollTop: 0 });
    assert.ok(
        (tall.end - tall.start) > (short.end - short.start),
        'a taller panel renders more rows'
    );
});

test('a zero-height panel still renders a row', () => {
    // A collapsed mobile sheet must not produce a window of nothing, or there is
    // no row left to measure the height from when it reopens.
    const w = visibleWindow({ total: 1000, rowHeight: ROW, viewportHeight: 0, scrollTop: 0 });
    assert.ok(w.end > w.start, 'at least one row');
});
