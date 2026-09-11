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

// Pure receiver-centric sky geometry. No DOM, no framework, so it is directly
// testable under `node --test`.

// Mean Earth radius. The effective radius folds in standard optical refraction,
// which bends light slightly around the curve and so raises the apparent
// elevation of distant targets. The optical value suits "where would I point a
// camera"; switching this one constant to 4/3 gives the radio-propagation figure.
export const EARTH_RADIUS_M = 6371000;
export const REFRACTION_K = 7 / 6;
export const EFFECTIVE_RADIUS_M = EARTH_RADIUS_M * REFRACTION_K;

const DEG = Math.PI / 180;
const RAD = 180 / Math.PI;

const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

// Smallest forward component treated as being in front of the camera.
export const MIN_FORWARD = 1e-4;

// Chip size ramp bounds. True angular size is useless here — a 70 m airliner
// subtends about a quarter of a pixel at 100 nm — so the glyph is a symbol and
// the ramp is log-compressed between a legible floor and a modest ceiling. A
// true 1/distance law would imply a 250x size ratio across the range.
export const CHIP_MIN_PX = 6;
export const CHIP_MAX_PX = 16;
export const CHIP_NEAR_KM = 2;
export const CHIP_FAR_KM = 300;

// ---------- angles ----------

// Normalizes to [0, 360).
export function wrap360(deg) {
    const d = deg % 360;
    return d < 0 ? d + 360 : d;
}

// Normalizes to (-180, 180].
export function wrap180(deg) {
    const d = wrap360(deg);
    return d > 180 ? d - 360 : d;
}

// ---------- bearing, elevation, horizon ----------

// Initial great-circle bearing from one coordinate to another, in degrees
// clockwise from true north.
export function bearingTo(lat1, lon1, lat2, lon2) {
    const phi1 = lat1 * DEG;
    const phi2 = lat2 * DEG;
    const dLambda = (lon2 - lon1) * DEG;
    const y = Math.sin(dLambda) * Math.cos(phi2);
    const x = Math.cos(phi1) * Math.sin(phi2) - Math.sin(phi1) * Math.cos(phi2) * Math.cos(dLambda);
    return wrap360(Math.atan2(y, x) * RAD);
}

// Elevation angle and slant range to an aircraft, corrected for the curvature of
// the Earth and for atmospheric refraction.
//
// Working in the plane through the Earth's centre, the receiver, and the
// aircraft: place the receiver at (0, r1) and the aircraft at
// (r2*sin gamma, r2*cos gamma). The slant range is then the chord between them,
// and (r2*cos gamma - r1) / slant is the cosine of the zenith angle, hence the
// sine of the elevation. A single asin covers both hemispheres and returns
// negative values for aircraft below the horizon, which is what the renderer
// wants.
//
// The correction is not cosmetic. An aircraft at 10 km altitude and 300 km ground
// range sits at about 0.75 degrees — essentially on the horizon — where naive
// flat-Earth trigonometry would claim 1.91 degrees and float it in clear sky.
export function elevationAndRange(groundKm, aircraftAltM, receiverAltM = 0) {
    const radius = EFFECTIVE_RADIUS_M;
    const gamma = (groundKm * 1000) / radius;
    const r1 = radius + receiverAltM;
    const r2 = radius + aircraftAltM;
    const cosGamma = Math.cos(gamma);
    const slantM = Math.sqrt(r1 * r1 + r2 * r2 - 2 * r1 * r2 * cosGamma);

    if (slantM < 1e-6) {
        return { elevationDeg: 90, slantRangeKm: 0 };
    }

    return {
        elevationDeg: Math.asin(clamp((r2 * cosGamma - r1) / slantM, -1, 1)) * RAD,
        slantRangeKm: slantM / 1000
    };
}

// The receiver's horizon: how far below level it sits, and how far away it is.
// A receiver at sea level sees the horizon exactly level at zero distance, which
// is why the scene has no ground plane — at realistic antenna heights there is
// almost no visible ground to draw.
export function horizon(receiverAltM = 0) {
    const radius = EFFECTIVE_RADIUS_M;

    if (!(receiverAltM > 0)) {
        return { depressionDeg: 0, distanceKm: 0 };
    }

    const angle = Math.acos(clamp(radius / (radius + receiverAltM), -1, 1));
    return { depressionDeg: -(angle * RAD), distanceKm: (radius * angle) / 1000 };
}

// Lowest altitude that still clears the horizon at a given ground range. Used for
// diagnostics, and it explains why sub-horizon traffic is rare in practice: an
// aircraft low enough to need clamping is usually too low to be received at that
// range at all.
export function minAltitudeAboveHorizonM(groundKm, receiverAltM = 0) {
    const radius = EFFECTIVE_RADIUS_M;
    return (radius + receiverAltM) / Math.cos((groundKm * 1000) / radius) - radius;
}

// ---------- camera ----------

// Unit vector in local east / north / up coordinates.
export function enuVector(azimuthDeg, elevationDeg) {
    const a = azimuthDeg * DEG;
    const e = elevationDeg * DEG;
    const cosE = Math.cos(e);
    return [cosE * Math.sin(a), cosE * Math.cos(a), Math.sin(e)];
}

