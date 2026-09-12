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

// Receiver-centric sky renderer: a virtual camera at the configured receiver
// position looking along a user-controlled heading, with aircraft placed by
// bearing and elevation angle rather than on a flat map.
//
// The public surface deliberately mirrors Map/MapManager.js so App.jsx can hold a
// single active-view reference and call the same method names in either mode.
// Plain 2D canvas, drawn in immediate mode: a full frame costs well under a
// millisecond even at aircraft counts far above what the default range produces,
// so there is no retained scene graph to keep in sync with the push feed.

import {
    bearingTo,
    elevationAndRange,
    horizon,
    enuVector,
    cameraBasis,
    projectRectilinear,
    projectEquirect,
    frustumCosLimit,
    focalPx,
    bearingTickStep,
    bearingLabelStep,
    safeArea,
    chipSizePx,
    hazeAlpha,
    wrap360,
    shortestTurnDeg,
    isSubHorizon,
    aircraftAltitudeM,
    destinationPoint,
    clampPitch,
    ribbonScaleNm
} from '../Services/SkyViewGeometry.js';
import { sunPosition, moonPosition, moonPhase, isUp } from '../Services/Ephemeris.js';
import { defaultPalette, skyPalette, css } from '../Services/SkyPalette.js';
import { haversineDistance, nmToKm } from '../Services/UnitConversion.js';
import { CATEGORIES, SELECTED_COLOR, interpolateColor } from '../Map/AircraftIcons.js';

// The compass sits just under the horizon and travels with it. The ribbon instead
// occupies a fixed strip at the foot of the view: bearing is a horizontal axis, so
// pinning it there costs nothing in alignment — a chip's stem still meets its bar at
// the same x — while filling the ground that would otherwise be dead space, and
// keeping the ribbon still as the camera tilts rather than sliding with the horizon.
const COMPASS_BAND_PX = 28;
// A fixed strip at the foot of the view. Sizing it from whatever ground the current
// pitch left over made it resize as the camera moved, which draws the eye to the
// chrome rather than to the sky, and gave the coverage profile a scale that changed
// underneath it.
const RIBBON_BAND_PX = 64;
// Inset of the plotted area within the band, leaving room for the scale labels to
// sit on their gridlines without any part of them escaping the strip.
const RIBBON_PAD_TOP = 11;
const RIBBON_PAD_BOTTOM = 7;
// Distance of the scale labels from the right edge of the view. They sit on the
// right because the aircraft list and detail panel occupy the left, and either can
// reach far enough down the viewport to cover a left-hand gutter entirely.
const RIBBON_AXIS_MARGIN = 12;

// Where the horizon sits, as a fraction of the view height, with the camera level.
// The projection's principal point is placed here rather than at the middle of the
// frame: a level camera would otherwise put the horizon halfway up and give half the
// view to ground that has nothing to draw in it. Pitching up from here moves the
// horizon down and eventually out of the frame, which is what looking up should do.
const HORIZON_AT_REST = 0.82;

// Shorter than this and the velocity tick is a meaningless stub, which is worse
// than no tick: a track pointing at or away from the receiver foreshortens to
// nothing.
const MIN_TICK_PX = 4;

const HIT_RADIUS_PX = 18;
const FOV_MIN = 30;
const FOV_MAX = 120;
// Angle subtended at the camera by a point that many pixels from the view's centre.
// This is what makes a drag move the scene with the pointer instead of at some fixed
// rate: a fixed rate is wrong by the ratio of the view's angular width to its pixel
// width — on a phone in the flattened panorama, a whole turn would take six
// screen-widths of dragging — and in the camera projection it also drifts towards the
// edges, where a degree covers fewer pixels than it does at the centre.
function viewAngleAt(offsetPx, safe) {
    if (settings && settings.skyFlatten) {
        // The panorama is linear in azimuth across the full canvas.
        return (offsetPx / Math.max(1, canvas.width / dpr)) * 360;
    }
    return (Math.atan(offsetPx / focalPx(camera.fov, safe.width)) * 180) / Math.PI;
}
const DRAG_THRESHOLD_PX = 4;
// A second tap within this long, and this close, is a double-tap. Detected from
// pointer events rather than the browser's dblclick, so mouse and touch take one
// path — binding both would fire the reset twice.
const DOUBLE_TAP_MS = 300;
const DOUBLE_TAP_SLOP_PX = 24;
const SWING_MS = 400;
const LABEL_FONT = '10px InterVariable, Inter, system-ui, sans-serif';
const CARDINAL_FONT = '600 12px InterVariable, Inter, system-ui, sans-serif';
const CARDINALS = { 0: 'N', 90: 'E', 180: 'S', 270: 'W' };
// Roughly how far apart the compass row wants its ticks and its numbers. The
// interval is chosen from these rather than fixed in degrees, so the row keeps the
// same density whatever the field of view or the mode does to the angular scale.
const TICK_TARGET_PX = 26;
const LABEL_TARGET_PX = 90;
const LABEL_LINE_H = 11;
const LABEL_GAP_PX = 4;
// The range outline is recorded in 5-degree bearing sectors. Drawing each at its
// true width lets neighbours merge into one silhouette, and leaves a real notch
// wherever nothing has been heard.
const OUTLINE_SECTOR_DEG = 5;

// The sun and moon are drawn as symbols at an exaggerated size, not at the half
// degree they really subtend. This is the same admission `chipSizePx` makes for
// aircraft, and for a sharper reason: the moon's phase is the point of drawing it,
// and the terminator is a shape that needs room. Telling a gibbous moon from a full
// one means resolving a dark sliver of about 2 px, which takes a disc near 20 px
// across — where true size yields 8 px on a wide desktop and only 4 px in a
// windowed browser with the aircraft list open.
//
// Do not "correct" this back to the true angle. It was written that way first, and
// the result was a terminator nobody could see on a disc smaller than a speck of
// traffic — the two most prominent objects in the sky rendering smaller than an
// aircraft 100 nm away.
//
// Size still tracks the field of view, so zooming in still enlarges them, and the
// moon still grows and shrinks by 12 per cent across its month.
const CELESTIAL_SCALE = 3;
const CELESTIAL_MIN_PX = 20;
const CELESTIAL_MAX_PX = 48;
// Below this illuminated fraction the lit sliver is thinner than a line, so the
// moon is drawn as an outline instead of a hairline crescent that would vanish.
const MOON_OUTLINE_FRACTION = 0.03;
// Outermost halo ring, as a multiple of the disc radius.
const SUN_HALO_SCALE = 2.6;
// How far above its centre a body's label sits, again in disc radii. Shared by
// both bodies so the two labels line up: sizing each to its own body put the sun's
// label 20 px higher than the moon's, which read as two different treatments
// rather than one.
//
// It clears the inner, denser halo ring rather than the outermost one. Clearing
// 2.6 is what pushed the sun's label so far out; at 1.7 only the 10%-alpha wash
// falls behind the text, which is invisible against the sky.
const CELESTIAL_LABEL_REACH = 1.7;
const SUN_CORE = '#fff8d8';
const SUN_HALO = 'rgb(255, 214, 92)';
const SUN_RIM = 'rgba(240, 176, 40, 0.85)';
const MOON_LIT = '#f2efe6';
const MOON_UNLIT = 'rgba(90, 96, 107, 0.45)';
const MOON_RIM = 'rgba(70, 76, 88, 0.75)';
const TRAIL_COLORS = {
    normal: 'rgb(0, 97, 146)',
    military: 'rgb(0, 110, 0)',
    privacy: 'rgb(160, 0, 0)'
};

