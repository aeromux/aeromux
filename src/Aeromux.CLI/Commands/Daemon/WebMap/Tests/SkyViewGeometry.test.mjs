// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Unit tests for the receiver-centric sky geometry. Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    bearingTo,
    elevationAndRange,
    horizon,
    minAltitudeAboveHorizonM,
    enuVector,
    cameraBasis,
    projectRectilinear,
    projectEquirect,
    verticalFovDeg,
    safeArea,
    pitchRange,
    clampPitch,
    chipSizePx,
    isSubHorizon,
    aircraftAltitudeM,
    destinationPoint,
    wrap180,
    bearingTickStep,
    bearingLabelStep,
    BEARING_STEPS,
    ribbonScaleNm,
    ribbonStepNm,
    RIBBON_SCALE_STEP_NM,
    receiverBox
} from '../Services/SkyViewGeometry.js';
import { nmToKm, haversineDistance, convertNauticalMiles } from '../Services/UnitConversion.js';

// Shorthand for the tolerance-based comparisons the geometry needs; the pinned
// figures below come from the documented worked examples, so a drift in the maths
// surfaces here rather than as a subtly wrong picture.
const near = (actual, expected, tolerance, label) => assert.ok(
    Math.abs(actual - expected) <= tolerance,
    `${label}: expected about ${expected}, got ${actual}`
);

const SAFE = safeArea(1400, 900, {});

// -------------------- bearingTo --------------------

test('cardinal bearings', () => {
    near(bearingTo(0, 0, 1, 0), 0, 0.01, 'north');
    near(bearingTo(0, 0, 0, 1), 90, 0.01, 'east');
    near(bearingTo(0, 0, -1, 0), 180, 0.01, 'south');
    near(bearingTo(0, 0, 0, -1), 270, 0.01, 'west');
});

test('bearing wraps across the antimeridian', () => {
    near(bearingTo(0, 179.5, 0, -179.5), 90, 0.01, 'eastbound across 180');
});

// -------------------- elevation and curvature --------------------

test('10 km at 300 km sits on the horizon, not at the flat-Earth angle', () => {
    const { elevationDeg } = elevationAndRange(300, 10000, 0);
    near(elevationDeg, 0.75, 0.01, 'refracted elevation');
    // The whole point of the correction: flat-Earth trigonometry claims 1.91.
    assert.ok(elevationDeg < 1.0, 'must not float above the horizon');
});

test('slant range exceeds ground range and matches the worked case', () => {
    const { slantRangeKm } = elevationAndRange(300, 10000, 0);
    near(slantRangeKm, 300.3, 0.1, 'slant range');
    assert.ok(slantRangeKm > 300, 'slant exceeds ground');
});

test('directly overhead is 90 degrees', () => {
    near(elevationAndRange(0, 10000, 0).elevationDeg, 90, 1e-6, 'overhead');
});

test('raising the receiver lowers every elevation angle monotonically', () => {
    const atSeaLevel = elevationAndRange(50, 3000, 0).elevationDeg;
    const at100m = elevationAndRange(50, 3000, 100).elevationDeg;
    const at500m = elevationAndRange(50, 3000, 500).elevationDeg;
    assert.ok(atSeaLevel > at100m && at100m > at500m);
});

test('an aircraft far away at receiver altitude is below the horizon', () => {
    assert.ok(elevationAndRange(100, 0, 0).elevationDeg < 0);
});

// -------------------- horizon --------------------

test('a sea-level receiver has a level horizon at zero distance', () => {
    const h = horizon(0);
    assert.equal(h.depressionDeg, 0);
    assert.equal(h.distanceKm, 0);
});

test('a 300 m receiver matches the documented horizon', () => {
    const h = horizon(300);
    near(-h.depressionDeg, 0.515, 0.01, 'depression');
    near(h.distanceKm, 66.8, 0.5, 'distance');
});

test('10 m and 50 m horizons are close enough to rule out a ground plane', () => {
    near(horizon(10).distanceKm, 12.2, 0.2, '10 m antenna');
    near(horizon(50).distanceKm, 27.3, 0.3, '50 m antenna');
});

