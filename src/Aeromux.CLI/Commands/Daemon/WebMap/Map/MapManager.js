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

import { resolveShape } from './AircraftIconResolver.js';
import { SHAPES } from './AircraftShapes.js';
import {
    ALTITUDE_STEP,
    MAX_ALTITUDE,
    SELECTED_COLOR,
    CATEGORIES,
    ICON_SIZE,
    interpolateColor,
    ensureRegistered,
    preregisterUnknownVariants,
    clearImageCaches,
    setMap as setIconMap,
    loggedUnknownTypes,
} from './AircraftIcons.js';
import {
    metersPerPixel,
    mercatorXFromLongitude,
    mercatorYFromLatitude,
    mercatorZFromAltitude,
    liftMeters,
    cameraAltitudeMeters,
    canPlace,
    projectWorld
} from '../Services/AltitudeProjection.js';
import * as AltitudeLayer from './AltitudeLayer.js';
import { RDYLGN_STOPS, payloadToFeatures } from '../Services/HeatmapScale.js';
import { createHudItem, setText, formatBearing, createRowFitter } from '../Services/HudDom.js';
import {
    distanceNm,
    bearingDeg,
    polygonAreaNm2,
    boundsAreaNm2,
    formatDistanceNm,
    formatAreaNm2
} from '../Services/ViewportMetrics.js';

let map = null;
let viewportCallback = null;
let markerClickCallback = null;
let mapClickCallback = null;
let markerHoverEnterCallback = null;
let markerHoverLeaveCallback = null;
let debounceTimer = null;
let selectedIcao = null;
let rangeOutlineAdded = false;
let pendingRangeOutline = null;
let heatmapInitialized = false;
let pendingHeatmap = null;
let heatmapHoverCallback = null;
let hoveredIcao = null;
let hoveredCoords = null;
let hoveredProps = null;
let selectedCoords = null;
let selectedProps = null;
let selectedTooltipCallback = null;
// Last hover payload actually emitted. The pointer produces a mousemove per frame
// while it sits over an aircraft, and each emission re-renders the whole component
// tree — including the aircraft list, which redraws every row for a change it does
// not depend on. Emitting only when something the tooltip shows has actually moved
// costs one comparison and removes that entirely.
let lastHoverEmit = null;

// Readout state. The row is written to imperatively for the reason the Sky View's
// is: it changes on every frame of a pan, and routing that through the component
// tree would re-render the aircraft list with it.
let hud = null;
let hudNodes = null;
let hudChips = [];
let hudRefit = null;
let hudFrame = 0;
// How wide the row may be: the space between the aircraft list and the control
// panel. Unbounded until the panels have been measured, which is the behavior the
// row had before it was bounded at all.
let hudBudget = Infinity;
let active = true;
let insets = {};
let distanceUnit = 'nm';
let inViewCount = 0;
let totalCount = 0;
let receiver = null;
let outlineCoordinates = [];
let outlineMaxNm = 0;

// A pixel of movement is below what the tooltip can show, so it is not worth a
// render.
const HOVER_EMIT_EPSILON_PX = 1;

function emitHover(props, x, y) {
    if (!markerHoverEnterCallback) return;

    if (lastHoverEmit
        && lastHoverEmit.icao === props.icao
        && Math.abs(lastHoverEmit.x - x) < HOVER_EMIT_EPSILON_PX
        && Math.abs(lastHoverEmit.y - y) < HOVER_EMIT_EPSILON_PX) {
        return;
    }

    lastHoverEmit = { icao: props.icao, x, y };
    markerHoverEnterCallback({ ...props, x, y });
}

function clearHoverEmit() {
    lastHoverEmit = null;
}

// Trail colors per aircraft category — matches the CSS category dot colors (darkened for line contrast)
const TRAIL_COLORS = {
    normal:   'rgb(0, 97, 146)',
    military: 'rgb(0, 110, 0)',
    privacy:  'rgb(160, 0, 0)',
};

// The same three for the 3D trail, which is drawn by a shader rather than by the
// style and so needs components rather than CSS.
const TRAIL_RGB = {
    normal:   [0, 97, 146],
    military: [0, 110, 0],
    privacy:  [160, 0, 0],
};

// Aircraft at their real height
//
// Tilted, the map otherwise draws an aircraft at FL350 and the warehouse it is over
// at the same point. Each aircraft is given the coordinate whose ground position
// projects to where it actually is in the air, so the symbol layer keeps doing its
// own work and hit testing, hover and rotation come along unchanged.

// MapLibre's own default, stated here because raising it is not free: past about
// 71.6 degrees the horizon enters the frame, and the canvas is cleared to
// transparent above it, so the page behind would show through. MapLibre also calls
// anything above 60 experimental.
const MAX_PITCH = 45;

// Below this there is nothing worth drawing: an aircraft on the ground, or one
// reporting no altitude, would otherwise carry a ring with a stalk of no length.
const MIN_LIFT_M = 30;

// The mark an aircraft leaves on the ground it is over: its own silhouette, lying
// flat, at the point the stalk lands on.
//
// Not a sun shadow. A real one at FL350 falls about 10 nm away with the sun 30
// degrees up, and further as the sun drops, which reads as a second aircraft rather
// than as this one. This is the convention instead: the object above, its mark
// directly below, and the eye reads the gap between them as height.
//
// It lies in the ground plane, so unlike everything tried in the air it foreshortens
// with the tilt, which is what makes it read as being on the ground rather than as
// another copy of the aircraft.
const SHADOW_COLOR = [40, 44, 50];
const SHADOW_OPACITY = 0.35;
// Slightly smaller than the aircraft, which is what distance to the ground does.
const SHADOW_SCALE = 0.9;
const SHADOW_FALLBACK = 'aircraft-unknown-shadow';
const shadowImageName = (shapeName, selected) =>
    `aircraft-${shapeName}-shadow${selected ? '-selected' : ''}`;
// The selection's mark is its own shadow in the selection color, outlined in black
// like every other icon on the map. One mark on the ground, carrying both what the
// aircraft is and that it is the one being followed.
let shadowVisible = null;