let canvas = null;
let ctx = null;
let dpr = 1;
let receiver = null;
let settings = null;
let insets = {};
let outline = [];
// The outline as bearing and distance, derived once per change rather than per
// frame. The server sends plain coordinates, so both have to be computed here.
let outlinePolar = [];
let outlineMaxNm = 0;
let outlineScaleNm = 0;
let camera = { heading: 0, pitch: 0, fov: 75 };
let aircraft = new Map();
let selectedIcao = null;
let hoveredIcao = null;
let trail = [];
let trailColor = TRAIL_COLORS.normal;
// Whether this renderer is the visible one. Starts true so the module is usable on
// its own; the application sets it on every view change.
let active = true;
let frameRequested = false;
let anim = null;
let animToken = 0;
// The number of pointers down decides the gesture: one rotates, two pinch.
const pointers = new Map();
let drag = null;
let pinch = null;
let lastTap = null;
let hitIndex = [];
let lastFrame = null;
let hud = null;
let hudNodes = null;
// Wall time, read through one indirection so tests can pin it. The celestial
// positions are the only thing here that depends on the date rather than on
// elapsed time, and the test harness virtualises performance.now() but not Date.
let clock = () => new Date();
let markerClickCallback = null;
let mapClickCallback = null;
let markerHoverEnterCallback = null;
let markerHoverLeaveCallback = null;
let selectedTooltipCallback = null;

const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const overlaps = (a, b) =>
    a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

// ---------- lifecycle ----------

export function init(containerId) {
    const container = document.getElementById(containerId);
    canvas = document.createElement('canvas');
    if (container && container.appendChild) {
        container.appendChild(canvas);
    }
    ctx = canvas.getContext('2d');
    buildHud(container);

    canvas.addEventListener('pointerdown', onPointerDown);
    canvas.addEventListener('pointermove', onPointerMove);
    canvas.addEventListener('pointerup', onPointerUp);
    // A cancelled pointer never produces an up. Without this the gesture is never
    // torn down, and the camera carries on turning with nothing on screen.
    canvas.addEventListener('pointercancel', onPointerCancel);
    canvas.addEventListener('wheel', onWheel, { passive: false });

    resize();
    return canvas;
}

export function destroy() {
    canvas = null;
    ctx = null;
    hud = null;
    hudNodes = null;
    lastFrame = null;
    hitIndex = [];
}

// The readout lives in the DOM rather than on the canvas so it picks up the same
// typography, panel treatment and spacing as the rest of the interface, and lines
// up with the panels instead of floating at an arbitrary offset. It is written to
// imperatively — it changes on every frame of a drag, and routing that through the
// component tree would re-render the aircraft list sixty times a second.
// Bearing and elevation in the same three-digit bearing style as the heading
// readout, so the two line up when read together.
function formatAzEl(body) {
    const azimuth = String(Math.round(wrap360(body.azimuthDeg))).padStart(3, '0');
    return `${azimuth}° ${Math.round(body.elevationDeg)}°`;
}

function buildHud(container) {
    if (!container || !container.appendChild) return;

    const item = (labelText, leading) => {
        const wrap = document.createElement('div');
        wrap.className = 'sky-hud-item';
        const label = document.createElement('span');
        label.className = 'sky-hud-label';
        label.textContent = labelText;
        const value = document.createElement('span');
        value.className = 'sky-hud-value';
        if (leading) {
            wrap.appendChild(value);
            wrap.appendChild(label);
        } else {
            wrap.appendChild(label);
            wrap.appendChild(value);
        }
        return { wrap, value };
    };

    hud = document.createElement('div');
    // Carries the shared panel class, so its background, blur, shadow and corner
    // radius are the ones every other floating panel uses rather than a private copy
    // of the same values that can drift away from them.
    hud.className = 'panel sky-hud';

    const heading = item('HDG', false);
    const fov = item('FOV', false);
    const count = item('in view', true);
    const sun = item('SUN', false);
    const moon = item('MOON', false);
    const note = document.createElement('div');
    note.className = 'sky-hud-note';

    hud.appendChild(heading.wrap);
    hud.appendChild(fov.wrap);
    hud.appendChild(count.wrap);
    hud.appendChild(sun.wrap);
    hud.appendChild(moon.wrap);
    hud.appendChild(note);
    container.appendChild(hud);

    hudNodes = {
        heading: heading.value,
        fov: fov.value,
        fovItem: fov.wrap,
        count: count.value,
        sun: sun.value,
        sunItem: sun.wrap,
        moon: moon.value,
        moonItem: moon.wrap,
        note
    };
}

function setText(node, text) {
    if (node && node.textContent !== text) {
        node.textContent = text;
    }
}

export function resize() {
    if (!canvas || !ctx) return;

    dpr = Math.min(window.devicePixelRatio || 1, 2);
    const rect = canvas.getBoundingClientRect();
    canvas.width = Math.round(rect.width * dpr);
    canvas.height = Math.round(rect.height * dpr);
    // Scale once here so every draw call works in CSS pixels.
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    requestDraw();
}

// ---------- inputs ----------

export function setReceiver(lat, lon, altM) {
    receiver = { lat, lon, altM: altM || 0 };
    rebuildOutlinePolar();
    requestDraw();
}

export function setRangeOutline(coordinates) {
    outline = coordinates || [];
    rebuildOutlinePolar();
    requestDraw();
}

// The range outline arrives as bare latitude/longitude pairs — the farthest
// position seen in each bearing sector. The ribbon needs how far that is and in
// which direction, both of which follow from the receiver position, so they are
// derived here instead of asking the server for them.
function rebuildOutlinePolar() {
    if (!receiver || !outline.length) {
        outlinePolar = [];
        outlineMaxNm = 0;
        outlineScaleNm = 0;
        return;
    }

    outlinePolar = outline.map((point) => {
        const bearing = bearingTo(receiver.lat, receiver.lon, point.Latitude, point.Longitude);
        // Snapped to the sector it was recorded in. The coordinate is the farthest
        // aircraft seen in that sector, which may lie anywhere across its five
        // degrees — drawing a block centred on it would make neighbouring sectors
        // overlap and leave slivers between others, instead of tiling.
        const sectorStart = Math.floor(bearing / OUTLINE_SECTOR_DEG) * OUTLINE_SECTOR_DEG;
        return {
            bearing,
            sectorStart,
            sectorEnd: sectorStart + OUTLINE_SECTOR_DEG,
            distanceNm: haversineDistance(
                receiver.lat, receiver.lon, point.Latitude, point.Longitude
            ) / 1.852
        };
    });
    outlineMaxNm = outlinePolar.reduce((max, o) => Math.max(max, o.distanceNm), 0);
    outlineScaleNm = ribbonScaleNm(outlineMaxNm);
}

// A hidden renderer must neither paint nor publish. Its canvas is not on screen, so
// every frame it draws is wasted, and the tooltips it emits fight with the visible
// view's over the same state.
export function setActive(next) {
    active = next;
    if (active) requestDraw();
}

export function setSafeInsets(next) {
    insets = next || {};
    requestDraw();
}

export function setSettings(next) {
    settings = next;
    camera.fov = clamp(next.skyFov ?? 75, FOV_MIN, FOV_MAX);
    if (next.skyPitch != null) {
        camera.pitch = next.skyPitch;
    }
    if (canvas) {
        camera.pitch = clampCameraPitch(camera.pitch);
    }
    requestDraw();
}

export function updateMarkers(aircraftMap) {
    aircraft = aircraftMap;
    requestDraw();
}

export function highlightSelected(icao) {
    selectedIcao = icao;
    requestDraw();
}

export function clearSelection() {
    selectedIcao = null;
    requestDraw();
}

// Expects state-history entries carrying both a position and an altitude. The
// flat map trail cannot be used here: it has no altitude, so every point would be
// skipped and the path would silently never appear.
export function updateTrail(entries) {
    trail = entries || [];
    requestDraw();
}

export function clearTrail() {
    trail = [];
    requestDraw();
}

export function setTrailColor(category) {
    trailColor = TRAIL_COLORS[category] || TRAIL_COLORS.normal;
    requestDraw();
}

export function onMarkerClick(callback) { markerClickCallback = callback; }
export function onMapClick(callback) { mapClickCallback = callback; }
export function onMarkerHover(enterCb, leaveCb) {
    markerHoverEnterCallback = enterCb;
    markerHoverLeaveCallback = leaveCb;
}
export function onSelectedTooltip(callback) { selectedTooltipCallback = callback; }

// ---------- frame ----------

function currentSafe() {
    return safeArea(canvas.width / dpr, canvas.height / dpr, insets);
}

// Every redraw trigger funnels through here. Marker updates arrive at the 50 ms
// buffered-flush cadence and interaction far faster, so coalescing into one frame
// keeps a burst from queuing a draw per event. Nothing else may call draw().
function requestDraw() {
    if (frameRequested || !ctx) return;
    frameRequested = true;
    requestAnimationFrame(() => {
        frameRequested = false;
        draw();
    });
}

