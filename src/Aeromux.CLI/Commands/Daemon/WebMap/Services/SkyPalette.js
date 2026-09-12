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

// What the sky looks like at a given solar elevation, and what that implies for
// everything drawn against it. Pure, so the colour decisions are testable without
// a canvas.
//
// The reason this module exists is not the colours. The aircraft palette was
// measured against a pale daylight sky, and tinting the sky breaks that
// assumption: at civil twilight a mid-blue background sits almost exactly on the
// dark end of the altitude ramp, which takes cruise and military traffic to about
// 1.2:1 — effectively invisible. Night is survivable for the fills but not for the
// outline, which is black, and black on a night sky is 1.12:1.
//
// The fix follows a decision already taken in the Sky View: a chip is found by its
// outline, not by its fill. So the outline follows the sky — dark on a light sky,
// light on a dark one — and the fills are left exactly as they are, which keeps the
// altitude ramp meaning what it means in daylight.

const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);

// Sky phases by solar elevation, in degrees. Between two neighbours the colours
// are interpolated, so dusk is a continuous change rather than a set of steps.
//
// `zenith`, `middle` and `horizon` are the three gradient stops from the top of the
// view down to the horizon; `ground` is the band below it, which has to follow or it
// glows against a night sky.
const PHASES = [
    {
        name: 'day',
        elevationDeg: 10,
        zenith: [157, 196, 228],
        middle: [207, 227, 243],
        horizon: [239, 245, 250],
        ground: [222, 211, 196]
    },
    {
        name: 'golden',
        elevationDeg: 0,
        zenith: [108, 158, 204],
        middle: [198, 198, 208],
        horizon: [246, 217, 176],
        ground: [188, 170, 148]
    },
    {
        name: 'civil',
        elevationDeg: -6,
        zenith: [40, 62, 102],
        middle: [74, 106, 148],
        horizon: [150, 136, 150],
        ground: [96, 88, 84]
    },
    {
        name: 'nautical',
        elevationDeg: -12,
        zenith: [18, 30, 58],
        middle: [32, 50, 86],
        horizon: [62, 74, 104],
        ground: [48, 46, 50]
    },
    {
        name: 'night',
        elevationDeg: -18,
        zenith: [8, 12, 24],
        middle: [10, 16, 30],
        horizon: [11, 18, 32],
        ground: [20, 20, 24]
    }
];

// Outline tones. Both are softened rather than pure black and white: a chip sits on
// a graded background, and a hard white rim on a night sky reads as a light source
// of its own.
const OUTLINE_DARK = [0, 0, 0];
const OUTLINE_LIGHT = [236, 240, 247];
const OUTLINE_DARK_ALPHA = 0.55;
const OUTLINE_LIGHT_ALPHA = 0.75;

// Everything else drawn on the canvas: the elevation grid, the compass ticks and
// their labels, the horizon line, the stems, the aircraft labels. All of it is
// black at whatever opacity suits, which works only while the sky is pale.
const INK_DARK = [0, 0, 0];
const INK_LIGHT = [226, 232, 240];

// The coverage ribbon's own backing, and the chips behind its scale labels. These
// are the surfaces that would glow against a night sky if they stayed white.
const PANEL_LIGHT = [255, 255, 255];
const PANEL_DARK = [16, 22, 36];

// The ribbon's range profile. The brand blue is dark on dark, so it lifts to a
// lighter blue once the sky goes.
const RIBBON_DARK_SKY = [96, 165, 230];
const RIBBON_LIGHT_SKY = [0, 97, 146];

// Relative luminance above which the sky counts as light and wants a dark outline.
// Sits between civil twilight and the golden hour, which is where the sky stops
// being something a dark line shows up against.
export const LIGHT_SKY_LUMINANCE = 0.22;

// ---------- colour helpers ----------

