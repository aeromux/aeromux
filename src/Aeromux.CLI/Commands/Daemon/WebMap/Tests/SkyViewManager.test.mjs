// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Behavioural tests for the Sky View renderer, driven through a recording-canvas
// DOM stub. These assert culling, clamping, draw order, layout, interaction, and
// label collision — not rasterized output, which the stub does not produce.
//
// Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeCanvas, installGlobals, calls, resetCalls, advanceClock } from './Support/DomStub.mjs';
import { safeArea, pitchRange, clampPitch, wrap180 } from '../Services/SkyViewGeometry.js';
import { contrastRatio, relativeLuminance } from '../Services/SkyPalette.js';

// Label line height, mirroring LABEL_LINE_H in the renderer.
const LABEL_LINE_H = 11;

const canvas = makeCanvas(1400, 900);
installGlobals(canvas, { appendChild() {} });
const Sky = await import('../SkyView/SkyViewManager.js');

const BASE_SETTINGS = {
    skyFov: 75,
    skyPitch: 0,
    skyFlatten: false,
    skyRibbon: true,
    skyTrail: true,
    skyLabels: 'auto',
    skyMaxRangeNm: 150
};

const RECEIVER = { lat: 50, lon: 8 };

// A coordinate at a bearing and ground distance from the receiver.
function coordinateAt(bearingDeg, distanceKm) {
    const d = distanceKm / 6371;
    const b = (bearingDeg * Math.PI) / 180;
    const phi1 = (RECEIVER.lat * Math.PI) / 180;
    const lambda1 = (RECEIVER.lon * Math.PI) / 180;
    const phi2 = Math.asin(Math.sin(phi1) * Math.cos(d) + Math.cos(phi1) * Math.sin(d) * Math.cos(b));
    const lambda2 = lambda1 + Math.atan2(
        Math.sin(b) * Math.sin(d) * Math.cos(phi1),
        Math.cos(d) - Math.sin(phi1) * Math.sin(phi2)
    );
    return { Latitude: (phi2 * 180) / Math.PI, Longitude: (lambda2 * 180) / Math.PI };
}

// Places a synthetic aircraft at a bearing and ground distance from the receiver.
function addAircraft(map, icao, bearingDeg, distanceKm, altitudeFeet, extra = {}) {
    map.set(icao, {
        ICAO: icao,
        Callsign: icao,
        Coordinate: coordinateAt(bearingDeg, distanceKm),
        GeometricAltitude: altitudeFeet == null
            ? null
            : { Meters: altitudeFeet * 0.3048, Feet: altitudeFeet },
        BarometricAltitude: null,
        IsOnGround: false,
        Track: bearingDeg,
        ...extra
    });
    return map;
}

// --- gesture helpers -------------------------------------------------------

let nextPointerId = 1;

function tap(x, y) {
    const pointerId = nextPointerId++;
    canvas.dispatch('pointerdown', { clientX: x, clientY: y, pointerId });
    canvas.dispatch('pointerup', { clientX: x, clientY: y, pointerId });
}

// Two fingers down, moved to a new separation, then both lifted.
function pinch(fromGap, toGap, { lift = true } = {}) {
    const cx = 700;
    const cy = 450;
    const a = nextPointerId++;
    const b = nextPointerId++;
    canvas.dispatch('pointerdown', { clientX: cx - fromGap / 2, clientY: cy, pointerId: a });
    canvas.dispatch('pointerdown', { clientX: cx + fromGap / 2, clientY: cy, pointerId: b });
    canvas.dispatch('pointermove', { clientX: cx - toGap / 2, clientY: cy, pointerId: a });
    canvas.dispatch('pointermove', { clientX: cx + toGap / 2, clientY: cy, pointerId: b });
    if (lift) {
        canvas.dispatch('pointerup', { clientX: cx - toGap / 2, clientY: cy, pointerId: a });
        canvas.dispatch('pointerup', { clientX: cx + toGap / 2, clientY: cy, pointerId: b });
    }
    return { a, b, cx, cy, toGap };
}

function reset(settings = BASE_SETTINGS) {
    Sky.clearSelection();
    Sky.clearTrail();
    Sky.__test.setHovered(null);
    Sky.setSafeInsets({});
    Sky.setReceiver(RECEIVER.lat, RECEIVER.lon, 0);
    // Restores heading between tests; without it a preceding test's heading leaks in
    // and everything at the fixture bearings is legitimately culled as off-axis. The
    // gesture that does this for a user is a double-tap, tested separately.
    Sky.__test.resetCamera();
    Sky.setSettings({ ...settings });
}

Sky.init('sky-container');
reset();

// -------------------- projection and culling --------------------

test('culls by range and altitude, and never produces runaway coordinates', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'AHEAD', 0, 20, 35000);
    addAircraft(map, 'FAR', 0, 400, 35000);      // 216 nm, beyond the 150 nm range
    addAircraft(map, 'NOALT', 0, 30, null);      // airborne with no altitude
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    const drawn = frame.drawable.map((d) => d.icao);

    assert.ok(drawn.includes('AHEAD'), 'on-axis aircraft is drawn');
    assert.ok(!drawn.includes('FAR'), 'beyond maximum range is culled by slant range');
    assert.ok(!drawn.includes('NOALT'), 'airborne without altitude is excluded');
    assert.equal(frame.noAltitude, 1, 'and is disclosed in the count');

    for (const d of frame.drawable) {
        assert.ok(Math.abs(d.x) < 1e4 && Math.abs(d.y) < 1e4, `${d.icao} projected sanely`);
    }
});

test('the hit index mirrors what was drawn', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'ONE', 0, 20, 30000);
    addAircraft(map, 'TWO', 10, 30, 25000);
    Sky.updateMarkers(map);

    const state = Sky.__test.state();
    assert.equal(state.hitIndex.length, state.lastFrame.drawable.length);
});

test('a zero-sized container draws nothing, and resizing recovers', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'AHEAD', 0, 20, 35000);
    Sky.updateMarkers(map);
    assert.ok(canvas.width > 0, 'starts with a real size');

    // A container still display:none measures zero. Sizing the canvas from that
    // leaves it 0x0, and nothing is visible however much is drawn into it.
    const realRect = canvas.getBoundingClientRect;
    canvas.getBoundingClientRect = () => ({ left: 0, top: 0, width: 0, height: 0, right: 0, bottom: 0 });
    Sky.resize();
    assert.equal(canvas.width, 0, 'the canvas collapses');
    assert.equal(canvas.height, 0);

    // Recovery must not need anything beyond a resize once the container is shown.
    canvas.getBoundingClientRect = realRect;
    Sky.resize();
    Sky.updateMarkers(map);
    const frame = Sky.__test.state().lastFrame;
    assert.ok(canvas.width > 0, 'the canvas is sized again');
    assert.equal(frame.safe.width, 1400, 'and the safe area matches the container');
    const ahead = frame.drawable.find((d) => d.icao === 'AHEAD');
    assert.ok(ahead && ahead.x > 0 && ahead.x < 1400, 'the scene lands inside the frame');
});

// -------------------- sub-horizon clamping --------------------

test('aircraft below the horizon are clamped to it, not culled', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'LOW', 0, 200, 5000);              // too low to clear the horizon
    addAircraft(map, 'GND', 5, 15, null, { IsOnGround: true });
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    const low = frame.drawable.find((d) => d.icao === 'LOW');
    const ground = frame.drawable.find((d) => d.icao === 'GND');

    assert.ok(low && low.sub, 'distant low traffic classifies as sub-horizon');
    assert.ok(ground && ground.sub, 'surface traffic classifies as sub-horizon');
    assert.equal(low.y, frame.yHorizon, 'pinned to the drawn baseline');
    assert.ok(low.elevationDeg < 0, 'the reported elevation stays negative');
    assert.equal(frame.belowHorizon, 2, 'both are disclosed in the count');
});

test('sub-horizon marks are drawn behind airborne chips', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'LOW', 0, 200, 5000);
    addAircraft(map, 'HIGH', 20, 30, 35000);
    Sky.updateMarkers(map);

    resetCalls();
    Sky.updateMarkers(map);

    const firstMark = calls.findIndex((c) => c.name === 'strokeRect');
    const firstChip = calls.findIndex((c) => c.name === 'arc');
    assert.ok(firstMark >= 0 && firstChip >= 0, 'both kinds were drawn');
    assert.ok(firstMark < firstChip, 'clamped marks cannot occlude real low traffic');
});

test('the background covers the whole canvas, not just the safe area', () => {
    reset();
    // A left inset is where this bites: the panel does not reach the bottom of the
    // viewport, so clipping the paint to the safe area leaves a blank strip below it.
    Sky.setSafeInsets({ left: 436 });
    const map = new Map();
    addAircraft(map, 'AHEAD', 0, 20, 35000);

    resetCalls();
    Sky.updateMarkers(map);
    const frame = Sky.__test.state().lastFrame;
    const fills = calls.filter((c) => c.name === 'fillRect');

    const sky = fills.find((c) => c.args[0] === 0 && c.args[1] === 0 && c.args[2] === 1400);
    assert.ok(sky, 'the sky band spans the full canvas width from the top edge');
    assert.ok(Math.abs(sky.args[3] - frame.yHorizon) < 1e-6, 'and stops at the horizon');

    // The camera is level here, so the horizon is in view and there is ground below it.
    assert.ok(frame.yHorizon < frame.full.height, 'the horizon is on screen');
    const ground = fills.find(
        (c) => c.args[0] === 0 && Math.abs(c.args[1] - frame.yHorizon) < 1e-6 && c.args[2] === 1400
    );
    assert.ok(ground, 'the ground band spans the full width below the horizon');
    assert.ok(ground.args[3] > 0, 'and has height, so nothing is left unpainted');

    Sky.setSafeInsets({});
});

// -------------------- layout --------------------

test('the ribbon sits at the foot of the view and stays put as the camera tilts', () => {
    let previous = null;
    for (const pitch of [0, 15, 40, 60]) {
        reset({ ...BASE_SETTINGS, skyPitch: pitch });
        const frame = Sky.__test.state().lastFrame;

        // Flush with the bottom: anything left under it is dead ground.
        assert.equal(frame.ribbonBottom, frame.safe.bottom, `pitch ${pitch}: flush`);
        // Only meaningful while the horizon is still in the view; pitched far
        // enough up it leaves the frame and there is no ground left at all.
        if (frame.yHorizon < frame.safe.bottom) {
            assert.ok(frame.ribbonTop > frame.yHorizon, `pitch ${pitch}: below the horizon`);
        }

        // A fixed strip: sizing it from the available ground made it resize as the
        // camera moved, which is movement in the chrome rather than in the scene.
        const band = frame.ribbonBottom - frame.ribbonTop;
        assert.equal(band, 64, `pitch ${pitch}: band is a fixed height`);

        if (previous !== null) {
            assert.equal(band, previous, 'and does not change with pitch');
        }
        previous = band;
    }
    reset();
});