function project(azimuthDeg, elevationDeg, frame) {
    return settings.skyFlatten
        ? projectEquirect(azimuthDeg, elevationDeg, camera.heading, frame.panorama, frame.yHorizon)
        : projectRectilinear(enuVector(azimuthDeg, elevationDeg), frame.basis, frame.proj, camera.fov, frame.minCos);
}

// Horizontal position of a bearing along the azimuth axis. Culling is suppressed
// because pitching up puts the horizon below the frame, where the ordinary cull
// would reject it — yet the axis itself remains meaningful and is still drawn.
// Points behind the camera are still rejected.
function projectAxisX(bearingDeg, frame) {
    const p = settings.skyFlatten
        ? projectEquirect(bearingDeg, frame.hz.depressionDeg, camera.heading, frame.panorama, frame.yHorizon)
        : projectRectilinear(
            enuVector(bearingDeg, frame.hz.depressionDeg), frame.basis, frame.proj, camera.fov, 0
        );
    if (!p) return null;
    return p.x >= 0 && p.x <= frame.full.width ? p.x : null;
}

// Projects without the frustum cull. The moon's terminator is oriented by the
// on-screen direction to the sun, which stays meaningful when the sun itself is
// far outside the frame — so the sun must still yield a position there. Points
// behind the camera are still rejected.
function projectUnculled(azimuthDeg, elevationDeg, frame) {
    return settings.skyFlatten
        ? projectEquirect(azimuthDeg, elevationDeg, camera.heading, frame.panorama, frame.yHorizon)
        : projectRectilinear(
            enuVector(azimuthDeg, elevationDeg), frame.basis, frame.proj, camera.fov, 0
        );
}

// Pixel diameter of a body: the angle it really subtends, scaled up to something
// legible and then clamped. The angle drives it, so the relationship to the field
// of view and the moon's monthly variation both survive; the scale is what makes
// the phase readable.
function celestialSizePx(diameterDeg, frame) {
    // The flattened panorama is anisotropic — degrees per pixel differ across and
    // up — so a disc scaled from the true angle would be an ellipse. The horizontal
    // scale is used and the body stays a circle; at this width the whole sky is on
    // screen at once, so both bodies sit at the floor regardless.
    const px = settings.skyFlatten
        ? (diameterDeg / 360) * frame.panorama.width
        : focalPx(camera.fov, frame.safe.width) * diameterDeg * Math.PI / 180;
    return clamp(px * CELESTIAL_SCALE, CELESTIAL_MIN_PX, CELESTIAL_MAX_PX);
}

// Where the sun and moon are this instant, ready to draw.
//
// Computed once per frame for the whole frame: the readout needs the same values
// the drawing does, and the sun's position is needed even when the sun is not
// drawn, because it orients the moon. Recomputed every frame rather than on a
// timer — the arithmetic is a few microseconds, while a 30-second interval would
// step each body about 2 px at the default field of view, which is visible
// stuttering bought for nothing.
function computeCelestial(frame) {
    // Computed for either half of the feature. The markers need both bodies; the
    // sky tint needs the sun's elevation even when nothing is drawn for it, so the
    // two settings cannot gate the arithmetic between them.
    if (!settings.skyCelestial && !settings.skyTwilight) return null;

    const now = clock();
    const sun = sunPosition(now, receiver.lat, receiver.lon, receiver.altM);
    const moon = moonPosition(now, receiver.lat, receiver.lon, receiver.altM);

    return {
        sun: {
            ...sun,
            // Unculled, so it can orient the moon from off-screen at night.
            point: projectUnculled(sun.azimuthDeg, sun.elevationDeg, frame),
            // A sun behind the camera projects to nothing at all, and the moon still
            // has to be lit from the right side. Its antipode is in front of the
            // camera exactly then, and in a pinhole projection a great circle maps to
            // a straight line — so the moon, the sun and the antipode stay collinear
            // on screen and the direction is simply reversed.
            antipodePoint: projectUnculled(
                wrap360(sun.azimuthDeg + 180), -sun.elevationDeg, frame
            ),
            up: isUp(sun, frame.hz.depressionDeg),
            sizePx: celestialSizePx(sun.diameterDeg, frame)
        },
        moon: {
            ...moon,
            phase: moonPhase(now),
            point: project(moon.azimuthDeg, moon.elevationDeg, frame),
            up: isUp(moon, frame.hz.depressionDeg),
            sizePx: celestialSizePx(moon.diameterDeg, frame)
        }
    };
}

function projectPoint(coord, altM, frame) {
    const groundKm = haversineDistance(receiver.lat, receiver.lon, coord.Latitude, coord.Longitude);
    const azimuth = bearingTo(receiver.lat, receiver.lon, coord.Latitude, coord.Longitude);
    const { elevationDeg } = elevationAndRange(groundKm, altM, receiver.altM);
    return project(azimuth, elevationDeg, frame);
}

// Lowest the horizon may sit and still leave room for the compass ticks and the
// coverage ribbon, both of which hang off it.
function horizonFloor(safe) {
    // Tolerates settings not having arrived yet: the camera can be reset before the
    // first frame, and the renderer must not depend on ordering there.
    const ribbon = settings ? settings.skyRibbon : false;
    return safe.bottom - COMPASS_BAND_PX - (ribbon ? RIBBON_BAND_PX : 0);
}

// Pitch is bounded only by the zenith edge of the frame. The horizon is free to
// leave the view when the camera looks far enough up — that is what looking up
// means — and because it is always drawn where it truly projects, and screen
// position is monotonic in elevation, nothing above the horizon can ever appear
// below the line.
function clampCameraPitch(value, safe = currentSafe()) {
    return clampPitch(value, camera.fov, safe);
}

function horizonBaselineY(proj, basis, hz) {
    if (settings.skyFlatten) {
        return horizonFloor(proj);
    }

    // Culling is suppressed because this point is on the camera axis by construction
    // and may legitimately fall outside the frame — pitched far enough up, the
    // horizon leaves the view entirely. In a pinhole projection the horizontal plane
    // is a great circle, so the horizon is a straight horizontal line at any pitch.
    const p = projectRectilinear(
        enuVector(camera.heading, hz.depressionDeg), basis, proj, camera.fov, 0
    );
    return p ? p.y : proj.centreY;
}

