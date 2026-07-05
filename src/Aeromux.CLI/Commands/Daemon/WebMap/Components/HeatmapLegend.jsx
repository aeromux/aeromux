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
import { RDYLGN_STOPS, legendTicks } from '../Services/HeatmapScale.js';

// Legend for the traffic-density heatmap, shown inside the settings panel's Heatmap
// section. Shows the green→red gradient annotated with representative counts on its
// logarithmic scale, plus an optional peak note when the busiest cell exceeds the anchor.
export function HeatmapLegend({ scaleMax, maxCount }) {
    const gradient = `linear-gradient(to right, ${RDYLGN_STOPS
        .map(([t, c]) => `${c} ${Math.round(t * 100)}%`)
        .join(', ')})`;
    const [lo, mid, hi] = legendTicks(scaleMax);

    return (
        <div class="heatmap-legend">
            <div class="settings-field-label">Aircraft / cell</div>
            <div class="heatmap-legend-bar" style={{ background: gradient }} />
            <div class="heatmap-legend-ticks">
                <span>{lo}</span>
                <span>{mid}</span>
                <span>{hi}</span>
            </div>
            {maxCount > scaleMax && <div class="heatmap-legend-peak">peak: {maxCount}</div>}
        </div>
    );
}
