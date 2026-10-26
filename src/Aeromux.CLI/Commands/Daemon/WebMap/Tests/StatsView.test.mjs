// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see http://www.gnu.org/licenses.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadStatsView, saveStatsView, resetAllSettings } from '../Services/UnitConversion.js';

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

test('nothing stored: rates over the last minute, by downlink format', () => {
    installStorage();
    assert.deepEqual(loadStatsView(), { scope: 'now', kind: 'DF' });
});

test('a stored view survives a save and load round-trip', () => {
    installStorage();
    saveStatsView({ scope: 'total', kind: 'BDS' });
    assert.deepEqual(loadStatsView(), { scope: 'total', kind: 'BDS' });
});

test('an unknown stored value falls back to its default, the other is kept', () => {
    const store = installStorage();
    store.set('aeromux-stats', JSON.stringify({ scope: 'hourly', kind: 'TC' }));
    assert.deepEqual(loadStatsView(), { scope: 'now', kind: 'TC' });
});

test('unreadable storage falls back to the defaults', () => {
    const store = installStorage();
    store.set('aeromux-stats', '{not json');
    assert.deepEqual(loadStatsView(), { scope: 'now', kind: 'DF' });
});

test('resetting the settings clears the view too', () => {
    installStorage();
    saveStatsView({ scope: 'total', kind: 'TC' });
    resetAllSettings();
    assert.deepEqual(loadStatsView(), { scope: 'now', kind: 'DF' });
});