let altitudeEnabled = true;
let altitudeScale = 1;
// The selected aircraft's path, kept because it is drawn twice: flat on the ground
// while the map is level, climbing with the aircraft once it is tilted.
let trailPoints = [];
let flatTrailVisible = true;
let wasLifting = false;

// Aircraft as last built, each with its true ground position and its lift beside the
// feature. Placement is recomputed from these as the camera moves, which costs no
// icon resolution and no image registration.
let aircraftRecords = [];

export function init(containerId) {
    map = new maplibregl.Map({
        container: containerId,
        style: {
            version: 8,
            sources: {
                osm: {
                    type: 'raster',
                    tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
                    tileSize: 256,
                    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                }
            },
            layers: [{ id: 'osm', type: 'raster', source: 'osm' }]
        },
        center: [0, 0],
        zoom: 2,
        maxPitch: MAX_PITCH
    });

    map.on('load', async () => {
        setIconMap(map);
        // Eager-register the 64 `unknown` fallback variants before any
        // feature can reference them via the layer's coalesce. The
        // ~0.3–1s blank-marker window during this decode is an
        // accepted trade for guaranteed-correct first frames.
        await preregisterUnknownVariants();
        addLayers();

        if (pendingRangeRings) {
            const p = pendingRangeRings;
            pendingRangeRings = null;
            updateRangeRings(p.lat, p.lon, p.visible, p.distanceUnit);
        }

        if (pendingRangeOutline) {
            const p = pendingRangeOutline;
            pendingRangeOutline = null;
            updateRangeOutline(p.coordinates, p.visible);
        }

        if (pendingHeatmap) {
            const p = pendingHeatmap;
            pendingHeatmap = null;
            setHeatmap(p);
        }
    });

    // Base-style change wipes MapLibre's registered images; clear the
    // local caches and re-run eager preregister so the layer's
    // coalesce has a valid fallback for the next tick. Lazy
    // registration of type-specific bitmaps resumes naturally.
    map.on('styledata', async () => {
        if (!map.isStyleLoaded()) return;
        clearImageCaches();
        await preregisterUnknownVariants();
    });

    // Viewport change events (debounced)
    const fireViewport = () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            if (viewportCallback) {
                const bounds = map.getBounds();
                viewportCallback({
                    south: bounds.getSouth(),
                    west: bounds.getWest(),
                    north: bounds.getNorth(),
                    east: bounds.getEast()
                });
            }
        }, 200);
    };
    map.on('moveend', fireViewport);
    map.on('zoomend', fireViewport);

    // Marker click
    map.on('click', 'aircraft-layer', (e) => {
        if (e.features && e.features.length > 0 && markerClickCallback) {
            markerClickCallback(e.features[0].properties.icao);
        }
    });

    // Map background click (deselect)
    map.on('click', (e) => {
        const features = map.queryRenderedFeatures(e.point, { layers: ['aircraft-layer'] });
        if (features.length === 0 && mapClickCallback) {
            mapClickCallback();
        }
    });

    // Marker hover
    map.on('mouseenter', 'aircraft-layer', () => {
        map.getCanvas().style.cursor = 'pointer';
    });

    map.on('mousemove', 'aircraft-layer', (e) => {
        if (e.features && e.features.length > 0 && markerHoverEnterCallback) {
            const f = e.features[0];
            hoveredIcao = f.properties.icao;
            hoveredCoords = f.geometry.coordinates;
            hoveredProps = {
                icao: f.properties.icao,
                callsign: f.properties.callsign,
                altitude: f.properties.altitude,
                speed: f.properties.speed
            };
            const pt = map.project(hoveredCoords);
            emitHover(hoveredProps, pt.x, pt.y);
        }
    });

    map.on('mouseleave', 'aircraft-layer', () => {
        map.getCanvas().style.cursor = '';
        hoveredIcao = null;
        hoveredCoords = null;
        hoveredProps = null;
        clearHoverEmit();
        if (markerHoverLeaveCallback) {
            markerHoverLeaveCallback();
        }
    });

    // Heatmap cell hover — aircraft markers take priority.
    map.on('mousemove', (e) => {
        if (!heatmapHoverCallback || !heatmapInitialized) return;
        const overAircraft = map.queryRenderedFeatures(e.point, { layers: ['aircraft-layer'] });
        if (overAircraft.length > 0) { heatmapHoverCallback(null); return; }
        const count = heatmapCellAt(e.point);
        heatmapHoverCallback(count != null ? { count, x: e.point.x, y: e.point.y } : null);
    });
    map.on('mouseout', () => { if (heatmapHoverCallback) heatmapHoverCallback(null); });

    buildHud(document.getElementById(containerId));

    // Re-project tooltip positions on map move/zoom
    map.on('move', () => {
        requestHud();
        // Before the tooltips below, which read the coordinates it rewrites.
        refreshPlacement();
        if (hoveredIcao && hoveredCoords && hoveredProps) {
            const pt = map.project(hoveredCoords);
            emitHover(hoveredProps, pt.x, pt.y);
        }
        if (selectedIcao && selectedCoords && selectedProps && selectedTooltipCallback) {
            const pt = map.project(selectedCoords);
            selectedTooltipCallback({ ...selectedProps, x: pt.x, y: pt.y });
        }
    });

    return map;
}