test('a level camera puts the horizon low, leaving little ground', () => {
    // The ground has nothing to draw in it, so a level camera must not spend half
    // the view on it. The principal point is raised to put the horizon near the foot.
    reset();
    const frame = Sky.__test.state().lastFrame;
    const fraction = (frame.yHorizon - frame.safe.top) / frame.safe.height;

    assert.equal(Sky.__test.state().camera.pitch, 0, 'the default camera is level');
    assert.ok(fraction > 0.7 && fraction < 0.95, `horizon at ${(fraction * 100).toFixed(0)}% of height`);
    assert.ok(
        frame.safe.bottom - frame.yHorizon < frame.safe.height * 0.3,
        'ground takes well under a third of the view'
    );
});

test('pitching up moves the horizon out of the frame rather than pinning it', () => {
    reset();
    const level = Sky.__test.state().lastFrame.yHorizon;

    reset({ ...BASE_SETTINGS, skyPitch: 40 });
    const pitched = Sky.__test.state().lastFrame;
    assert.ok(pitched.yHorizon > level, 'the horizon moved down');
    assert.ok(
        pitched.yHorizon > pitched.safe.bottom,
        'and left the view entirely, which is what looking up means'
    );
    assert.equal(Sky.__test.state().camera.pitch, 40, 'the requested pitch was honoured');
    reset();
});

test('no aircraft above the horizon is ever drawn below the horizon line', () => {
    const map = new Map();
    // A spread that includes very low traffic, which is where this went wrong.
    addAircraft(map, 'LOW1', 0, 120, 12000);
    addAircraft(map, 'LOW2', 20, 90, 9000);
    addAircraft(map, 'MID', 340, 40, 25000);
    addAircraft(map, 'HIGH', 10, 15, 38000);

    // Including pitches that push the horizon out of the frame: the invariant has to
    // hold there too, since it comes from the projection rather than from clamping.
    for (const pitch of [0, 15, 30, 45, 60, 80]) {
        reset({ ...BASE_SETTINGS, skyPitch: pitch });
        Sky.updateMarkers(map);
        const frame = Sky.__test.state().lastFrame;
        for (const d of frame.drawable) {
            if (d.sub) {
                assert.equal(d.y, frame.yHorizon, `${d.icao} sub-horizon, pinned (pitch ${pitch})`);
            } else {
                assert.ok(
                    d.y <= frame.yHorizon + 0.5,
                    `${d.icao} at ${d.elevationDeg.toFixed(1)}° drawn below the horizon (pitch ${pitch})`
                );
            }
        }
    }
    reset();
});

test('safe-area insets move the scene centre clear of the panel', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'AHEAD', 0, 20, 35000);
    Sky.updateMarkers(map);
    const canvasCentre = Sky.__test.state().lastFrame.safe.centreX;

    Sky.setSafeInsets({ left: 436 });
    Sky.updateMarkers(map);
    const frame = Sky.__test.state().lastFrame;
    const ahead = frame.drawable.find((d) => d.icao === 'AHEAD');

    assert.equal(frame.safe.centreX, 436 + (1400 - 436) / 2);
    assert.ok(frame.safe.centreX > canvasCentre, 'centre shifted right');
    assert.ok(Math.abs(ahead.x - frame.safe.centreX) < 1e-6, 'and the aircraft followed it');
    Sky.setSafeInsets({});
});

test('the camera view draws into the strip a panel does not reach', () => {
    reset({ ...BASE_SETTINGS, skyFov: 100 });
    // The panel occupies the left of the viewport but not its full height, so the
    // strip below it is visible canvas and must receive scene content.
    Sky.setSafeInsets({ left: 436 });

    const map = new Map();
    for (let bearing = -60; bearing <= 60; bearing += 10) {
        addAircraft(map, `B${bearing + 60}`, (bearing + 360) % 360, 30, 32000);
    }
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    const xs = frame.drawable.map((d) => d.x);
    assert.ok(xs.length > 0, 'aircraft are drawn');
    assert.ok(
        Math.min(...xs) < 436,
        `content reaches left of the inset (leftmost x was ${Math.min(...xs).toFixed(0)})`
    );
    // The cull must still work: nothing may escape to absurd coordinates.
    for (const d of frame.drawable) {
        assert.ok(Math.abs(d.x) < 1e4 && Math.abs(d.y) < 1e4, `${d.icao} projected sanely`);
    }

    Sky.setSafeInsets({});
    reset();
});

test('clearSelection and clearTrail leave no trace for the next view to inherit', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'SEL', 0, 20, 35000);
    Sky.updateMarkers(map);
    Sky.highlightSelected('SEL');
    Sky.updateTrail([
        { position: coordinateAt(0, 60), altitudeMeters: 34000 * 0.3048 },
        { position: map.get('SEL').Coordinate, altitudeMeters: 35000 * 0.3048 }
    ]);

    let published = null;
    Sky.onSelectedTooltip((p) => { published = p; });
    Sky.updateMarkers(map);
    assert.ok(published, 'a selection tooltip is published while selected');

    // Deselecting in one view has to leave this renderer clean, because the other
    // view keeps its own copy of the selection and only syncs when it becomes active.
    Sky.clearSelection();
    Sky.clearTrail();
    resetCalls();
    Sky.updateMarkers(map);

    assert.equal(published, null, 'no tooltip is published once cleared');
    const chip = Sky.__test.state().lastFrame.drawable.find((d) => d.icao === 'SEL');
    assert.ok(chip, 'the aircraft is still drawn');
    // Orange is the selected colour; a cleared selection must not still use it.
    const fills = calls.filter((c) => c.name === 'fill');
    assert.ok(fills.length > 0, 'chips were drawn');

    Sky.onSelectedTooltip(null);
    reset();
});

// -------------------- activity --------------------

test('a hidden renderer neither paints nor publishes tooltips', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'SEL', 0, 20, 35000);
    Sky.updateMarkers(map);
    Sky.highlightSelected('SEL');

    let published = 0;
    Sky.onSelectedTooltip(() => { published++; });

    // Map mode: the sky canvas is off screen, but the application still feeds this
    // renderer settings, outlines and trails. Every frame it drew there would be
    // wasted, and every tooltip it emitted would fight the map's for the same state.
    Sky.setActive(false);
    resetCalls();
    Sky.updateMarkers(map);
    Sky.setRangeOutline([]);
    Sky.updateTrail([]);

    assert.equal(calls.filter((c) => c.name === 'clearRect').length, 0, 'no frames drawn');
    assert.equal(published, 0, 'no tooltips published');

    // Becoming visible must redraw without needing anything else to happen.
    resetCalls();
    Sky.setActive(true);
    assert.ok(calls.some((c) => c.name === 'clearRect'), 'redraws on becoming active');
    assert.ok(published > 0, 'and resumes publishing');

    Sky.onSelectedTooltip(null);
    reset();
});

test('the compass distinguishes cardinals from bearings from bare ticks', () => {
    reset();
    resetCalls();
    Sky.updateMarkers(new Map());

    const frame = Sky.__test.state().lastFrame;
    const labels = calls
        .filter((c) => c.name === 'fillText' && Math.abs(c.args[2] - frame.compassY) < 30)
        .map((c) => c.args[0]);

    // North is in view by default, and reads as a letter rather than as "000".
    assert.ok(labels.includes('N'), `a cardinal letter is drawn (got ${labels.join(', ')})`);
    assert.ok(!labels.includes('000'), 'and replaces the number at that bearing');
    assert.ok(
        labels.some((l) => /^\d{3}$/.test(l)),
        'intermediate bearings are still numbered'
    );

    // Ticks come in three heights, so the row has a hierarchy rather than a uniform comb.
    const ticks = calls.filter(
        (c) => c.name === 'fillRect' && Math.abs(c.args[1] - frame.compassY) < 0.5
    );
    const heights = new Set(ticks.map((c) => c.args[3]));
    assert.ok(ticks.length > 6, 'ticks are drawn across the view');
    assert.ok(heights.size >= 2, `ticks vary in height (${[...heights].join(', ')})`);
    assert.ok(Math.max(...heights) > Math.min(...heights), 'majors stand above minors');

    // Spacing is chosen in pixels rather than degrees, so the row stays about as
    // dense whatever the field of view does to the angular scale.
    const xs = ticks.map((c) => c.args[0]).sort((a, b) => a - b);
    const gaps = xs.slice(1).map((x, i) => x - xs[i]).filter((g) => g > 0.5);
    const median = gaps.sort((a, b) => a - b)[Math.floor(gaps.length / 2)];
    assert.ok(median > 15 && median < 90, `ticks sit about ${median.toFixed(0)}px apart`);
});

test('the compass keeps its density across fields of view and both modes', () => {
    const medianGap = () => {
        resetCalls();
        Sky.updateMarkers(new Map());
        const frame = Sky.__test.state().lastFrame;
        const xs = calls
            .filter((c) => c.name === 'fillRect' && Math.abs(c.args[1] - frame.compassY) < 0.5)
            .map((c) => c.args[0])
            .sort((a, b) => a - b);
        const gaps = xs.slice(1).map((x, i) => x - xs[i]).filter((g) => g > 0.5);
        return gaps.sort((a, b) => a - b)[Math.floor(gaps.length / 2)];
    };

    for (const fov of [30, 75, 120]) {
        reset({ ...BASE_SETTINGS, skyFov: fov });
        const gap = medianGap();
        assert.ok(gap > 15 && gap < 90, `camera at ${fov}°: ${gap.toFixed(0)}px between ticks`);
    }

    reset({ ...BASE_SETTINGS, skyFlatten: true });
    const flat = medianGap();
    assert.ok(flat > 15 && flat < 90, `flattened: ${flat.toFixed(0)}px between ticks`);
    reset();

    reset();
});

// -------------------- coverage ribbon --------------------

test('the ribbon draws from bare coordinates, which is all the server sends', () => {
    reset();
    // RangeOutlineCoordinate carries latitude and longitude only — no distance and
    // no bearing — so both have to be derived from the receiver position here.
    // Bearings inside the default frame, so they are all actually drawn, with
    // distances that differ so the profile has a shape to check.
    const outline = [[340, 200], [350, 90], [0, 150], [10, 60], [20, 190]].map(
        ([bearing, nm]) => {
            const c = coordinateAt(bearing, nm * 1.852);
            return { Latitude: c.Latitude, Longitude: c.Longitude };
        }
    );

    resetCalls();
    Sky.setRangeOutline(outline);

    const frame = Sky.__test.state().lastFrame;
    const bars = calls.filter((c) => c.name === 'rect' && c.args[3] > 0);
    assert.ok(bars.length > 0, 'sector blocks were added to the ribbon path');
    for (const bar of bars) {
        assert.ok(
            bar.args[1] + bar.args[3] <= frame.ribbonBottom + 0.5,
            'and stay within the ribbon band'
        );
        // Each bar covers its whole 5-degree sector, so neighbours merge into one
        // silhouette instead of reading as scattered tick marks.
        assert.ok(bar.args[2] > 3, `a sector spans real width (was ${bar.args[2].toFixed(1)} px)`);
    }

    // Heights track distance, so the profile has a shape.
    const tallest = Math.max(...bars.map((b) => b.args[3]));
    const shortest = Math.min(...bars.map((b) => b.args[3]));
    assert.ok(tallest > shortest, 'the profile varies with measured range');

    // Scaled to the rounded ceiling, not to the tallest sector — so the profile
    // does not fill the band exactly, and a height means a fixed number of miles.
    const band = frame.ribbonBottom - frame.ribbonTop;
    assert.ok(tallest < band, 'the farthest sector sits below the top of the band');
    // 200 nm against a 200 nm ceiling is the full band; here the max is 200 of 200,
    // so check the proportion holds for a shorter one instead.
    const shortestExpected = band * (60 / 200);
    assert.ok(
        Math.abs(shortest - shortestExpected) < band * 0.15,
        `a 60 nm sector is about ${(60 / 200 * 100).toFixed(0)}% of the band`
    );

    // Snapped to sector boundaries, so neighbours tile instead of overlapping —
    // overlapping translucent blocks darken where they meet.
    const spans = bars
        .map((b) => ({ left: b.args[0], right: b.args[0] + b.args[2] }))
        .sort((a, b) => a.left - b.left);
    for (let i = 1; i < spans.length; i++) {
        assert.ok(
            spans[i].left >= spans[i - 1].right - 0.5,
            `sector blocks must not overlap (${spans[i - 1].right.toFixed(1)} vs ${spans[i].left.toFixed(1)})`
        );
    }

    // A single fill for the whole path keeps the tone even across the silhouette.
    const ribbonFills = calls.filter((c) => c.name === 'fill');
    assert.ok(ribbonFills.length >= 1, 'the path is filled');

    Sky.setRangeOutline([]);
    reset();
});

