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

import { h, render } from 'preact';
import { App } from './Components/App.jsx';
import { WebGLError } from './Components/WebGLError.jsx';

// Pre-flight WebGL probe — gates mounting <App /> when the browser cannot
// create a WebGL context (typically due to disabled hardware acceleration).
// Mirrors maplibre-gl's own context-creation order (webgl2 → webgl);
// maplibregl.supported() was removed in v5 so we roll our own. The outer
// try/catch defends against locked-down environments where canvas creation
// itself throws.
function isWebGLAvailable() {
    try {
        const canvas = document.createElement('canvas');
        return !!(canvas.getContext('webgl2') || canvas.getContext('webgl'));
    } catch {
        return false;
    }
}

render(isWebGLAvailable() ? <App /> : <WebGLError />, document.getElementById('app'));