test('minimum altitude above the horizon matches the documented figures', () => {
    const feet = (km) => minAltitudeAboveHorizonM(km, 0) * 3.28084;
    near(feet(nmToKm(150)), 17042, 50, '150 nm');
    near(feet(nmToKm(100)), 7572, 30, '100 nm');
    near(feet(nmToKm(50)), 1893, 10, '50 nm');
});

// -------------------- rectilinear projection --------------------

test('the camera axis lands at the safe-area centre', () => {
    const basis = cameraBasis(90, 0);
    const p = projectRectilinear(enuVector(90, 0), basis, SAFE, 75);
    near(p.x, SAFE.centreX, 1e-6, 'x');
    near(p.y, SAFE.centreY, 1e-6, 'y');
});

test('behind-camera points are culled', () => {
    const basis = cameraBasis(90, 0);
    assert.equal(projectRectilinear(enuVector(270, 0), basis, SAFE, 75), null);
});

test('points near the camera plane are culled, not projected absurdly', () => {
    const basis = cameraBasis(0, 0);
    // Ninety degrees off-axis: the forward component is about zero, so an
    // insufficient cull would divide by it instead of rejecting the point.
    assert.equal(projectRectilinear(enuVector(90, 0), basis, SAFE, 75), null);
    assert.equal(projectRectilinear(enuVector(270, 0), basis, SAFE, 75), null);
});

test('everything that does project lands within a sane multiple of the frame', () => {
    const basis = cameraBasis(0, 15);
    for (let az = 0; az < 360; az += 3) {
        for (const el of [0, 10, 30, 60, 85]) {
            const p = projectRectilinear(enuVector(az, el), basis, SAFE, 75);
            if (!p) continue;
            assert.ok(
                Math.abs(p.x) < 4 * SAFE.width && Math.abs(p.y) < 4 * SAFE.height,
                `azimuth ${az} elevation ${el} projected to ${p.x}, ${p.y}`
            );
        }
    }
});

test('minCos of zero projects an on-axis point without culling', () => {
    const basis = cameraBasis(0, 15);
    const p = projectRectilinear(enuVector(0, 0), basis, SAFE, 75, 0);
    assert.ok(p && Math.abs(p.x - SAFE.centreX) < 1e-6);
});

test('the field-of-view edge lands on the safe-area edge', () => {
    const basis = cameraBasis(0, 0);
    const p = projectRectilinear(enuVector(75 / 2, 0), basis, SAFE, 75);
    near(p.x, SAFE.right, 0.5, 'right edge');
});

test('safe-area insets move the principal point, not just the crop', () => {
    const inset = safeArea(1400, 900, { left: 436 });
    const basis = cameraBasis(90, 0);
    const p = projectRectilinear(enuVector(90, 0), basis, inset, 75);
    near(p.x, 436 + (1400 - 436) / 2, 1e-6, 'centre shifts clear of the panel');
    assert.ok(p.x > SAFE.centreX);
});

test('vertical extent follows the aspect ratio, so the sky does not always fit', () => {
    near(verticalFovDeg(75, safeArea(1400, 900, {})), 53, 1, 'desktop');
    near(verticalFovDeg(75, safeArea(1920, 1080, {})), 47, 1, '1080p');
    near(verticalFovDeg(75, safeArea(390, 700, {})), 108, 2, 'phone portrait');
    near(verticalFovDeg(75, safeArea(844, 390, {})), 39, 1, 'phone landscape');
});

// -------------------- equirectangular projection --------------------

test('flattened mode puts the horizon on the baseline and the zenith on top', () => {
    const yHorizon = 800;
    near(projectEquirect(0, 0, 0, SAFE, yHorizon).y, yHorizon, 1e-6, 'horizon');
    near(projectEquirect(0, 90, 0, SAFE, yHorizon).y, SAFE.top, 1e-6, 'zenith');
});

test('flattened mode is continuous across the antimeridian seam', () => {
    const left = projectEquirect(wrap180(179.999), 10, 0, SAFE, 800).x;
    const right = projectEquirect(wrap180(-179.999), 10, 0, SAFE, 800).x;
    near(Math.abs(left - right), SAFE.width, 1, 'opposite edges');
});

test('flattened mode honours heading so a bearing can be centred', () => {
    near(projectEquirect(120, 20, 120, SAFE, 800).x, SAFE.centreX, 1e-6, 'centred');
});

// -------------------- pitch range --------------------