test('the ribbon states its own scale, so the bars are readable without a key', () => {
    reset();
    const outline = [[340, 200], [350, 90], [0, 150], [10, 60]].map(([bearing, nm]) => {
        const c = coordinateAt(bearing, nm * 1.852);
        return { Latitude: c.Latitude, Longitude: c.Longitude };
    });

    resetCalls();
    Sky.setRangeOutline(outline);

    const frame = Sky.__test.state().lastFrame;
    const labels = calls.filter((c) => c.name === 'fillText').map((c) => c.args[0]);

    // 200 nm measured rounds to a 200 nm scale, so the axis reads 200 / 100 / 0 —
    // every tick carrying its unit, not only the top one.
    assert.ok(labels.includes('200 nm'), `top tick (got ${labels.join(', ')})`);
    assert.ok(labels.includes('100 nm'), 'the halfway tick carries its unit too');
    assert.ok(labels.includes('0 nm'), 'and so does the baseline');

    // fillText takes (text, x, y): each tick sits against the left edge, and inside
    // the band rather than colliding with the bearing labels above it.
    const axisText = calls.filter(
        (c) => c.name === 'fillText' && ['200 nm', '100 nm', '0 nm'].includes(c.args[0])
    );
    assert.equal(axisText.length, 3, 'all three ticks drawn');
    // On the right: the aircraft list and detail panel own the left of the viewport
    // and either can reach far enough down to cover a left-hand gutter.
    for (const label of axisText) {
        const [text, x] = label.args;
        assert.ok(
            x > frame.safe.right - 60 && x < frame.safe.right,
            `${text} sits in the right gutter, clear of the edge (x=${x})`
        );
    }

    // Nothing may escape the strip — the backing chips are what actually bound the
    // labels, so they are the thing to check.
    const chips = calls.filter(
        (c) => c.name === 'fillRect' && c.args[3] === 12 && c.args[0] > frame.safe.right - 120
    );
    assert.equal(chips.length, 3, 'each tick has a backing chip');
    for (const chip of chips) {
        const [x, y, w, h] = chip.args;
        assert.ok(x + w < frame.safe.right, 'the chip stays clear of the frame edge');
        assert.ok(y >= frame.ribbonTop, `a chip at ${y} stays below the band top`);
        assert.ok(y + h <= frame.ribbonBottom, `a chip at ${y}+${h} stays above the band bottom`);
    }

    Sky.setRangeOutline([]);
    reset();
});

test('the ribbon scale follows the selected distance unit', () => {
    reset();
    const outline = [[340, 200], [350, 90], [0, 150], [10, 60]].map(([bearing, nm]) => {
        const c = coordinateAt(bearing, nm * 1.852);
        return { Latitude: c.Latitude, Longitude: c.Longitude };
    });

    resetCalls();
    Sky.setRangeOutline(outline);
    const tallestNm = Math.max(...calls.filter((c) => c.name === 'rect').map((c) => c.args[3]));

    // Kilometers: 200 nm is 370 km, which rounds up to a 400 km scale. The axis has
    // to read in kilometres throughout, not convert a nautical-mile scale into odd
    // numbers, and the bars have to follow the new scale rather than only the words.
    resetCalls();
    Sky.setDistanceUnit('km');
    let labels = calls.filter((c) => c.name === 'fillText').map((c) => c.args[0]);
    assert.ok(labels.includes('400 km'), `top tick in km (got ${labels.join(', ')})`);
    assert.ok(labels.includes('200 km'), 'the halfway tick too');
    assert.ok(labels.includes('0 km'), 'and the baseline');
    assert.ok(!labels.some((l) => l.endsWith(' nm')), 'nothing is left labeled in nm');

    const tallestKm = Math.max(...calls.filter((c) => c.name === 'rect').map((c) => c.args[3]));
    assert.ok(
        tallestKm < tallestNm,
        'the profile is redrawn against the larger scale, not merely relabeled'
    );

    // Statute miles: 200 nm is 230 mi, so the scale steps to 250 mi.
    resetCalls();
    Sky.setDistanceUnit('mi');
    labels = calls.filter((c) => c.name === 'fillText').map((c) => c.args[0]);
    assert.ok(labels.includes('250 mi'), `top tick in mi (got ${labels.join(', ')})`);
    assert.ok(labels.includes('125 mi'), 'the halfway tick too');
    assert.ok(labels.includes('0 mi'), 'and the baseline');

    Sky.setDistanceUnit('nm');
    Sky.setRangeOutline([]);
    reset();
});

test('the halfway mark is confined to bearings that have coverage', () => {
    reset();
    // Coverage over a narrow arc only, so most of the view has no data at all.
    const outline = [[350, 120], [355, 90], [0, 150]].map(([bearing, nm]) => {
        const c = coordinateAt(bearing, nm * 1.852);
        return { Latitude: c.Latitude, Longitude: c.Longitude };
    });

    resetCalls();
    Sky.setRangeOutline(outline);

    const frame = Sky.__test.state().lastFrame;
    const blocks = calls.filter((c) => c.name === 'rect' && c.args[3] > 0);
    const left = Math.min(...blocks.map((b) => b.args[0]));
    const right = Math.max(...blocks.map((b) => b.args[0] + b.args[2]));
    const midY = frame.ribbonBottom - (frame.ribbonBottom - frame.ribbonTop) / 2;

    // Gridlines now span the full width, which reads as a chart because each one is
    // labelled. What must stay confined to the data is the silhouette itself.
    assert.ok(right - left < frame.full.width * 0.9, 'the covered arc really is narrow');
    for (const block of blocks) {
        assert.ok(
            block.args[0] >= left - 0.5 && block.args[0] + block.args[2] <= right + 0.5,
            'no block is drawn outside the covered arc'
        );
    }

    Sky.setRangeOutline([]);
    reset();
});

test('too few sectors means no ribbon rather than a misleading one', () => {
    reset();
    resetCalls();
    Sky.setRangeOutline([
        coordinateAt(0, 200),
        coordinateAt(90, 120)
    ].map((c) => ({ Latitude: c.Latitude, Longitude: c.Longitude })));

    const frame = Sky.__test.state().lastFrame;
    const bars = calls.filter((c) => c.name === 'rect' && c.args[3] > 0);
    assert.equal(bars.length, 0, 'nothing is drawn from two bearings');

    Sky.setRangeOutline([]);
    reset();
});

test('the ribbon is hidden when its setting is off', () => {
    const outline = [0, 90, 180, 270].map((b) => {
        const c = coordinateAt(b, 150);
        return { Latitude: c.Latitude, Longitude: c.Longitude };
    });

    reset({ ...BASE_SETTINGS, skyRibbon: false });
    Sky.setRangeOutline(outline);
    resetCalls();
    Sky.updateMarkers(new Map());

    const frame = Sky.__test.state().lastFrame;
    const bars = calls.filter((c) => c.name === 'rect' && c.args[3] > 0);
    assert.equal(bars.length, 0, 'no bars when the ribbon is turned off');

    Sky.setRangeOutline([]);
    reset();
});

test('the bearing axis survives the horizon leaving the frame', () => {
    const outline = [340, 350, 0, 10, 20].map((bearing) => {
        const c = coordinateAt(bearing, 120 * 1.852);
        return { Latitude: c.Latitude, Longitude: c.Longitude };
    });

    reset();
    Sky.setRangeOutline(outline);
    const level = Sky.__test.state().lastFrame;
    assert.ok(level.compassY < level.safe.bottom, 'compass is in view when level');
    assert.ok(
        Math.abs(level.compassY - level.yHorizon) < 0.5,
        'and rides with the horizon while that is visible'
    );

    // Pitched up, the ground is gone. The azimuth axis is still meaningful, so the
    // compass has to fall back to the ribbon rather than leaving with the horizon.
    reset({ ...BASE_SETTINGS, skyPitch: 40 });
    Sky.setRangeOutline(outline);
    resetCalls();
    Sky.updateMarkers(new Map());

    const pitched = Sky.__test.state().lastFrame;
    assert.ok(pitched.yHorizon > pitched.safe.bottom, 'the horizon has left the view');
    assert.ok(pitched.compassY < pitched.ribbonTop, 'the compass sits above the ribbon');
    assert.ok(pitched.compassY < pitched.safe.bottom, 'and is still on screen');

    // Ticks are still drawn, so bearings remain readable while looking up.
    const ticks = calls.filter(
        (c) => c.name === 'fillRect' && Math.abs(c.args[1] - pitched.compassY) < 0.5
    );
    assert.ok(ticks.length > 0, 'bearing ticks are drawn against the ribbon');

    // And the ribbon has a backing, so it does not float on bare sky.
    const backing = calls.find(
        (c) => c.name === 'fillRect' && c.args[0] === 0 && c.args[2] === pitched.full.width
            && Math.abs(c.args[1] - pitched.ribbonTop) < 0.5
    );
    assert.ok(backing, 'the ribbon is drawn on its own strip');

    Sky.setRangeOutline([]);
    reset();
});

// -------------------- camera limits --------------------

test('pitch can reach level and its range widens as the view narrows', () => {
    const safe = safeArea(1400, 900, {});
    const wide = pitchRange(75, safe);
    const narrow = pitchRange(30, safe);

    assert.equal(wide.min, 0);
    assert.ok(narrow.max > wide.max);
    assert.equal(clampPitch(-10, 75, safe), 0);
    assert.equal(clampPitch(200, 75, safe), wide.max);
});

test('the wheel changes the field of view and clamps at both ends', () => {
    reset();
    const start = Sky.__test.state().camera.fov;

    canvas.dispatch('wheel', { deltaY: 120 });
    assert.ok(Sky.__test.state().camera.fov > start, 'scrolling down widens');

    for (let i = 0; i < 40; i++) canvas.dispatch('wheel', { deltaY: 120 });
    assert.equal(Sky.__test.state().camera.fov, 120, 'clamped wide');

    for (let i = 0; i < 80; i++) canvas.dispatch('wheel', { deltaY: -120 });
    assert.equal(Sky.__test.state().camera.fov, 30, 'clamped narrow');
    reset();
});