function addLayers() {
    // Dark overlay (below trails and aircraft, above map tiles)
    map.addSource('overlay-source', {
        type: 'geojson',
        data: {
            type: 'Feature',
            geometry: {
                type: 'Polygon',
                coordinates: [[[-180, -90], [180, -90], [180, 90], [-180, 90], [-180, -90]]]
            },
            properties: {}
        }
    });

    map.addLayer({
        id: 'overlay-layer',
        type: 'fill',
        source: 'overlay-source',
        paint: {
            'fill-color': 'rgba(0, 0, 0, 0.30)'
        }
    });

    // Trail layer (below aircraft)
    map.addSource('trail-source', {
        type: 'geojson',
        data: { type: 'Feature', geometry: { type: 'LineString', coordinates: [] }, properties: {} }
    });

    map.addLayer({
        id: 'trail-layer',
        type: 'line',
        source: 'trail-source',
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: {
            'line-color': TRAIL_COLORS.normal,
            'line-width': 3,
        }
    });

    // Aircraft source and layer
    map.addSource('aircraft-source', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] }
    });

    // Aircraft layer: per-feature iconImage / iconFallback / iconScale
    // / iconRotate properties set by updateMarkers(). coalesce falls
    // back to the eagerly-pre-registered `unknown` variant while a
    // type-specific bitmap is mid-decode or has failed decode.
    map.addLayer({
        id: 'aircraft-layer',
        type: 'symbol',
        source: 'aircraft-source',
        layout: {
            'icon-image': ['coalesce',
                ['image', ['get', 'iconImage']],
                ['image', ['get', 'iconFallback']],
            ],
            'icon-size':   ['*', ICON_SIZE, ['get', 'iconScale']],
            'icon-rotate': ['get', 'iconRotate'],
            'icon-rotation-alignment': 'map',
            // Icons lie in the ground plane, which is MapLibre's default for this
            // rotation alignment and foreshortens them as the map tilts. Facing them
            // at the camera instead was tried and looked wrong: a top-down silhouette
            // standing upright reads as a cutout rather than as an aircraft seen from
            // above, and the map is a plan view even when it is tilted.
            'icon-allow-overlap': true,
            'icon-ignore-placement': true
        },
        paint: {
            'icon-opacity': 1
        }
    });

    // Ground shadows, below everything the aircraft themselves are drawn with, so a
    // stalk rises out of its own mark.
    map.addSource('aircraft-shadow-source', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] }
    });

    ensureRegistered(SHADOW_FALLBACK, 'unknown', SHADOW_COLOR);

    map.addLayer({
        id: 'aircraft-shadow-layer',
        type: 'symbol',
        source: 'aircraft-shadow-source',
        layout: {
            visibility: 'none',
            'icon-image': ['coalesce',
                ['image', ['get', 'iconShadow']],
                ['image', SHADOW_FALLBACK],
            ],
            'icon-size':   ['*', ICON_SIZE * SHADOW_SCALE, ['get', 'iconScale']],
            'icon-rotate': ['get', 'iconRotate'],
            // Both aligned to the map, so the mark lies in the ground plane and
            // foreshortens with the tilt the way something lying on the ground does.
            'icon-rotation-alignment': 'map',
            'icon-pitch-alignment': 'map',
            'icon-allow-overlap': true,
            'icon-ignore-placement': true
        },
        paint: {
            // One opacity for every shadow, the selection included. Its color already
            // tells it apart, and a second, harder orange mark was doing the job
            // twice over. A constant also keeps this off the expression path, where a
            // per-feature case that fails to evaluate falls back to fully opaque and
            // looks like no opacity at all.
            'icon-opacity': SHADOW_OPACITY
        }
    }, 'aircraft-layer');

    // Stalks, rings and the 3D trail, below the aircraft so a stalk passes under
    // traffic rather than over it.
    AltitudeLayer.addTo(map, 'aircraft-layer');
}

export function setCenter(lat, lon, zoom) {
    if (map) {
        map.jumpTo({ center: [lon, lat], zoom: zoom || 8 });
    }
}

export function fitToAircraft(positions) {
    if (!map || positions.length === 0) return;
    const bounds = new maplibregl.LngLatBounds();
    positions.forEach(p => bounds.extend([p.lon, p.lat]));
    map.fitBounds(bounds, { padding: 50, maxZoom: 12 });
}

export function updateMarkers(aircraftMap) {
    if (!map) return;

    const records = [];
    aircraftMap.forEach((aircraft, icao) => {
        if (!aircraft.Coordinate) return;

        // Barometric first, geometric as the fallback, which is the order the state
        // history records (AircraftStateTracker) and the order the live trail append
        // already follows. An icon placed by a different rule than its own trail
        // would sit several hundred feet off the end of it.
        const altitudeSource = aircraft.BarometricAltitude ?? aircraft.GeometricAltitude;
        const altitude = altitudeSource ? altitudeSource.Feet : 0;
        const heading  = aircraft.Track || aircraft.TrackOnGround || 0;
        const selected = icao === selectedIcao;
        const category = aircraft.Military ? 'military'
                       : (aircraft.Ladd || aircraft.Pia) ? 'privacy'
                       : 'normal';

        // Layer-by-layer resolve to a shape + per-type scale.
        const { shapeName, scale, resolvedVia } = resolveShape(
            aircraft.TypeCode,
            aircraft.TypeIcaoClass,
            aircraft.TypeWtc,
            aircraft.Category
        );

        // Log once per session per unmapped TypeCode so maintainers
        // running with verbose console can grow the resolver tables.
        if (aircraft.TypeCode
                && resolvedVia !== 'designator'
                && !loggedUnknownTypes.has(aircraft.TypeCode)) {
            loggedUnknownTypes.add(aircraft.TypeCode);
            console.debug('[aircraft-icon] unmapped TypeCode', {
                typeCode:      aircraft.TypeCode,
                typeIcaoClass: aircraft.TypeIcaoClass,
                typeWtc:       aircraft.TypeWtc,
                category:      aircraft.Category,
                shapeUsed:     shapeName,
                resolvedVia,
            });
        }

        // Clamp altitude into the discrete bucket grid. Math.max(0,…)
        // catches below-MSL altitudes (Dead Sea airports, calibration
        // drift, occasional negative-altitude broadcasts).
        const altStep = Math.max(0, Math.min(
            Math.round(altitude / ALTITUDE_STEP) * ALTITUDE_STEP,
            MAX_ALTITUDE
        ));

        // Palette stops by category (defaults defensively to 'normal').
        const palette = (CATEGORIES.find(c => c.prefix === category)
                         ?? CATEGORIES[0]).stops;
        const fillColor = selected
            ? SELECTED_COLOR
            : interpolateColor(altStep, palette);

        const imageName = selected
            ? `aircraft-${shapeName}-selected`
            : `aircraft-${shapeName}-${category}-${altStep}`;
        const fallback = selected
            ? `aircraft-unknown-selected`
            : `aircraft-unknown-${category}-${altStep}`;

        // Fire-and-forget; updateMarkers ticks aren't awaited.
        // ensureRegistered dedupes per imageName.
        ensureRegistered(imageName, shapeName, fillColor);
        // One shadow per shape rather than per altitude bucket: a mark on the ground
        // takes no palette. The selection gets a second variant, the same shape
        // outlined in the selection color.
        ensureRegistered(shadowImageName(shapeName, false), shapeName, SHADOW_COLOR);
        if (selected) {
            ensureRegistered(shadowImageName(shapeName, true), shapeName, SELECTED_COLOR);
        }

        const feature = {
            type: 'Feature',
            geometry: {
                type: 'Point',
                coordinates: [aircraft.Coordinate.Longitude, aircraft.Coordinate.Latitude]
            },
            properties: {
                icao,
                callsign: aircraft.Callsign || icao,
                altitude,
                speed: aircraft.Speed ? aircraft.Speed.Knots : 0,
                heading,
                selected,
                category,

                iconImage:    imageName,
                iconFallback: fallback,
                iconShadow:   shadowImageName(shapeName, selected),
                iconScale:    scale,
                // Per-feature rotation; balloon (the only noRotate
                // shape currently) renders north-up regardless of
                // heading.
                iconRotate:   SHAPES[shapeName].noRotate ? 0 : heading,
                shapeName,
                resolvedVia,
            }
        };
        records.push({
            feature,
            lon: aircraft.Coordinate.Longitude,
            lat: aircraft.Coordinate.Latitude,
            lift: liftMeters(altitude, altitudeScale),
            selected
        });
    });

    aircraftRecords = records;
    rebuildMarks();
    emitPlacement();
}