test('pitch can reach level and widens as the field of view narrows', () => {
    const wide = pitchRange(75, SAFE);
    const narrow = pitchRange(30, SAFE);
    assert.equal(wide.min, 0, 'level is reachable');
    near(wide.max, 90 - 53.13 / 2, 0.5, 'maximum at 75 degrees');
    assert.ok(narrow.max > wide.max, 'a narrower view may tilt further');
});

test('clampPitch applies both bounds', () => {
    assert.equal(clampPitch(-10, 75, SAFE), 0);
    assert.equal(clampPitch(200, 75, SAFE), pitchRange(75, SAFE).max);
});

// -------------------- chip sizing --------------------

test('chip size decreases monotonically and clamps at both ends', () => {
    assert.equal(chipSizePx(1), 16);
    assert.equal(chipSizePx(2), 16);
    assert.equal(chipSizePx(300), 6);
    assert.equal(chipSizePx(900), 6);

    const sizes = [2, 10, 50, 150, 300].map(chipSizePx);
    for (let i = 1; i < sizes.length; i++) {
        assert.ok(sizes[i] < sizes[i - 1], 'strictly decreasing');
    }
});

// -------------------- sub-horizon classification --------------------

test('classification brackets the horizon exactly', () => {
    const depression = horizon(50).depressionDeg;
    assert.equal(isSubHorizon(depression + 0.01, depression), false);
    assert.equal(isSubHorizon(depression - 0.01, depression), true);
});

test('surface traffic is sub-horizon for a sea-level receiver at any range', () => {
    const depression = horizon(0).depressionDeg;
    for (const nm of [1, 5, 15, 40]) {
        const { elevationDeg } = elevationAndRange(nmToKm(nm), 0, 0);
        assert.ok(isSubHorizon(elevationDeg, depression), `${nm} nm`);
    }
});

// -------------------- altitude source --------------------

test('geometric altitude is preferred and barometric is the fallback', () => {
    assert.deepEqual(
        aircraftAltitudeM({
            GeometricAltitude: { Meters: 1000 },
            BarometricAltitude: { Meters: 900 }
        }),
        { metres: 1000, source: 'geometric' }
    );
    assert.deepEqual(
        aircraftAltitudeM({ GeometricAltitude: null, BarometricAltitude: { Meters: 900 } }),
        { metres: 900, source: 'barometric' }
    );
    assert.equal(aircraftAltitudeM({}), null);
});

// -------------------- destinationPoint --------------------

test('destinationPoint steps the right way and the right distance', () => {
    const north = destinationPoint(50, 8, 0, 100);
    assert.ok(north.Latitude > 50, 'moved north');
    near(north.Longitude, 8, 0.01, 'longitude held');
    near(haversineDistance(50, 8, north.Latitude, north.Longitude), 100, 0.5, 'distance north');

    const east = destinationPoint(50, 8, 90, 100);
    assert.ok(east.Longitude > 8, 'moved east');
    near(haversineDistance(50, 8, east.Latitude, east.Longitude), 100, 0.5, 'distance east');
});

// -------------------- compass spacing --------------------

test('every bearing step divides 90, so cardinals always land on a tick', () => {
    for (const step of BEARING_STEPS) {
        assert.equal(90 % step, 0, `${step} divides 90`);
    }
});

test('the tick interval keeps ticks at least the target apart', () => {
    // A degree covers plenty of pixels when zoomed in, so a fine interval fits.
    assert.equal(bearingTickStep(0.022, 26), 1);
    // The default camera view.
    assert.equal(bearingTickStep(0.063, 26), 2);
    // The flattened panorama on a desktop, and again on a phone where the same
    // 360 degrees are squeezed into a quarter of the width.
    assert.equal(bearingTickStep(0.257, 26), 10);
    assert.equal(bearingTickStep(0.923, 26), 30);

    for (const degPerPx of [0.02, 0.063, 0.14, 0.257, 0.6, 0.923]) {
        const step = bearingTickStep(degPerPx, 26);
        assert.ok(step / degPerPx >= 26, `${degPerPx}: ticks are not cramped`);
    }
});