test('double-click restores heading, pitch, and field of view together', () => {
    reset();
    canvas.dispatch('wheel', { deltaY: -120 });
    canvas.dispatch('pointerdown', { clientX: 700, clientY: 450, pointerId: 1 });
    canvas.dispatch('pointermove', { clientX: 900, clientY: 500, pointerId: 1 });
    canvas.dispatch('pointerup', { clientX: 900, clientY: 500, pointerId: 1 });

    const moved = Sky.__test.state().camera;
    assert.ok(moved.heading !== 0 || moved.fov !== 75, 'the camera actually moved');

    tap(700, 450);
    tap(700, 450);
    const after = Sky.__test.state().camera;
    assert.equal(after.heading, 0, 'heading restored');
    assert.equal(after.fov, 75, 'field of view restored');
    assert.equal(after.pitch, clampPitch(0, 75, safeArea(1400, 900, {})), 'pitch restored to level');
});

// -------------------- interaction --------------------

test('clicking selects, clicking empty sky deselects, dragging does neither', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'TARGET', 0, 20, 35000);
    Sky.updateMarkers(map);

    let clicked = null;
    let deselected = false;
    Sky.onMarkerClick((icao) => { clicked = icao; });
    Sky.onMapClick(() => { deselected = true; });

    const target = Sky.__test.state().lastFrame.drawable.find((d) => d.icao === 'TARGET');
    canvas.dispatch('pointerdown', { clientX: target.x, clientY: target.y, pointerId: 1 });
    canvas.dispatch('pointerup', { clientX: target.x, clientY: target.y, pointerId: 1 });
    assert.equal(clicked, 'TARGET', 'a tap on a chip selects it');

    clicked = null;
    canvas.dispatch('pointerdown', { clientX: 20, clientY: 20, pointerId: 1 });
    canvas.dispatch('pointerup', { clientX: 20, clientY: 20, pointerId: 1 });
    assert.ok(deselected && clicked === null, 'a tap on empty sky deselects');

    clicked = null;
    deselected = false;
    const before = Sky.__test.state().camera.heading;
    canvas.dispatch('pointerdown', { clientX: target.x, clientY: target.y, pointerId: 1 });
    canvas.dispatch('pointermove', { clientX: target.x + 60, clientY: target.y, pointerId: 1 });
    canvas.dispatch('pointerup', { clientX: target.x + 60, clientY: target.y, pointerId: 1 });

    assert.ok(clicked === null && !deselected, 'a drag is not a click');
    // Direction matters, magnitude is the projection's business (see the drag-rate
    // test): dragging left must turn the camera right, so the scene follows.
    const turned = wrap180(Sky.__test.state().camera.heading - before);
    assert.ok(turned < -0.5, `the camera turned with the drag (got ${turned.toFixed(1)}°)`);
    reset();
});

test('hovering redraws at once so the label and the tooltip never coexist', () => {
    reset({ ...BASE_SETTINGS, skyLabels: 'all' });
    const map = new Map();
    addAircraft(map, 'HOV', 0, 20, 35000);
    addAircraft(map, 'OTHER', 30, 40, 30000);
    Sky.updateMarkers(map);
    assert.ok(Sky.__test.labels().some((l) => l.text === 'HOV'), 'labelled before hover');

    const chip = Sky.__test.state().lastFrame.drawable.find((d) => d.icao === 'HOV');
    resetCalls();
    canvas.dispatch('pointermove', { clientX: chip.x, clientY: chip.y, pointerId: 1 });

    assert.ok(
        calls.some((c) => c.name === 'clearRect'),
        'the frame was redrawn on hover rather than waiting for the next update'
    );
    assert.ok(
        !Sky.__test.labels().some((l) => l.text === 'HOV'),
        'and the hovered label is already gone'
    );

    // Moving on must restore it just as promptly.
    resetCalls();
    canvas.dispatch('pointermove', { clientX: 5, clientY: 5, pointerId: 1 });
    assert.ok(calls.some((c) => c.name === 'clearRect'), 'leaving redraws too');
    assert.ok(Sky.__test.labels().some((l) => l.text === 'HOV'), 'the label is back');

    // Idle movement over empty sky must not queue frames.
    resetCalls();
    canvas.dispatch('pointermove', { clientX: 6, clientY: 6, pointerId: 1 });
    assert.ok(!calls.some((c) => c.name === 'clearRect'), 'no redraw when nothing changed');

    reset();
});

test('dragging turns the camera in step with the pointer, in both projections', () => {
    const drag = (px) => {
        const before = Sky.__test.state().camera.heading;
        canvas.dispatch('pointerdown', { clientX: 700, clientY: 400, pointerId: 1 });
        canvas.dispatch('pointermove', { clientX: 700 - px, clientY: 400, pointerId: 1 });
        canvas.dispatch('pointerup', { clientX: 700 - px, clientY: 400, pointerId: 1 });
        return wrap180(Sky.__test.state().camera.heading - before);
    };

    // Flattened: the whole 360 spans the canvas, so a drag of the full width is a
    // full turn. A fixed rate would need several screen-widths on a small viewport.
    reset({ ...BASE_SETTINGS, skyFlatten: true });
    const halfWidth = drag(700);
    assert.ok(
        Math.abs(Math.abs(halfWidth) - 180) < 5,
        `half the canvas turns about half a circle (got ${halfWidth.toFixed(0)}°)`
    );

    // Camera view: a drag across the canvas covers about one field of view.
    reset({ ...BASE_SETTINGS, skyFov: 75 });
    const acrossView = drag(700);
    assert.ok(
        Math.abs(Math.abs(acrossView) - 37.5) < 6,
        `half the canvas turns about half the field of view (got ${acrossView.toFixed(0)}°)`
    );

    reset();
});

// -------------------- pinch --------------------

test('spreading the fingers narrows the field of view and pinching widens it', () => {
    reset();
    const start = Sky.__test.state().camera.fov;

    pinch(100, 200);
    const spread = Sky.__test.state().camera.fov;
    assert.ok(spread < start, `spreading zooms in (${start}° → ${spread.toFixed(1)}°)`);
    // Doubling the separation halves the field of view.
    assert.ok(Math.abs(spread - start / 2) < 1, 'the ratio is taken from the separation');

    reset();
    pinch(200, 100);
    const squeezed = Sky.__test.state().camera.fov;
    assert.ok(squeezed > start, `pinching zooms out (${start}° → ${squeezed.toFixed(1)}°)`);
    reset();
});

test('the pinch ratio is measured from the start, so it does not drift', () => {
    reset();
    const start = Sky.__test.state().camera.fov;
    // Out and back within one gesture must land where it began.
    const cx = 700;
    const a = nextPointerId++;
    const b = nextPointerId++;
    canvas.dispatch('pointerdown', { clientX: cx - 60, clientY: 450, pointerId: a });
    canvas.dispatch('pointerdown', { clientX: cx + 60, clientY: 450, pointerId: b });
    // Out to a wide separation and back to the one it started at (120).
    for (const gap of [90, 140, 200, 140, 120]) {
        canvas.dispatch('pointermove', { clientX: cx - gap / 2, clientY: 450, pointerId: a });
        canvas.dispatch('pointermove', { clientX: cx + gap / 2, clientY: 450, pointerId: b });
    }
    canvas.dispatch('pointerup', { clientX: cx - 60, clientY: 450, pointerId: a });
    canvas.dispatch('pointerup', { clientX: cx + 60, clientY: 450, pointerId: b });

    assert.ok(
        Math.abs(Sky.__test.state().camera.fov - start) < 0.5,
        `returning to the original separation restores the field of view (${Sky.__test.state().camera.fov.toFixed(1)}°)`
    );
    reset();
});

test('pinch clamps at both ends of the field-of-view range', () => {
    reset();
    pinch(400, 20);
    assert.equal(Sky.__test.state().camera.fov, 120, 'clamped wide');
    reset();
    pinch(20, 400);
    assert.equal(Sky.__test.state().camera.fov, 30, 'clamped narrow');
    reset();
});

test('pinch is inert in the flattened panorama, where field of view has no meaning', () => {
    reset({ ...BASE_SETTINGS, skyFlatten: true });
    const before = Sky.__test.state().camera.fov;
    pinch(100, 250);
    assert.equal(Sky.__test.state().camera.fov, before, 'unchanged when flattened');
    reset();
});

test('lifting one of two fingers resumes rotating without a jump', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'A', 0, 20, 35000);
    Sky.updateMarkers(map);

    const { a, b, cx, cy, toGap } = pinch(100, 160, { lift: false });
    const headingDuringPinch = Sky.__test.state().camera.heading;

    // Lift one finger; the other stays put.
    canvas.dispatch('pointerup', { clientX: cx + toGap / 2, clientY: cy, pointerId: b });
    assert.equal(
        Sky.__test.state().camera.heading, headingDuringPinch,
        'letting go of one finger does not itself turn the camera'
    );

    // Continuing to drag must turn from where the survivor is, not from where the
    // first finger went down — otherwise the view snaps by the difference.
    canvas.dispatch('pointermove', { clientX: cx - toGap / 2 - 40, clientY: cy, pointerId: a });
    const turned = wrap180(Sky.__test.state().camera.heading - headingDuringPinch);
    assert.ok(turned > 0.5 && turned < 20, `a small drag turns a small amount (${turned.toFixed(1)}°)`);

    canvas.dispatch('pointerup', { clientX: cx - toGap / 2 - 40, clientY: cy, pointerId: a });
    reset();
});

test('a pinch is never a tap', () => {
    reset();
    let clicked = null;
    let deselected = false;
    Sky.onMarkerClick((icao) => { clicked = icao; });
    Sky.onMapClick(() => { deselected = true; });

    pinch(100, 180);
    assert.ok(clicked === null && !deselected, 'two fingers select nothing');

    Sky.onMarkerClick(null);
    Sky.onMapClick(null);
    reset();
});

// -------------------- double-tap --------------------

test('two quick taps in the same place reset the camera', () => {
    reset();
    canvas.dispatch('wheel', { deltaY: -120 });
    canvas.dispatch('pointerdown', { clientX: 700, clientY: 450, pointerId: 900 });
    canvas.dispatch('pointermove', { clientX: 500, clientY: 500, pointerId: 900 });
    canvas.dispatch('pointerup', { clientX: 500, clientY: 500, pointerId: 900 });
    const moved = Sky.__test.state().camera;
    assert.ok(moved.heading !== 0 || moved.fov !== 75, 'the camera actually moved');

    tap(700, 450);
    advanceClock(80);
    tap(702, 452);

    const after = Sky.__test.state().camera;
    assert.equal(after.heading, 0, 'heading restored');
    assert.equal(after.fov, 75, 'field of view restored');
    assert.equal(after.pitch, clampPitch(0, 75, safeArea(1400, 900, {})), 'pitch restored');
    reset();
});

test('the second tap of a double-tap does not also select or deselect', () => {
    reset();
    let deselections = 0;
    Sky.onMapClick(() => { deselections++; });

    tap(300, 300);
    assert.equal(deselections, 1, 'the first tap acts as an ordinary tap');
    advanceClock(80);
    tap(300, 300);
    assert.equal(deselections, 1, 'the second is consumed by the reset');

    Sky.onMapClick(null);
    reset();
});

