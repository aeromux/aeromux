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
    safeArea,
    chipSizePx,
    hazeAlpha,
    wrap360,
    shortestTurnDeg,
    isSubHorizon,
    aircraftAltitudeM,
    destinationPoint,
    clampPitch
} from '../Services/SkyViewGeometry.js';
import { haversineDistance, nmToKm } from '../Services/UnitConversion.js';
import { CATEGORIES, SELECTED_COLOR, interpolateColor } from '../Map/AircraftIcons.js';

// Space reserved below the horizon for the compass labels and the coverage
// ribbon. The ribbon hangs off the horizon rather than off the bottom of the
// frame so that it shares the azimuth axis with the sky above it: a chip's stem
// runs down to the ribbon bar at the same bearing.
const COMPASS_BAND_PX = 18;
const RIBBON_BAND_PX = 34;

// Shorter than this and the velocity tick is a meaningless stub, which is worse
// than no tick: a track pointing at or away from the receiver foreshortens to
// nothing.
const MIN_TICK_PX = 4;

const HIT_RADIUS_PX = 18;
const FOV_MIN = 30;
const FOV_MAX = 120;
const DEG_PER_PX = 0.15;
const DRAG_THRESHOLD_PX = 4;
const SWING_MS = 400;
const LABEL_FONT = '10px InterVariable, Inter, system-ui, sans-serif';
const LABEL_LINE_H = 11;
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
let camera = { heading: 0, pitch: 15, fov: 75 };
let aircraft = new Map();
let selectedIcao = null;
let hoveredIcao = null;
let trail = [];
let trailColor = TRAIL_COLORS.normal;
let frameRequested = false;
let anim = null;
let animToken = 0;
let drag = null;
let hitIndex = [];
let lastFrame = null;
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

    canvas.addEventListener('pointerdown', onPointerDown);
    canvas.addEventListener('pointermove', onPointerMove);
    canvas.addEventListener('pointerup', onPointerUp);
    canvas.addEventListener('wheel', onWheel, { passive: false });
    canvas.addEventListener('dblclick', resetCamera);

    resize();
    return canvas;
}

export function destroy() {
    canvas = null;
    ctx = null;
    lastFrame = null;
    hitIndex = [];
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
    requestDraw();
}

export function setRangeOutline(coordinates) {
    outline = coordinates || [];
    requestDraw();
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
        camera.pitch = clampPitch(camera.pitch, camera.fov, currentSafe());
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
        ? projectEquirect(azimuthDeg, elevationDeg, camera.heading, frame.safe, frame.yHorizon)
        : projectRectilinear(enuVector(azimuthDeg, elevationDeg), frame.basis, frame.safe, camera.fov, frame.minCos);
}

function projectPoint(coord, altM, frame) {
    const groundKm = haversineDistance(receiver.lat, receiver.lon, coord.Latitude, coord.Longitude);
    const azimuth = bearingTo(receiver.lat, receiver.lon, coord.Latitude, coord.Longitude);
    const { elevationDeg } = elevationAndRange(groundKm, altM, receiver.altM);
    return project(azimuth, elevationDeg, frame);
}

function horizonBaselineY(safe, basis, hz) {
    const reserved = COMPASS_BAND_PX + (settings.skyRibbon ? RIBBON_BAND_PX : 0);
    const floor = safe.bottom - reserved;

    if (settings.skyFlatten) {
        return floor;
    }

    // The on-axis horizon point is always inside the frame, so culling is
    // suppressed. In a pinhole projection the horizontal plane is a great circle,
    // which means the horizon is a straight horizontal line at any pitch.
    const p = projectRectilinear(
        enuVector(camera.heading, hz.depressionDeg), basis, safe, camera.fov, 0
    );
    return Math.min(p ? p.y : safe.centreY, floor);
}

