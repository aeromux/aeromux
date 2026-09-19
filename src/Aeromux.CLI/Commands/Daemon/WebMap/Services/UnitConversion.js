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

import { normalizeStoredFraction } from './SheetHeight.js';

// User preferences (units, sort, interface settings) persisted across sessions
const STORAGE_KEY = 'aeromux-units';

const DEFAULTS = {
    speed: 'kts',
    altitude: 'ft',
    distance: 'nm'
};

// Aircraft list sort column and direction persisted across sessions
const SORT_STORAGE_KEY = 'aeromux-sort';
const SORT_DEFAULTS = { column: 'callsign', direction: 'asc' };

export function loadSort() {
    try {
        const stored = localStorage.getItem(SORT_STORAGE_KEY);
        if (stored) {
            const parsed = JSON.parse(stored);
            return {
                column: parsed.column || SORT_DEFAULTS.column,
                direction: parsed.direction || SORT_DEFAULTS.direction
            };
        }
    } catch (e) {
        // Ignore parse errors
    }
    return { ...SORT_DEFAULTS };
}

export function saveSort(sort) {
    try {
        localStorage.setItem(SORT_STORAGE_KEY, JSON.stringify(sort));
    } catch (e) {
        // Ignore storage errors
    }
}

// Interface settings (range rings toggle, etc.) persisted across sessions
const SETTINGS_STORAGE_KEY = 'aeromux-settings';
const SETTINGS_DEFAULTS = {
    rangeRings: true,
    rangeOutline: true,
    aircraftPhotos: true,
    heatmap: false,          // traffic-density overlay, off by default
    heatmapCellNm: 2,        // fixed-nm display cell size
    heatmapWindowHours: 24,  // rolling window
    viewMode: 'map',         // 'map' | 'sky' — which view is rendering
    mapAltitude: true,       // aircraft drawn at their real height on a tilted map
    mapAltitudeScale: 2,     // 1 | 2 | 5 — how much that height is exaggerated
    skyMaxRangeNm: 150,      // Sky View range from the receiver
    skyFov: 75,              // horizontal field of view, degrees
    skyPitch: 0,             // camera tilt above the horizon, degrees
    skyFlatten: false,       // 360-degree equirectangular panorama
    skyRibbon: true,         // coverage ribbon below the horizon
    skyTrail: true,          // 3D path for the selected aircraft
    skyCelestial: true,      // sun and moon markers at their real positions
    skyTwilight: true,       // sky colour follows the sun's height
    skyLabels: null          // 'selection' | 'auto' | 'all'; null = not yet
                             // resolved for this device, see App.jsx
};

