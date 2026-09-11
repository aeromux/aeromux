// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the device-dependent Sky View setting defaults. Run with
// `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { resolveDeviceDefaults } from '../Services/UnitConversion.js';

test('an unresolved label setting becomes auto on a desktop', () => {
    const resolved = resolveDeviceDefaults({ skyLabels: null }, false);
    assert.equal(resolved.skyLabels, 'auto');
});

test('an unresolved label setting becomes selection-only on a phone', () => {
    // No hover to disambiguate a cluster, and far less room for labels.
    const resolved = resolveDeviceDefaults({ skyLabels: null }, true);
    assert.equal(resolved.skyLabels, 'selection');
});

test('an explicit choice is never overridden', () => {
    for (const choice of ['selection', 'auto', 'all']) {
        assert.equal(resolveDeviceDefaults({ skyLabels: choice }, true).skyLabels, choice);
        assert.equal(resolveDeviceDefaults({ skyLabels: choice }, false).skyLabels, choice);
    }
});

test('resolving leaves every other setting untouched', () => {
    const stored = { skyLabels: null, skyFov: 100, skyMaxRangeNm: 250, rangeRings: false };
    const resolved = resolveDeviceDefaults(stored, false);
    assert.equal(resolved.skyFov, 100);
    assert.equal(resolved.skyMaxRangeNm, 250);
    assert.equal(resolved.rangeRings, false);
});

test('an already-resolved object is returned as-is, so callers can detect a change', () => {
    const stored = { skyLabels: 'auto' };
    // Reset-to-defaults relies on this identity check to know whether to persist.
    assert.equal(resolveDeviceDefaults(stored, false), stored);
});
