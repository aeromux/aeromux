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

// Pure heatmap colour-scale helpers, isolated from MapLibre so they can be
// unit-tested with `node --test`. Reads the PascalCase HeatmapUpdated payload.

// Reversed ColorBrewer RdYlGn, seven stops (green → red), keyed by t = 0..1.
export const RDYLGN_STOPS = [
    [0.00, '#1a9850'], [0.17, '#66bd63'], [0.33, '#a6d96a'],
    [0.50, '#fee08b'], [0.67, '#fdae61'], [0.83, '#f46d43'], [1.00, '#d73027'],
];

// Log zero-to-max colour position, clamped to [0, 1]. Returns 0 (green) for
// count <= 1 or a degenerate scaleMax <= 1 (log(1) = 0).
export function colourT(count, scaleMax) {
    if (!(scaleMax > 1) || !(count > 1)) return 0;
    const t = Math.log(count) / Math.log(scaleMax);
    return t < 0 ? 0 : t > 1 ? 1 : t;
}

// Representative tick counts at t = 0, 0.5, 1 on the log scale.
export function legendTicks(scaleMax) {
    return [1, Math.max(1, Math.round(Math.sqrt(scaleMax))), scaleMax];
}

// HeatmapUpdated payload (PascalCase) → GeoJSON polygon features carrying the
// raw count and a precomputed colour position t.
export function payloadToFeatures(payload) {
    const scaleMax = payload.ScaleMax;
    return (payload.Cells || []).map((c) => ({
        type: 'Feature',
        properties: { count: c.Count, t: colourT(c.Count, scaleMax) },
        geometry: {
            type: 'Polygon',
            coordinates: [[
                [c.West, c.South], [c.East, c.South],
                [c.East, c.North], [c.West, c.North], [c.West, c.South],
            ]],
        },
    }));
}