export function loadSettings() {
    try {
        const stored = localStorage.getItem(SETTINGS_STORAGE_KEY);
        if (stored) {
            const parsed = JSON.parse(stored);
            return {
                rangeRings: parsed.rangeRings !== undefined ? parsed.rangeRings : SETTINGS_DEFAULTS.rangeRings,
                rangeOutline: parsed.rangeOutline !== undefined ? parsed.rangeOutline : SETTINGS_DEFAULTS.rangeOutline,
                aircraftPhotos: parsed.aircraftPhotos !== undefined ? parsed.aircraftPhotos : SETTINGS_DEFAULTS.aircraftPhotos,
                heatmap: parsed.heatmap !== undefined ? parsed.heatmap : SETTINGS_DEFAULTS.heatmap,
                heatmapCellNm: parsed.heatmapCellNm !== undefined ? parsed.heatmapCellNm : SETTINGS_DEFAULTS.heatmapCellNm,
                heatmapWindowHours: parsed.heatmapWindowHours !== undefined ? parsed.heatmapWindowHours : SETTINGS_DEFAULTS.heatmapWindowHours,
                viewMode: parsed.viewMode !== undefined ? parsed.viewMode : SETTINGS_DEFAULTS.viewMode,
                mapAltitude: parsed.mapAltitude !== undefined ? parsed.mapAltitude : SETTINGS_DEFAULTS.mapAltitude,
                mapAltitudeScale: parsed.mapAltitudeScale !== undefined ? parsed.mapAltitudeScale : SETTINGS_DEFAULTS.mapAltitudeScale,
                skyMaxRangeNm: parsed.skyMaxRangeNm !== undefined ? parsed.skyMaxRangeNm : SETTINGS_DEFAULTS.skyMaxRangeNm,
                skyFov: parsed.skyFov !== undefined ? parsed.skyFov : SETTINGS_DEFAULTS.skyFov,
                skyPitch: parsed.skyPitch !== undefined ? parsed.skyPitch : SETTINGS_DEFAULTS.skyPitch,
                skyFlatten: parsed.skyFlatten !== undefined ? parsed.skyFlatten : SETTINGS_DEFAULTS.skyFlatten,
                skyRibbon: parsed.skyRibbon !== undefined ? parsed.skyRibbon : SETTINGS_DEFAULTS.skyRibbon,
                skyTrail: parsed.skyTrail !== undefined ? parsed.skyTrail : SETTINGS_DEFAULTS.skyTrail,
                skyCelestial: parsed.skyCelestial !== undefined ? parsed.skyCelestial : SETTINGS_DEFAULTS.skyCelestial,
                skyTwilight: parsed.skyTwilight !== undefined ? parsed.skyTwilight : SETTINGS_DEFAULTS.skyTwilight,
                skyLabels: parsed.skyLabels !== undefined ? parsed.skyLabels : SETTINGS_DEFAULTS.skyLabels
            };
        }
    } catch (e) {
        // Ignore parse errors
    }
    return { ...SETTINGS_DEFAULTS };
}

// Some defaults depend on the device rather than being fixed, so they are stored as
// null until first resolved. Anything that restores defaults has to run settings
// through here, otherwise the sentinel survives and no option in the group matches —
// the control then renders with nothing selected at all.
export function resolveDeviceDefaults(settings, isMobile) {
    if (settings.skyLabels != null) return settings;
    return { ...settings, skyLabels: isMobile ? 'selection' : 'auto' };
}

export function saveSettings(settings) {
    try {
        localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(settings));
    } catch (e) {
        // Ignore storage errors
    }
}

// Mobile bottom-sheet height, persisted as a viewport fraction so it survives
// rotation and transfers sensibly between devices of different sizes.
const SHEET_HEIGHT_STORAGE_KEY = 'aeromux-sheet-height';

export function loadSheetHeight() {
    try {
        const stored = localStorage.getItem(SHEET_HEIGHT_STORAGE_KEY);
        if (stored !== null) {
            return normalizeStoredFraction(parseFloat(stored));
        }
    } catch (e) {
        // Ignore parse errors
    }
    return null;
}

export function saveSheetHeight(fraction) {
    try {
        localStorage.setItem(SHEET_HEIGHT_STORAGE_KEY, String(fraction));
    } catch (e) {
        // Ignore storage errors
    }
}

export function clearSheetHeight() {
    try {
        localStorage.removeItem(SHEET_HEIGHT_STORAGE_KEY);
    } catch (e) {
        // Ignore storage errors
    }
}

// Clears all persisted preferences so load functions fall back to defaults
export function resetAllSettings() {
    try {
        localStorage.removeItem(STORAGE_KEY);
        localStorage.removeItem(SORT_STORAGE_KEY);
        localStorage.removeItem(SETTINGS_STORAGE_KEY);
        localStorage.removeItem(SHEET_HEIGHT_STORAGE_KEY);
    } catch (e) {
        // Ignore storage errors
    }
}

// Nautical miles to kilometers. Settings store distances in nautical miles
// regardless of the user's display unit, so conversions happen at the point of
// use rather than in storage.
export function nmToKm(nm) {
    return nm * 1.852;
}

