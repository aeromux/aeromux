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

import { h } from 'preact';
import { useRef, useEffect } from 'preact/hooks';

const CHART_HEIGHT = 160;
const PADDING_V = { top: 8, bottom: 28 };
const LABEL_GAP = 6;
const COLOR_ALTITUDE = '#006192';
const COLOR_SPEED = '#e67e22';
const COLOR_GRID = 'rgba(0,0,0,0.06)';
const COLOR_LABEL = '#9ca3af';
const FONT = '10px InterVariable, Inter, system-ui, sans-serif';

function getAltValue(entry, unit) {
    return unit === 'm' ? entry.altitudeMeters : entry.altitudeFeet;
}

function getSpdValue(entry, unit) {
    if (unit === 'kmh') return entry.speedKmh;
    if (unit === 'mph') return entry.speedMph;
    return entry.speedKnots;
}

function altUnitLabel(unit) { return unit === 'm' ? 'm' : 'ft'; }

function spdUnitLabel(unit) {
    if (unit === 'kmh') return 'km/h';
    if (unit === 'mph') return 'mph';
    return 'kts';
}

const TARGET_TICKS = 6;
const NICE_MULTIPLIERS = [1, 2, 5, 10];

// Snap min/max to "nice" round numbers so the axis shows exactly TARGET_TICKS evenly-spaced labels.
function computeRange(values, clampMinZero) {
    const valid = values.filter(v => v != null);
    if (valid.length === 0) return null;
    let min = Math.min(...valid);
    let max = Math.max(...valid);
    if (clampMinZero && min > 0) min = 0;
    if (min === max) { min -= 1; max += 1; }

    const intervals = TARGET_TICKS - 1;
    const range = max - min;
    const roughStep = range / intervals;
    const mag = Math.pow(10, Math.floor(Math.log10(roughStep)));

    // Try each nice step multiplier until we find one where
    // snapping min down and max up fits within TARGET_TICKS
    for (const m of NICE_MULTIPLIERS) {
        const step = Math.max(1, mag * m);
        const niceMin = Math.floor(min / step) * step;
        const niceMax = Math.ceil(max / step) * step;
        const ticks = Math.round((niceMax - niceMin) / step) + 1;
        if (ticks <= TARGET_TICKS) {
            // Extend upward to exactly TARGET_TICKS
            return { min: niceMin, max: niceMin + step * intervals, step };
        }
    }

    // Fallback: use 10*mag
    const step = Math.max(1, 10 * mag);
    const niceMin = Math.floor(min / step) * step;
    return { min: niceMin, max: niceMin + step * intervals, step };
}

function formatTime(ts) {
    const d = new Date(ts);
    const hh = String(d.getHours()).padStart(2, '0');
    const mm = String(d.getMinutes()).padStart(2, '0');
    return `${hh}:${mm}`;
}

function formatAxisValue(v) {
    const n = Math.round(v);
    return Math.abs(n) >= 1000 ? n.toLocaleString() : String(n);
}

// Draw a data line, breaking the path at null values to produce visible gaps.
function drawLine(ctx, entries, times, getValue, yPos, xPos, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.5;
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';

    let drawing = false;
    ctx.beginPath();
    for (let i = 0; i < entries.length; i++) {
        const v = getValue(entries[i]);
        if (v == null) {
            if (drawing) { ctx.stroke(); ctx.beginPath(); drawing = false; }
            continue;
        }
        const x = xPos(times[i]);
        const y = yPos(v);
        if (!drawing) { ctx.moveTo(x, y); drawing = true; }
        else { ctx.lineTo(x, y); }
    }
    if (drawing) ctx.stroke();
}