export function css(rgb, alpha = 1) {
    const [r, g, b] = rgb.map((v) => Math.round(v));
    return alpha >= 1 ? `rgb(${r}, ${g}, ${b})` : `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function mix(a, b, t) {
    return [
        a[0] + (b[0] - a[0]) * t,
        a[1] + (b[1] - a[1]) * t,
        a[2] + (b[2] - a[2]) * t
    ];
}

// WCAG relative luminance, on 0 to 1.
export function relativeLuminance(rgb) {
    const channel = (v) => {
        const c = v / 255;
        return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * channel(rgb[0]) + 0.7152 * channel(rgb[1]) + 0.0722 * channel(rgb[2]);
}

// WCAG contrast ratio between two colours, from 1 (identical) to 21.
export function contrastRatio(a, b) {
    const la = relativeLuminance(a);
    const lb = relativeLuminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}

// ---------- the palette ----------

// Sky colours for a solar elevation, interpolated between the two phases it falls
// between. Above the day threshold and below the night one it holds steady.
export function skyPalette(solarElevationDeg) {
    let lower = PHASES[0];
    let upper = PHASES[0];
    let t = 0;

    if (solarElevationDeg >= PHASES[0].elevationDeg) {
        lower = upper = PHASES[0];
    } else if (solarElevationDeg <= PHASES[PHASES.length - 1].elevationDeg) {
        lower = upper = PHASES[PHASES.length - 1];
    } else {
        for (let i = 0; i < PHASES.length - 1; i++) {
            const high = PHASES[i];
            const low = PHASES[i + 1];
            if (solarElevationDeg <= high.elevationDeg && solarElevationDeg >= low.elevationDeg) {
                upper = high;
                lower = low;
                // Towards the lower phase as the sun drops.
                t = (high.elevationDeg - solarElevationDeg) / (high.elevationDeg - low.elevationDeg);
                break;
            }
        }
    }

    const zenith = mix(upper.zenith, lower.zenith, t);
    const middle = mix(upper.middle, lower.middle, t);
    const horizon = mix(upper.horizon, lower.horizon, t);
    const ground = mix(upper.ground, lower.ground, t);

    // One luminance for the frame, taken from the band just above the horizon.
    // That is where the traffic is and where a chip has to be found; the zenith is
    // darker than the rest and would flip the outline too early.
    const luminance = relativeLuminance(middle);

    return {
        phase: t < 0.5 ? upper.name : lower.name,
        zenith,
        middle,
        horizon,
        ground,
        luminance,
        ...inkFor(luminance)
    };
}

// The foreground tones a sky of this luminance needs. Grouped with the outline
// because they flip together: a sky dark enough to need a light outline needs light
// grid lines and labels for the same reason.
export function inkFor(luminance) {
    const light = luminance < LIGHT_SKY_LUMINANCE;
    return {
        outline: outlineFor(luminance),
        ink: light ? INK_LIGHT : INK_DARK,
        panel: light ? PANEL_DARK : PANEL_LIGHT,
        ribbon: light ? RIBBON_DARK_SKY : RIBBON_LIGHT_SKY,
        dark: light
    };
}

// The outline colour a sky of this luminance needs.
export function outlineFor(luminance) {
    return luminance >= LIGHT_SKY_LUMINANCE
        ? { rgb: OUTLINE_DARK, alpha: OUTLINE_DARK_ALPHA, light: false }
        : { rgb: OUTLINE_LIGHT, alpha: OUTLINE_LIGHT_ALPHA, light: true };
}

// The daylight palette, for callers that have no solar position — the renderer
// before the first frame, or the sun and moon switched off.
export function defaultPalette() {
    return skyPalette(90);
}

// Names of the phases, for tests that want to sweep all of them.
export function phaseNames() {
    return PHASES.map((p) => p.name);
}

// The representative sky colour of a named phase, for the same reason.
export function phaseByName(name) {
    const found = PHASES.find((p) => p.name === name);
    return found ? skyPalette(clamp(found.elevationDeg, -18, 90)) : null;
}