function computeFrame() {
    const safe = currentSafe();
    const hz = horizon(receiver.altM);
    const basis = cameraBasis(camera.heading, camera.pitch);
    // The safe area governs where the camera centres and where the horizon sits;
    // painting still covers the whole canvas, so no strip is left blank where a
    // panel does not in fact reach. The panels are opaque and float above, exactly
    // as they do over the map.
    const full = { width: canvas.width / dpr, height: canvas.height / dpr };
    // The flattened panorama spans the whole canvas rather than the safe area.
    // Insetting it would squeeze all 360 degrees into what the panels leave over
    // and strand the rest of the width empty, and unlike the camera view there is
    // no centring to protect here: every bearing is on screen at once, and heading
    // drag brings anything hidden behind a panel back out.
    const panorama = { left: 0, width: full.width, top: safe.top };
    // Same rectangle, but with the principal point raised so a level camera puts the
    // horizon near the foot of the view instead of halfway up it.
    const proj = { ...safe, centreY: safe.top + HORIZON_AT_REST * safe.height };
    // Everywhere a point may land, measured about the principal point at the
    // centre of the safe area. The canvas reaches further on the panel side than
    // the safe area does, and culling to the safe area alone would leave that
    // strip permanently empty.
    const coverage = {
        width: 2 * Math.max(proj.centreX, full.width - proj.centreX),
        height: 2 * Math.max(proj.centreY, full.height - proj.centreY)
    };
    const frame = {
        safe, full, panorama, proj, hz, basis,
        minCos: frustumCosLimit(camera.fov, proj, coverage)
    };
    frame.yHorizon = horizonBaselineY(proj, basis, hz);
    // Rides with the horizon while it is in view, and falls back to the ribbon when
    // pitching up carries the horizon out of the frame — otherwise looking at the sky
    // would leave no bearing reference at all.
    frame.ribbonBottom = safe.bottom;
    frame.ribbonTop = frame.ribbonBottom - RIBBON_BAND_PX;
    frame.compassY = Math.min(frame.yHorizon, frame.ribbonTop - COMPASS_BAND_PX);
    // After the horizon baseline, which the flattened projection measures from.
    frame.celestial = computeCelestial(frame);
    // What the sky looks like this frame, and what everything drawn against it has
    // to do in response. With the tint switched off the view keeps the fixed daylight
    // sky it has always had, and the chip outlines stay dark to match it.
    frame.palette = settings.skyTwilight && frame.celestial
        ? skyPalette(frame.celestial.sun.elevationDeg)
        : defaultPalette();

    const maxRangeKm = nmToKm(settings.skyMaxRangeNm);
    const drawable = [];
    let belowHorizon = 0;
    let noAltitude = 0;
    let inRange = 0;

    for (const [icao, a] of aircraft) {
        if (!a.Coordinate) continue;

        const alt = aircraftAltitudeM(a);
        if (!alt && !a.IsOnGround) {
            noAltitude++;
            continue;
        }

        const groundKm = haversineDistance(
            receiver.lat, receiver.lon, a.Coordinate.Latitude, a.Coordinate.Longitude
        );
        // The subscription box circumscribes the range circle, so its corners hold
        // aircraft beyond range. Cull by true distance: the circle is what the user
        // should see, not the square.
        if (groundKm > maxRangeKm) continue;
        inRange++;

        const azimuth = bearingTo(
            receiver.lat, receiver.lon, a.Coordinate.Latitude, a.Coordinate.Longitude
        );
        const { elevationDeg, slantRangeKm } = elevationAndRange(
            groundKm, alt ? alt.metres : 0, receiver.altM
        );

        // Aircraft the Earth hides are clamped to the horizon in a distinct style
        // rather than culled: surface traffic would otherwise vanish, which reads
        // as a defect. The glyph is clamped; the reported elevation is not.
        const sub = a.IsOnGround || isSubHorizon(elevationDeg, hz.depressionDeg);
        const p = project(azimuth, sub ? hz.depressionDeg : elevationDeg, frame);
        if (!p) continue;
        // Counted after the visibility cull, so the readout describes what clamping
        // hides rather than what merely sits behind the camera.
        if (sub) belowHorizon++;

        drawable.push({
            icao,
            aircraft: a,
            x: p.x,
            // Pinned to the drawn baseline, which may itself have been clamped
            // upward to clear the reserved bands; the raw projected y would float
            // the mark below the horizon line.
            y: sub ? frame.yHorizon : p.y,
            azimuth,
            elevationDeg,
            slantRangeKm,
            sub,
            altitudeFeet: alt ? alt.metres / 0.3048 : 0,
            size: chipSizePx(slantRangeKm),
            alpha: hazeAlpha(slantRangeKm, maxRangeKm)
        });
    }

    // Far to near, so nearer chips paint over farther ones and nearer aircraft win
    // contested label slots and hit tests.
    drawable.sort((a, b) => b.slantRangeKm - a.slantRangeKm);
    Object.assign(frame, { drawable, belowHorizon, noAltitude, inRange });
    return frame;
}

// ---------- drawing ----------

function draw() {
    if (!active || !ctx || !receiver || !settings) return;

    const frame = computeFrame();
    lastFrame = frame;

    ctx.clearRect(0, 0, canvas.width / dpr, canvas.height / dpr);
    drawSkyGradient(frame);
    drawElevationGrid(frame);
    drawCompass(frame);
    drawHorizon(frame);
    // Behind every aircraft, so a chip crossing the sun stays readable.
    drawCelestial(frame);
    if (settings.skyRibbon) drawRibbon(frame);
    drawStems(frame);
    if (settings.skyTrail) drawTrail(frame);
    drawChips(frame);
    drawLabels(frame);
    drawHud(frame);

    hitIndex = frame.drawable
        .filter((d) => isOnScreen(d, frame))
        .map((d) => ({ icao: d.icao, x: d.x, y: d.y, r: d.size }));
    publishSelectedTooltip(frame);
}

// Sky above the horizon, ground below it, both edge to edge. There is no ground
// plane in the scene — at realistic antenna heights almost none would be visible —
// so the lower band is a plain tone that grounds the horizon rather than a
// perspective surface.
function drawSkyGradient(frame) {
    const skyHeight = Math.max(0, frame.yHorizon);
    if (skyHeight > 0) {
        // Deepest at the zenith and washing out towards the horizon, which is both
        // how the sky actually looks and where the colour can go without costing
        // anything: the top of the view is nearly always empty, while the band just
        // above the horizon is where the traffic is and where the chips — themselves
        // blue — need the background to stay out of their way.
        const gradient = ctx.createLinearGradient(0, 0, 0, frame.yHorizon);
        gradient.addColorStop(0, css(frame.palette.zenith));
        gradient.addColorStop(0.55, css(frame.palette.middle));
        gradient.addColorStop(1, css(frame.palette.horizon));
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, frame.full.width, skyHeight);
    }

    const groundHeight = Math.max(0, frame.full.height - frame.yHorizon);
    if (groundHeight > 0) {
        // A muted earth tone. The band really is the ground — occluded by the curve of
        // the Earth rather than absent — so warmth is honest here even though no
        // terrain is drawn on it, and it separates from the sky far better than a
        // neutral did. Kept desaturated on purpose: the selected aircraft is orange,
        // and a clamped one sits astride this very boundary, so a deeper brown would
        // start swallowing it. Green is avoided for a different reason — it is the
        // military category colour.
        // Follows the sky, or a warm band sits glowing under a night horizon.
        ctx.fillStyle = css(frame.palette.ground);
        ctx.fillRect(0, frame.yHorizon, frame.full.width, groundHeight);
    }
}

// Strokes a polyline, lifting the pen at a null point and wherever two consecutive
// points land on opposite sides of the view. The second case is the panorama seam:
// a path crossing due north leaves one edge and re-enters at the other, and joining
// those points would draw a line straight back across everything between them.
function strokeSeamAware(points, seamJump) {
    ctx.beginPath();
    let penDown = false;
    let previousX = 0;

    for (const p of points) {
        if (!p) {
            penDown = false;
            continue;
        }
        if (penDown && Math.abs(p.x - previousX) > seamJump) {
            penDown = false;
        }
        if (penDown) {
            ctx.lineTo(p.x, p.y);
        } else {
            ctx.moveTo(p.x, p.y);
            penDown = true;
        }
        previousX = p.x;
    }

    ctx.stroke();
}

// Constant-elevation small circles, sampled in azimuth. Spacing is uneven on
// purpose: nearly all traffic sits below 30 degrees, so the grid is tighter low
// down where it is needed.
function drawElevationGrid(frame) {
    ctx.strokeStyle = css(frame.palette.ink, 0.08);
    ctx.lineWidth = 1;

    for (const elevation of [10, 20, 30, 45, 60]) {
        const points = [];
        for (let az = camera.heading - 180; az <= camera.heading + 180; az += 2) {
            points.push(project(wrap360(az), elevation, frame));
        }
        strokeSeamAware(points, frame.full.width / 2);
    }
}

// Three levels, so a glance finds a direction and a closer look reads a bearing:
// a cardinal letter, a numeric bearing every 30 degrees, and a bare tick every 10.
// A single uniform row of numbers gives the eye nothing to land on.
function drawCompass(frame) {
    ctx.textAlign = 'center';

    // Degrees covered by a pixel at the centre of the view. In the camera view this
    // varies across the frame — a degree covers fewer pixels towards the edges — so
    // the centre is the representative value and ticks crowd slightly at the sides,
    // which is the perspective being honest.
    const degreesPerPixel = settings.skyFlatten
        ? 360 / Math.max(1, frame.full.width)
        : 180 / Math.PI / focalPx(camera.fov, frame.safe.width);
    const tickStep = bearingTickStep(degreesPerPixel, TICK_TARGET_PX);
    const labelStep = bearingLabelStep(tickStep, degreesPerPixel, LABEL_TARGET_PX);

    for (let az = 0; az < 360; az += tickStep) {
        const x = projectAxisX(az, frame);
        if (x === null) continue;

        const cardinal = CARDINALS[az];
        const labelled = az % labelStep === 0;

        ctx.fillStyle = cardinal
            ? css(frame.palette.ink, 0.65)
            : css(frame.palette.ink, 0.28);
        ctx.fillRect(x, frame.compassY, cardinal ? 2 : 1, cardinal ? 11 : labelled ? 8 : 4);

        if (cardinal) {
            ctx.font = CARDINAL_FONT;
            ctx.fillStyle = css(frame.palette.ink, 0.75);
            ctx.fillText(cardinal, x, frame.compassY + 24);
        } else if (labelled) {
            ctx.font = LABEL_FONT;
            ctx.fillStyle = css(frame.palette.ink, 0.45);
            ctx.fillText(String(az).padStart(3, '0'), x, frame.compassY + 20);
        }
    }

    ctx.textAlign = 'left';
    ctx.font = LABEL_FONT;
}