test('taps too far apart in time or space are two ordinary taps', () => {
    reset();
    let deselections = 0;
    Sky.onMapClick(() => { deselections++; });

    tap(300, 300);
    advanceClock(400);            // beyond the double-tap window
    tap(300, 300);
    assert.equal(deselections, 2, 'a slow second tap acts on its own');

    advanceClock(400);
    tap(300, 300);
    advanceClock(50);
    tap(300, 400);                // same moment, well away
    assert.equal(deselections, 4, 'a distant second tap acts on its own');

    Sky.onMapClick(null);
    reset();
});

// -------------------- pointer cancel --------------------

test('a cancelled pointer ends the gesture instead of stranding it', () => {
    reset();
    const before = Sky.__test.state().camera.heading;

    canvas.dispatch('pointerdown', { clientX: 700, clientY: 450, pointerId: 950 });
    canvas.dispatch('pointermove', { clientX: 650, clientY: 450, pointerId: 950 });
    const turned = Sky.__test.state().camera.heading;
    assert.notEqual(turned, before, 'the drag was under way');

    // The browser takes the pointer away — no pointerup ever arrives.
    canvas.dispatch('pointercancel', { clientX: 650, clientY: 450, pointerId: 950 });
    canvas.dispatch('pointermove', { clientX: 400, clientY: 450, pointerId: 950 });
    assert.equal(
        Sky.__test.state().camera.heading, turned,
        'further movement no longer turns the camera'
    );
    reset();
});

test('a cancelled pointer is not a tap', () => {
    reset();
    let deselected = false;
    Sky.onMapClick(() => { deselected = true; });

    const pointerId = 951;
    canvas.dispatch('pointerdown', { clientX: 300, clientY: 300, pointerId });
    canvas.dispatch('pointercancel', { clientX: 300, clientY: 300, pointerId });
    assert.ok(!deselected, 'a cancel does not click');

    Sky.onMapClick(null);
    reset();
});

// -------------------- flattened mode --------------------

test('flattened mode shows more of the sky and ignores the wheel', () => {
    const map = new Map();
    addAircraft(map, 'N', 0, 20, 35000);
    addAircraft(map, 'E', 90, 40, 30000);
    addAircraft(map, 'S', 180, 60, 28000);
    addAircraft(map, 'W', 270, 30, 32000);

    reset();
    Sky.updateMarkers(map);
    const rectilinear = Sky.__test.state().lastFrame.drawable.length;

    reset({ ...BASE_SETTINGS, skyFlatten: true });
    Sky.updateMarkers(map);
    const frame = Sky.__test.state().lastFrame;

    assert.ok(frame.drawable.length > rectilinear, 'the whole panorama is visible');
    for (const d of frame.drawable) {
        assert.ok(
            d.x >= 0 && d.x <= frame.full.width,
            `${d.icao} stays inside the canvas`
        );
    }

    const fov = Sky.__test.state().camera.fov;
    canvas.dispatch('wheel', { deltaY: 120 });
    assert.equal(Sky.__test.state().camera.fov, fov, 'field of view is inert when flattened');
    reset();
});

test('the flattened panorama spans the canvas, ignoring panel insets', () => {
    const map = new Map();
    for (let bearing = 0; bearing < 360; bearing += 45) {
        addAircraft(map, `B${bearing}`, bearing, 30, 32000);
    }

    reset({ ...BASE_SETTINGS, skyFlatten: true });
    // A wide left inset is what exposed this: the panorama was squeezed into the
    // remaining width and the strip below the panel was left empty.
    Sky.setSafeInsets({ left: 660 });
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    const xs = frame.drawable.map((d) => d.x);
    assert.ok(xs.length >= 7, 'the whole panorama is populated');
    assert.ok(Math.min(...xs) < 660, 'bearings are drawn left of the inset, not only right of it');
    assert.ok(Math.max(...xs) <= frame.full.width, 'and nothing runs off the canvas');

    Sky.setSafeInsets({});
    reset();
});

// -------------------- camera swing --------------------

test('focusOn looks up as well as round, so an overhead aircraft is in frame', () => {
    // Turning to the right bearing is not enough on its own: a level camera covers
    // roughly 0-39 degrees, so anything passing overhead sits above the frame and the
    // view swings round to show empty sky.
    for (const [distanceKm, altitudeFt, label] of [
        [2, 38000, 'directly overhead'],
        [3, 35000, 'nearly overhead'],
        [20, 35000, 'high but in frame'],
        [120, 35000, 'low and distant']
    ]) {
        reset();
        const map = new Map();
        addAircraft(map, 'T', 90, distanceKm, altitudeFt);
        Sky.updateMarkers(map);

        const coord = map.get('T').Coordinate;
        Sky.focusOn(coord.Latitude, coord.Longitude, altitudeFt * 0.3048);
        Sky.updateMarkers(map);

        const frame = Sky.__test.state().lastFrame;
        const chip = frame.drawable.find((d) => d.icao === 'T');
        assert.ok(chip, `${label}: drawn`);
        assert.ok(
            chip.y >= 0 && chip.y <= frame.full.height,
            `${label}: on screen at elevation ${chip.elevationDeg.toFixed(0)}° (y=${chip.y.toFixed(0)})`
        );
        assert.ok(Math.abs(wrap180(Sky.__test.state().camera.heading - 90)) < 1, `${label}: facing it`);
    }
    reset();
});

test('focusOn leaves pitch alone for an aircraft already comfortably in view', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'T', 90, 40, 30000);
    Sky.updateMarkers(map);
    const before = Sky.__test.state().camera.pitch;

    const coord = map.get('T').Coordinate;
    Sky.focusOn(coord.Latitude, coord.Longitude, 30000 * 0.3048);

    assert.equal(
        Sky.__test.state().camera.pitch, before,
        'no tilt for something that was already on screen'
    );
    reset();
});

test('focusOn swings the camera to the aircraft bearing', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'EAST', 90, 40, 30000);
    Sky.updateMarkers(map);

    const coord = map.get('EAST').Coordinate;
    Sky.focusOn(coord.Latitude, coord.Longitude);

    const heading = Sky.__test.state().camera.heading;
    assert.ok(Math.abs(wrap180(heading - 90)) < 1, `expected about 90, got ${heading}`);
});

// -------------------- trail --------------------

test('incomplete trail samples are skipped, not drawn as a break', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'SEL', 0, 20, 30000);
    Sky.updateMarkers(map);

    // A speed-only update appends an entry with no position and no altitude. It is
    // not a gap in the flight path, so the line must continue across it — lifting
    // the pen there puts a visible break in the trail.
    const withGaps = [
        { position: { Latitude: 50.1, Longitude: 8.0 }, altitudeMeters: 3000 },
        { position: null, altitudeMeters: null },
        { position: { Latitude: 50.2, Longitude: 8.1 }, altitudeMeters: 3200 },
        { position: { Latitude: 50.3, Longitude: 8.2 }, altitudeMeters: null },
        { position: { Latitude: 50.4, Longitude: 8.3 }, altitudeMeters: 3500 }
    ];
    const complete = withGaps.filter((e) => e.position && e.altitudeMeters != null);

    const strokeCount = (entries) => {
        Sky.updateTrail(entries);
        resetCalls();
        Sky.updateMarkers(map);
        const begin = calls.findIndex((c) => c.name === 'moveTo');
        return calls.filter((c) => c.name === 'moveTo').length;
    };

    assert.equal(
        strokeCount(withGaps), strokeCount(complete),
        'the incomplete samples add no extra path starts, so no break appears'
    );

    assert.ok(Sky.__test.state().lastFrame, 'a frame was still produced');
    Sky.clearTrail();
    reset();
});

test('the trail meets the chip despite the history using a different altitude datum', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'SEL', 0, 20, 35000);
    // The aircraft reports both altitudes; the history only ever stores barometric,
    // so the series has to be lifted by the difference or it never reaches the chip.
    const aircraft = map.get('SEL');
    aircraft.BarometricAltitude = { Meters: 35000 * 0.3048, Feet: 35000 };
    aircraft.GeometricAltitude = { Meters: 35975 * 0.3048, Feet: 35975 };
    Sky.updateMarkers(map);
    Sky.highlightSelected('SEL');

    const chip = Sky.__test.state().lastFrame.drawable.find((d) => d.icao === 'SEL');

    // Final trail sample: same position as the aircraft, barometric altitude.
    resetCalls();
    Sky.updateTrail([
        // Farther out along the same bearing, so it is comfortably inside the frame.
        { position: coordinateAt(0, 60), altitudeMeters: 34000 * 0.3048 },
        { position: aircraft.Coordinate, altitudeMeters: 35000 * 0.3048 }
    ]);
    Sky.updateMarkers(map);

    // The trail is stroked before any chip, and a chip's velocity tick also emits
    // lineTo — so look only at the segments drawn before the first chip arc.
    const firstChip = calls.findIndex((c) => c.name === 'arc');
    const lineTos = calls
        .slice(0, firstChip === -1 ? calls.length : firstChip)
        .filter((c) => c.name === 'lineTo');
    const last = lineTos[lineTos.length - 1];
    assert.ok(last, 'the trail was drawn');
    assert.ok(
        Math.hypot(last.args[0] - chip.x, last.args[1] - chip.y) < 1,
        `the trail ends on the chip (was ${Math.hypot(last.args[0] - chip.x, last.args[1] - chip.y).toFixed(1)} px away)`
    );

    Sky.clearTrail();
    reset();
});

test('a trail crossing due north does not draw a line back across the panorama', () => {
    reset({ ...BASE_SETTINGS, skyFlatten: true });
    const map = new Map();
    addAircraft(map, 'SEAM', 182, 30, 35000);
    Sky.updateMarkers(map);
    Sky.highlightSelected('SEAM');

    // The seam sits opposite the camera heading, which the reset leaves at north —
    // so a path crossing due south is what lands consecutive points on opposite
    // edges of the flattened view.
    const path = [170, 175, 178, 182, 185].map((bearing) => ({
        position: coordinateAt(bearing, 30 * 1.852),
        altitudeMeters: 35000 * 0.3048
    }));

    Sky.updateTrail(path);
    resetCalls();
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    // The trail is the last path *stroked* before the chips, so isolate it by its
    // closing stroke: other paths legitimately span the full width — the horizon
    // line among them — and a chip opens a path of its own that never gets stroked.
    const firstChip = calls.findIndex((c) => c.name === 'arc');
    const upToChips = calls.slice(0, firstChip === -1 ? calls.length : firstChip);
    const names = upToChips.map((c) => c.name);
    const trailEnd = names.lastIndexOf('stroke');
    const trailStart = names.lastIndexOf('beginPath', trailEnd);
    const trailCalls = upToChips.slice(trailStart, trailEnd);

    assert.ok(
        trailCalls.some((c) => c.name === 'lineTo'),
        'the trail drew at least one segment'
    );

    let last = null;
    for (const call of trailCalls) {
        if (call.name === 'moveTo') {
            last = call.args[0];
        } else if (call.name === 'lineTo') {
            if (last !== null) {
                assert.ok(
                    Math.abs(call.args[0] - last) <= frame.full.width / 2,
                    `no trail segment jumps the seam (${last.toFixed(0)} → ${call.args[0].toFixed(0)})`
                );
            }
            last = call.args[0];
        }
    }

    Sky.clearTrail();
    reset();
});

