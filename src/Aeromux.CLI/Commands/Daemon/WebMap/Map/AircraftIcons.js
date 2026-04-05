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

// Renders the aircraft silhouette (from Graphics/aircraft.svg) onto HTML Canvas
// elements with a black stroke border and altitude-based fill color. Each color
// variant is registered as a separate MapLibre image, avoiding SDF limitations.

const CANVAS_SIZE = 128;
const STROKE_WIDTH = 5;
const VIEWBOX = 122.88;

// SVG path data from Graphics/aircraft.svg (viewBox 0 0 122.88 122.88)
const AIRCRAFT_PATH = 'M16.63,105.75c0.01-4.03,2.3-7.97,6.03-12.38L1.09,79.73c-1.36-0.59-1.33-1.42-0.54-2.4l4.57-3.9c0.83-0.51,1.71-0.73,2.66-0.47l26.62,4.5l22.18-24.02L4.8,18.41c-1.31-0.77-1.42-1.64-0.07-2.65l7.47-5.96l67.5,18.97L99.64,7.45c6.69-5.79,13.19-8.38,18.18-7.15c2.75,0.68,3.72,1.5,4.57,4.08c1.65,5.06-0.91,11.86-6.96,18.86L94.11,43.18l18.97,67.5l-5.96,7.47c-1.01,1.34-1.88,1.23-2.65-0.07L69.43,66.31L45.41,88.48l4.5,26.62c0.26,0.94,0.05,1.82-0.47,2.66l-3.9,4.57c-0.97,0.79-1.81,0.82-2.4-0.54l-13.64-21.57c-4.43,3.74-8.37,6.03-12.42,6.03C16.71,106.24,16.63,106.11,16.63,105.75L16.63,105.75z';

// Altitude color stops (feet → [r, g, b])
const COLOR_STOPS = [
    [0,     [179, 217, 255]],  // #b3d9ff
    [10000, [102, 178, 255]],  // #66b2ff
    [25000, [51,  153, 255]],  // #3399ff
    [40000, [0,   97,  146]]   // #006192
];

const SELECTED_COLOR = [230, 126, 34]; // #e67e22
const ALTITUDE_STEP = 2000;
const MAX_ALTITUDE = 40000;

function interpolateColor(altitude) {
    if (altitude <= COLOR_STOPS[0][0]) return COLOR_STOPS[0][1];
    if (altitude >= COLOR_STOPS[COLOR_STOPS.length - 1][0]) return COLOR_STOPS[COLOR_STOPS.length - 1][1];

    for (let i = 0; i < COLOR_STOPS.length - 1; i++) {
        const [altLow, colorLow] = COLOR_STOPS[i];
        const [altHigh, colorHigh] = COLOR_STOPS[i + 1];
        if (altitude >= altLow && altitude <= altHigh) {
            const t = (altitude - altLow) / (altHigh - altLow);
            return [
                Math.round(colorLow[0] + t * (colorHigh[0] - colorLow[0])),
                Math.round(colorLow[1] + t * (colorHigh[1] - colorLow[1])),
                Math.round(colorLow[2] + t * (colorHigh[2] - colorLow[2]))
            ];
        }
    }
    return COLOR_STOPS[COLOR_STOPS.length - 1][1];
}

function renderIcon(rgb) {
    const canvas = document.createElement('canvas');
    canvas.width = CANVAS_SIZE;
    canvas.height = CANVAS_SIZE;
    const ctx = canvas.getContext('2d');

    // Rotate so the nose points north (up) — the SVG path faces northeast (~45°)
    // Scale reduced to 0.65 to fit the rotated shape within the canvas bounds
    const scale = (CANVAS_SIZE / VIEWBOX) * 0.65;
    ctx.translate(CANVAS_SIZE / 2, CANVAS_SIZE / 2);
    ctx.rotate(-45 * Math.PI / 180);
    ctx.translate(-CANVAS_SIZE / 2, -CANVAS_SIZE / 2);

    // Scale the 122.88 viewBox into the canvas with margin for stroke
    const offset = (CANVAS_SIZE - VIEWBOX * scale) / 2;
    ctx.translate(offset, offset);
    ctx.scale(scale, scale);

    const path = new Path2D(AIRCRAFT_PATH);

    ctx.fillStyle = `rgb(${rgb[0]},${rgb[1]},${rgb[2]})`;
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = STROKE_WIDTH / scale;
    ctx.lineJoin = 'round';

    ctx.stroke(path);
    ctx.fill(path);

    const imageData = ctx.getImageData(0, 0, CANVAS_SIZE, CANVAS_SIZE);
    return { width: CANVAS_SIZE, height: CANVAS_SIZE, data: new Uint8Array(imageData.data.buffer) };
}

export function registerAircraftIcons(map) {
    const pixelRatio = CANVAS_SIZE / 34;

    // Altitude-based variants
    for (let alt = 0; alt <= MAX_ALTITUDE; alt += ALTITUDE_STEP) {
        const color = interpolateColor(alt);
        const canvas = renderIcon(color);
        map.addImage('aircraft-alt-' + alt, canvas, { pixelRatio });
    }

    // Selected variant (orange)
    const selectedCanvas = renderIcon(SELECTED_COLOR);
    map.addImage('aircraft-selected', selectedCanvas, { pixelRatio });
}

export function getIconImageExpression() {
    const steps = ['step', ['get', 'altitude'], 'aircraft-alt-0'];
    for (let alt = ALTITUDE_STEP; alt <= MAX_ALTITUDE; alt += ALTITUDE_STEP) {
        steps.push(alt, 'aircraft-alt-' + alt);
    }

    return [
        'case',
        ['==', ['get', 'selected'], true],
        'aircraft-selected',
        steps
    ];
}
