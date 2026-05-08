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

/**
 * Fallback UI rendered when the browser cannot create a WebGL context.
 * Mounted from Index.jsx in place of <App /> — never coexists with the
 * normal map view. Mirrors the left panel's logo header for visual
 * continuity, then explains how to enable hardware acceleration.
 */
export function WebGLError() {
    return (
        <div class="webgl-error panel">
            <div class="logo-header">
                <img src="img/logo.svg" alt="Aeromux" class="logo-img" />
                <div class="logo-text">
                    <div class="logo-title">AEROMUX</div>
                    <div class="logo-subtitle">Web Map</div>
                </div>
            </div>
            <div class="webgl-error-body">
                <h2 class="webgl-error-title">Hardware acceleration required</h2>
                <p>
                    Aeromux Web Map uses WebGL to render the map. Your browser
                    reports that WebGL is unavailable, which usually means
                    hardware acceleration is disabled.
                </p>
                <p>
                    To continue, enable hardware acceleration in your browser
                    settings and reload this page. In Chrome and Edge, this is
                    found under <em>Settings → System → Use hardware acceleration
                    when available</em>. In Firefox, under <em>Settings → General
                    → Performance</em>.
                </p>
            </div>
        </div>
    );
}