// Aircraft at their real height
// --------------------------------------------------------------------------

// Nothing is lifted on a level map: the lift would be zero, and paying for the
// arithmetic to compute a zero would be paying for nothing on the common case.
function liftingActive() {
    return altitudeEnabled && !!map && map.getPitch() > 0;
}

// Stalks, rings and the trail, which change only when the aircraft or the scale do.
// Camera movement leaves them alone: a stalk's top is a height above a fixed point on
// the ground, which is the same place whatever the camera is doing.
function rebuildMarks() {
    const lifted = aircraftRecords.filter(record => record.lift >= MIN_LIFT_M);

    AltitudeLayer.setMarks(lifted.map(record => ({
        lon: record.lon,
        lat: record.lat,
        lift: record.lift,
        selected: record.selected
    })));

    // Shadows sit at the true ground position, which no camera movement changes, so
    // they are rebuilt with the data rather than with the view.
    const source = map && map.getSource('aircraft-shadow-source');
    if (!source) return;
    source.setData({
        type: 'FeatureCollection',
        features: lifted.map(record => ({
            type: 'Feature',
            geometry: { type: 'Point', coordinates: [record.lon, record.lat] },
            properties: {
                iconShadow: record.feature.properties.iconShadow,
                iconScale:  record.feature.properties.iconScale,
                iconRotate: record.feature.properties.iconRotate
            }
        }))
    });
}

// Where each aircraft goes on screen, given where the camera is now.
//
// Every aircraft is given the coordinate whose GROUND position projects to where the
// aircraft actually is in the air. MapLibre never learns about altitude, so hit
// testing, hover, the pinned tooltip and icon rotation keep working unchanged.
function emitPlacement() {
    const source = map && map.getSource('aircraft-source');
    if (!source) return;

    const lifting = liftingActive();
    AltitudeLayer.setVisible(lifting);

    if (lifting !== shadowVisible && map.getLayer('aircraft-shadow-layer')) {
        shadowVisible = lifting;
        map.setLayoutProperty('aircraft-shadow-layer', 'visibility', lifting ? 'visible' : 'none');
    }

    // Two trails for one aircraft would be two lines saying the same thing, so the
    // flat one stands down while the climbing one is drawn. Only while it actually is:
    // a trail whose points carry no altitude produces no climbing line, and hiding the
    // flat one then would leave the aircraft with no trail at all.
    const flatWanted = !(lifting && AltitudeLayer.hasTrail());
    if (flatWanted !== flatTrailVisible && map.getLayer('trail-layer')) {
        flatTrailVisible = flatWanted;
        map.setLayoutProperty('trail-layer', 'visibility', flatWanted ? 'visible' : 'none');
    }

    const frame = AltitudeLayer.cameraFrame();
    const canvas = map.getCanvas();
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;

    // No matrix until the first frame has been drawn, and no lift on a level map.
    // Both fall back to the ground position, which is exactly what this map did
    // before any of this existed.
    const placing = lifting && frame.matrix && width > 0 && height > 0;

    let ceiling = 0;
    if (placing) {
        const center = map.getCenter();
        const mpp = metersPerPixel(center.lat, map.getZoom());
        ceiling = cameraAltitudeMeters(frame.fov, height, map.getPitch(), mpp);
    }

    const features = [];
    const placed = new Set();

    for (const record of aircraftRecords) {
        const feature = record.feature;

        if (!placing || record.lift < MIN_LIFT_M) {
            feature.geometry.coordinates = [record.lon, record.lat];
            features.push(feature);
            placed.add(record);
            continue;
        }

        // Above the camera's own altitude the aircraft is above the horizon, where no
        // ground point projects to it. It is left out rather than dropped onto the
        // ground: it has legitimately risen off the top of the screen, and the ground
        // is not where it is. Its stalk and ring still stand, so it reads as a mark
        // with a line leaving the frame.
        if (!canPlace(record.lift, ceiling)) continue;

        const screen = projectWorld(
            frame.matrix,
            [
                mercatorXFromLongitude(record.lon),
                mercatorYFromLatitude(record.lat),
                mercatorZFromAltitude(record.lift, record.lat)
            ],
            width,
            height
        );
        if (!screen) continue;

        const lngLat = map.unproject([screen.x, screen.y]);
        // Unprojecting a pixel above the horizon does not fail, it answers with a
        // point derived from behind the camera. Nothing outside the projection can be
        // a real coordinate, so it is the last thing checked before the map is told.
        if (!lngLat || !Number.isFinite(lngLat.lat) || !Number.isFinite(lngLat.lng)) continue;
        if (Math.abs(lngLat.lat) > 85.05) continue;

        feature.geometry.coordinates = [lngLat.lng, lngLat.lat];
        features.push(feature);
        placed.add(record);
    }

    source.setData({ type: 'FeatureCollection', features });
    // The drawing layer renders with the matrix these features were placed from, so a
    // stalk and its icon can never disagree.
    AltitudeLayer.setPlacementMatrix(placing ? frame.matrix : null);

    syncTooltips(placed);
}

