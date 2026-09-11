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
import { useRef, useState, useLayoutEffect } from 'preact/hooks';
import { convertSpeed, convertAltitude } from '../Services/UnitConversion.js';

// Clear of the viewport edge, so the box is never half off-screen.
const EDGE_MARGIN_PX = 8;

export function HoverTooltip({ hover, units, pinned = false }) {
    const ref = useRef(null);
    const [width, setWidth] = useState(0);

    // The box is centred on the aircraft with a transform, which is applied after
    // layout — so close to the right edge the browser sizes it to the space that is
    // left and the text wraps. Keeping it from wrapping fixes the squeeze; measuring
    // the resulting width lets the position be pulled back inside the viewport, which
    // is what keeps it readable there.
    useLayoutEffect(() => {
        if (ref.current) setWidth(ref.current.offsetWidth);
    });

    if (!hover) return null;

    const speed = hover.speed ? convertSpeed(hover.speed, units.speed) : null;
    const alt = hover.altitude ? convertAltitude(hover.altitude, units.altitude) : null;

    const half = width / 2;
    const left = width
        ? Math.min(
            Math.max(hover.x, half + EDGE_MARGIN_PX),
            window.innerWidth - half - EDGE_MARGIN_PX
        )
        : hover.x;

    return (
        <div ref={ref} class={pinned ? 'hover-tooltip hover-tooltip--pinned' : 'hover-tooltip'} style={{ left: left + 'px', top: hover.y - 20 + 'px', transform: 'translate(-50%, -100%)' }}>
            <div class="hover-tooltip-callsign">{hover.callsign || 'N/A'}</div>
            <div class="hover-tooltip-field">{hover.icao}</div>
            {speed && <div class="hover-tooltip-field">{speed.value} {speed.label}</div>}
            {alt && <div class="hover-tooltip-field">{alt.value} {alt.label}</div>}
            {/* Sky View only: where to look for this aircraft. Omitted entirely in
                map mode, where the fields are absent. The elevation shown is the
                true angle, negative for aircraft the Earth's curvature hides, even
                though such a chip is drawn clamped to the horizon. */}
            {hover.azimuthDeg != null && (
                <div class="hover-tooltip-field">
                    AZ {Math.round(hover.azimuthDeg)}° · EL {hover.elevationDeg.toFixed(1)}°
                </div>
            )}
        </div>
    );
}
