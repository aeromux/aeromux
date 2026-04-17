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
const SETTINGS_DEFAULTS = { rangeRings: true, rangeOutline: true };

export function loadSettings() {
    try {
        const stored = localStorage.getItem(SETTINGS_STORAGE_KEY);
        if (stored) {
            const parsed = JSON.parse(stored);
            return {
                rangeRings: parsed.rangeRings !== undefined ? parsed.rangeRings : SETTINGS_DEFAULTS.rangeRings,
                rangeOutline: parsed.rangeOutline !== undefined ? parsed.rangeOutline : SETTINGS_DEFAULTS.rangeOutline
            };
        }
    } catch (e) {
        // Ignore parse errors
    }
    return { ...SETTINGS_DEFAULTS };
}

export function saveSettings(settings) {
    try {
        localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(settings));
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
    } catch (e) {
        // Ignore storage errors
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
