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

// Pure geometry helpers for the resizable mobile bottom sheet. Kept free of
// DOM/localStorage so they can be unit-tested with `node --test`.

// Absolute floor: keep the header plus roughly one list row visible.
export const MIN_SHEET_PX = 160;
// Ceiling as a fraction of the viewport, so the map always peeks above the sheet.
export const MAX_SHEET_FRACTION = 0.85;
// Floor as a fraction, applied when restoring a persisted height on a device
// whose viewport differs from the one the value was captured on.
export const MIN_SHEET_FRACTION = 0.15;

function clamp(value, lo, hi) {
    return Math.max(lo, Math.min(value, hi));
}

// Clamp a desired pixel height to the allowed range for the given viewport.
// On very short viewports the pixel floor can exceed the fractional ceiling;
// in that case the ceiling wins so the value never exceeds the viewport.
export function clampSheetPx(desiredPx, viewportPx) {
    const maxPx = viewportPx * MAX_SHEET_FRACTION;
    const minPx = Math.min(MIN_SHEET_PX, maxPx);
    return clamp(desiredPx, minPx, maxPx);
}

// Convert a pixel height to a viewport fraction, clamped to the persisted range.
export function pxToFraction(px, viewportPx) {
    if (!(viewportPx > 0)) return MIN_SHEET_FRACTION;
    return clamp(px / viewportPx, MIN_SHEET_FRACTION, MAX_SHEET_FRACTION);
}

// Validate a value loaded from storage. Returns a usable fraction or null
// (null => no saved preference, fall back to the CSS default).
export function normalizeStoredFraction(value) {
    if (typeof value !== 'number' || !Number.isFinite(value)) return null;
    if (value <= 0) return null;
    return clamp(value, MIN_SHEET_FRACTION, MAX_SHEET_FRACTION);
}
