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

import { registerAircraftIcons, getIconImageExpression } from './AircraftIcons.js';

let map = null;
let viewportCallback = null;
let markerClickCallback = null;
let mapClickCallback = null;
let markerHoverEnterCallback = null;
let markerHoverLeaveCallback = null;
let debounceTimer = null;
let selectedIcao = null;
let hoveredIcao = null;
let hoveredCoords = null;
let hoveredProps = null;

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

    map.on('load', () => {
        registerAircraftIcons(map);
        addLayers();

        if (pendingRangeRings) {
            const p = pendingRangeRings;
            pendingRangeRings = null;
            updateRangeRings(p.lat, p.lon, p.visible, p.distanceUnit);
        }
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

    // Re-project tooltip position on map move/zoom
    map.on('move', () => {
        if (hoveredIcao && hoveredCoords && hoveredProps && markerHoverEnterCallback) {
            const pt = map.project(hoveredCoords);
            markerHoverEnterCallback({ ...hoveredProps, x: pt.x, y: pt.y });
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
        lineMetrics: true,
        data: { type: 'Feature', geometry: { type: 'LineString', coordinates: [] }, properties: {} }
    });

    map.addLayer({
        id: 'trail-layer',
        type: 'line',
        source: 'trail-source',
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: {
            'line-color': '#006192',
            'line-width': 3,
            'line-gradient': [
                'interpolate', ['linear'], ['line-progress'],
                0, 'rgba(0, 97, 146, 0.15)',
                1, 'rgba(0, 97, 146, 1)'
            ]
        }
    });

    // Aircraft source and layer
    map.addSource('aircraft-source', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] }
    });

    // Aircraft layer with pre-rendered canvas icons (fill + black stroke baked in)
    map.addLayer({
        id: 'aircraft-layer',
        type: 'symbol',
        source: 'aircraft-source',
        layout: {
            'icon-image': getIconImageExpression(),
            'icon-size': 1.0,
            'icon-rotate': ['get', 'heading'],
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
        map.flyTo({ center: [lon, lat], zoom: zoom || 8 });
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
    aircraftMap.forEach((aircraft, icao) => {
        if (!aircraft.Coordinate) return;
        features.push({
            type: 'Feature',
            geometry: {
                type: 'Point',
                coordinates: [aircraft.Coordinate.Longitude, aircraft.Coordinate.Latitude]
            },
            properties: {
                icao: icao,
                callsign: aircraft.Callsign || icao,
                altitude: aircraft.BarometricAltitude ? aircraft.BarometricAltitude.Feet : 0,
                speed: aircraft.Speed ? aircraft.Speed.Knots : 0,
                heading: aircraft.Track || aircraft.TrackOnGround || 0,
                selected: icao === selectedIcao
            }
        });
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
}

export function highlightSelected(icao) {
    selectedIcao = icao;
}

export function clearSelection() {
    selectedIcao = null;
}

export function panTo(lat, lon) {
    if (map) {
        map.flyTo({ center: [lon, lat], zoom: Math.max(map.getZoom(), 8) });
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

export function onViewportChange(callback) { viewportCallback = callback; }
export function onMarkerClick(callback) { markerClickCallback = callback; }
export function onMapClick(callback) { mapClickCallback = callback; }
export function onMarkerHover(enterCb, leaveCb) {
    markerHoverEnterCallback = enterCb;
    markerHoverLeaveCallback = leaveCb;
}
