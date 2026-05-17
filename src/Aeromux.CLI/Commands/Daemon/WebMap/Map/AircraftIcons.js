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

// Renders type-specific aircraft silhouettes as SVG-data-URI bitmaps
// and registers them with MapLibre via addImage(). One bitmap per
// (shape, category, altStep) variant, plus one per (shape) for the
// selected state. The 64 `unknown` fallback variants are pre-decoded
// eagerly at map setup; type-specific bitmaps are decoded lazily on
// first sight and dedupe-and-poison via the `registeredImages` Set
// so failed decodes are never retried.

import { SHAPES } from './AircraftShapes.js';

// ---------- public constants ----------

// Over-resolution factor on the registered bitmap. Chosen to comfortably
// exceed Retina (DPR 2) and iPhone (DPR 3) device pixel ratios even at
// the resolver's largest per-type scale (~1.12). MapLibre is told the
// bitmap is over-resolved via the pixelRatio option to addImage(), so
// it draws at the shape's nominal w × h logical pixels.
export const PIXEL_RATIO  = 4;
// Body-stroke width in logical pixels, before multiplying by per-shape
// strokeScale. Initial value tuned to land at a comfortable visual
// weight at the default PIXEL_RATIO; expect a follow-up tuning pass on
// Retina/iPhone.
export const STROKE_WIDTH = 0.75;
export const ALTITUDE_STEP = 2000;
export const MAX_ALTITUDE  = 40000;

// Global on-screen icon-size multiplier applied at draw time, in
// addition to the resolver's per-type iconScale. Lets us tune overall
// marker size in one place without touching the underlying shape data
// or the per-type scaling tables.
export const ICON_SIZE = 1.25;

// Altitude color stops per palette (feet → [r, g, b]). Identical to
// the previous canvas pipeline — preserving the visual gradient is
// part of the contract for this rewrite.
const COLOR_STOPS_NORMAL = [
    [0,     [179, 217, 255]],  // light blue
    [10000, [102, 178, 255]],  // medium blue
    [25000, [51,  153, 255]],  // strong blue
    [40000, [0,   97,  146]]   // dark blue
];

const COLOR_STOPS_MILITARY = [
    [0,     [179, 230, 179]],  // light green
    [10000, [102, 194, 102]],  // medium green
    [25000, [51,  163, 51]],   // strong green
    [40000, [0,   110, 0]]     // dark green
];

const COLOR_STOPS_PRIVACY = [
    [0,     [255, 179, 179]],  // light salmon
    [10000, [255, 102, 102]],  // medium red
    [25000, [220, 50,  50]],   // strong red
    [40000, [160, 0,   0]]     // dark crimson
];

export const CATEGORIES = [
    { prefix: 'normal',   stops: COLOR_STOPS_NORMAL },
    { prefix: 'military', stops: COLOR_STOPS_MILITARY },
    { prefix: 'privacy',  stops: COLOR_STOPS_PRIVACY }
];

export const SELECTED_COLOR = [230, 126, 34]; // #e67e22

// ---------- public colour helper ----------

// Linear interpolation between adjacent palette stops. Returns the
// nearest stop color for out-of-range altitudes (i.e. clamps).
export function interpolateColor(altitude, stops) {
    if (altitude <= stops[0][0]) return stops[0][1];
    if (altitude >= stops[stops.length - 1][0]) return stops[stops.length - 1][1];

    for (let i = 0; i < stops.length - 1; i++) {
        const [altLow,  colorLow]  = stops[i];
        const [altHigh, colorHigh] = stops[i + 1];
        if (altitude >= altLow && altitude <= altHigh) {
            const t = (altitude - altLow) / (altHigh - altLow);
            return [
                Math.round(colorLow[0] + t * (colorHigh[0] - colorLow[0])),
                Math.round(colorLow[1] + t * (colorHigh[1] - colorLow[1])),
                Math.round(colorLow[2] + t * (colorHigh[2] - colorLow[2]))
            ];
        }
    }
    return stops[stops.length - 1][1];
}

// ---------- module-local registration state ----------

// Set of MapLibre image names this module has already attempted to
// register (regardless of decode outcome). The .add() happens before
// the await so two close-spaced ticks dedupe to a single decode, and
// a permanent decode failure stays in the Set so the poisoned slot
// is never retried.
export const registeredImages = new Set();

// Set of TypeCode strings already emitted as `[aircraft-icon]
// unmapped TypeCode` debug logs in the current session. Exported so
// the MapManager log site can `.has() / .add()` against the same Set
// the styledata handler clears.
export const loggedUnknownTypes = new Set();

// MapLibre map reference, captured at first call. ensureRegistered()
// and preregisterUnknownVariants() require an attached map.
let map = null;