function draw(canvas, entries, units) {
    const ctx = canvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    const cssHeight = CHART_HEIGHT;

    // Reset inline width so CSS width:100% applies, then measure actual layout width.
    canvas.style.width = '';
    const cssWidth = canvas.clientWidth;

    canvas.width = cssWidth * dpr;
    canvas.height = cssHeight * dpr;
    canvas.style.width = cssWidth + 'px';
    canvas.style.height = cssHeight + 'px';
    ctx.scale(dpr, dpr);
    ctx.clearRect(0, 0, cssWidth, cssHeight);

    const times = entries.map(e => e.timestamp);
    const altValues = entries.map(e => getAltValue(e, units.altitude));
    const spdValues = entries.map(e => getSpdValue(e, units.speed));

    const altRange = computeRange(altValues, true);
    const spdRange = computeRange(spdValues, false);

    if (!altRange && !spdRange) return;

    // Measure each axis label width independently for tight padding
    ctx.font = FONT;
    let leftMaxWidth = 0;
    let rightMaxWidth = 0;
    if (altRange) {
        for (let v = altRange.min; v <= altRange.max; v += altRange.step) {
            const w = ctx.measureText(formatAxisValue(v)).width;
            if (w > leftMaxWidth) leftMaxWidth = w;
        }
    }
    if (spdRange) {
        for (let v = spdRange.min; v <= spdRange.max; v += spdRange.step) {
            const w = ctx.measureText(formatAxisValue(v)).width;
            if (w > rightMaxWidth) rightMaxWidth = w;
        }
    }
    if (!altRange) leftMaxWidth = rightMaxWidth;
    if (!spdRange) rightMaxWidth = leftMaxWidth;

    const plotLeft = Math.ceil(leftMaxWidth) + LABEL_GAP;
    const plotRight = cssWidth - Math.ceil(rightMaxWidth) - LABEL_GAP;
    const plotTop = PADDING_V.top;
    const plotBottom = cssHeight - PADDING_V.bottom;
    const plotWidth = plotRight - plotLeft;
    const plotHeight = plotBottom - plotTop;

    if (plotWidth <= 0 || plotHeight <= 0) return;

    const timeMin = times[0];
    const timeMax = times[times.length - 1];
    const timeRange = timeMax - timeMin || 1;

    const xPos = (t) => plotLeft + ((t - timeMin) / timeRange) * plotWidth;
    const yPosAlt = altRange
        ? (v) => plotBottom - ((v - altRange.min) / (altRange.max - altRange.min || 1)) * plotHeight
        : null;
    const yPosSpd = spdRange
        ? (v) => plotBottom - ((v - spdRange.min) / (spdRange.max - spdRange.min || 1)) * plotHeight
        : null;

    // Grid lines from altitude ticks (or speed if no altitude)
    const gridRange = altRange || spdRange;
    const gridYPos = altRange ? yPosAlt : yPosSpd;
    ctx.strokeStyle = COLOR_GRID;
    ctx.lineWidth = 1;
    for (let v = gridRange.min; v <= gridRange.max; v += gridRange.step) {
        const y = Math.round(gridYPos(v)) + 0.5;
        ctx.beginPath();
        ctx.moveTo(plotLeft, y);
        ctx.lineTo(plotRight, y);
        ctx.stroke();
    }

    // Axis labels
    ctx.fillStyle = COLOR_LABEL;

    // Left Y-axis (altitude)
    if (altRange) {
        ctx.textAlign = 'right';
        ctx.textBaseline = 'middle';
        for (let v = altRange.max; v >= altRange.min; v -= altRange.step) {
            const y = yPosAlt(v);
            ctx.fillText(formatAxisValue(v), plotLeft - LABEL_GAP, y);
        }
    }

    // Right Y-axis (speed)
    if (spdRange) {
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        for (let v = spdRange.max; v >= spdRange.min; v -= spdRange.step) {
            const y = yPosSpd(v);
            ctx.fillText(formatAxisValue(v), plotRight + LABEL_GAP, y);
        }
    }

    // X-axis time labels
    const desiredTimeTicks = Math.max(2, Math.min(6, Math.floor(plotWidth / 60)));
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    for (let i = 0; i < desiredTimeTicks; i++) {
        const t = timeMin + (timeRange * i) / (desiredTimeTicks - 1);
        const x = xPos(t);
        ctx.fillText(formatTime(t), x, plotBottom + 12);
    }

    // Data lines
    if (altRange && yPosAlt) {
        drawLine(ctx, entries, times, (e) => getAltValue(e, units.altitude), yPosAlt, xPos, COLOR_ALTITUDE);
    }
    if (spdRange && yPosSpd) {
        drawLine(ctx, entries, times, (e) => getSpdValue(e, units.speed), yPosSpd, xPos, COLOR_SPEED);
    }
}

export function FlightProfileChart({ entries, units }) {
    const canvasRef = useRef(null);
    const rafRef = useRef(null);

    useEffect(() => {
        if (!canvasRef.current || !entries || entries.length === 0) return;

        const redraw = () => {
            if (rafRef.current) cancelAnimationFrame(rafRef.current);
            rafRef.current = requestAnimationFrame(() => {
                if (canvasRef.current) draw(canvasRef.current, entries, units);
            });
        };

        redraw();

        const ro = new ResizeObserver(redraw);
        ro.observe(canvasRef.current.parentElement);

        return () => {
            ro.disconnect();
            if (rafRef.current) cancelAnimationFrame(rafRef.current);
        };
    }, [entries, units.altitude, units.speed]);

    if (!entries || entries.length === 0) {
        return (
            <div class="flight-profile-container">
                <div class="flight-profile-empty">No flight profile data</div>
            </div>
        );
    }

    return (
        <div class="flight-profile-container">
            <div class="flight-profile-legend">
                <div class="flight-profile-legend-item">
                    <span class="flight-profile-legend-line" style={{ backgroundColor: COLOR_ALTITUDE }}></span>
                    <span>Altitude ({altUnitLabel(units.altitude)})</span>
                </div>
                <div class="flight-profile-legend-item">
                    <span class="flight-profile-legend-line" style={{ backgroundColor: COLOR_SPEED }}></span>
                    <span>Speed ({spdUnitLabel(units.speed)})</span>
                </div>
            </div>
            <canvas ref={canvasRef} class="flight-profile-canvas" />
        </div>
    );
}
