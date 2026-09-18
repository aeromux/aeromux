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
// Unlike its neighbours here this module touches the DOM, which is why it holds no
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