// The hover and selection tooltips are pinned to coordinates that now move with the
// camera, so they are refreshed with the source rather than independently. An
// aircraft that could not be placed is treated exactly as one that has gone: it is
// not on screen, and a tooltip pointing at where it would have been says nothing.
function syncTooltips(placed) {
    if (hoveredIcao) {
        const record = aircraftRecords.find(r => r.feature.properties.icao === hoveredIcao);
        if (record && placed.has(record)) {
            hoveredCoords = record.feature.geometry.coordinates;
            hoveredProps = {
                icao: record.feature.properties.icao,
                callsign: record.feature.properties.callsign,
                altitude: record.feature.properties.altitude,
                speed: record.feature.properties.speed
            };
            const pt = map.project(hoveredCoords);
            emitHover(hoveredProps, pt.x, pt.y);
        } else {
            hoveredIcao = null;
            hoveredCoords = null;
            hoveredProps = null;
            clearHoverEmit();
            if (markerHoverLeaveCallback) {
                markerHoverLeaveCallback();
            }
        }
    }

    if (selectedIcao && selectedTooltipCallback) {
        const record = aircraftRecords.find(r => r.feature.properties.icao === selectedIcao);
        if (record && placed.has(record)) {
            selectedCoords = record.feature.geometry.coordinates;
            selectedProps = {
                icao: record.feature.properties.icao,
                callsign: record.feature.properties.callsign,
                altitude: record.feature.properties.altitude,
                speed: record.feature.properties.speed
            };
            const pt = map.project(selectedCoords);
            selectedTooltipCallback({ ...selectedProps, x: pt.x, y: pt.y });
        } else {
            selectedCoords = null;
            selectedProps = null;
            selectedTooltipCallback(null);
        }
    }
}

// Called on every frame of a camera movement. No icon resolution, no image
// registration, no geometry rebuild: only where the aircraft already on screen now
// belong.
function refreshPlacement() {
    if (!map || !active) return;
    // A level map does no work here at all, which is the common case and was the
    // whole cost of this feature before it existed. The one frame after a tilt
    // returns to level still runs, to put the aircraft back on their ground
    // positions.
    const lifting = liftingActive();
    if (!lifting && !wasLifting) return;
    wasLifting = lifting;
    emitPlacement();
}

// Whether aircraft are drawn at their height, and by how much the height is
// exaggerated. The scale changes how tall every stalk is, so the geometry is rebuilt
// with it.
export function setAltitudeMode(enabled, scale) {
    const scaleChanged = scale !== altitudeScale;
    altitudeEnabled = enabled !== false;
    altitudeScale = Number.isFinite(scale) && scale > 0 ? scale : 1;

    if (scaleChanged) {
        for (const record of aircraftRecords) {
            record.lift = liftMeters(record.feature.properties.altitude, altitudeScale);
        }
        rebuildMarks();
        rebuildTrail();
    }
    emitPlacement();
    requestHud();
}

export function highlightSelected(icao) {
    selectedIcao = icao;
}

export function clearSelection() {
    selectedIcao = null;
    selectedCoords = null;
    selectedProps = null;
    if (selectedTooltipCallback) selectedTooltipCallback(null);
}

export function panTo(lat, lon, keepZoom = false) {
    if (map) {
        if (keepZoom) {
            map.jumpTo({ center: [lon, lat], zoom: map.getZoom() });
        } else {
            map.flyTo({ center: [lon, lat], zoom: Math.max(map.getZoom(), 8), duration: 500 });
        }
    }
}

// View-neutral "bring this location into view", so the caller does not need to
// know which view is active. The sky renderer's counterpart swings its camera to
// the bearing instead of panning.
export function focusOn(lat, lon) {
    panTo(lat, lon, true);
}

// Re-measures the container. Required after the map has been hidden with
// display:none, because MapLibre caches the zero size it measured while hidden and
// would otherwise render into nothing on the way back.
export function resize() {
    if (map) {
        map.resize();
        // The span is measured across the canvas, so a new canvas width is a new span.
        requestHud();
    }
}

export function destroy() {
    if (map) {
        map.remove();
        map = null;
    }
    aircraftRecords = [];
    trailPoints = [];
    flatTrailVisible = true;
    AltitudeLayer.clear();
    hud = null;
    hudNodes = null;
    hudChips = [];
    hudRefit = null;
    hudFrame = 0;
}

// The selected aircraft's path.
//
// Takes state-history entries, the same shape the Sky View's trail takes, so the two
// views cannot draw different paths for the same aircraft. Position-history entries
// are accepted as well and drawn flat, which is the fallback where state history is
// switched off in the tracking configuration and no altitude exists to climb with.
export function updateTrail(entries) {
    if (!map) return;
    const source = map.getSource('trail-source');
    if (!source) return;

    trailPoints = (entries || []).map(entry => {
        const position = entry.position ?? entry;
        if (!position || position.Latitude == null || position.Longitude == null) return null;
        return {
            lon: position.Longitude,
            lat: position.Latitude,
            altitudeFeet: entry.altitudeFeet ?? null
        };
    }).filter(Boolean);

    const coordinates = trailPoints.length >= 2 ? trailPoints.map(p => [p.lon, p.lat]) : [];
    source.setData({
        type: 'Feature',
        geometry: { type: 'LineString', coordinates },
        properties: {}
    });
    rebuildTrail();
}

// The climbing trail, in the units the layer draws in. A point with no altitude
// leaves a gap rather than a dive to sea level.
function rebuildTrail() {
    AltitudeLayer.setTrail(trailPoints.map(point => ({
        lon: point.lon,
        lat: point.lat,
        lift: point.altitudeFeet == null ? null : liftMeters(point.altitudeFeet, altitudeScale)
    })));
    // The flat trail's visibility depends on whether that produced anything.
    emitPlacement();
}

export function setTrailColor(category) {
    AltitudeLayer.setTrailColor(TRAIL_RGB[category] || TRAIL_RGB.normal);
    if (!map || !map.getLayer('trail-layer')) return;
    map.setPaintProperty('trail-layer', 'line-color', TRAIL_COLORS[category] || TRAIL_COLORS.normal);
}

