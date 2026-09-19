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

// Where an aircraft in the air lands on a tilted map, and whether it can land there
// at all. Takes plain numbers and a matrix rather than a map, so it has no DOM and no
// MapLibre in it and runs directly under `node --test`.
//
// Two consumers share it and must not disagree: the aircraft placement in
// Map/MapManager.js, which turns a height into a coordinate, and Map/AltitudeLayer.js,
// which draws the stalk that has to arrive at the same point.
//
// Every formula here is MapLibre's own, restated rather than imported, because the
// library exposes none of them to callers. Where that is true it is noted, since the
// two have to stay in step.

// MapLibre measures altitude against a sphere of this radius, and derives its pixel
// scale from the same figure, so both the lift and the ground scale below come from
// one constant rather than from the more familiar equatorial one.
export const EARTH_RADIUS_M = 6371008.8;
export const EARTH_CIRCUMFERENCE_M = 2 * Math.PI * EARTH_RADIUS_M;

// MapLibre's transform holds this regardless of the tile size a source declares: the
// raster tiles in this map are 256 pixels and are simply requested a zoom level
// deeper. Using 256 here would halve every altitude on screen, which is a mistake
// that looks plausible rather than broken, so it is stated once and tested.
export const TRANSFORM_TILE_SIZE = 512;

const DEG = Math.PI / 180;

// Circumference of the line of latitude, which is what a meter is measured against
// once the map is in mercator. MapLibre's circumferenceAtLatitude.
export function circumferenceAtLatitude(latitude) {
    return EARTH_CIRCUMFERENCE_M * Math.cos(latitude * DEG);
}

// How much ground one screen pixel covers. The map's scale, and with it everything
// about how visible an altitude is.
export function metersPerPixel(latitude, zoom) {
    return circumferenceAtLatitude(latitude) / (TRANSFORM_TILE_SIZE * Math.pow(2, zoom));
}

// Mercator coordinates. x and y carry no unit of length at all, which is why z is
// expressed against the circumference at its own latitude. MapLibre's
// mercatorXfromLng, mercatorYfromLat and mercatorZfromAltitude.
export function mercatorXFromLongitude(longitude) {
    return (180 + longitude) / 360;
}

export function mercatorYFromLatitude(latitude) {
    return (180 - (180 / Math.PI) * Math.log(Math.tan(Math.PI / 4 + (latitude * DEG) / 2))) / 360;
}

export function mercatorZFromAltitude(altitudeMeters, latitude) {
    return altitudeMeters / circumferenceAtLatitude(latitude);
}

// Feet to meters with the exaggeration applied. The one place either happens, so the
// icon, the stalk and the trail cannot end up at different heights.
export function liftMeters(altitudeFeet, scale = 1) {
    if (!Number.isFinite(altitudeFeet) || !Number.isFinite(scale)) return 0;
    // Below sea level is a real reading (Dead Sea airports, calibration drift) but
    // not a thing to draw a stalk downward for.
    return Math.max(0, altitudeFeet) * 0.3048 * scale;
}

// How high the camera itself is above the ground plane.
//
// This is the ceiling on the whole feature: an aircraft higher than the camera is
// above the horizon, and above the horizon no ground point projects to it, so it
// cannot be given a coordinate. The layer's render callback supplies the field of
// view already in radians, so nothing here has to assume MapLibre's default.
export function cameraAltitudeMeters(fovRadians, canvasHeightPx, pitchDegrees, metersPerPixelAtCenter) {
    if (!Number.isFinite(fovRadians) || fovRadians <= 0) return 0;
    const cameraToCenter = (0.5 / Math.tan(fovRadians / 2)) * canvasHeightPx;
    return cameraToCenter * Math.cos(pitchDegrees * DEG) * metersPerPixelAtCenter;
}

// Whether a lift can become a ground coordinate at all.
//
// Tested rather than discovered: unprojecting a pixel above the horizon does not
// fail, it returns a finite coordinate derived from where the ray crosses the ground
// plane behind the camera, which is a plausible-looking wrong answer. There is no
// error to catch afterwards, so the question has to be asked first.
export function canPlace(lift, cameraAltitude) {
    if (!Number.isFinite(lift) || !Number.isFinite(cameraAltitude)) return false;
    if (cameraAltitude <= 0) return false;
    return lift < cameraAltitude;
}

// A mercator point through a model-view-projection matrix to screen pixels.
//
// The matrix is MapLibre's own, handed to a custom layer each frame, and is
// column-major as WebGL matrices are. Returns null for anything behind the camera,
// which is the only case the projection itself can detect: a point above the horizon
// is in front of the camera and projects perfectly well, and is the caller's problem
// through canPlace.
export function projectWorld(matrix, world, width, height) {
    if (!matrix || !world) return null;

    const [x, y, z] = world;
    const w = matrix[3] * x + matrix[7] * y + matrix[11] * z + matrix[15];
    if (!Number.isFinite(w) || w <= 0) return null;

    const clipX = matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12];
    const clipY = matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13];

    return {
        x: ((clipX / w) + 1) / 2 * width,
        y: (1 - (clipY / w)) / 2 * height
    };
}
