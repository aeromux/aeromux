// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Tests for the sky palette and the chip outline derived from it.
//
// These assert properties rather than hex values: the colours are a judgement and
// will be adjusted, but "an aircraft can be found against the sky" must hold
// whatever they are adjusted to.
//
// Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    skyPalette,
    outlineFor,
    defaultPalette,
    relativeLuminance,
    contrastRatio,
    phaseNames,
    phaseByName,
    css,
    LIGHT_SKY_LUMINANCE
} from '../Services/SkyPalette.js';

// The four corners of the aircraft palette, from Map/AircraftIcons.js: the pale
// end of the altitude ramp, its dark end, the military tone, and the selection.
const CATEGORY_FILLS = {
    'low altitude': [179, 217, 255],
    cruise: [0, 97, 146],
    military: [0, 110, 0],
    selected: [230, 126, 34]
};

// Below this a mark is not reliably findable against its background.
const USABLE_CONTRAST = 3;

// ---------- the phase table ----------

test('the sky darkens monotonically as the sun goes down', () => {
    let previous = Infinity;
    for (let elevation = 30; elevation >= -25; elevation -= 0.5) {
        const { luminance } = skyPalette(elevation);
        assert.ok(
            luminance <= previous + 1e-9,
            `sky brightened as the sun fell, at ${elevation} degrees`
        );
        previous = luminance;
    }
});

test('the sky is continuous through dusk, with no visible step', () => {
    // Interpolated between neighbouring phases rather than switched, so there is no
    // frame where the whole background jumps.
    let previous = skyPalette(30).middle;
    for (let elevation = 30; elevation >= -25; elevation -= 0.25) {
        const current = skyPalette(elevation).middle;
        for (let channel = 0; channel < 3; channel++) {
            assert.ok(
                Math.abs(current[channel] - previous[channel]) < 6,
                `step of ${Math.abs(current[channel] - previous[channel])} at ${elevation} degrees`
            );
        }
        previous = current;
    }
});

test('it holds steady above daylight and below full night', () => {
    assert.deepEqual(skyPalette(90).middle, skyPalette(15).middle);
    assert.deepEqual(skyPalette(-18).middle, skyPalette(-60).middle);
});

test('the named phases run from day to night in order', () => {
    assert.deepEqual(phaseNames(), ['day', 'golden', 'civil', 'nautical', 'night']);
    const luminances = phaseNames().map((n) => phaseByName(n).luminance);
    for (let i = 1; i < luminances.length; i++) {
        assert.ok(luminances[i] < luminances[i - 1], `${phaseNames()[i]} is not darker`);
    }
});

test('the default palette is the daylight one', () => {
    assert.deepEqual(defaultPalette().middle, skyPalette(90).middle);
    assert.equal(defaultPalette().outline.light, false, 'daylight wants a dark outline');
});

// ---------- the outline ----------

test('the outline flips across the luminance threshold', () => {
    assert.equal(outlineFor(LIGHT_SKY_LUMINANCE + 0.01).light, false, 'light sky, dark outline');
    assert.equal(outlineFor(LIGHT_SKY_LUMINANCE - 0.01).light, true, 'dark sky, light outline');
});

test('the outline flips exactly once as the sun sets', () => {
    let flips = 0;
    let previous = skyPalette(30).outline.light;
    for (let elevation = 30; elevation >= -25; elevation -= 0.25) {
        const current = skyPalette(elevation).outline.light;
        if (current !== previous) flips++;
        previous = current;
    }
    assert.equal(flips, 1, 'the outline should not oscillate through dusk');
});

test('the outline is findable against the sky at every phase', () => {
    // This is the property the whole module exists for.
    for (const name of phaseNames()) {
        const palette = phaseByName(name);
        const ratio = contrastRatio(palette.outline.rgb, palette.middle);
        assert.ok(ratio >= USABLE_CONTRAST, `${name}: outline vs sky is only ${ratio.toFixed(2)}`);
    }
});

test('every aircraft category stays findable at every phase', () => {
    // A chip is found either by its fill standing out or by its outline doing so.
    // One of the two has to work at every phase, for every category.
    for (const name of phaseNames()) {
        const palette = phaseByName(name);
        const outlineRatio = contrastRatio(palette.outline.rgb, palette.middle);
        for (const [category, fill] of Object.entries(CATEGORY_FILLS)) {
            const fillRatio = contrastRatio(fill, palette.middle);
            assert.ok(
                Math.max(fillRatio, outlineRatio) >= USABLE_CONTRAST,
                `${category} at ${name}: fill ${fillRatio.toFixed(2)}, outline ${outlineRatio.toFixed(2)}`
            );
        }
    }
});

test('a fixed black outline would fail at night, which is why this is not fixed', () => {
    // The measurement that forced the change. Without it the sky could not be
    // tinted at all without losing traffic after dusk.
    const night = phaseByName('night');
    const black = contrastRatio([0, 0, 0], night.middle);
    assert.ok(black < 1.5, `black on a night sky is ${black.toFixed(2)}, expected near 1.1`);
    assert.ok(
        contrastRatio(night.outline.rgb, night.middle) > 10,
        'the adaptive outline is an order of magnitude better there'
    );
});

test('the darkest fills are the ones twilight would have swallowed', () => {
    // Civil twilight, not night, is the failure case: a mid-blue sky sits almost on
    // the dark end of the altitude ramp. Recorded here so the reason the outline
    // exists cannot be quietly lost.
    const civil = phaseByName('civil');
    assert.ok(contrastRatio(CATEGORY_FILLS.cruise, civil.middle) < 2, 'cruise fill vanishes');
    assert.ok(contrastRatio(CATEGORY_FILLS.military, civil.middle) < 2, 'military fill vanishes');
    assert.ok(
        contrastRatio(civil.outline.rgb, civil.middle) >= USABLE_CONTRAST,
        'but the outline carries them'
    );
});

// ---------- colour helpers ----------

test('luminance and contrast match their definitions at the extremes', () => {
    assert.ok(Math.abs(relativeLuminance([255, 255, 255]) - 1) < 1e-9);
    assert.equal(relativeLuminance([0, 0, 0]), 0);
    // White on black is the maximum the WCAG ratio can express.
    assert.ok(Math.abs(contrastRatio([255, 255, 255], [0, 0, 0]) - 21) < 0.01);
    assert.equal(contrastRatio([80, 80, 80], [80, 80, 80]), 1);
});

test('css renders with and without an alpha channel', () => {
    assert.equal(css([1, 2, 3]), 'rgb(1, 2, 3)');
    assert.equal(css([1, 2, 3], 0.5), 'rgba(1, 2, 3, 0.5)');
    // Interpolation produces fractions, and a canvas colour must not carry them.
    assert.equal(css([1.4, 2.5, 3.6]), 'rgb(1, 3, 4)');
});