export function clearTrail() {
    updateTrail([]);
}

export function getViewportBounds() {
    if (!map) return null;
    const bounds = map.getBounds();
    return {
        south: bounds.getSouth(),
        west: bounds.getWest(),
        north: bounds.getNorth(),
        east: bounds.getEast()
    };
}

// Readout — how much ground is on screen, and where it is
//
// The row lives in the DOM rather than on the map canvas so it picks up the same
// typography and panel treatment as the rest of the interface. Its chips come from
// Services/HudDom.js and its arithmetic from Services/ViewportMetrics.js, both
// shared with the Sky View's readout, so the two rows cannot drift apart.

function buildHud(container) {
    if (!container || !container.appendChild) return;

    hud = document.createElement('div');
    hud.className = 'panel view-hud map-hud';

    const heading = createHudItem('HDG', false);
    const span = createHudItem('SPAN', false);
    const area = createHudItem('AREA', false);
    const center = createHudItem('CTR', false);
    const count = createHudItem('in view', true);
    const range = createHudItem('RANGE', false);
    const alt = createHudItem('ALT', false);

    for (const chip of [heading, span, area, center, count, range, alt]) {
        hud.appendChild(chip.wrap);
    }
    container.appendChild(hud);

    hudNodes = {
        heading: heading.value,
        span: span.value,
        area: area.value,
        center: center.value,
        count: count.value,
        range: range.value,
        alt: alt.value
    };

    // Least valuable first, which is the order they are given up in when the space
    // between the panels runs out. The span goes last because it is the scale, the
    // one reading the map never had and the cheapest of them to keep.
    hudChips = [
        { key: 'range', wrap: range.wrap, eligible: false },
        { key: 'area', wrap: area.wrap, eligible: false },
        { key: 'center', wrap: center.wrap, eligible: false },
        { key: 'heading', wrap: heading.wrap, eligible: false },
        { key: 'count', wrap: count.wrap, eligible: true },
        { key: 'span', wrap: span.wrap, eligible: true },
        // Last, and so the last to be given up. Every other chip is a reading, and
        // losing one costs a number. This one is a disclaimer: an exaggerated map
        // with nothing on it saying so is a map presenting itself as true.
        { key: 'alt', wrap: alt.wrap, eligible: false }
    ];

    hudRefit = createRowFitter(hud, hudChips);
    requestHud();
}

// Coalesced to one write per frame: `move` fires far more often than that during a
// drag, and every value in the row comes from the same camera.
function requestHud() {
    if (hudFrame || !hud) return;
    hudFrame = requestAnimationFrame(drawHud);
}

// MapLibre hands back lng/lat; the metrics take lat/lon.
function toCoordinate(lngLat) {
    return { lat: lngLat.lat, lon: lngLat.lng };
}

function drawHud() {
    hudFrame = 0;
    if (!map || !hudNodes || !active) return;

    // Both views stay mounted, so the inactive one is merely hidden. Measuring a
    // canvas with no size would report a viewport that spans no ground at all.
    const canvas = map.getCanvas();
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (!width || !height) return;

    // Which chips have anything to say. What of that actually fits is settled at the
    // end, by the pass over the row.
    const eligible = {};

    const bearing = map.getBearing();
    const northUp = Math.round(Math.abs(bearing)) % 360 === 0;
    eligible.heading = !northUp;
    if (!northUp) {
        setText(hudNodes.heading, formatBearing(bearing));
    }

    // Midpoints of the vertical edges rather than corners: a corner can be above the
    // horizon on a tilted map, where it stands for no ground position at all, while a
    // point at half the height of the screen stays below it at any reachable pitch.
    const span = distanceNm(
        toCoordinate(map.unproject([0, height / 2])),
        toCoordinate(map.unproject([width, height / 2]))
    );
    setText(hudNodes.span, formatDistanceNm(span, distanceUnit));
    eligible.span = true;

    // Tilted, there is no honest answer: the top of the screen can be looking at the
    // horizon, and the map's own bounds are derived from the same geometry. Reporting
    // nothing is visibly different from reporting a number that is quietly wrong.
    const level = map.getPitch() === 0;

    // Only while the map is both tilted and exaggerating, which is the only state in
    // which it is drawing aircraft higher than they are.
    eligible.alt = !level && altitudeEnabled && altitudeScale !== 1;
    if (eligible.alt) {
        setText(hudNodes.alt, `×${altitudeScale}`);
    }

    eligible.area = level;
    if (level) {
        // North-up the viewport is a latitude-longitude rectangle, which has a closed
        // form. Rotated it is not, and the corners are what is actually on screen:
        // the box around a rotated viewport is larger than the viewport itself.
        const areaNm2 = northUp
            ? boundsAreaNm2(getViewportBounds())
            : polygonAreaNm2([[0, 0], [width, 0], [width, height], [0, height]]
                .map((point) => toCoordinate(map.unproject(point))));
        setText(hudNodes.area, formatAreaNm2(areaNm2, distanceUnit));
    }

    eligible.center = !!receiver;
    if (receiver) {
        const center = toCoordinate(map.getCenter());
        const away = distanceNm(receiver, center);
        const distance = formatDistanceNm(away, distanceUnit);
        // The map opens centered on the receiver, where the bearing is undefined and
        // would otherwise swing through every value in the first pixel of a pan.
        setText(hudNodes.center, away < 0.1
            ? distance
            : `${formatBearing(bearingDeg(receiver, center))} ${distance}`);
    }

    setText(hudNodes.count, `${inViewCount}/${totalCount}`);
    eligible.count = true;

    eligible.range = outlineMaxNm > 0;
    if (eligible.range) {
        setText(hudNodes.range, formatDistanceNm(outlineMaxNm, distanceUnit));
    }

    // Matches the panels' own inset from the corner, so the row lines up with them
    // rather than sitting proud of the top edge.
    hud.style.left = `${(insets.left || 0) + 16}px`;
    hud.style.top = `${(insets.top || 0) + 16}px`;
    // A value can outgrow its chip between fit passes; the row wraps for a moment
    // rather than reaching under the control panel again.
    hud.style.maxWidth = Number.isFinite(hudBudget) ? `${hudBudget}px` : '';

    refit(eligible);
}

