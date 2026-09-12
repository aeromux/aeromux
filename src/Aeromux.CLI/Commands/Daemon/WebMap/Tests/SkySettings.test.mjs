// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the device-dependent Sky View setting defaults. Run with
// `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { resolveDeviceDefaults, loadSettings, saveSettings } from '../Services/UnitConversion.js';

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

// A minimal localStorage, so the persistence round-trip can be exercised at all.
function installStorage() {
    const store = new Map();
    globalThis.localStorage = {
        getItem: (k) => (store.has(k) ? store.get(k) : null),
        setItem: (k, v) => { store.set(k, String(v)); },
        removeItem: (k) => { store.delete(k); }
    };
    return store;
}

test('every stored setting survives a save and load round-trip', () => {
    // loadSettings rebuilds its result key by key, so a setting added to the
    // defaults but forgotten there is silently dropped on the next load — the
    // control then renders against a value the user never chose. This asserts the
    // whole object rather than one key, so the next setting added is covered too.
    installStorage();
    const defaults = loadSettings();

    // Flip everything away from its default, so a key that is not carried through
    // reverts visibly rather than coincidentally matching.
    const modified = {};
    for (const [key, value] of Object.entries(defaults)) {
        if (typeof value === 'boolean') modified[key] = !value;
        else if (typeof value === 'number') modified[key] = value + 1;
        else if (value === null) modified[key] = 'all';
        else modified[key] = value === 'map' ? 'sky' : 'auto';
    }

    saveSettings(modified);
    assert.deepEqual(loadSettings(), modified);
});

test('both halves of the sun and moon feature are on by default', () => {
    installStorage();
    const settings = loadSettings();
    assert.equal(settings.skyCelestial, true, 'the markers');
    assert.equal(settings.skyTwilight, true, 'and the sky tint');
});