function computeFrame() {
    const safe = currentSafe();
    const hz = horizon(receiver.altM);
    const basis = cameraBasis(camera.heading, camera.pitch);
    const frame = { safe, hz, basis, minCos: frustumCosLimit(camera.fov, safe) };
    frame.yHorizon = horizonBaselineY(safe, basis, hz);
    frame.ribbonTop = frame.yHorizon + COMPASS_BAND_PX;
    frame.ribbonBottom = Math.min(safe.bottom, frame.ribbonTop + RIBBON_BAND_PX);

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
    if (!ctx || !receiver || !settings) return;

    const frame = computeFrame();
    lastFrame = frame;

    ctx.clearRect(0, 0, canvas.width / dpr, canvas.height / dpr);
    drawSkyGradient(frame);
    drawElevationGrid(frame);
    drawCompass(frame);
    drawHorizon(frame);
    if (settings.skyRibbon) drawRibbon(frame);
    drawStems(frame);
    if (settings.skyTrail) drawTrail(frame);
    drawChips(frame);
    drawLabels(frame);
    drawHud(frame);

    hitIndex = frame.drawable.map((d) => ({ icao: d.icao, x: d.x, y: d.y, r: d.size }));
    publishSelectedTooltip(frame);
}

function drawSkyGradient(frame) {
    const gradient = ctx.createLinearGradient(0, frame.safe.top, 0, frame.yHorizon);
    gradient.addColorStop(0, '#cfe3f2');
    gradient.addColorStop(1, '#eef5fa');
    ctx.fillStyle = gradient;
    ctx.fillRect(
        frame.safe.left, frame.safe.top, frame.safe.width, frame.yHorizon - frame.safe.top
    );
}

// Constant-elevation small circles, sampled in azimuth and drawn as broken
// polylines. Spacing is uneven on purpose: nearly all traffic sits below 30
// degrees, so the grid is tighter low down where it is needed.
function drawElevationGrid(frame) {
    ctx.strokeStyle = 'rgba(0, 0, 0, 0.08)';
    ctx.lineWidth = 1;

    for (const elevation of [10, 20, 30, 45, 60]) {
        ctx.beginPath();
        let penDown = false;
        for (let az = camera.heading - 180; az <= camera.heading + 180; az += 2) {
            const p = project(wrap360(az), elevation, frame);
            if (!p) {
                penDown = false;
                continue;
            }
            if (penDown) {
                ctx.lineTo(p.x, p.y);
            } else {
                ctx.moveTo(p.x, p.y);
                penDown = true;
            }
        }
        ctx.stroke();
    }
}

function drawCompass(frame) {
    ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
    ctx.font = LABEL_FONT;

    for (let az = 0; az < 360; az += 10) {
        const p = project(az, frame.hz.depressionDeg, frame);
        if (!p) continue;
        const major = az % 30 === 0;
        ctx.fillRect(p.x, frame.yHorizon, 1, major ? 7 : 4);
        if (major) {
            ctx.fillText(String(az).padStart(3, '0'), p.x - 9, frame.yHorizon + 16);
        }
    }
}

function drawHorizon(frame) {
    ctx.strokeStyle = 'rgba(0, 0, 0, 0.45)';
    ctx.lineWidth = 1.25;
    ctx.beginPath();
    ctx.moveTo(frame.safe.left, frame.yHorizon);
    ctx.lineTo(frame.safe.right, frame.yHorizon);
    ctx.stroke();
}

// Measured reception range per bearing, on its own distance scale taken from the
// largest value present. The range-outline tracker reaches far beyond the Sky
// View's own maximum range, so clipping the ribbon to the view's range would
// discard real coverage.
function drawRibbon(frame) {
    if (!outline || outline.length < 3) return;

    const maxNm = Math.max(...outline.map((o) => o.DistanceNm || 0));
    if (!(maxNm > 0)) return;

    const height = frame.ribbonBottom - frame.ribbonTop;
    ctx.fillStyle = 'rgba(0, 97, 146, 0.25)';

    for (const o of outline) {
        const bearing = o.Bearing
            ?? bearingTo(receiver.lat, receiver.lon, o.Latitude, o.Longitude);
        const p = project(bearing, frame.hz.depressionDeg, frame);
        if (!p) continue;
        const bar = height * clamp((o.DistanceNm || 0) / maxNm, 0, 1);
        ctx.fillRect(p.x - 1, frame.ribbonBottom - bar, 2, bar);
    }
}