// What fits is settled by the shared fitter, which measures only when the space, the
// set of chips, or the width of the values has actually changed.
function refit(eligible) {
    for (const chip of hudChips) {
        chip.eligible = !!eligible[chip.key];
    }
    if (hudRefit) hudRefit(hudBudget);
}

// The farthest the receiver has heard, from the coverage outline. Derived here rather
// than asked of the server for the same reason the Sky View derives it: the outline
// is a list of positions, and how far away they are follows from the receiver.
// Whichever of the two arrives second triggers the measurement.
function rebuildOutlineMax() {
    if (!receiver || !outlineCoordinates.length) {
        outlineMaxNm = 0;
        return;
    }

    outlineMaxNm = outlineCoordinates.reduce(
        (max, point) => Math.max(max, distanceNm(receiver, { lat: point.Latitude, lon: point.Longitude })),
        0
    );
}

export function setActive(next) {
    active = next;
    if (active) requestHud();
}

export function setSafeInsets(next) {
    insets = next || {};
    hudBudget = Number.isFinite(insets.readoutMaxWidth) ? insets.readoutMaxWidth : Infinity;
    requestHud();
}

export function setDistanceUnit(unit) {
    distanceUnit = unit;
    requestHud();
}

export function setCounts(inView, total) {
    inViewCount = inView;
    totalCount = total;
    requestHud();
}

export function setReceiver(lat, lon) {
    receiver = { lat, lon };
    rebuildOutlineMax();
    requestHud();
}

// Range outline — receiver coverage boundary polygon
let rangeOutlineInitialized = false;

function ensureRangeOutlineSources() {
    if (rangeOutlineInitialized || !map) return;
    if (!map.getLayer('overlay-layer')) return;
    rangeOutlineInitialized = true;

    const emptyPoly = { type: 'Feature', geometry: { type: 'Polygon', coordinates: [] }, properties: {} };

    map.addSource('range-outline-source', { type: 'geojson', data: emptyPoly });

    // Keep the outline beneath the range rings and their labels. If the rings
    // were already added, anchor below their bottom-most layer; otherwise fall
    // back to trail-layer (the rings, added later, still land above the outline).
    const beforeId = map.getLayer('range-rings-layer') ? 'range-rings-layer' : 'trail-layer';

    map.addLayer({
        id: 'range-outline-fill-layer',
        type: 'fill',
        source: 'range-outline-source',
        paint: {
            'fill-color': '#006192',
            'fill-opacity': 0.08
        }
    }, beforeId);

    map.addLayer({
        id: 'range-outline-line-layer',
        type: 'line',
        source: 'range-outline-source',
        paint: {
            'line-color': '#006192',
            'line-width': 1.5
        }
    }, beforeId);
}

export function updateRangeOutline(coordinates, visible) {
    // The readout reports how far the receiver has heard whether or not the outline
    // itself is drawn: it is a fact about the receiver, not about the overlay. Taken
    // before any of the drawing paths below can return early.
    outlineCoordinates = coordinates || [];
    rebuildOutlineMax();
    requestHud();

    if (!map) return;
    ensureRangeOutlineSources();

    if (!rangeOutlineInitialized) {
        pendingRangeOutline = { coordinates, visible };
        return;
    }

    const source = map.getSource('range-outline-source');
    if (!source) return;

    if (!visible || !coordinates || coordinates.length < 3) {
        source.setData({ type: 'Feature', geometry: { type: 'Polygon', coordinates: [] }, properties: {} });
        return;
    }

    const ring = coordinates.map(c => [c.Longitude, c.Latitude]);
    ring.push(ring[0]);

    source.setData({
        type: 'Feature',
        geometry: { type: 'Polygon', coordinates: [ring] },
        properties: {}
    });
}

// Range rings — distances in nautical miles, converted to km for haversine calculations
const RANGE_NM = [100, 150, 200];
const NM_TO_KM = 1.852;
let rangeRingsAdded = false;
let pendingRangeRings = null;

function generateCircleCoords(lat, lon, radiusKm, points = 64) {
    const coords = [];
    const R = 6371;
    for (let i = 0; i <= points; i++) {
        const bearing = (i / points) * 2 * Math.PI;
        const latRad = lat * Math.PI / 180;
        const lonRad = lon * Math.PI / 180;
        const d = radiusKm / R;
        const newLat = Math.asin(
            Math.sin(latRad) * Math.cos(d) +
            Math.cos(latRad) * Math.sin(d) * Math.cos(bearing)
        );
        const newLon = lonRad + Math.atan2(
            Math.sin(bearing) * Math.sin(d) * Math.cos(latRad),
            Math.cos(d) - Math.sin(latRad) * Math.sin(newLat)
        );
        coords.push([newLon * 180 / Math.PI, newLat * 180 / Math.PI]);
    }
    return coords;
}

function ensureRangeRingSources() {
    if (rangeRingsAdded || !map) return;
    if (!map.getLayer('trail-layer')) return;
    rangeRingsAdded = true;

    const emptyFC = { type: 'FeatureCollection', features: [] };
    const emptyPoint = { type: 'Feature', geometry: { type: 'Point', coordinates: [0, 0] }, properties: {} };

    map.addSource('range-rings-source', { type: 'geojson', data: emptyFC });
    map.addSource('range-labels-source', { type: 'geojson', data: emptyFC });
    map.addSource('range-center-source', { type: 'geojson', data: emptyPoint });

    // Ring lines — inserted after overlay, before trail
    map.addLayer({
        id: 'range-rings-layer',
        type: 'line',
        source: 'range-rings-source',
        paint: {
            'line-color': '#006192',
            'line-width': 2,
            'line-dasharray': [4, 4]
        }
    }, 'trail-layer');

    // Generate blue rectangle image for label backgrounds
    const bgSize = 64;
    const bgCanvas = document.createElement('canvas');
    bgCanvas.width = bgSize;
    bgCanvas.height = bgSize;
    const bgCtx = bgCanvas.getContext('2d');
    bgCtx.fillStyle = '#006192';
    bgCtx.beginPath();
    bgCtx.roundRect(0, 0, bgSize, bgSize, 4);
    bgCtx.fill();
    map.addImage('range-label-bg', { width: bgSize, height: bgSize, data: bgCtx.getImageData(0, 0, bgSize, bgSize).data });

    // Ring labels
    map.addLayer({
        id: 'range-labels-layer',
        type: 'symbol',
        source: 'range-labels-source',
        layout: {
            'text-field': ['get', 'label'],
            'text-size': 12,
            'text-font': ['Open Sans Regular'],
            'text-offset': [0, -0.8],
            'text-allow-overlap': true,
            'icon-image': 'range-label-bg',
            'icon-text-fit': 'both',
            'icon-text-fit-padding': [2, 6, 2, 6],
            'icon-allow-overlap': true
        },
        paint: {
            'text-color': '#ffffff'
        }
    }, 'trail-layer');

    // Center point
    map.addLayer({
        id: 'range-center-layer',
        type: 'circle',
        source: 'range-center-source',
        paint: {
            'circle-radius': 5,
            'circle-color': '#006192',
            'circle-stroke-color': '#000000',
            'circle-stroke-width': 1.5
        }
    }, 'trail-layer');
}