function drawHorizon(frame) {
    ctx.strokeStyle = css(frame.palette.ink, 0.45);
    ctx.lineWidth = 1.25;
    ctx.beginPath();
    ctx.moveTo(0, frame.yHorizon);
    ctx.lineTo(frame.full.width, frame.yHorizon);
    ctx.stroke();
}

// The sun and the moon where they actually are.
//
// Nothing is drawn for a body that is down. Aircraft the Earth hides are clamped
// to the horizon in a distinct style so that surface traffic does not silently
// vanish, but a sun that has set is genuinely not there and a marker for it would
// be an invention.
function drawCelestial(frame) {
    const celestial = frame.celestial;
    // The markers' own gate: the positions may have been computed purely to colour
    // the sky, in which case there is nothing to draw.
    if (!celestial || !settings.skyCelestial) return;

    if (celestial.sun.up && celestial.sun.point) {
        drawSun(celestial.sun);
    }
    if (celestial.moon.up && celestial.moon.point) {
        drawMoon(celestial.moon, celestial.sun);
    }
}

// A disc with a soft surround. The surround is two translucent rings rather than a
// radial gradient: it reads the same at these sizes, and the warm rim is what
// keeps it from being mistaken for an aircraft chip, which is cool blue and
// hard-edged.
function drawSun(sun) {
    const { x, y } = sun.point;
    const r = sun.sizePx / 2;

    ctx.fillStyle = SUN_HALO;
    for (const [scale, alpha] of [[SUN_HALO_SCALE, 0.10], [1.7, 0.18]]) {
        ctx.globalAlpha = alpha;
        ctx.beginPath();
        ctx.arc(x, y, r * scale, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.globalAlpha = 1;

    ctx.fillStyle = SUN_CORE;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = SUN_RIM;
    ctx.lineWidth = 1;
    ctx.stroke();
}

// A disc with the unlit part shaded.
//
// The terminator is oriented by the on-screen direction from the moon to the sun.
// The lit side faces the sun by definition, so taking the direction in screen
// space is exactly right and sidesteps converting the astronomical position angle
// of the bright limb through the parallactic angle. It holds at night too, because
// the sun's position is computed whether or not it is drawn.
function drawMoon(moon, sun) {
    const { x, y } = moon.point;
    const r = moon.sizePx / 2;
    const lit = moon.phase.fraction;

    let dx = -1;
    let dy = 0;
    if (sun.point) {
        dx = sun.point.x - x;
        dy = sun.point.y - y;
    } else if (sun.antipodePoint) {
        dx = x - sun.antipodePoint.x;
        dy = y - sun.antipodePoint.y;
    }

    ctx.save();
    ctx.translate(x, y);
    // Local frame with the sun along positive x, so the lit limb is the right-hand
    // side of the disc and the terminator is symmetric about the x axis.
    ctx.rotate(Math.atan2(dy, dx));

    if (lit < MOON_OUTLINE_FRACTION) {
        // Too thin to fill: a hairline crescent would disappear, and an outline at
        // least says where the moon is.
        ctx.strokeStyle = MOON_RIM;
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();
        return;
    }

    // The whole disc in the unlit tone, then the lit region painted over it.
    ctx.fillStyle = MOON_UNLIT;
    ctx.beginPath();
    ctx.arc(0, 0, r, 0, Math.PI * 2);
    ctx.fill();

    // The terminator is a half ellipse whose width follows the illuminated
    // fraction: the full disc at new and full, a straight line at half, and it
    // crosses to the far side as the moon becomes gibbous.
    const waist = r * Math.abs(1 - 2 * lit);
    ctx.fillStyle = MOON_LIT;
    ctx.beginPath();
    ctx.arc(0, 0, r, -Math.PI / 2, Math.PI / 2);
    ctx.ellipse(0, 0, waist, r, 0, Math.PI / 2, -Math.PI / 2, lit < 0.5);
    ctx.closePath();
    ctx.fill();

    ctx.strokeStyle = MOON_RIM;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(0, 0, r, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
}

// Measured reception range per bearing, on its own distance scale taken from the
// largest value present. The range-outline tracker reaches far beyond the Sky
// View's own maximum range, so clipping the ribbon to the view's range would
// discard real coverage.
function drawRibbon(frame) {
    // The tracker only emits an outline once enough bearing sectors are populated,
    // so a short list means there is not yet a coverage shape worth showing.
    if (outlinePolar.length < 3 || !(outlineScaleNm > 0)) return;

    // The plotted area sits inside the band, so a bar at full scale still leaves room
    // for its label and nothing is drawn outside the strip.
    const plotTop = frame.ribbonTop + RIBBON_PAD_TOP;
    const plotBottom = frame.ribbonBottom - RIBBON_PAD_BOTTOM;
    const height = plotBottom - plotTop;

    // Its own backing. Pitched up the ground leaves the frame, and without this the
    // bars would sit on bare sky looking like part of the scene rather than a scale
    // along its foot.
    ctx.fillStyle = css(frame.palette.panel, 0.55);
    ctx.fillRect(0, frame.ribbonTop, frame.full.width, frame.ribbonBottom - frame.ribbonTop);
    ctx.strokeStyle = css(frame.palette.ink, 0.10);
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(0, frame.ribbonTop);
    ctx.lineTo(frame.full.width, frame.ribbonTop);
    ctx.stroke();

    // Gridlines at full width, which reads as a chart rather than as stray marks now
    // that each one is labelled with its value.
    ctx.strokeStyle = css(frame.palette.ink, 0.08);
    ctx.beginPath();
    for (const y of [plotTop, plotTop + height / 2]) {
        ctx.moveTo(0, y);
        ctx.lineTo(frame.full.width, y);
    }
    ctx.stroke();

    // One path filled once, rather than a rectangle per sector. Filling the union
    // keeps the tone even: overlapping translucent rectangles would darken where
    // they meet, and abutting ones leave a hairline seam from antialiasing.
    const spans = [];
    ctx.beginPath();
    for (const o of outlinePolar) {
        const left = project(wrap360(o.sectorStart), frame.hz.depressionDeg, frame);
        const right = project(wrap360(o.sectorEnd), frame.hz.depressionDeg, frame);
        if (!left || !right) continue;

        const x = Math.min(left.x, right.x);
        const width = Math.abs(right.x - left.x);
        // A sector straddling the panorama seam wraps to the far edge, and would
        // otherwise be drawn as a block spanning most of the view.
        if (!(width > 0) || width > frame.full.width / 4) continue;

        // Scaled to the rounded reach, not to the view's maximum range: reception
        // commonly extends beyond the range being drawn, and clipping to that would
        // discard real measured coverage.
        const bar = height * clamp(o.distanceNm / outlineScaleNm, 0, 1);
        ctx.rect(x, plotBottom - bar, width, bar);
        spans.push([x, x + width]);
    }
    ctx.fillStyle = css(frame.palette.ribbon, 0.38);
    ctx.fill();

    // Baseline, so the profile reads as sitting on zero rather than floating.
    ctx.strokeStyle = css(frame.palette.ink, 0.25);
    ctx.beginPath();
    ctx.moveTo(0, plotBottom);
    ctx.lineTo(frame.full.width, plotBottom);
    ctx.stroke();

    drawRibbonAxis(frame, plotTop, plotBottom);
}

// Without these the bars are self-evidently something, but nothing says what. Every
// tick carries its unit: reading a bare "50" against a distance scale means working
// out what it is measured in from the one label that happens to say.
function drawRibbonAxis(frame, plotTop, plotBottom) {
    const half = plotTop + (plotBottom - plotTop) / 2;
    const ticks = [
        [plotTop, `${outlineScaleNm} nm`],
        [half, `${Math.round(outlineScaleNm / 2)} nm`],
        [plotBottom, '0 nm']
    ];
    const right = frame.safe.right - RIBBON_AXIS_MARGIN;

    ctx.font = LABEL_FONT;
    ctx.textAlign = 'right';

    for (const [y, text] of ticks) {
        const width = ctx.measureText(text).width;
        // A chip behind each, because a bar may reach any height here and plain text
        // over one is unreadable. Centred on its gridline, which the band's padding
        // guarantees room for.
        ctx.fillStyle = css(frame.palette.panel, 0.8);
        ctx.fillRect(right - width - 4, y - 6, width + 7, 12);
        ctx.fillStyle = css(frame.palette.ink, 0.6);
        ctx.fillText(text, right, y + 3.5);
    }

    ctx.textAlign = 'left';
}

// Ties each airborne chip to its bearing on the compass and to the ribbon bar
// below it. Sub-horizon aircraft already sit on the horizon, so a stem would have
// no length.
function drawStems(frame) {
    ctx.strokeStyle = css(frame.palette.ink, 0.12);
    ctx.lineWidth = 1;
    ctx.beginPath();

    for (const d of frame.drawable) {
        if (d.sub) continue;
        ctx.moveTo(d.x, d.y);
        ctx.lineTo(d.x, frame.yHorizon);
    }

    ctx.stroke();
}

// The history records barometric altitude while a chip is placed on geometric
// altitude, and the two differ by several hundred feet. Left alone the trail would
// run parallel to the flight path but never meet the chip at its end. The aircraft
// reports both right now, so its own difference lifts the whole series onto the
// same datum the chip uses. Zero when it reports only one of the two, in which case
// both are already on that one.
function trailDatumOffsetM() {
    const selected = selectedIcao ? aircraft.get(selectedIcao) : null;
    const geometric = selected?.GeometricAltitude?.Meters;
    const barometric = selected?.BarometricAltitude?.Meters;

    if (geometric == null || barometric == null) return 0;
    return geometric - barometric;
}

function drawTrail(frame) {
    if (!trail.length) return;

    const datumOffsetM = trailDatumOffsetM();

    const points = [];
    for (const entry of trail) {
        // An entry without a position or an altitude is a sample of something else
        // — a speed-only update, say — not evidence that the aircraft left a gap in
        // its path. Skipped entirely, so the line continues across it; lifting the
        // pen here would put a break in the trail every time one arrived.
        if (!entry.position || entry.altitudeMeters == null) {
            continue;
        }
        // Leaving the frame is different: the path genuinely goes off-screen there,
        // so a null is pushed and the segments stay broken rather than bridging.
        points.push(projectPoint(entry.position, entry.altitudeMeters + datumOffsetM, frame));
    }

    ctx.strokeStyle = trailColor;
    ctx.lineWidth = 1.5;
    strokeSeamAware(points, frame.full.width / 2);
}

// Reuses the map's altitude ramp and category palettes so the two views cannot
// drift apart visually.
function colorOf(d) {
    if (d.icao === selectedIcao) return SELECTED_COLOR;

    const a = d.aircraft;
    const prefix = a.Military ? 'military' : (a.Ladd || a.Pia) ? 'privacy' : 'normal';
    const category = CATEGORIES.find((c) => c.prefix === prefix) || CATEGORIES[0];
    return interpolateColor(d.altitudeFeet, category.stops);
}

function drawChips(frame) {
    // Sub-horizon marks first, so a genuine low-elevation aircraft is never
    // occluded by a clamped one.
    for (const d of frame.drawable) {
        if (d.sub) drawSubHorizonMark(d, frame);
    }
    for (const d of frame.drawable) {
        if (!d.sub) drawChip(d, frame);
    }
}

// A flattened half-height mark sitting on the horizon. It must read as "at or
// below your horizon", never as "in the sky at zero degrees".
function drawSubHorizonMark(d, frame) {
    const [r, g, b] = colorOf(d);
    const w = d.size;
    const h = Math.max(2, d.size * 0.35);

    ctx.globalAlpha = d.alpha * 0.8;
    ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
    ctx.fillRect(d.x - w / 2, d.y - h / 2, w, h);
    ctx.globalAlpha = 1;
    const outline = frame.palette.outline;
    ctx.strokeStyle = css(outline.rgb, outline.alpha * 0.9);
    ctx.lineWidth = 0.75;
    ctx.strokeRect(d.x - w / 2, d.y - h / 2, w, h);
}

function drawChip(d, frame) {
    const [r, g, b] = colorOf(d);

    ctx.globalAlpha = d.alpha;
    ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
    ctx.beginPath();
    ctx.arc(d.x, d.y, d.size / 2, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 1;
    // A definite outline, so a chip is found by its edge rather than by its fill
    // standing out from the sky. The low-altitude end of the ramp is pale blue on a
    // pale blue sky — barely over 1.3:1 — and without this the background could
    // never be given any colour without losing those aircraft.
    //
    // The tone follows the sky rather than being fixed black: black is 1.12:1
    // against a night sky, so a fixed outline would lose exactly the aircraft this
    // outline exists to keep.
    const outline = frame.palette.outline;
    ctx.strokeStyle = css(outline.rgb, outline.alpha);
    ctx.lineWidth = 1;
    ctx.stroke();

    drawVelocityTick(d, frame);
}

function drawVelocityTick(d, frame) {
    const track = d.aircraft.Track ?? d.aircraft.TrackOnGround;
    if (track == null) return;

    const ahead = destinationPoint(
        d.aircraft.Coordinate.Latitude, d.aircraft.Coordinate.Longitude, track, 2
    );
    const p2 = projectPoint(ahead, d.altitudeFeet * 0.3048, frame);
    if (!p2) return;

    const length = Math.hypot(p2.x - d.x, p2.y - d.y);
    if (length < MIN_TICK_PX) return;

    const scale = Math.min(1, (d.size * 0.9) / length);
    ctx.beginPath();
    ctx.moveTo(d.x, d.y);
    ctx.lineTo(d.x + (p2.x - d.x) * scale, d.y + (p2.y - d.y) * scale);
    ctx.stroke();
}

// Greedy slot assignment, walked nearest-first so nearer aircraft win a contested
// slot. A plain distance threshold would be worse than useless: it labels most
// densely exactly where traffic is densest and labels therefore collide.
// The sun and the moon, as label entries for the layout below.
//
// Deliberately independent of the label mode. There are only ever two of them and
// they are landmarks rather than clutter, and the mode defaults to selection-only
// on a phone — which would leave the two discs unnamed exactly where saying what
// they are helps most. A near-new moon in particular draws as a bare outline that
// reads as nothing at all without its label.
function celestialLabels(frame) {
    const celestial = frame.celestial;
    if (!celestial || !settings.skyCelestial) return [];

    // One offset for both, from the larger of the two discs, so the moon's 12 per
    // cent monthly variation cannot reintroduce a mismatch. The offsets are then not
    // merely similar but identical.
    const radius = Math.max(celestial.sun.sizePx, celestial.moon.sizePx) / 2;
    const reach = radius * CELESTIAL_LABEL_REACH + LABEL_GAP_PX;

    const labels = [];
    const add = (body, text) => {
        if (!body.up || !body.point) return;
        if (!isOnScreen(body.point, frame)) return;
        labels.push({ text, x: body.point.x, y: body.point.y - reach });
    };

    add(celestial.sun, 'Sun');
    add(celestial.moon, 'Moon');
    return labels;
}

function layoutLabels(frame) {
    const mode = settings.skyLabels || 'auto';
    const occupied = [];
    const placed = [];
    ctx.font = LABEL_FONT;

    // Placed before the traffic, so the two fixed points in the view keep their
    // labels and the callsigns arrange themselves around them.
    for (const label of celestialLabels(frame)) {
        const width = ctx.measureText(label.text).width;
        occupied.push({
            x: label.x - width / 2, y: label.y - LABEL_LINE_H, w: width, h: LABEL_LINE_H
        });
        placed.push(label);
    }

    for (let i = frame.drawable.length - 1; i >= 0; i--) {
        const d = frame.drawable[i];

        // The selected and hovered aircraft already have their callsign in a
        // tooltip, so a label here would just duplicate it next to the chip.
        if (d.icao === selectedIcao || d.icao === hoveredIcao) continue;
        if (mode === 'selection') continue;
        // A busy airport would otherwise pile labels along the horizon.
        if (d.sub) continue;

        const text = d.aircraft.Callsign || d.icao;
        // Centred above the chip: offset to one side reads as belonging to whatever
        // sits that way, which is ambiguous once chips are close together.
        const x = d.x;
        const y = d.y - d.size - LABEL_GAP_PX;
        const width = ctx.measureText(text).width;
        const rect = { x: x - width / 2, y: y - LABEL_LINE_H, w: width, h: LABEL_LINE_H };

        if (mode !== 'all' && occupied.some((o) => overlaps(o, rect))) continue;

        occupied.push(rect);
        placed.push({ text, x, y });
    }

    return placed;
}

function drawLabels(frame) {
    ctx.fillStyle = css(frame.palette.ink, 0.75);
    ctx.font = LABEL_FONT;
    ctx.textAlign = 'center';
    for (const label of layoutLabels(frame)) {
        ctx.fillText(label.text, label.x, label.y);
    }
    ctx.textAlign = 'left';
}

function drawHud(frame) {
    if (!hudNodes) return;

    setText(hudNodes.heading, `${String(Math.round(camera.heading)).padStart(3, '0')}°`);
    setText(hudNodes.count, `${frame.drawable.length}/${frame.inRange}`);

    // Field of view has no meaning once the whole sky is on screen at fixed scale.
    if (settings.skyFlatten) {
        hudNodes.fovItem.style.display = 'none';
    } else {
        hudNodes.fovItem.style.display = '';
        setText(hudNodes.fov, `${Math.round(camera.fov)}°`);
    }

    // Each body is reported only while it is up, for the same reason neither is
    // drawn when it is down: a bearing to a sun that has set is not information.
    const celestial = settings.skyCelestial ? frame.celestial : null;
    const sunUp = celestial && celestial.sun.up;
    hudNodes.sunItem.style.display = sunUp ? '' : 'none';
    if (sunUp) {
        setText(hudNodes.sun, formatAzEl(celestial.sun));
    }
    const moonUp = celestial && celestial.moon.up;
    hudNodes.moonItem.style.display = moonUp ? '' : 'none';
    if (moonUp) {
        // The illuminated fraction is the part worth reading off: it says which
        // shape on screen is the right one.
        const percent = Math.round(celestial.moon.phase.fraction * 100);
        setText(hudNodes.moon, `${formatAzEl(celestial.moon)} · ${percent}%`);
    }

    // Disclose what is not drawn as an ordinary chip, so the clamping and the
    // exclusions are visible rather than quietly applied.
    const notes = [];
    if (frame.belowHorizon) notes.push(`${frame.belowHorizon} below horizon`);
    if (frame.noAltitude) notes.push(`${frame.noAltitude} no altitude`);
    setText(hudNodes.note, notes.join(' · '));
    hudNodes.note.style.display = notes.length ? '' : 'none';

    // Matches the panels' own inset from the corner, so the readout lines up with
    // them rather than sitting proud of the top edge.
    hud.style.left = `${(insets.left || 0) + 16}px`;
    hud.style.top = `${(insets.top || 0) + 16}px`;
}

// Shape matched to what the shared hover tooltip already reads. The true
// elevation is reported even for a chip clamped to the horizon, which is what
// keeps the clamping honest.
function hoverPayload(d) {
    const altitude = d.aircraft.GeometricAltitude ?? d.aircraft.BarometricAltitude;
    const speed = d.aircraft.Speed ?? d.aircraft.SpeedOnGround;

    return {
        icao: d.icao,
        callsign: d.aircraft.Callsign,
        // Plain numbers in knots and feet, matching what the map emits: the shared
        // tooltip converts from those, and handing it the wrapper objects instead
        // produces NaN rather than an error.
        altitude: altitude ? altitude.Feet : null,
        speed: speed ? speed.Knots : null,
        x: d.x,
        y: d.y,
        azimuthDeg: d.azimuth,
        elevationDeg: d.elevationDeg
    };
}

// Whether a chip is actually on screen. The frustum cull works against a cone that
// circumscribes the canvas, so an aircraft just outside the frame survives it — its
// chip is then harmlessly clipped, but a tooltip anchored to it would be pulled back
// into view by the edge clamping and point at nothing.
function isOnScreen(d, frame) {
    return d.x >= 0 && d.x <= frame.full.width && d.y >= 0 && d.y <= frame.full.height;
}

function publishSelectedTooltip(frame) {
    if (!selectedTooltipCallback) return;
    const d = frame.drawable.find((x) => x.icao === selectedIcao);
    selectedTooltipCallback(d && isOnScreen(d, frame) ? hoverPayload(d) : null);
}

// ---------- interaction ----------

function hitTest(clientX, clientY) {
    const rect = canvas.getBoundingClientRect();
    const x = clientX - rect.left;
    const y = clientY - rect.top;

    // Backwards, because the draw list is ordered far to near.
    for (let i = hitIndex.length - 1; i >= 0; i--) {
        const h = hitIndex[i];
        if (Math.hypot(h.x - x, h.y - y) <= Math.max(HIT_RADIUS_PX, h.r)) return h;
    }

    return null;
}

// Begins a rotate gesture from wherever the given pointer currently is. Called on
// the first pointer down, and again when a pinch drops back to one finger — the
// second case is why it takes a position rather than reading the event: continuing
// against the position the *first* finger started at would jump the view.
function beginDrag(x, y) {
    const rect = canvas.getBoundingClientRect();
    const safe = currentSafe();
    drag = {
        x,
        y,
        // Offsets from the view centre, so the angle under the pointer can be
        // compared between where the drag began and where it is now.
        originX: rect.left + safe.centreX,
        originY: rect.top + safe.centreY,
        moved: 0,
        heading: camera.heading,
        pitch: camera.pitch,
        // A gesture that became a pinch is never a tap, even after the second
        // finger lifts and this reverts to a drag.
        pinched: drag ? drag.pinched : false
    };
}

const pointerDistance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

function onPointerDown(e) {
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    canvas.setPointerCapture(e.pointerId);

    if (pointers.size === 1) {
        drag = null;
        beginDrag(e.clientX, e.clientY);
        return;
    }

    if (pointers.size === 2) {
        const [a, b] = [...pointers.values()];
        if (drag) drag.pinched = true;
        pinch = { startDistance: pointerDistance(a, b), startFov: camera.fov };
        drag = null;
    }
}

function onPointerMove(e) {
    if (pointers.has(e.pointerId)) {
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    }

    if (pinch && pointers.size >= 2) {
        anim = null;
        // Field of view has no meaning in the flattened panorama, where the whole
        // sky is already on screen at a fixed scale — the wheel is inert there too.
        if (settings.skyFlatten) return;

        const [a, b] = [...pointers.values()];
        const spread = pointerDistance(a, b);
        if (!(spread > 0) || !(pinch.startDistance > 0)) return;

        // Measured from the start of the gesture rather than the previous move, so
        // it cannot accumulate drift: spreading the fingers widens the separation
        // and narrows the view, which is zooming in.
        camera.fov = clamp(
            pinch.startFov * (pinch.startDistance / spread),
            FOV_MIN,
            FOV_MAX
        );
        // Re-clamped after the change: a wider view moves where the horizon lands.
        camera.pitch = clampCameraPitch(camera.pitch);
        requestDraw();
        return;
    }

    if (!drag) {
        const hit = hitTest(e.clientX, e.clientY);
        const previous = hoveredIcao;
        hoveredIcao = hit ? hit.icao : null;

        if (hit) {
            if (markerHoverEnterCallback) {
                const d = lastFrame && lastFrame.drawable.find((x) => x.icao === hit.icao);
                markerHoverEnterCallback(d ? hoverPayload(d) : { icao: hit.icao, x: hit.x, y: hit.y });
            }
        } else if (markerHoverLeaveCallback) {
            markerHoverLeaveCallback();
        }

        // Hovering hides the aircraft's sky label, since the tooltip now shows the
        // callsign. The tooltip appears at once, so the label has to go at once too;
        // waiting for the next marker update leaves both on screen for up to a
        // second. Redrawn only when the hovered aircraft actually changes, so moving
        // the pointer across empty sky costs nothing.
        if (hoveredIcao !== previous) {
            requestDraw();
        }
        return;
    }

    // A deliberate drag overrides any camera animation still in flight.
    anim = null;
    const safe = currentSafe();
    drag.moved = Math.max(
        drag.moved,
        Math.hypot(e.clientX - drag.x, e.clientY - drag.y)
    );

    // The turn is the difference between the angle under the pointer now and the
    // angle under it when the drag began, so whatever was grabbed stays grabbed.
    const turn = viewAngleAt(e.clientX - drag.originX, safe)
        - viewAngleAt(drag.x - drag.originX, safe);
    camera.heading = wrap360(drag.heading - turn);

    if (!settings.skyFlatten) {
        const tilt = viewAngleAt(e.clientY - drag.originY, safe)
            - viewAngleAt(drag.y - drag.originY, safe);
        camera.pitch = clampCameraPitch(drag.pitch + tilt, safe);
    }
    requestDraw();
}

// Shared teardown. `wasTap` is false for a cancel: a pointer taken away by the
// browser is not a click.
function endPointer(e, wasTap) {
    pointers.delete(e.pointerId);
    canvas.releasePointerCapture(e.pointerId);

    const moved = drag ? drag.moved : 0;
    const pinched = pinch !== null || (drag && drag.pinched);

    if (pointers.size >= 2) return;

    if (pointers.size === 1) {
        // Dropping from two fingers to one: resume rotating from where the
        // surviving finger is now, not from where the first one started.
        pinch = null;
        const [remaining] = [...pointers.values()];
        drag = null;
        beginDrag(remaining.x, remaining.y);
        if (pinched) drag.pinched = true;
        return;
    }

    pinch = null;
    drag = null;

    if (!wasTap || pinched || moved > DRAG_THRESHOLD_PX) return;

    const now = performance.now();
    const isDouble = lastTap
        && now - lastTap.time <= DOUBLE_TAP_MS
        && Math.hypot(e.clientX - lastTap.x, e.clientY - lastTap.y) <= DOUBLE_TAP_SLOP_PX;

    if (isDouble) {
        // Only this tap's selection is suppressed; the first has already acted.
        // Holding every tap to see whether a second follows would put a visible
        // delay on ordinary selection, which is the far more common action.
        lastTap = null;
        resetCamera();
        return;
    }

    lastTap = { time: now, x: e.clientX, y: e.clientY };

    const hit = hitTest(e.clientX, e.clientY);
    if (hit) {
        if (markerClickCallback) markerClickCallback(hit.icao);
    } else if (mapClickCallback) {
        mapClickCallback();
    }
}

function onPointerUp(e) {
    endPointer(e, true);
}

function onPointerCancel(e) {
    endPointer(e, false);
}

function onWheel(e) {
    e.preventDefault();
    // Field of view is meaningless once the whole sky is on screen at fixed scale.
    if (settings.skyFlatten) return;

    camera.fov = clamp(camera.fov * (e.deltaY > 0 ? 1.08 : 1 / 1.08), FOV_MIN, FOV_MAX);
    // Re-clamped after the change: a wider view moves where the horizon lands.
    camera.pitch = clampCameraPitch(camera.pitch);
    requestDraw();
}

// Restores heading, pitch, and field of view together. Leaving heading untouched
// would make the gesture a partial reset, and an in-flight swing would otherwise
// keep turning the camera after the user asked for a reset.
function resetCamera() {
    anim = null;
    camera.heading = 0;
    camera.fov = 75;
    camera.pitch = clampCameraPitch(0);
    requestDraw();
}

// Counterpart of the map's pan-to: brings a location into view by swinging the
// camera to its bearing. Because the projection's principal point is the centre of
// the safe area, aiming the camera axis at the aircraft places it clear of the
// panels automatically, with no offset arithmetic.
// Where an aircraft being brought into view should end up vertically, as a
// fraction of the view height. Not the principal point, which sits low at 82% —
// an aircraft parked just above the horizon is technically in frame but reads as
// an afterthought.
const FOCUS_TARGET_HEIGHT = 0.45;

// Turning to face an aircraft is not enough on its own: one passing overhead sits
// far above a level camera's frame, so the view swings to the right bearing and
// still shows empty sky. Pitch is therefore brought along, but only when the
// aircraft would otherwise be off-frame — nudging the camera for something already
// comfortably in view would be movement for its own sake.
function focusPitchFor(elevationDeg, safe) {
    const focal = focalPx(camera.fov, safe.width);
    if (!(focal > 0)) return camera.pitch;

    const axisY = safe.top + HORIZON_AT_REST * safe.height;
    // Where it lands now, at the current pitch.
    const currentY = axisY - focal * Math.tan((elevationDeg - camera.pitch) * Math.PI / 180);
    const top = safe.top + 24;
    const bottom = safe.top + safe.height * 0.9;
    if (currentY >= top && currentY <= bottom) return camera.pitch;

    // Otherwise tilt so it lands at the target height.
    const targetY = safe.top + FOCUS_TARGET_HEIGHT * safe.height;
    const above = (Math.atan((axisY - targetY) / focal) * 180) / Math.PI;
    return clampCameraPitch(elevationDeg - above, safe);
}

export function focusOn(lat, lon, altitudeM = 0) {
    if (!receiver) return;

    const target = bearingTo(receiver.lat, receiver.lon, lat, lon);
    const headingDelta = shortestTurnDeg(camera.heading, target);

    let pitchDelta = 0;
    if (!settings || !settings.skyFlatten) {
        const safe = currentSafe();
        const groundKm = haversineDistance(receiver.lat, receiver.lon, lat, lon);
        const { elevationDeg } = elevationAndRange(groundKm, altitudeM, receiver.altM);
        pitchDelta = focusPitchFor(elevationDeg, safe) - camera.pitch;
    }

    if (Math.abs(headingDelta) < 0.5 && Math.abs(pitchDelta) < 0.5) return;

    // The token makes a second call supersede the first rather than leaving two
    // animation chains fighting over the camera.
    anim = {
        fromHeading: camera.heading,
        headingDelta,
        fromPitch: camera.pitch,
        pitchDelta,
        t0: performance.now(),
        token: ++animToken
    };
    stepSwing(anim.token);
}

function stepSwing(token) {
    if (!anim || anim.token !== token) return;

    const t = Math.min(1, (performance.now() - anim.t0) / SWING_MS);
    const eased = 1 - Math.pow(1 - t, 3);
    camera.heading = wrap360(anim.fromHeading + anim.headingDelta * eased);
    camera.pitch = anim.fromPitch + anim.pitchDelta * eased;
    requestDraw();

    if (t < 1) {
        requestAnimationFrame(() => stepSwing(token));
    } else {
        anim = null;
    }
}

// Test-only window onto internal state. The renderer keeps its frame and label
// layout private, but those are exactly what needs asserting, and reconstructing
// them from recorded draw calls would test the reconstruction rather than the
// renderer. Not referenced by application code.
export const __test = {
    state: () => ({ camera, hitIndex, lastFrame }),
    // Test setup needs a deterministic way back to the default camera. The gesture
    // that does this in the application is a double-tap, which is exercised by its
    // own tests; driving it from every setUp would make unrelated tests depend on
    // gesture timing and would fire a stray deselect on the first tap.
    resetCamera: () => resetCamera(),
    labels: () => (lastFrame ? layoutLabels(lastFrame) : []),
    setHovered: (icao) => { hoveredIcao = icao; },
    // Celestial positions depend on the date, and the test harness virtualises
    // performance.now() but not Date. Passing null restores the wall clock.
    setClock: (fn) => { clock = fn || (() => new Date()); },
    hud: () => hudNodes
};