// ---------- SVG-data-URI rendering ----------

// Builds a synthetic SVG string for one (shape, fillRGB) pair, encoded
// as a base64 data URI. Uses `paint-order="stroke"` with stroke-width
// 2× the desired width so the fill cleanly overpaints the inside half
// of the stroke and the outside edge stays crisp.
function buildSvgDataUri(shapeName, fillRGB, strokeRGB = '#000') {
    const shape  = SHAPES[shapeName];
    if (!shape) {
        throw new Error(`buildSvgDataUri: missing shape '${shapeName}'`);
    }
    const fill   = `rgb(${fillRGB[0]},${fillRGB[1]},${fillRGB[2]})`;
    const stroke = strokeRGB;
    const sw     = STROKE_WIDTH * (shape.strokeScale ?? 1);
    const accW   = 0.6 * STROKE_WIDTH * (shape.accentMult ?? 1);
    const wPx    = shape.w * PIXEL_RATIO;
    const hPx    = shape.h * PIXEL_RATIO;

    let svg = `<svg xmlns="http://www.w3.org/2000/svg" `
            + `width="${wPx}" height="${hPx}" `
            + `viewBox="${shape.viewBox}"`
            + (shape.noAspect ? ` preserveAspectRatio="none"` : '')
            + `>`;
    if (shape.transform) {
        svg += `<g transform="${shape.transform}">`;
    }

    // Body — paint-order="stroke" + 2× width gives a clean fill/stroke seam.
    const paths = Array.isArray(shape.path) ? shape.path : [shape.path];
    for (const p of paths) {
        svg += `<path paint-order="stroke" fill="${fill}" stroke="${stroke}" `
             + `stroke-width="${2 * sw}" stroke-linejoin="round" d="${p}"/>`;
    }
    // Accent paths (engines, panel lines, etc.) — stroke only, 60% width × accentMult.
    if (shape.accent) {
        const accents = Array.isArray(shape.accent) ? shape.accent : [shape.accent];
        for (const a of accents) {
            svg += `<path fill="none" stroke="${stroke}" `
                 + `stroke-width="${accW}" stroke-linejoin="round" d="${a}"/>`;
        }
    }

    if (shape.transform) {
        svg += `</g>`;
    }
    svg += `</svg>`;
    return 'data:image/svg+xml;base64,' + btoa(svg);
}

// ---------- registration ----------

// Lazily register one variant. The first caller for a given imageName
// claims the slot in `registeredImages` before awaiting the decoding so
// concurrent ticks dedupe to a single decode. A decode failure leaves
// the imageName in `registeredImages` (poisoned) so the slot is never
// retried — the layer's `coalesce` keeps the affected features showing
// the corresponding `unknown` fallback.
export async function ensureRegistered(imageName, shapeName, fillRGB) {
    if (!map) {
        throw new Error('ensureRegistered: no map bound — call setMap() first');
    }
    if (registeredImages.has(imageName)) return;
    registeredImages.add(imageName);
    try {
        const img = new Image();
        img.src = buildSvgDataUri(shapeName, fillRGB);
        await img.decode();
        if (!map.hasImage(imageName)) {
            map.addImage(imageName, img, { pixelRatio: PIXEL_RATIO });
        }
    } catch (e) {
        console.warn(`[aircraft-icon] decode failed for '${shapeName}' (${imageName}): ${e.message}`);
        // imageName stays in registeredImages → never retried.
    }
}

// Binds the MapLibre map instance used by all subsequent
// ensureRegistered() and preregisterUnknownVariants() calls. Called
// once from MapManager during map setup.
export function setMap(mapInstance) {
    map = mapInstance;
}

// Eagerly registers all 64 `unknown` fallback variants in parallel:
// one per (category, altStep) × 3 palettes + 1 selected. Awaited
// before SignalR subscription so the layer's coalesce always has a
// fallback for the first frame of any feature.
export async function preregisterUnknownVariants() {
    const tasks = [];
    for (const { prefix, stops } of CATEGORIES) {
        for (let alt = 0; alt <= MAX_ALTITUDE; alt += ALTITUDE_STEP) {
            const fillColor = interpolateColor(alt, stops);
            tasks.push(ensureRegistered(`aircraft-unknown-${prefix}-${alt}`,
                                         'unknown', fillColor));
        }
    }
    tasks.push(ensureRegistered('aircraft-unknown-selected', 'unknown', SELECTED_COLOR));
    await Promise.all(tasks);
}

// Clears both registration caches. Called from the MapManager
// `styledata` handler before re-running preregisterUnknownVariants() —
// MapLibre wipes registered images on a base-style change, so the
// caches must follow.
export function clearImageCaches() {
    registeredImages.clear();
    loggedUnknownTypes.clear();
}