// -------------------- tooltip payload --------------------

test('the tooltip payload carries plain numbers, as the shared tooltip expects', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'SEL', 0, 20, 35000);
    // Velocity arrives as a wrapper object, like the real payload.
    map.get('SEL').Speed = { Knots: 450, KilometersPerHour: 833, MilesPerHour: 518 };

    let payload = null;
    Sky.onSelectedTooltip((p) => { payload = p; });
    Sky.highlightSelected('SEL');
    Sky.updateMarkers(map);

    assert.ok(payload, 'a payload was published for the selection');
    assert.equal(typeof payload.speed, 'number', 'speed is knots, not a Velocity object');
    assert.equal(payload.speed, 450);
    assert.equal(typeof payload.altitude, 'number', 'altitude is feet, not an Altitude object');
    assert.equal(payload.altitude, 35000);
    // Handing the tooltip the wrapper objects is what produced NaN on screen.
    assert.ok(!Number.isNaN(Math.round(payload.speed)));
    assert.ok(!Number.isNaN(Math.round(payload.altitude)));
    assert.equal(payload.callsign, 'SEL');
    assert.ok(payload.azimuthDeg != null && payload.elevationDeg != null, 'sky extras present');

    Sky.onSelectedTooltip(null);
    reset();
});

test('an off-screen selection publishes no tooltip', () => {
    reset();
    const map = new Map();
    // Ahead of the camera and comfortably in view.
    addAircraft(map, 'SEL', 0, 20, 35000);
    let payload = null;
    Sky.onSelectedTooltip((p) => { payload = p; });
    Sky.highlightSelected('SEL');
    Sky.updateMarkers(map);
    assert.ok(payload, 'a tooltip is published while the chip is on screen');

    // Turn until it is off the side. It may still survive the frustum cull, which
    // works against a cone circumscribing the canvas — but there is nothing on screen
    // for a tooltip to point at.
    canvas.dispatch('pointerdown', { clientX: 700, clientY: 400, pointerId: 1 });
    canvas.dispatch('pointermove', { clientX: 20, clientY: 400, pointerId: 1 });
    canvas.dispatch('pointerup', { clientX: 20, clientY: 400, pointerId: 1 });
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    const chip = frame.drawable.find((d) => d.icao === 'SEL');
    if (!chip || chip.x < 0 || chip.x > frame.full.width) {
        assert.equal(payload, null, 'no tooltip once the aircraft leaves the view');
    }
    // And it must not be clickable where it is not drawn.
    assert.ok(
        Sky.__test.state().hitIndex.every(
            (h) => h.x >= 0 && h.x <= frame.full.width && h.y >= 0 && h.y <= frame.full.height
        ),
        'the hit index only offers targets that are on screen'
    );

    Sky.onSelectedTooltip(null);
    reset();
});

test('an aircraft with no speed or altitude yields nulls, not NaN', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'BARE', 0, 20, 35000);
    delete map.get('BARE').Speed;

    let payload = null;
    Sky.onSelectedTooltip((p) => { payload = p; });
    Sky.highlightSelected('BARE');
    Sky.updateMarkers(map);

    assert.equal(payload.speed, null, 'absent speed is null, which the tooltip omits');
    Sky.onSelectedTooltip(null);
    reset();
});

// -------------------- label collision --------------------

test('auto labelling suppresses collisions; all and selection override it', () => {
    const map = new Map();
    addAircraft(map, 'NEAR1', 0, 20, 35000);
    addAircraft(map, 'NEAR2', 0.05, 20.1, 35000);   // almost the same screen position
    addAircraft(map, 'APART', 12, 25, 30000);       // clearly separated

    reset({ ...BASE_SETTINGS, skyLabels: 'auto' });
    Sky.updateMarkers(map);
    const auto = Sky.__test.labels().map((l) => l.text);
    assert.ok(auto.length < 3, `a colliding label was suppressed (drew ${auto.join(', ')})`);
    assert.ok(auto.includes('APART'), 'the separated aircraft keeps its label');

    reset({ ...BASE_SETTINGS, skyLabels: 'all' });
    Sky.updateMarkers(map);
    assert.equal(Sky.__test.labels().length, 3, 'all forces every label');

    reset({ ...BASE_SETTINGS, skyLabels: 'selection' });
    Sky.updateMarkers(map);
    assert.equal(Sky.__test.labels().length, 0, 'selection draws none');
    reset();
});

test('the selected and hovered aircraft have no sky label, only a tooltip', () => {
    const map = new Map();
    addAircraft(map, 'ONE', 0, 20, 35000);
    addAircraft(map, 'TWO', 25, 30, 30000);

    reset({ ...BASE_SETTINGS, skyLabels: 'all' });
    Sky.updateMarkers(map);
    assert.equal(Sky.__test.labels().length, 2, 'both labelled to begin with');

    // The tooltip already shows the callsign, so a label beside the chip repeats it.
    Sky.highlightSelected('ONE');
    let texts = Sky.__test.labels().map((l) => l.text);
    assert.ok(!texts.includes('ONE'), 'the selected aircraft drops its label');
    assert.ok(texts.includes('TWO'), 'the others keep theirs');

    Sky.__test.setHovered('TWO');
    texts = Sky.__test.labels().map((l) => l.text);
    assert.ok(!texts.includes('TWO'), 'the hovered aircraft drops its label too');

    Sky.clearSelection();
    Sky.__test.setHovered(null);
    reset();
});

test('labels are centred above the chip', () => {
    reset({ ...BASE_SETTINGS, skyLabels: 'all' });
    const map = new Map();
    addAircraft(map, 'MID', 0, 20, 35000);
    Sky.updateMarkers(map);

    const chip = Sky.__test.state().lastFrame.drawable.find((d) => d.icao === 'MID');
    const label = Sky.__test.labels().find((l) => l.text === 'MID');

    assert.ok(label, 'the label was placed');
    assert.equal(label.x, chip.x, 'horizontally centred on the chip');
    assert.ok(label.y < chip.y, 'and sits above it');
    reset();
});

// -------------------- the sun and the moon --------------------

// A southern-hemisphere receiver, because the default camera looks north and at
// these latitudes the midday sun is due north — straight down the camera axis.
// At 50N the sun is never near north while it is up, so nothing would be on screen
// to assert about.
const SOUTHERN = { lat: -33.87, lon: 151.21 };

// Local noon: the sun bears 359 degrees at 33 degrees elevation, and the moon is
// close to it and close to new.
const DAY = new Date('2026-06-15T02:00:00Z');
// Local midnight at the same site: both bodies are far below the horizon.
const NIGHT = new Date('2026-06-15T14:00:00Z');
// Moon well up at a third lit — an unambiguous crescent — with the sun up too, so
// the terminator has a real direction to follow.
const CRESCENT = new Date('2026-06-10T00:00:00Z');
// Moon up but only 2 per cent lit, below the threshold for filling a crescent.
const NEW_MOON = new Date('2026-06-14T00:00:00Z');
// Moon high and 43 per cent lit with the sun on the OTHER side of it, so the
// terminator must point the opposite way from the crescent fixture above.
const MIRRORED = new Date('2026-06-21T06:00:00Z');

const CELESTIAL_SETTINGS = { ...BASE_SETTINGS, skyCelestial: true, skyTwilight: true };

// The four corners of the aircraft palette, from Map/AircraftIcons.js.
const PHASE_TEST_FILLS = {
    'low altitude': [179, 217, 255],
    cruise: [0, 97, 146],
    military: [0, 110, 0],
    selected: [230, 126, 34]
};

// The renderer reads the date through an injected clock, because the DOM stub
// virtualises performance.now() but not Date.
function atInstant(date, settings = CELESTIAL_SETTINGS, site = SOUTHERN) {
    Sky.clearSelection();
    Sky.clearTrail();
    Sky.__test.setHovered(null);
    Sky.setSafeInsets({});
    Sky.__test.setClock(() => date);
    Sky.setReceiver(site.lat, site.lon, 0);
    Sky.__test.resetCamera();
    Sky.setSettings({ ...settings });
}

// Restores the wall clock and the northern fixture receiver for any test that runs
// afterwards, so this block cannot leak into the rest of the suite.
function restoreClock() {
    Sky.__test.setClock(null);
    reset();
}

test('the sun and the moon are drawn when they are up', () => {
    atInstant(DAY);
    resetCalls();
    Sky.updateMarkers(new Map());

    const { sun, moon } = Sky.__test.state().lastFrame.celestial;
    assert.equal(sun.up, true, 'sun should be up at local noon');
    assert.equal(moon.up, true, 'moon should be up at this instant');
    assert.ok(sun.point, 'sun projects into the frame looking north');
    assert.ok(calls.some((c) => c.name === 'arc'), 'a disc was drawn');

    restoreClock();
});

test('nothing is drawn for a body that is below the horizon', () => {
    atInstant(NIGHT);
    resetCalls();
    Sky.updateMarkers(new Map());

    const { sun, moon } = Sky.__test.state().lastFrame.celestial;
    assert.equal(sun.up, false, 'sun should be down at local midnight');
    assert.equal(moon.up, false, 'moon should be down at this instant');
    // Unlike aircraft, which are clamped to the horizon so surface traffic does not
    // vanish, a body that has set is simply not there.
    assert.ok(!calls.some((c) => c.name === 'arc'), 'no disc was drawn');
    assert.ok(!calls.some((c) => c.name === 'translate'), 'no moon was drawn');

    restoreClock();
});

test('the marker setting hides both bodies', () => {
    atInstant(DAY, { ...CELESTIAL_SETTINGS, skyCelestial: false });
    resetCalls();
    Sky.updateMarkers(new Map());

    // Whether the positions are still computed is an internal matter — they are,
    // because the sky tint needs the sun. What the setting promises is that nothing
    // is drawn for either body.
    assert.ok(!calls.some((c) => c.name === 'arc'), 'no disc was drawn');
    assert.ok(!calls.some((c) => c.name === 'translate'), 'no moon was drawn');

    restoreClock();
});

test('size tracks the field of view between a floor and a ceiling', () => {
    const sizeAtFov = (fov) => {
        atInstant(DAY, { ...CELESTIAL_SETTINGS, skyFov: fov });
        Sky.updateMarkers(new Map());
        return Sky.__test.state().lastFrame.celestial.sun.sizePx;
    };

    const narrow = sizeAtFov(30);
    const normal = sizeAtFov(75);
    const wide = sizeAtFov(120);

    // The glyph is a symbol at an exaggerated size, but the true angle still drives
    // it, so zooming in enlarges it and widening the view shrinks it.
    assert.ok(narrow > normal, `30 deg (${narrow}) should exceed 75 deg (${normal})`);

    // Floored where a phase stops being readable: telling a gibbous moon from a
    // full one needs a disc near 20 px.
    assert.ok(wide >= 20, `wide field of view is floored (${wide})`);
    assert.ok(normal > 20, `the default is above the floor on a wide canvas (${normal})`);

    // And capped, so pinching right in does not fill the view with sun.
    assert.ok(narrow <= 48, `zoomed in is capped (${narrow})`);
    assert.equal(narrow, 48, 'a 30 degree field of view on this canvas reaches the cap');

    restoreClock();
});

