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
import { makeCanvas, installGlobals, calls, resetCalls } from './Support/DomStub.mjs';
import { safeArea, pitchRange, clampPitch, wrap180 } from '../Services/SkyViewGeometry.js';

const canvas = makeCanvas(1400, 900);
installGlobals(canvas, { appendChild() {} });
const Sky = await import('../SkyView/SkyViewManager.js');

const BASE_SETTINGS = {
    skyFov: 75,
    skyPitch: 15,
    skyFlatten: false,
    skyRibbon: true,
    skyTrail: true,
    skyLabels: 'auto',
    skyMaxRangeNm: 150
};

const RECEIVER = { lat: 50, lon: 8 };

// Places a synthetic aircraft at a bearing and ground distance from the receiver.
function addAircraft(map, icao, bearingDeg, distanceKm, altitudeFeet, extra = {}) {
    const d = distanceKm / 6371;
    const b = (bearingDeg * Math.PI) / 180;
    const phi1 = (RECEIVER.lat * Math.PI) / 180;
    const lambda1 = (RECEIVER.lon * Math.PI) / 180;
    const phi2 = Math.asin(Math.sin(phi1) * Math.cos(d) + Math.cos(phi1) * Math.sin(d) * Math.cos(b));
    const lambda2 = lambda1 + Math.atan2(
        Math.sin(b) * Math.sin(d) * Math.cos(phi1),
        Math.cos(d) - Math.sin(phi1) * Math.sin(phi2)
    );

    map.set(icao, {
        ICAO: icao,
        Callsign: icao,
        Coordinate: { Latitude: (phi2 * 180) / Math.PI, Longitude: (lambda2 * 180) / Math.PI },
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

function reset(settings = BASE_SETTINGS) {
    Sky.clearSelection();
    Sky.clearTrail();
    Sky.__test.setHovered(null);
    Sky.setSafeInsets({});
    Sky.setReceiver(RECEIVER.lat, RECEIVER.lon, 0);
    // Double-click is the camera reset, and it is the only way to restore heading
    // between tests. Without it a preceding test's heading leaks in and everything
    // at the fixture bearings is legitimately culled as off-axis.
    canvas.dispatch('dblclick', {});
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

// -------------------- layout --------------------

test('the ribbon hangs below the horizon and inside the safe area', () => {
    reset();
    const frame = Sky.__test.state().lastFrame;
    assert.ok(frame.ribbonTop > frame.yHorizon);
    assert.ok(frame.ribbonBottom <= frame.safe.bottom);
});

test('at steep pitch the horizon is clamped to clear the reserved bands', () => {
    reset({ ...BASE_SETTINGS, skyPitch: 60 });
    const withRibbon = Sky.__test.state().lastFrame;
    assert.equal(withRibbon.yHorizon, withRibbon.safe.bottom - 18 - 34);

    reset({ ...BASE_SETTINGS, skyPitch: 60, skyRibbon: false });
    const withoutRibbon = Sky.__test.state().lastFrame;
    assert.ok(withoutRibbon.yHorizon > withRibbon.yHorizon, 'hiding the ribbon reclaims its band');
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

    canvas.dispatch('dblclick', {});
    const after = Sky.__test.state().camera;
    assert.equal(after.heading, 0, 'heading restored');
    assert.equal(after.fov, 75, 'field of view restored');
    assert.equal(after.pitch, clampPitch(15, 75, safeArea(1400, 900, {})), 'pitch restored');
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
    assert.ok(
        Math.abs(wrap180(Sky.__test.state().camera.heading - before + 9)) < 1.5,
        'and it rotated the camera instead'
    );
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
            d.x >= frame.safe.left - 1 && d.x <= frame.safe.right + 1,
            `${d.icao} stays inside the frame`
        );
    }

    const fov = Sky.__test.state().camera.fov;
    canvas.dispatch('wheel', { deltaY: 120 });
    assert.equal(Sky.__test.state().camera.fov, fov, 'field of view is inert when flattened');
    reset();
});

// -------------------- camera swing --------------------

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

test('a trail with missing positions or altitudes breaks rather than throwing', () => {
    reset();
    const map = new Map();
    addAircraft(map, 'SEL', 0, 20, 30000);
    Sky.updateMarkers(map);

    Sky.updateTrail([
        { position: { Latitude: 50.1, Longitude: 8 }, altitudeMeters: 3000 },
        { position: { Latitude: 50.2, Longitude: 8 }, altitudeMeters: null },
        { position: null, altitudeMeters: 3000 },
        { position: { Latitude: 50.3, Longitude: 8 }, altitudeMeters: 3500 }
    ]);

    assert.ok(Sky.__test.state().lastFrame, 'a frame was still produced');
    Sky.clearTrail();
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
    assert.equal(Sky.__test.labels().length, 0, 'selection draws none by default');

    Sky.highlightSelected('NEAR1');
    assert.ok(
        Sky.__test.labels().some((l) => l.text === 'NEAR1'),
        'the selected aircraft is always labelled'
    );

    Sky.__test.setHovered('APART');
    assert.ok(
        Sky.__test.labels().some((l) => l.text === 'APART'),
        'the hovered aircraft is always labelled'
    );
    reset();
});
