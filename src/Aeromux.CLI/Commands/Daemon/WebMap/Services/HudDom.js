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

// The pieces both view readouts are built from.
//
// Each view writes its own readout imperatively, because both change on every frame
// of a drag and routing that through the component tree would re-render the aircraft
// list with them. What they must not do is each grow a private copy of the same
// chip: the two rows sit in the same place on screen, in the same panel treatment,
// and are read the same way, so the DOM they are made of belongs in one file.
//
// Unlike its neighbors here this module touches the DOM, which is why it holds no
// arithmetic beyond the bearing format the two rows share.

import { wrap360 } from './SkyViewGeometry.js';

// One label-and-value chip. `leading` puts the value first, for the readings where
// the number is the subject and the label is its unit ("3/11 in view").
export function createHudItem(labelText, leading) {
    const wrap = document.createElement('div');
    wrap.className = 'view-hud-item';

    const label = document.createElement('span');
    label.className = 'view-hud-label';
    label.textContent = labelText;

    const value = document.createElement('span');
    value.className = 'view-hud-value';

    if (leading) {
        wrap.appendChild(value);
        wrap.appendChild(label);
    } else {
        wrap.appendChild(label);
        wrap.appendChild(value);
    }

    return { wrap, value };
}

// Written only when it changed, so a readout refreshed on every frame does not hand
// the browser a fresh text node sixty times a second.
export function setText(node, text) {
    if (node && node.textContent !== text) {
        node.textContent = text;
    }
}

// Three digits, zero-padded, the way a bearing is spoken. Shared so the Sky View's
// heading, the bearings to the sun and moon, and the Map View's rotation all read
// identically.
export function formatBearing(deg) {
    return `${String(Math.round(wrap360(deg))).padStart(3, '0')}°`;
}

// Which chips a row of this width can hold.
//
// The row sits between the aircraft list and the control panel, and what is left
// between them is often far less than the row would take: on a tablet in portrait
// about 268px against the 690px the Map View's six chips want. Left unbounded
// the row runs underneath the control panel, where half of it cannot be read.
//
// So chips drop, one at a time, until the rest fit, and when not even the first of
// them fits the row shows nothing at all. `chips` is ordered least valuable first,
// which is the order they are given up in; each carries the width it measured.
export function fitChips(budgetPx, chips, { gap = 0, padding = 0 } = {}) {
    const kept = new Set();
    if (!Array.isArray(chips) || !chips.length || !Number.isFinite(budgetPx)) return kept;

    let used = padding;
    // From the most valuable end, which is the end of the list.
    for (let i = chips.length - 1; i >= 0; i--) {
        const chip = chips[i];
        if (!chip || !Number.isFinite(chip.width)) continue;
        const cost = chip.width + (kept.size ? gap : 0);
        if (used + cost > budgetPx) break;
        used += cost;
        kept.add(chip.key);
    }

    return kept;
}

// Measure a row and apply what fits, in one pass.
//
// `chips` is ordered least valuable first, each carrying its element and whether it
// has anything to say at all: a chip withheld for its own reasons (a heading on a
// north-up map, an area on a tilted one) is never a candidate for the space.
//
// Every candidate is shown, all the widths are read together, and the outcome is
// applied, all without returning to the browser in between, so nothing is ever
// painted mid-measurement. The spacing is read from the row itself rather than
// restated here, so the CSS stays the single source of it.
export function fitRow(hud, chips, budgetPx) {
    if (!hud || !Array.isArray(chips) || !chips.length) return new Set();

    for (const chip of chips) {
        chip.wrap.style.display = chip.eligible ? '' : 'none';
    }

    // An unbounded row has nothing to decide, and measuring it would cost a layout
    // for an answer already known. This is the state before the panels have been
    // measured for the first time.
    if (!Number.isFinite(budgetPx)) {
        const all = new Set(chips.filter((chip) => chip.eligible).map((chip) => chip.key));
        hud.style.display = all.size ? '' : 'none';
        return all;
    }

    const kept = fitChips(
        budgetPx,
        chips.filter((chip) => chip.eligible).map((chip) => ({ key: chip.key, width: chip.wrap.offsetWidth })),
        rowSpacing(hud)
    );

    for (const chip of chips) {
        chip.wrap.style.display = chip.eligible && kept.has(chip.key) ? '' : 'none';
    }
    // An empty row is no row: the panel treatment on its own says nothing.
    hud.style.display = kept.size ? '' : 'none';

    return kept;
}

function rowSpacing(hud) {
    if (typeof getComputedStyle !== 'function') return { gap: 0, padding: 0 };
    const style = getComputedStyle(hud);
    return {
        gap: parseFloat(style.columnGap) || 0,
        padding: (parseFloat(style.paddingLeft) || 0) + (parseFloat(style.paddingRight) || 0)
    };
}

// A row that re-measures itself only when it has reason to.
//
// Measuring costs a layout, and both rows are rewritten on every frame of a drag, so
// the pass is tied to events rather than to frames: the width available has changed,
// a chip has appeared or withdrawn, or the values themselves have changed width and
// the throttle has elapsed. Each caller keeps its own fitter, which keeps the
// bookkeeping out of the renderers.
export function createRowFitter(hud, chips, { intervalMs = 500, now = Date.now } = {}) {
    let last = { budget: null, shape: '', text: '', at: 0 };

    return function refit(budgetPx) {
        const shape = chips.map((chip) => (chip.eligible ? '1' : '0')).join('');
        // Length rather than content: what matters here is how much room the values
        // ask for, not what they say.
        const text = chips.map((chip) => (chip.eligible ? chip.wrap.textContent.length : 0)).join(',');
        const at = now();

        const changed = last.budget !== budgetPx
            || last.shape !== shape
            || (last.text !== text && at - last.at >= intervalMs);
        if (!changed) return;

        fitRow(hud, chips, budgetPx);
        last = { budget: budgetPx, shape, text, at };
    };
}