// Right-handed camera basis from a heading (clockwise from north) and a pitch
// (above the horizon).
export function cameraBasis(headingDeg, pitchDeg) {
    const psi = headingDeg * DEG;
    const phi = pitchDeg * DEG;
    const sinPsi = Math.sin(psi);
    const cosPsi = Math.cos(psi);
    const sinPhi = Math.sin(phi);
    const cosPhi = Math.cos(phi);

    return {
        forward: [cosPhi * sinPsi, cosPhi * cosPsi, sinPhi],
        right: [cosPsi, -sinPsi, 0],
        up: [-sinPsi * sinPhi, -cosPsi * sinPhi, cosPhi]
    };
}

export function focalPx(fovDeg, safeWidth) {
    return (safeWidth / 2) / Math.tan((fovDeg * DEG) / 2);
}

// Cosine of the half-angle of the cone that circumscribes the drawable area.
// Anything outside it cannot land on screen and must be rejected before the
// perspective divide: a point close to the camera plane divided by a near-zero
// forward component projects to coordinates in the tens of thousands instead of
// being culled, which then poisons grid polylines and hit testing.
//
// The focal length comes from the safe area, because that is what sets the scale
// and the principal point. The cone, though, has to cover everywhere a point may
// legitimately land. Pass `coverage` — the extent of the full canvas measured
// about the principal point — whenever the two differ, or content that belongs in
// the strip a panel does not actually reach gets culled and leaves it blank.
export function frustumCosLimit(fovDeg, safe, coverage = safe) {
    const focal = focalPx(fovDeg, safe.width);
    return Math.cos(Math.atan(Math.hypot(coverage.width / 2, coverage.height / 2) / focal));
}

// ---------- projection ----------

// Rectilinear (camera-like) projection. The principal point is the centre of the
// safe area rather than of the canvas: panels float above a full-viewport canvas,
// so projecting to the canvas centre would place the scene's centre — and
// anything the camera has just swung to — behind a panel.
//
// Returns null for anything outside the frame cone. Pass minCos = 0 to project a
// point already known to be on-axis without culling it.
export function projectRectilinear(v, basis, safe, fovDeg, minCos) {
    const zc = dot(v, basis.forward);
    const limit = Math.max(MIN_FORWARD, minCos ?? frustumCosLimit(fovDeg, safe));

    if (zc <= limit) {
        return null;
    }

    const focal = focalPx(fovDeg, safe.width);
    return {
        x: safe.centreX + focal * (dot(v, basis.right) / zc),
        y: safe.centreY - focal * (dot(v, basis.up) / zc)
    };
}

// Equirectangular projection for the flattened 360-degree panorama. Azimuth maps
// linearly across the full width; elevation runs from the horizon baseline up to
// the top of the safe area. The baseline is not the bottom of the canvas — the
// compass labels and the coverage ribbon live below it — so it is passed in.
export function projectEquirect(azimuthDeg, elevationDeg, headingDeg, safe, yHorizon) {
    const alpha = wrap180(azimuthDeg - headingDeg);
    return {
        x: safe.left + safe.width * (0.5 + alpha / 360),
        y: yHorizon - (elevationDeg / 90) * (yHorizon - safe.top)
    };
}

// Vertical extent implied by a horizontal field of view and the aspect ratio. The
// whole sky does not fit at once on a typical desktop: 75 degrees horizontal on a
// 1400x900 area yields only about 53 degrees vertically, which is why pitch is a
// useful control rather than a decoration.
export function verticalFovDeg(fovDeg, safe) {
    return 2 * Math.atan((safe.height / 2) / focalPx(fovDeg, safe.width)) * RAD;
}

// ---------- layout ----------

// The canvas rectangle minus whatever panels currently cover it. With zero insets
// this degenerates to the full canvas.
export function safeArea(width, height, insets = {}) {
    const left = insets.left || 0;
    const top = insets.top || 0;
    const w = Math.max(1, width - left - (insets.right || 0));
    const h = Math.max(1, height - top - (insets.bottom || 0));

    return {
        left,
        top,
        width: w,
        height: h,
        right: left + w,
        bottom: top + h,
        centreX: left + w / 2,
        centreY: top + h / 2
    };
}

// Allowed pitch range. The lower bound is level, since nothing is drawn below the
// horizon and tilting down would show empty space. The upper bound keeps the
// camera from tilting past the zenith edge of the frame, and widens as the field
// of view narrows — a zoomed-in view may lose the horizon, a wide one may not.
export function pitchRange(fovDeg, safe) {
    return { min: 0, max: Math.max(0, 90 - verticalFovDeg(fovDeg, safe) / 2) };
}

export function clampPitch(pitchDeg, fovDeg, safe) {
    const { min, max } = pitchRange(fovDeg, safe);
    return clamp(pitchDeg, min, max);
}

// ---------- appearance ----------