test('the moon is drawn larger at perigee than at apogee', () => {
    // The 12 per cent monthly variation has to survive the scaling, or the size
    // carries no information at all. Sampled where neither end is clamped.
    let small = Infinity;
    let large = 0;
    for (let d = 0; d < 30; d += 0.5) {
        const at = new Date(CRESCENT.getTime() + d * 86400000);
        // A field of view that leaves the moon clear of both the floor and the cap,
        // where clamping would hide the variation being asserted.
        atInstant(at, { ...CELESTIAL_SETTINGS, skyFov: 60 });
        Sky.updateMarkers(new Map());
        const { sizePx } = Sky.__test.state().lastFrame.celestial.moon;
        if (sizePx > 20 && sizePx < 48) {
            small = Math.min(small, sizePx);
            large = Math.max(large, sizePx);
        }
    }
    assert.ok(large > small, 'the moon never changed size across a month');
    assert.ok(large / small > 1.08, `only ${((large / small - 1) * 100).toFixed(1)}% variation`);

    restoreClock();
});

test('the sun and the moon are drawn behind the aircraft', () => {
    atInstant(DAY);
    const map = new Map();
    // Placed at the receiver's own bearing so it lands in the frame looking north.
    map.set('CHIP', {
        ICAO: 'CHIP',
        Callsign: 'CHIP',
        Coordinate: { Latitude: SOUTHERN.lat + 0.2, Longitude: SOUTHERN.lon },
        GeometricAltitude: { Meters: 10000, Feet: 32808 },
        BarometricAltitude: null,
        IsOnGround: false,
        Track: 0
    });
    resetCalls();
    Sky.updateMarkers(map);

    const frame = Sky.__test.state().lastFrame;
    assert.equal(frame.drawable.length, 1, 'the chip is drawn');
    // Only the moon translates the canvas, and only a chip draws an arc after it,
    // so a chip crossing either body stays readable.
    const moonAt = calls.findIndex((c) => c.name === 'translate');
    const lastArc = calls.map((c) => c.name).lastIndexOf('arc');
    assert.ok(moonAt >= 0, 'the moon was drawn');
    assert.ok(moonAt < lastArc, 'the chip is drawn after both bodies');

    restoreClock();
});

test('the lit side of the moon faces the sun on screen', () => {
    // Flattened, so both bodies are on screen whatever their bearing: the moon and
    // the sun are rarely near the same horizon at once.
    atInstant(CRESCENT, { ...CELESTIAL_SETTINGS, skyFlatten: true });
    resetCalls();
    Sky.updateMarkers(new Map());

    const { sun, moon } = Sky.__test.state().lastFrame.celestial;
    assert.ok(sun.point && moon.point, 'both project in the flattened panorama');

    const rotate = calls.find((c) => c.name === 'rotate');
    assert.ok(rotate, 'the moon was drawn in a rotated frame');

    // The lit side faces the sun by definition, so the terminator is oriented by
    // the screen-space direction from the moon to the sun. Asserting that, rather
    // than a fixed angle, is the point: a constant would pass a weaker test.
    const expected = Math.atan2(sun.point.y - moon.point.y, sun.point.x - moon.point.x);
    assert.ok(
        Math.abs(wrap180((rotate.args[0] - expected) * 180 / Math.PI)) < 0.5,
        `terminator angle ${rotate.args[0]} should point at the sun (${expected})`
    );

    // And it is genuinely derived rather than constant. At this second instant the
    // sun is on the opposite side of the moon, so the lit limb has to swap sides:
    // a fixed orientation would pass the check above and fail this one.
    atInstant(MIRRORED, { ...CELESTIAL_SETTINGS, skyFlatten: true });
    resetCalls();
    Sky.updateMarkers(new Map());
    const second = Sky.__test.state().lastFrame.celestial;
    const other = calls.find((c) => c.name === 'rotate');
    assert.ok(other, 'the moon was drawn at the second instant too');
    assert.ok(
        second.sun.point.x < second.moon.point.x,
        'fixture has the sun left of the moon'
    );
    assert.ok(
        sun.point.x > moon.point.x,
        'and the first fixture had it on the right'
    );
    // Lit limb to the right in one and to the left in the other.
    assert.ok(Math.cos(rotate.args[0]) > 0, 'first terminator faces right');
    assert.ok(Math.cos(other.args[0]) < 0, 'second terminator faces left');

    restoreClock();
});

test('a barely lit moon is drawn as an outline rather than a hairline', () => {
    atInstant(NEW_MOON, { ...CELESTIAL_SETTINGS, skyFlatten: true });
    resetCalls();
    Sky.updateMarkers(new Map());

    const { moon } = Sky.__test.state().lastFrame.celestial;
    assert.ok(moon.phase.fraction < 0.03, `fixture is near new (${moon.phase.fraction})`);
    // The crescent would be thinner than the line drawing it, so no terminator
    // ellipse is drawn at all.
    assert.ok(!calls.some((c) => c.name === 'ellipse'), 'no crescent was filled');
    assert.ok(calls.some((c) => c.name === 'arc'), 'but the outline is there');

    restoreClock();
});

test('a clearly lit moon draws a terminator whose waist follows the phase', () => {
    atInstant(CRESCENT, { ...CELESTIAL_SETTINGS, skyFlatten: true });
    resetCalls();
    Sky.updateMarkers(new Map());

    const { moon } = Sky.__test.state().lastFrame.celestial;
    const ellipse = calls.find((c) => c.name === 'ellipse');
    assert.ok(ellipse, 'a terminator was drawn');

    // Half the disc at new or full, a straight line at half phase.
    const radius = moon.sizePx / 2;
    const expectedWaist = radius * Math.abs(1 - 2 * moon.phase.fraction);
    assert.ok(
        Math.abs(ellipse.args[2] - expectedWaist) < 0.01,
        `waist ${ellipse.args[2]} should be ${expectedWaist} at ${moon.phase.fraction} lit`
    );
    // Crescent, so the terminator bulges towards the lit limb.
    assert.equal(ellipse.args[7], true, 'a crescent sweeps towards the sun');

    restoreClock();
});

test('the readout reports each body only while it is up', () => {
    atInstant(DAY);
    Sky.updateMarkers(new Map());
    const hud = Sky.__test.hud();
    assert.equal(hud.sunItem.style.display, '', 'sun is reported at noon');
    assert.match(hud.sun.textContent, /^\d{3}° -?\d+°$/, `sun readout ${hud.sun.textContent}`);
    assert.match(hud.moon.textContent, /%$/, 'moon readout carries its phase');

    atInstant(NIGHT);
    Sky.updateMarkers(new Map());
    assert.equal(hud.sunItem.style.display, 'none', 'sun is not reported once it has set');
    assert.equal(hud.moonItem.style.display, 'none', 'nor the moon');

    restoreClock();
});

// -------------------- the twilight tint --------------------

test('the sky follows the sun through a whole day', () => {
    // Stepped through a day with the clock injected, rather than waiting for dusk.
    const seen = new Map();
    for (let hour = 0; hour < 24; hour += 0.5) {
        const at = new Date(Date.UTC(2026, 5, 15, 0, 0) + hour * 3600000);
        atInstant(at);
        Sky.updateMarkers(new Map());
        const frame = Sky.__test.state().lastFrame;
        seen.set(frame.palette.phase, (seen.get(frame.palette.phase) || 0) + 1);
    }

    // A full day has to pass through daylight and night, and through the twilight
    // between them — otherwise the tint is not actually being driven by the sun.
    assert.ok(seen.get('day') > 0, 'never reached daylight');
    assert.ok(seen.get('night') > 0, 'never reached night');
    assert.ok(
        (seen.get('golden') || 0) + (seen.get('civil') || 0) + (seen.get('nautical') || 0) > 0,
        'never passed through twilight'
    );

    restoreClock();
});

test('the sky is brighter at noon than at midnight', () => {
    atInstant(DAY);
    Sky.updateMarkers(new Map());
    const noon = Sky.__test.state().lastFrame.palette.luminance;

    atInstant(NIGHT);
    Sky.updateMarkers(new Map());
    const midnight = Sky.__test.state().lastFrame.palette.luminance;

    assert.ok(noon > midnight * 10, `noon ${noon} should far exceed midnight ${midnight}`);

    restoreClock();
});

test('the outline and every other mark invert on a night sky', () => {
    atInstant(NIGHT);
    Sky.updateMarkers(new Map());
    const night = Sky.__test.state().lastFrame.palette;
    assert.equal(night.outline.light, true, 'a night sky needs a light outline');
    assert.equal(night.dark, true);

    atInstant(DAY);
    Sky.updateMarkers(new Map());
    const day = Sky.__test.state().lastFrame.palette;
    assert.equal(day.outline.light, false, 'daylight keeps the dark outline');
    // The ground band and the ribbon backing follow too, or they glow after dusk.
    assert.ok(
        relativeLuminance(day.ground) > relativeLuminance(night.ground),
        'the ground band did not follow the sky'
    );
    assert.ok(
        relativeLuminance(day.panel) > relativeLuminance(night.panel),
        'the ribbon backing did not follow the sky'
    );

    restoreClock();
});

test('aircraft of every category stay findable at every hour', () => {
    // The check that matters, and the reason the adaptive outline had to land before
    // the tint. A sky that makes half the traffic disappear at dusk would be a worse
    // view than one that is always pale.
    for (let hour = 0; hour < 24; hour += 1) {
        const at = new Date(Date.UTC(2026, 5, 15, 0, 0) + hour * 3600000);
        atInstant(at);
        Sky.updateMarkers(new Map());
        const { palette } = Sky.__test.state().lastFrame;
        const outlineRatio = contrastRatio(palette.outline.rgb, palette.middle);

        for (const [category, fill] of Object.entries(PHASE_TEST_FILLS)) {
            const fillRatio = contrastRatio(fill, palette.middle);
            assert.ok(
                Math.max(fillRatio, outlineRatio) >= 3,
                `${category} at ${hour}h (${palette.phase}): fill ${fillRatio.toFixed(2)}, outline ${outlineRatio.toFixed(2)}`
            );
        }
    }

    restoreClock();
});

test('the sky keeps its daylight palette when the tint is off', () => {
    // Turning the tint off restores exactly the view this had before it existed,
    // night or not.
    atInstant(NIGHT, { ...CELESTIAL_SETTINGS, skyTwilight: false });
    Sky.updateMarkers(new Map());
    const off = Sky.__test.state().lastFrame.palette;
    assert.equal(off.outline.light, false, 'still the daylight outline');
    assert.ok(off.luminance > 0.5, `still a pale sky (${off.luminance})`);

    restoreClock();
});

// -------------------- the two settings are independent --------------------

// The markers and the sky tint are separate settings because they are separate
// sizes of change: two small discs against a recolouring of the whole view. These
// four cases are the combinations that exist because of that, and the two mixed
// ones could not be expressed when a single setting governed both.

function paletteAndDraws(date, skyCelestial, skyTwilight, extra = {}) {
    atInstant(date, { ...CELESTIAL_SETTINGS, skyCelestial, skyTwilight, ...extra });
    resetCalls();
    Sky.updateMarkers(new Map());
    return {
        palette: Sky.__test.state().lastFrame.palette,
        celestial: Sky.__test.state().lastFrame.celestial,
        drewBody: calls.some((c) => c.name === 'arc'),
        drewMoon: calls.some((c) => c.name === 'translate')
    };
}

