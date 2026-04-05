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

import { h } from 'preact';
import { convertSpeed, convertAltitude } from '../Services/UnitConversion.js';

export function HoverTooltip({ hover, units }) {
    if (!hover) return null;

    const speed = hover.speed ? convertSpeed(hover.speed, units.speed) : null;
    const alt = hover.altitude ? convertAltitude(hover.altitude, units.altitude) : null;

    return (
        <div class="hover-tooltip" style={{ left: hover.x + 'px', top: hover.y - 20 + 'px', transform: 'translate(-50%, -100%)' }}>
            <div class="hover-tooltip-callsign">{hover.callsign || 'N/A'}</div>
            <div class="hover-tooltip-field">{hover.icao}</div>
            {speed && <div class="hover-tooltip-field">{speed.value} {speed.label}</div>}
            {alt && <div class="hover-tooltip-field">{alt.value} {alt.label}</div>}
        </div>
    );
}