// Nautical miles from a value already in the display unit, the inverse of
// convertNauticalMiles and deliberately unrounded: it is used to express a scale
// step chosen in the display unit back in the nautical miles everything is measured
// in, where rounding would put the step slightly off the round number it stands for.
export function nmFromDisplayUnit(value, unit) {
    switch (unit) {
        case 'nm':
            return value;
        case 'mi':
            return value / 1.15078;
        default:
            return value / 1.852;
    }
}

export function convertNauticalMiles(nm, unit) {
    switch (unit) {
        case 'nm':
            return { value: Math.round(nm), label: 'nm' };
        case 'mi':
            return { value: Math.round(nm * 1.15078), label: 'mi' };
        default:
            return { value: Math.round(nm * 1.852), label: 'km' };
    }
}

// Short unit label for a distance unit ('nm' | 'km' | 'mi').
export function distanceUnitLabel(unit) {
    return unit === 'mi' ? 'mi' : unit === 'nm' ? 'nm' : 'km';
}

// Display string for a nautical-mile cell size in the selected unit. The underlying
// value stays in nm; this only converts the label (e.g. 2 nm → "3.7" km). Rendered to
// one decimal with a trailing ".0" stripped.
export function formatCellSizeLabel(nm, unit) {
    if (unit === 'nm') return String(nm);
    const factor = unit === 'mi' ? 1.15078 : 1.852;
    return String(Math.round(nm * factor * 10) / 10);
}

export function loadUnits() {
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            const parsed = JSON.parse(stored);
            return {
                speed: parsed.speed || DEFAULTS.speed,
                altitude: parsed.altitude || DEFAULTS.altitude,
                distance: parsed.distance || DEFAULTS.distance
            };
        }
    } catch (e) {
        // Ignore parse errors
    }
    return { ...DEFAULTS };
}

export function saveUnits(units) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(units));
    } catch (e) {
        // Ignore storage errors
    }
}

export function convertSpeed(knots, unit) {
    if (knots == null) return { value: null, label: unit };
    switch (unit) {
        case 'kmh':
            return { value: Math.round(knots * 1.852), label: 'km/h' };
        case 'mph':
            return { value: Math.round(knots * 1.15078), label: 'mph' };
        default:
            return { value: Math.round(knots), label: 'kts' };
    }
}

export function convertAltitude(feet, unit) {
    if (feet == null) return { value: null, label: unit };
    switch (unit) {
        case 'm':
            return { value: Math.round(feet * 0.3048), label: 'm' };
        default:
            return { value: Math.round(feet), label: 'ft' };
    }
}

export function convertDistance(km, unit) {
    if (km == null) return { value: null, label: unit };
    switch (unit) {
        case 'nm':
            return { value: (km / 1.852).toFixed(1), label: 'nm' };
        case 'mi':
            return { value: (km * 0.621371).toFixed(1), label: 'mi' };
        default:
            return { value: (km).toFixed(1), label: 'km' };
    }
}

export function formatAltitude(altObj, unit) {
    if (!altObj) return 'N/A';
    if (unit === 'm') {
        return `${Math.round(altObj.Meters).toLocaleString()} m`;
    }
    return `${Math.round(altObj.Feet).toLocaleString()} ft`;
}

export function formatSpeed(velObj, unit) {
    if (!velObj) return 'N/A';
    switch (unit) {
        case 'kmh':
            return `${Math.round(velObj.KilometersPerHour)} km/h`;
        case 'mph':
            return `${Math.round(velObj.MilesPerHour)} mph`;
        default:
            return `${Math.round(velObj.Knots)} kts`;
    }
}

export function haversineDistance(lat1, lon1, lat2, lon2) {
    const R = 6371; // km
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLon = (lon2 - lon1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
              Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
              Math.sin(dLon / 2) * Math.sin(dLon / 2);
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c;
}
