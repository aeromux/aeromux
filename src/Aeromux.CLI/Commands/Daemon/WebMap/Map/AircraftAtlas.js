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

// One texture holding every aircraft silhouette, for the layer that draws icons on the
// GPU.
//
// One entry per shape, drawn as a mask: white where the body is filled, black where it
// is stroked, transparent outside. The fragment shader multiplies the mask by the
// per-aircraft fill color, so a single entry serves all 64 altitude and category
// variants of a shape. 82 shapes pack into about 1520 pixels square.
//
// The symbol layer keeps its own per-variant images: it survives as the hit-test layer,
// and a symbol with no icon has no area to hit.

import { SHAPES } from './AircraftShapes.js';
import { buildSvgDataUri, PIXEL_RATIO } from './AircraftIcons.js';

// Headroom over the 1520 the shape table needs today, and the smallest size every WebGL
// implementation is required to support.
export const ATLAS_SIZE = 2048;

// Gap between entries. Linear filtering samples half a texel past each edge, so this
// keeps a shape from bleeding into its neighbor.
const PADDING = 2;

// White, so the shader's multiply yields the fill color unchanged.
const MASK_FILL = [255, 255, 255];

let canvas = null;
let rects = new Map();
let version = 0;
let building = null;

export function atlasCanvas() {
    return canvas;
}

// Bumped whenever the pixels change, so the layer knows to re-upload the texture.
export function atlasVersion() {
    return version;
}

// Where a shape lives in the atlas, in texture coordinates, or null if it is absent.
// Callers fall back to the `unknown` shape, as the symbol layer's `coalesce` does.
export function rectFor(shapeName) {
    return rects.get(shapeName) || null;
}

// Decodes every shape once and shelf-packs them, tallest first.
export function build() {
    if (building) return building;

    building = (async () => {
        const names = Object.keys(SHAPES);
        const entries = [];

        await Promise.all(names.map(async (shapeName) => {
            try {
                const image = new Image();
                image.src = buildSvgDataUri(shapeName, MASK_FILL);
                await image.decode();
                entries.push({
                    shapeName,
                    image,
                    width: Math.ceil(SHAPES[shapeName].w * PIXEL_RATIO),
                    height: Math.ceil(SHAPES[shapeName].h * PIXEL_RATIO)
                });
            } catch (error) {
                // A failed shape is skipped; the rest of the atlas still builds.
                console.warn(`[aircraft-atlas] decode failed for '${shapeName}': ${error.message}`);
            }
        }));

        if (!entries.length) return;

        entries.sort((a, b) => b.height - a.height);

        const packed = new Map();
        let shelfX = PADDING;
        let shelfY = PADDING;
        let shelfHeight = 0;
        let overflowed = false;

        for (const entry of entries) {
            if (shelfX + entry.width + PADDING > ATLAS_SIZE) {
                shelfX = PADDING;
                shelfY += shelfHeight + PADDING;
                shelfHeight = 0;
            }
            if (shelfY + entry.height + PADDING > ATLAS_SIZE) {
                // Tallest-first ordering means overflow hits the smallest shapes, which
                // fall back to `unknown`.
                overflowed = true;
                continue;
            }
            packed.set(entry.shapeName, { entry, x: shelfX, y: shelfY });
            shelfX += entry.width + PADDING;
            shelfHeight = Math.max(shelfHeight, entry.height);
        }

        if (overflowed) {
            console.warn('[aircraft-atlas] shape table outgrew the atlas; some shapes fall back');
        }

        const target = document.createElement('canvas');
        target.width = ATLAS_SIZE;
        target.height = ATLAS_SIZE;
        const context = target.getContext('2d', { willReadFrequently: false });
        if (!context) return;
        context.clearRect(0, 0, ATLAS_SIZE, ATLAS_SIZE);

        const next = new Map();
        for (const [shapeName, slot] of packed) {
            const { entry, x, y } = slot;
            context.drawImage(entry.image, x, y, entry.width, entry.height);
            next.set(shapeName, {
                // Texture coordinates, so the shader needs no knowledge of atlas size.
                u0: x / ATLAS_SIZE,
                v0: y / ATLAS_SIZE,
                u1: (x + entry.width) / ATLAS_SIZE,
                v1: (y + entry.height) / ATLAS_SIZE
            });
        }

        canvas = target;
        rects = next;
        version++;
    })();

    return building;
}