test('the label interval is a multiple of the tick interval', () => {
    // Otherwise a number would land between two ticks rather than on one.
    for (const degPerPx of [0.02, 0.063, 0.14, 0.257, 0.6, 0.923]) {
        const tick = bearingTickStep(degPerPx, 26);
        const label = bearingLabelStep(tick, degPerPx, 90);
        assert.equal(label % tick, 0, `${degPerPx}: ${label} is a multiple of ${tick}`);
        assert.ok(label >= tick, 'and never finer than the ticks');
        assert.ok(label / degPerPx >= 90, 'numbers stay far enough apart to read');
    }
});

test('an extreme scale falls back to the coarsest step rather than failing', () => {
    // Nothing on the ladder is coarse enough; the answer must still be usable.
    assert.equal(bearingTickStep(50, 26), 90);
    assert.equal(bearingLabelStep(90, 50, 90), 90);
});

// -------------------- ribbon scale --------------------

test('the ribbon scale rounds a measured reach up to a fixed step', () => {
    assert.equal(ribbonScaleNm(70), 100);
    assert.equal(ribbonScaleNm(1), 50);
    assert.equal(ribbonScaleNm(120), 150);
    assert.equal(ribbonScaleNm(200), 200, 'an exact multiple is left alone');
});

test('the scale holds until a step boundary is crossed', () => {
    // The point of rounding: one distant contact must not rescale the whole
    // profile, which would change what a given block height means.
    const step = RIBBON_SCALE_STEP_NM;
    const scale = ribbonScaleNm(step + 10);
    for (const reach of [step + 10, step + 25, step * 2 - 0.1]) {
        assert.equal(ribbonScaleNm(reach), scale, `a reach of ${reach} holds the scale`);
    }
    assert.ok(ribbonScaleNm(step * 2 + 1) > scale, 'and it steps up once passed');
});

test('no coverage yields no scale, so nothing is drawn', () => {
    assert.equal(ribbonScaleNm(0), 0);
    assert.equal(ribbonScaleNm(-5), 0);
});

test('the scale steps in the unit it will be labeled in', () => {
    // The point of stepping per unit: a reach of 122 nm has to land on a number the
    // axis can print, whichever unit the user reads it in.
    const reach = 122;
    for (const [unit, expected] of [['nm', 150], ['km', 300], ['mi', 150]]) {
        const scale = ribbonScaleNm(reach, ribbonStepNm(unit));
        const label = convertNauticalMiles(scale, unit);
        assert.equal(label.value, expected, `${unit} scale`);
        assert.equal(label.label, unit);
        assert.ok(scale >= reach, `${unit} scale covers the measured reach`);
        // Halving it has to stay round too, since the axis carries a middle tick.
        assert.equal(convertNauticalMiles(scale / 2, unit).value, expected / 2);
    }
});

test('an unknown unit falls back to the kilometer step', () => {
    assert.equal(ribbonStepNm(undefined), ribbonStepNm('km'));
    assert.equal(ribbonStepNm('parsecs'), ribbonStepNm('km'));
});

// -------------------- receiverBox --------------------

test('a normal box is non-inverted and circumscribes the range circle', () => {
    const box = receiverBox(50, 8, nmToKm(150));
    assert.ok(box.west < box.east, 'west of east');
    assert.ok(box.south < box.north, 'south of north');

    // Poleward sizing makes the span wider than naive cos(lat) sizing would.
    const naiveSpan = 2 * nmToKm(150) / (111.32 * Math.cos(50 * Math.PI / 180));
    assert.ok(box.east - box.west > naiveSpan, 'poleward edge widens the box');
});

test('a receiver near the antimeridian never yields an inverted box', () => {
    for (const lon of [179, -179, 180, -180]) {
        const box = receiverBox(-18, lon, nmToKm(150));
        assert.ok(box.west <= box.east, `longitude ${lon} produced an inverted box`);
        assert.equal(box.west, -180, `longitude ${lon} west`);
        assert.equal(box.east, 180, `longitude ${lon} east`);
    }
});

test('a pole inside the box falls back to full-width bounds', () => {
    const box = receiverBox(84, 20, nmToKm(300));
    assert.equal(box.west, -180);
    assert.equal(box.east, 180);
    assert.ok(box.north <= 85 && box.south >= -85, 'latitude clamped');
});

test('high latitude stays non-inverted', () => {
    const box = receiverBox(70, 20, nmToKm(150));
    assert.ok(box.west <= box.east);
    assert.ok(box.west >= -180 && box.east <= 180, 'within longitude range');
});
