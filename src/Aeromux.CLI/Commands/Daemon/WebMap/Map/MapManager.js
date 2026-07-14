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
import { RDYLGN_STOPS, payloadToFeatures } from '../Services/HeatmapScale.js';

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

// Trail colors per aircraft category — matches the CSS category dot colors (darkened for line contrast)
const TRAIL_COLORS = {
    normal:   'rgb(0, 97, 146)',
    military: 'rgb(0, 110, 0)',
    privacy:  'rgb(160, 0, 0)',
};

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
        zoom: 2
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
            markerHoverEnterCallback({ ...hoveredProps, x: pt.x, y: pt.y });
        }
    });

    map.on('mouseleave', 'aircraft-layer', () => {
        map.getCanvas().style.cursor = '';
        hoveredIcao = null;
        hoveredCoords = null;
        hoveredProps = null;
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

    // Re-project tooltip positions on map move/zoom
    map.on('move', () => {
        if (hoveredIcao && hoveredCoords && hoveredProps && markerHoverEnterCallback) {
            const pt = map.project(hoveredCoords);
            markerHoverEnterCallback({ ...hoveredProps, x: pt.x, y: pt.y });
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
            'icon-allow-overlap': true,
            'icon-ignore-placement': true
        },
        paint: {
            'icon-opacity': 1
        }
    });
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

    const features = [];
    let selectedFeature = null;
    aircraftMap.forEach((aircraft, icao) => {
        if (!aircraft.Coordinate) return;

        const altitude = aircraft.BarometricAltitude ? aircraft.BarometricAltitude.Feet : 0;
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
                iconScale:    scale,
                // Per-feature rotation; balloon (the only noRotate
                // shape currently) renders north-up regardless of
                // heading.
                iconRotate:   SHAPES[shapeName].noRotate ? 0 : heading,
                shapeName,
                resolvedVia,
            }
        };
        features.push(feature);
        // Capture the selected feature in-loop so the pinned tooltip can be
        // re-projected without a second O(n) scan over the feature list.
        if (selected) selectedFeature = feature;
    });

    const source = map.getSource('aircraft-source');
    if (source) {
        source.setData({ type: 'FeatureCollection', features });
    }

    // Update hovered aircraft coordinates and properties (aircraft may have moved)
    if (hoveredIcao) {
        const hoveredFeature = features.find(f => f.properties.icao === hoveredIcao);
        if (hoveredFeature) {
            hoveredCoords = hoveredFeature.geometry.coordinates;
            hoveredProps = {
                icao: hoveredFeature.properties.icao,
                callsign: hoveredFeature.properties.callsign,
                altitude: hoveredFeature.properties.altitude,
                speed: hoveredFeature.properties.speed
            };
            if (markerHoverEnterCallback) {
                const pt = map.project(hoveredCoords);
                markerHoverEnterCallback({ ...hoveredProps, x: pt.x, y: pt.y });
            }
        } else {
            hoveredIcao = null;
            hoveredCoords = null;
            hoveredProps = null;
            if (markerHoverLeaveCallback) {
                markerHoverLeaveCallback();
            }
        }
    }

    // Update the pinned tooltip for the selected aircraft (it may have moved,
    // just been selected, or expired). Clears when the selection has no
    // on-map feature (no position yet, or removed).
    if (selectedIcao && selectedTooltipCallback) {
        if (selectedFeature) {
            selectedCoords = selectedFeature.geometry.coordinates;
            selectedProps = {
                icao: selectedFeature.properties.icao,
                callsign: selectedFeature.properties.callsign,
                altitude: selectedFeature.properties.altitude,
                speed: selectedFeature.properties.speed
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

export function updateTrail(positions) {
    if (!map) return;
    const source = map.getSource('trail-source');
    if (!source) return;

    if (positions.length < 2) {
        source.setData({
            type: 'Feature',
            geometry: { type: 'LineString', coordinates: [] },
            properties: {}
        });
        return;
    }

    const coordinates = positions.map(p => [p.Longitude, p.Latitude]);
    source.setData({
        type: 'Feature',
        geometry: { type: 'LineString', coordinates },
        properties: {}
    });
}

export function setTrailColor(category) {
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