// Ties each airborne chip to its bearing on the compass and to the ribbon bar
// below it. Sub-horizon aircraft already sit on the horizon, so a stem would have
// no length.
function drawStems(frame) {
    ctx.strokeStyle = 'rgba(0, 0, 0, 0.12)';
    ctx.lineWidth = 1;
    ctx.beginPath();

    for (const d of frame.drawable) {
        if (d.sub) continue;
        ctx.moveTo(d.x, d.y);
        ctx.lineTo(d.x, frame.yHorizon);
    }

    ctx.stroke();
}

function drawTrail(frame) {
    if (!trail.length) return;

    ctx.strokeStyle = trailColor;
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    let penDown = false;

    for (const entry of trail) {
        // Altitude is nullable in the history, so break the path rather than
        // interpolating across a gap and drawing a line that was never flown.
        if (!entry.position || entry.altitudeMeters == null) {
            penDown = false;
            continue;
        }
        const p = projectPoint(entry.position, entry.altitudeMeters, frame);
        if (!p) {
            penDown = false;
            continue;
        }
        if (penDown) {
            ctx.lineTo(p.x, p.y);
        } else {
            ctx.moveTo(p.x, p.y);
            penDown = true;
        }
    }

    ctx.stroke();
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
        if (d.sub) drawSubHorizonMark(d);
    }
    for (const d of frame.drawable) {
        if (!d.sub) drawChip(d, frame);
    }
}