export function updateRangeRings(lat, lon, visible, distanceUnit) {
    if (!map) return;
    ensureRangeRingSources();

    if (!rangeRingsAdded) {
        pendingRangeRings = { lat, lon, visible, distanceUnit };
        return;
    }

    if (!visible || lat == null || lon == null) {
        map.getSource('range-rings-source').setData({ type: 'FeatureCollection', features: [] });
        map.getSource('range-labels-source').setData({ type: 'FeatureCollection', features: [] });
        map.getSource('range-center-source').setData({
            type: 'Feature', geometry: { type: 'Point', coordinates: [0, 0] }, properties: {}
        });
        map.setLayoutProperty('range-center-layer', 'visibility', 'none');
        return;
    }

    // Build ring polygons — convert nautical miles to km for the haversine circle generator
    const ringFeatures = RANGE_NM.map(nm => ({
        type: 'Feature',
        geometry: { type: 'LineString', coordinates: generateCircleCoords(lat, lon, nm * NM_TO_KM) },
        properties: {}
    }));

    // Build label points (at north edge of each ring)
    const labelFeatures = RANGE_NM.map(nm => {
        const radiusKm = nm * NM_TO_KM;
        const coords = generateCircleCoords(lat, lon, radiusKm, 64);
        // North point is at index 0 (bearing 0)
        const northPt = coords[0];
        let label;
        if (distanceUnit === 'nm') {
            label = `${nm} nm`;
        } else if (distanceUnit === 'mi') {
            label = `${Math.round(nm * 1.15078)} mi`;
        } else {
            label = `${Math.round(nm * NM_TO_KM)} km`;
        }
        return {
            type: 'Feature',
            geometry: { type: 'Point', coordinates: northPt },
            properties: { label }
        };
    });

    map.getSource('range-rings-source').setData({ type: 'FeatureCollection', features: ringFeatures });
    map.getSource('range-labels-source').setData({ type: 'FeatureCollection', features: labelFeatures });
    map.getSource('range-center-source').setData({
        type: 'Feature', geometry: { type: 'Point', coordinates: [lon, lat] }, properties: {}
    });
    map.setLayoutProperty('range-center-layer', 'visibility', 'visible');
}

// ---- Heatmap overlay ----

function ensureHeatmapSources() {
    if (heatmapInitialized || !map) return;
    if (!map.getLayer('overlay-layer')) return;
    heatmapInitialized = true;

    map.addSource('heatmap-source', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });

    // Data-driven green→red fill from the precomputed per-feature `t`.
    const colour = ['interpolate', ['linear'], ['get', 't'], ...RDYLGN_STOPS.flat()];

    // Insert directly above the base dim overlay so the heatmap sits at the very bottom
    // of the overlay stack — map → dim → heatmap → range outline → range rings → trail →
    // aircraft — regardless of the order the (lazy) range layers were added, so the rings
    // and outline stay readable on top of the fill.
    const styleLayers = map.getStyle().layers;
    const overlayIdx = styleLayers.findIndex((l) => l.id === 'overlay-layer');
    const beforeId = (overlayIdx >= 0 && overlayIdx + 1 < styleLayers.length)
        ? styleLayers[overlayIdx + 1].id
        : undefined;

    map.addLayer({
        id: 'heatmap-fill',
        type: 'fill',
        source: 'heatmap-source',
        paint: { 'fill-color': colour, 'fill-opacity': 0.55 },
    }, beforeId);

    map.addLayer({
        id: 'heatmap-border',
        type: 'line',
        source: 'heatmap-source',
        paint: { 'line-color': colour, 'line-width': 0.5, 'line-opacity': 0.6 },
    }, beforeId);
}

export function setHeatmap(payload) {
    if (!map) return;
    ensureHeatmapSources();
    if (!heatmapInitialized) { pendingHeatmap = payload; return; }
    const src = map.getSource('heatmap-source');
    if (src) src.setData({ type: 'FeatureCollection', features: payloadToFeatures(payload) });
}

export function clearHeatmap() {
    pendingHeatmap = null;
    if (!map || !heatmapInitialized) return;
    const src = map.getSource('heatmap-source');
    if (src) src.setData({ type: 'FeatureCollection', features: [] });
}

// Exact distinct-aircraft count of the heatmap cell under a screen point, or null.
export function heatmapCellAt(point) {
    if (!map || !map.getLayer('heatmap-fill')) return null;
    const features = map.queryRenderedFeatures(point, { layers: ['heatmap-fill'] });
    return features.length ? features[0].properties.count : null;
}

export function onHeatmapHover(callback) { heatmapHoverCallback = callback; }

export function onViewportChange(callback) { viewportCallback = callback; }
export function onMarkerClick(callback) { markerClickCallback = callback; }
export function onMapClick(callback) { mapClickCallback = callback; }
export function onMarkerHover(enterCb, leaveCb) {
    markerHoverEnterCallback = enterCb;
    markerHoverLeaveCallback = leaveCb;
}
export function onSelectedTooltip(callback) { selectedTooltipCallback = callback; }