// Log-compressed size ramp; see the CHIP_* constants for why it is not physical.
export function chipSizePx(slantKm) {
    const s = Math.max(slantKm, CHIP_NEAR_KM);
    const u = clamp(
        (Math.log(s) - Math.log(CHIP_NEAR_KM)) / (Math.log(CHIP_FAR_KM) - Math.log(CHIP_NEAR_KM)),
        0,
        1
    );
    return CHIP_MAX_PX + (CHIP_MIN_PX - CHIP_MAX_PX) * u;
}

// Atmospheric haze, so the far edge of coverage recedes instead of competing for
// attention. Carries the depth cue that the clamped size ramp gives up.
export function hazeAlpha(slantKm, maxRangeKm) {
    return 1 - 0.65 * clamp(slantKm / maxRangeKm, 0, 1);
}

// ---------- misc ----------

// Signed turn to a target heading, taking the shorter way around the compass.
export function shortestTurnDeg(fromDeg, toDeg) {
    return wrap180(toDeg - fromDeg);
}

export function isSubHorizon(elevationDeg, horizonDepressionDeg) {
    return elevationDeg < horizonDepressionDeg;
}

// Altitude to place an aircraft at, in metres, preferring GNSS over barometric.
//
// Barometric altitude in ADS-B is always referenced to standard pressure, at
// every level, so in a non-standard atmosphere it can be out by roughly 1000 ft
// at QNH 980. For a chart of altitude against time that is a constant offset; for
// an elevation angle it is not, since 500 ft at 5 nm moves an aircraft about a
// degree in the sky. Returns null when neither altitude is reported.
export function aircraftAltitudeM(aircraft) {
    const geometric = aircraft.GeometricAltitude;
    const barometric = aircraft.BarometricAltitude;

    if (geometric && geometric.Meters != null) {
        return { metres: geometric.Meters, source: 'geometric' };
    }

    if (barometric && barometric.Meters != null) {
        return { metres: barometric.Meters, source: 'barometric' };
    }

    return null;
}

// Coordinate a given distance along a bearing. The velocity tick is drawn by
// re-projecting a point ahead along the aircraft's track, so a track pointing at
// or away from the receiver correctly foreshortens to nothing.
export function destinationPoint(lat, lon, bearingDeg, distanceKm) {
    const delta = distanceKm / (EARTH_RADIUS_M / 1000);
    const theta = bearingDeg * DEG;
    const phi1 = lat * DEG;
    const lambda1 = lon * DEG;
    const sinPhi1 = Math.sin(phi1);
    const cosPhi1 = Math.cos(phi1);
    const sinDelta = Math.sin(delta);
    const cosDelta = Math.cos(delta);

    const phi2 = Math.asin(sinPhi1 * cosDelta + cosPhi1 * sinDelta * Math.cos(theta));
    const lambda2 = lambda1 + Math.atan2(
        Math.sin(theta) * sinDelta * cosPhi1,
        cosDelta - sinPhi1 * Math.sin(phi2)
    );

    return { Latitude: phi2 * RAD, Longitude: wrap180(lambda2 * RAD) };
}

// Round a measured reach up to the next step, giving the coverage ribbon a scale
// that only changes in jumps. Normalising to the raw maximum instead would rescale
// the whole profile every time a single distant contact arrived, so a block's height
// would mean a different number of miles from one minute to the next.
export const RIBBON_SCALE_STEP_NM = 50;

export function ribbonScaleNm(maxNm, stepNm = RIBBON_SCALE_STEP_NM) {
    if (!(maxNm > 0)) return 0;
    return Math.ceil(maxNm / stepNm) * stepNm;
}

// ---------- subscription bounds ----------

// Circumscribing latitude/longitude box around the receiver, used as the
// aircraft subscription region when the Sky View is active.
//
// Longitude is sized at the box's *poleward* edge, where a degree of longitude
// is shortest. Sizing at the receiver's own latitude under-covers the poleward
// corners — about 5% at 50 degrees north with a 150 nm range.
//
// Several degenerate cases cannot be expressed as a non-inverted box and fall
// back to full-width longitude. The antimeridian case is the important one: a
// receiver at 179 degrees east yields west = 176.5, east = -178.5, and the
// server's viewport test is a plain `lon >= west && lon <= east` range check
// with no wrap handling, so an inverted box would match nothing at all and the
// view would be silently empty.
export function receiverBox(lat, lon, rangeKm) {
    const dLat = rangeKm / 111.32;
    const south = lat - dLat;
    const north = lat + dLat;

    const fullWidth = {
        south: clamp(south, -85, 85),
        west: -180,
        north: clamp(north, -85, 85),
        east: 180
    };

    // A pole inside the box: every longitude is within range.
    if (north >= 85 || south <= -85) {
        return fullWidth;
    }

    const cosPoleward = Math.cos((Math.abs(lat) + dLat) * DEG);
    if (cosPoleward < 1e-6) {
        return fullWidth;
    }

    const dLon = rangeKm / (111.32 * cosPoleward);
    if (dLon >= 180) {
        return fullWidth;
    }

    const west = lon - dLon;
    const east = lon + dLon;
    if (west < -180 || east > 180) {
        return fullWidth;
    }

    return { south, west, north, east };
}
