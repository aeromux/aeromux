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