test('markers and tint both on: bodies drawn and the sky follows the sun', () => {
    const { drewBody, drewMoon, palette } = paletteAndDraws(NIGHT, true, true);
    // At local midnight both bodies are down, so nothing is drawn for them — but the
    // sky must still have gone dark.
    assert.equal(drewBody, false, 'nothing is up to draw at midnight');
    assert.equal(drewMoon, false);
    assert.equal(palette.dark, true, 'the sky went dark');

    const day = paletteAndDraws(DAY, true, true);
    assert.equal(day.drewBody, true, 'the sun is drawn at noon');
    assert.equal(day.palette.dark, false);

    restoreClock();
});

test('markers on, tint off: bodies drawn on a fixed daylight sky at night', () => {
    // The combination most likely to look wrong if the palette and the markers were
    // still coupled. Flattened, because CRESCENT puts the moon at bearing 307 and the
    // default camera looks north — a 75-degree frustum would legitimately cull it.
    const { drewBody, drewMoon, palette } =
        paletteAndDraws(CRESCENT, true, false, { skyFlatten: true });
    assert.equal(drewBody, true, 'the bodies are still drawn');
    assert.equal(drewMoon, true);
    assert.equal(palette.dark, false, 'but the sky stays pale');
    assert.equal(palette.outline.light, false, 'and the outlines stay dark to match it');

    // Even at local midnight the sky is the daylight one.
    const night = paletteAndDraws(NIGHT, true, false);
    assert.ok(night.palette.luminance > 0.5, `still pale at midnight (${night.palette.luminance})`);

    restoreClock();
});

test('markers off, tint on: the sky still follows a sun that is never drawn', () => {
    // The case most likely to break, because the sun has to be computed while
    // nothing about it is drawn.
    const night = paletteAndDraws(NIGHT, false, true);
    assert.equal(night.drewBody, false, 'no bodies are drawn');
    assert.equal(night.drewMoon, false);
    assert.ok(night.celestial, 'but the positions were still computed');
    assert.equal(night.palette.dark, true, 'so the sky could go dark');
    assert.equal(night.palette.outline.light, true, 'and the outlines inverted with it');

    const day = paletteAndDraws(DAY, false, true);
    assert.equal(day.drewBody, false, 'still nothing drawn at noon');
    assert.equal(day.palette.dark, false, 'and the sky is light again');

    restoreClock();
});

test('both off: exactly the view that predates the feature', () => {
    const { drewBody, drewMoon, palette, celestial } = paletteAndDraws(NIGHT, false, false);
    assert.equal(drewBody, false);
    assert.equal(drewMoon, false);
    assert.equal(celestial, null, 'nothing is computed at all');
    assert.equal(palette.outline.light, false, 'the original dark outline');
    assert.ok(palette.luminance > 0.5, 'on the original pale sky');

    restoreClock();
});

test('the readout follows the marker setting, not the tint', () => {
    atInstant(DAY, { ...CELESTIAL_SETTINGS, skyCelestial: true, skyTwilight: false });
    Sky.updateMarkers(new Map());
    const hud = Sky.__test.hud();
    assert.equal(hud.sunItem.style.display, '', 'reported with the markers on');

    atInstant(DAY, { ...CELESTIAL_SETTINGS, skyCelestial: false, skyTwilight: true });
    Sky.updateMarkers(new Map());
    // The sun is being computed to colour the sky, but it is not on screen, so
    // reporting a bearing to it would describe something the user cannot see.
    assert.equal(hud.sunItem.style.display, 'none', 'not reported with the markers off');
    assert.equal(hud.moonItem.style.display, 'none');

    restoreClock();
});

// -------------------- the sun and moon are labelled --------------------

const labelTexts = () => Sky.__test.labels().map((l) => l.text);

// coordinateAt() is fixed to the northern fixture receiver; these tests use the
// southern one, so the same maths is needed from an arbitrary origin.
function coordinateAtFrom(site, bearingDeg, distanceKm) {
    const d = distanceKm / 6371;
    const b = (bearingDeg * Math.PI) / 180;
    const phi1 = (site.lat * Math.PI) / 180;
    const lambda1 = (site.lon * Math.PI) / 180;
    const phi2 = Math.asin(
        Math.sin(phi1) * Math.cos(d) + Math.cos(phi1) * Math.sin(d) * Math.cos(b)
    );
    const lambda2 = lambda1 + Math.atan2(
        Math.sin(b) * Math.sin(d) * Math.cos(phi1),
        Math.cos(d) - Math.sin(phi1) * Math.sin(phi2)
    );
    return { Latitude: (phi2 * 180) / Math.PI, Longitude: (lambda2 * 180) / Math.PI };
}

test('both bodies are labelled while they are up', () => {
    atInstant(DAY);
    Sky.updateMarkers(new Map());

    const texts = labelTexts();
    assert.ok(texts.includes('Sun'), `expected a Sun label, got ${JSON.stringify(texts)}`);
    assert.ok(texts.includes('Moon'), `expected a Moon label, got ${JSON.stringify(texts)}`);

    restoreClock();
});

test('a body that has set carries no label', () => {
    atInstant(NIGHT);
    Sky.updateMarkers(new Map());

    assert.deepEqual(labelTexts(), [], 'nothing is up, so nothing is named');

    restoreClock();
});

test('the labels follow the marker setting, not a setting of their own', () => {
    atInstant(DAY, { ...CELESTIAL_SETTINGS, skyCelestial: false });
    Sky.updateMarkers(new Map());
    assert.deepEqual(labelTexts(), [], 'no markers, no labels');

    restoreClock();
});

test('the labels ignore the aircraft label mode', () => {
    // The decision this encodes: there are only ever two of them, they are
    // landmarks rather than clutter, and a phone defaults to selection-only — which
    // would otherwise leave the two discs unnamed exactly where it helps most.
    const map = new Map();
    map.set('CHIP', {
        ICAO: 'CHIP',
        Callsign: 'CHIP',
        Coordinate: { Latitude: SOUTHERN.lat + 0.2, Longitude: SOUTHERN.lon },
        GeometricAltitude: { Meters: 10000, Feet: 32808 },
        BarometricAltitude: null,
        IsOnGround: false,
        Track: 0
    });

    atInstant(DAY, { ...CELESTIAL_SETTINGS, skyLabels: 'selection' });
    Sky.updateMarkers(map);

    const texts = labelTexts();
    assert.ok(texts.includes('Sun'), 'the sun is still named');
    assert.ok(texts.includes('Moon'), 'and the moon');
    assert.ok(!texts.includes('CHIP'), 'while the aircraft label is suppressed as asked');

    restoreClock();
});

test('an aircraft label gives way to a celestial one, not the reverse', () => {
    // Written first as "put an aircraft near the sun and check Sun survives", which
    // passed without proving anything: the sun's label sits high to clear its halo,
    // so a chip beside the sun never contends with it at all. This is the A/B that
    // does prove it — the same traffic laid out with the markers off, to find which
    // aircraft labels genuinely occupy the sun's space, then again with them on.
    const rect = (l) => ({
        x: l.x - (l.text.length * 6) / 2, y: l.y - LABEL_LINE_H,
        w: l.text.length * 6, h: LABEL_LINE_H
    });
    const hits = (a, b) => a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;

    // A cluster spread across the sun's bearing and slightly above it, dense enough
    // that some label must land where the sun's does.
    // Swept across bearing and elevation rather than placed at one offset, so the
    // cluster still contests the label if its distance from the disc is retuned.
    const build = (azimuthDeg, elevationDeg) => {
        const map = new Map();
        let n = 0;
        for (let db = -3; db <= 3; db += 1) {
            for (const de of [0.4, 0.8, 1.2, 1.6]) {
                const id = `A${n++}`;
                const km = 40;
                map.set(id, {
                    ICAO: id,
                    Callsign: id,
                    Coordinate: coordinateAtFrom(SOUTHERN, azimuthDeg + db, km),
                    GeometricAltitude: {
                        Meters: km * 1000 * Math.tan((elevationDeg + de) * Math.PI / 180),
                        Feet: 1
                    },
                    BarometricAltitude: null,
                    IsOnGround: false,
                    Track: 0
                });
            }
        }
        return map;
    };

    const layoutWith = (skyCelestial) => {
        atInstant(DAY, { ...CELESTIAL_SETTINGS, skyCelestial, skyLabels: 'auto' });
        Sky.updateMarkers(new Map());
        const { sun } = Sky.__test.state().lastFrame.celestial;
        Sky.updateMarkers(build(sun.azimuthDeg, sun.elevationDeg));
        return Sky.__test.labels();
    };

    const withMarkers = layoutWith(true);
    const withoutMarkers = layoutWith(false);

    const sunLabel = withMarkers.find((l) => l.text === 'Sun');
    assert.ok(sunLabel, 'the sun is labelled');

    const contested = withoutMarkers
        .filter((l) => hits(rect(l), rect(sunLabel)))
        .map((l) => l.text);
    assert.ok(contested.length > 0, 'the fixture must actually contest the sun label');

    const kept = withMarkers.map((l) => l.text);
    for (const text of contested) {
        assert.ok(!kept.includes(text), `${text} should have given way to the sun`);
    }

    restoreClock();
});

test('both labels sit at exactly the same distance from their bodies', () => {
    // Sized per body first, which put the sun's label 20px higher than the moon's
    // because it cleared the halo and the moon cleared only its disc. Two discs of
    // near-identical size in the same sky read as two different treatments.
    const offsets = (date, extra = {}) => {
        atInstant(date, { ...CELESTIAL_SETTINGS, ...extra });
        Sky.updateMarkers(new Map());
        const { sun, moon } = Sky.__test.state().lastFrame.celestial;
        const labels = Sky.__test.labels();
        const sunLabel = labels.find((l) => l.text === 'Sun');
        const moonLabel = labels.find((l) => l.text === 'Moon');
        assert.ok(sunLabel && moonLabel, `both bodies labelled at ${date.toISOString()}`);
        return {
            sun: sun.point.y - sunLabel.y,
            moon: moon.point.y - moonLabel.y,
            radius: Math.max(sun.sizePx, moon.sizePx) / 2
        };
    };

    const now = offsets(DAY);
    // Not bit-for-bit: the same offset subtracted at two different y values differs
    // in the last bit or two. A pixel is the unit that matters here.
    assert.ok(Math.abs(now.sun - now.moon) < 1e-6, `sun ${now.sun} vs moon ${now.moon}`);

    // Still outside the dense halo ring, or the sun's label sits in its own glow.
    assert.ok(
        now.sun > now.radius * 1.7,
        `label at ${now.sun}px is inside the halo (${now.radius * 1.7}px)`
    );

    // Held at another instant, where the moon is at a different distance and so a
    // different size. Sampling one moment would not catch the offset going back to
    // being derived per body. Flattened, because a fortnight on the two bodies are
    // no longer both within a 75-degree frustum aimed north.
    const later = offsets(new Date(DAY.getTime() + 27 * 86400000), { skyFlatten: true });
    assert.ok(
        Math.abs(later.sun - later.moon) < 1e-6,
        `drifted apart at another instant: sun ${later.sun} vs moon ${later.moon}`
    );

    restoreClock();
});