// A flattened half-height mark sitting on the horizon. It must read as "at or
// below your horizon", never as "in the sky at zero degrees".
function drawSubHorizonMark(d) {
    const [r, g, b] = colorOf(d);
    const w = d.size;
    const h = Math.max(2, d.size * 0.35);

    ctx.globalAlpha = d.alpha * 0.8;
    ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
    ctx.fillRect(d.x - w / 2, d.y - h / 2, w, h);
    ctx.globalAlpha = 1;
    ctx.strokeStyle = 'rgba(0, 0, 0, 0.35)';
    ctx.lineWidth = 0.5;
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
    ctx.strokeStyle = 'rgba(0, 0, 0, 0.4)';
    ctx.lineWidth = 0.75;
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
function layoutLabels(frame) {
    const mode = settings.skyLabels || 'auto';
    const occupied = [];
    const placed = [];
    ctx.font = LABEL_FONT;

    for (let i = frame.drawable.length - 1; i >= 0; i--) {
        const d = frame.drawable[i];
        // Losing the label of the aircraft just clicked would be worse than an
        // overlap, so selection and hover always win and skip the collision test.
        const forced = d.icao === selectedIcao || d.icao === hoveredIcao;

        if (!forced) {
            if (mode === 'selection') continue;
            // A busy airport would otherwise pile labels along the horizon.
            if (d.sub) continue;
        }

        const text = d.aircraft.Callsign || d.icao;
        const x = d.x + d.size;
        const y = d.y - d.size;
        const rect = { x, y: y - LABEL_LINE_H, w: ctx.measureText(text).width, h: LABEL_LINE_H };

        if (!forced && mode !== 'all' && occupied.some((o) => overlaps(o, rect))) continue;

        occupied.push(rect);
        placed.push({ text, x, y });
    }

    return placed;
}

function drawLabels(frame) {
    ctx.fillStyle = 'rgba(0, 0, 0, 0.75)';
    ctx.font = LABEL_FONT;
    for (const label of layoutLabels(frame)) {
        ctx.fillText(label.text, label.x, label.y);
    }
}

function drawHud(frame) {
    const parts = [
        `HDG ${String(Math.round(camera.heading)).padStart(3, '0')}°`,
        settings.skyFlatten ? '360°' : `FOV ${Math.round(camera.fov)}°`,
        `${frame.drawable.length}/${frame.inRange} in view`
    ];

    // Disclose what is not drawn as a normal chip, so the clamping and the
    // exclusions are visible on screen rather than quietly applied.
    if (frame.belowHorizon) parts.push(`${frame.belowHorizon} below horizon`);
    if (frame.noAltitude) parts.push(`${frame.noAltitude} no altitude`);

    ctx.fillStyle = 'rgba(0, 0, 0, 0.55)';
    ctx.font = LABEL_FONT;
    ctx.fillText(parts.join(' · '), frame.safe.left + 10, frame.safe.top + 16);
}

// Shape matched to what the shared hover tooltip already reads. The true
// elevation is reported even for a chip clamped to the horizon, which is what
// keeps the clamping honest.
function hoverPayload(d) {
    return {
        icao: d.icao,
        callsign: d.aircraft.Callsign,
        altitude: d.aircraft.GeometricAltitude ?? d.aircraft.BarometricAltitude,
        speed: d.aircraft.Speed ?? d.aircraft.SpeedOnGround,
        x: d.x,
        y: d.y,
        azimuthDeg: d.azimuth,
        elevationDeg: d.elevationDeg
    };
}

function publishSelectedTooltip(frame) {
    if (!selectedTooltipCallback) return;
    const d = frame.drawable.find((x) => x.icao === selectedIcao);
    selectedTooltipCallback(d ? hoverPayload(d) : null);
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

function onPointerDown(e) {
    drag = {
        x: e.clientX,
        y: e.clientY,
        moved: 0,
        heading: camera.heading,
        pitch: camera.pitch
    };
    canvas.setPointerCapture(e.pointerId);
}

function onPointerMove(e) {
    if (!drag) {
        const hit = hitTest(e.clientX, e.clientY);
        hoveredIcao = hit ? hit.icao : null;
        if (hit) {
            if (markerHoverEnterCallback) {
                const d = lastFrame && lastFrame.drawable.find((x) => x.icao === hit.icao);
                markerHoverEnterCallback(d ? hoverPayload(d) : { icao: hit.icao, x: hit.x, y: hit.y });
            }
        } else if (markerHoverLeaveCallback) {
            markerHoverLeaveCallback();
        }
        return;
    }

    // A deliberate drag overrides any camera animation still in flight.
    anim = null;
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    drag.moved = Math.max(drag.moved, Math.hypot(dx, dy));
    camera.heading = wrap360(drag.heading - dx * DEG_PER_PX);
    if (!settings.skyFlatten) {
        camera.pitch = clampPitch(drag.pitch + dy * DEG_PER_PX, camera.fov, currentSafe());
    }
    requestDraw();
}

function onPointerUp(e) {
    const wasDrag = drag && drag.moved > DRAG_THRESHOLD_PX;
    drag = null;
    canvas.releasePointerCapture(e.pointerId);

    if (wasDrag) return;

    const hit = hitTest(e.clientX, e.clientY);
    if (hit) {
        if (markerClickCallback) markerClickCallback(hit.icao);
    } else if (mapClickCallback) {
        mapClickCallback();
    }
}

function onWheel(e) {
    e.preventDefault();
    // Field of view is meaningless once the whole sky is on screen at fixed scale.
    if (settings.skyFlatten) return;

    camera.fov = clamp(camera.fov * (e.deltaY > 0 ? 1.08 : 1 / 1.08), FOV_MIN, FOV_MAX);
    camera.pitch = clampPitch(camera.pitch, camera.fov, currentSafe());
    requestDraw();
}

// Restores heading, pitch, and field of view together. Leaving heading untouched
// would make the gesture a partial reset, and an in-flight swing would otherwise
// keep turning the camera after the user asked for a reset.
function resetCamera() {
    anim = null;
    camera.heading = 0;
    camera.fov = 75;
    camera.pitch = clampPitch(15, 75, currentSafe());
    requestDraw();
}

// Counterpart of the map's pan-to: brings a location into view by swinging the
// camera to its bearing. Because the projection's principal point is the centre of
// the safe area, aiming the camera axis at the aircraft places it clear of the
// panels automatically, with no offset arithmetic.
export function focusOn(lat, lon) {
    if (!receiver) return;

    const target = bearingTo(receiver.lat, receiver.lon, lat, lon);
    const delta = shortestTurnDeg(camera.heading, target);
    if (Math.abs(delta) < 0.5) return;

    // The token makes a second call supersede the first rather than leaving two
    // animation chains fighting over the heading.
    anim = { from: camera.heading, delta, t0: performance.now(), token: ++animToken };
    stepSwing(anim.token);
}

function stepSwing(token) {
    if (!anim || anim.token !== token) return;

    const t = Math.min(1, (performance.now() - anim.t0) / SWING_MS);
    camera.heading = wrap360(anim.from + anim.delta * (1 - Math.pow(1 - t, 3)));
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
    labels: () => (lastFrame ? layoutLabels(lastFrame) : []),
    setHovered: (icao) => { hoveredIcao = icao; }
};
