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

// What ties an aircraft drawn in the air back to the ground it is over: a stalk down
// to the shadow the map draws there, and the selected aircraft's trail climbing and
// descending with it.
//
// Two custom layers, for a reason worth stating. The probe draws nothing and exists
// only to capture the projection matrix, which MapLibre hands to custom layers and to
// nobody else. Placement in MapManager depends on that matrix, and placement putting
// aircraft in the right place must not be at the mercy of a shader that failed to
// compile, so the thing that captures it has nothing that can fail.
//
// Everything here is drawn as lines of a constant pixel width, which rules out
// gl.LINES, since browsers cap lineWidth at 1. Each segment is two triangles whose
// corners are pushed apart in screen space by the vertex shader.

import {
    mercatorXFromLongitude,
    mercatorYFromLatitude,
    mercatorZFromAltitude
} from '../Services/AltitudeProjection.js';

export const PROBE_LAYER_ID = 'altitude-probe';
export const DRAW_LAYER_ID = 'altitude-marks';

// Line widths in CSS pixels.
const STALK_WIDTH = 1;
const SELECTED_STALK_WIDTH = 2;
const TRAIL_WIDTH = 3;

// A stalk is a reference line rather than a thing to look at, so it is faint where it
// meets the ground and only gains weight as it arrives at the aircraft.
const STALK_ALPHA_GROUND = 0.18;
const STALK_ALPHA_TOP = 0.75;
const SELECTED_ALPHA_GROUND = 0.35;
const SELECTED_ALPHA_TOP = 1;

// Dark rather than light. The map tiles are light under their dark overlay, and a
// white line over them has little to hold on to; a near-black one reads as a shadow
// cast down to the ground, which is close to what it means.
const STALK_COLOR = [16, 22, 28];
const SELECTED_COLOR = [230, 126, 34];

// The camera, as the probe last saw it. Read by MapManager to place aircraft, which
// is why it is captured even while the drawing layer has nothing to draw.
//
// The matrix is `defaultProjectionData.mainMatrix` and not the tempting
// `modelViewProjectionMatrix` beside it: that one works in world pixels, while this
// is the one MapLibre scales for custom layers to feed mercator [0..1] coordinates
// into, which is what everything here produces.
let camera = { matrix: null, fov: 0 };

// MapLibre hands custom layers 64-bit matrices to protect building-scale geometry
// from precision loss. WebGL takes 32-bit ones, so it is narrowed here, once per
// frame rather than once per batch.
const matrix32 = new Float32Array(16);

// The matrix the aircraft currently on screen were placed with. The drawing layer
// renders with this rather than with the live one, so a stalk and its icon can never
// disagree: both are then a frame behind the base map during a drag, which is far
// less visible than a line that misses its aircraft.
let placementMatrix = null;

let map = null;
let gl = null;
let lineProgram = null;
let fillProgram = null;
let failed = false;
let visible = true;

// One batch per color, since color is a uniform rather than a vertex attribute.
const batches = {
    stalks: { data: null, buffer: null, count: 0, color: STALK_COLOR, alpha: 1, width: STALK_WIDTH },
    selectedStalk: { data: null, buffer: null, count: 0, color: SELECTED_COLOR, alpha: 1, width: SELECTED_STALK_WIDTH },
    // Before the path itself, so the line reads on top of its own curtain.
    trailRibbon: { data: null, buffer: null, count: 0, color: [0, 97, 146], alpha: 0.22, fill: true },
    trail: { data: null, buffer: null, count: 0, color: [0, 97, 146], alpha: 0.9, width: TRAIL_WIDTH }
};

// ---------------------------------------------------------------------------
// Shaders
// ---------------------------------------------------------------------------

// Shared by both programs: take two points already in clip space and produce the
// corner of the quad this vertex belongs to, pushed sideways by half the line width.
//
// A segment with either end behind the camera is sent outside the clip volume rather
// than drawn, because its screen position is meaningless there and the triangle would
// smear across the map.
const EXPAND_GLSL = `
vec4 expand(vec4 clipFrom, vec4 clipTo, vec2 corner, vec2 viewport, float width) {
    if (clipFrom.w <= 0.0 || clipTo.w <= 0.0) return vec4(2.0, 2.0, 2.0, 1.0);

    vec2 screenFrom = clipFrom.xy / clipFrom.w * viewport * 0.5;
    vec2 screenTo = clipTo.xy / clipTo.w * viewport * 0.5;
    vec2 delta = screenTo - screenFrom;
    float len = length(delta);
    vec2 dir = len > 0.001 ? delta / len : vec2(1.0, 0.0);
    vec2 normal = vec2(-dir.y, dir.x);

    vec4 clip = corner.x < 0.5 ? clipFrom : clipTo;
    clip.xy += normal * (width * 0.5 * corner.y) / viewport * 2.0 * clip.w;
    // Flat in depth. MapLibre gives each layer a slice of the depth buffer to keep
    // the style's own order, and there is nothing here to sort against anything
    // else: a stalk is a reference line, not an object in the scene.
    clip.z = 0.0;
    return clip;
}
`;

const LINE_VERTEX = `
attribute vec3 a_from;
attribute vec3 a_to;
attribute vec2 a_corner;
attribute float a_alpha;

uniform mat4 u_matrix;
uniform vec2 u_viewport;
uniform float u_width;

varying float v_alpha;
${EXPAND_GLSL}
void main() {
    v_alpha = a_alpha;
    gl_Position = expand(u_matrix * vec4(a_from, 1.0), u_matrix * vec4(a_to, 1.0),
                         a_corner, u_viewport, u_width);
}
`;

// The curtain between the ground track and the flight path. No screen-space
// expansion: this is a surface in the world rather than a line of constant width, and
// its shape IS the altitude profile.
const FILL_VERTEX = `
attribute vec3 a_pos;

uniform mat4 u_matrix;

varying float v_alpha;
void main() {
    v_alpha = 1.0;
    vec4 clip = u_matrix * vec4(a_pos, 1.0);
    if (clip.w <= 0.0) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        return;
    }
    clip.z = 0.0;
    gl_Position = clip;
}
`;

// The canvas is premultiplied, so the color is multiplied by its own alpha here
// rather than left to the blend function.
const FRAGMENT = `
precision mediump float;
uniform vec4 u_color;
varying float v_alpha;
void main() {
    float alpha = u_color.a * v_alpha;
    gl_FragColor = vec4(u_color.rgb * alpha, alpha);
}
`;

function compile(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`[altitude-layer] shader failed to compile: ${log}`);
    }
    return shader;
}

function link(vertexSource, attributes, uniforms) {
    const program = gl.createProgram();
    const vertex = compile(gl.VERTEX_SHADER, vertexSource);
    const fragment = compile(gl.FRAGMENT_SHADER, FRAGMENT);
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const log = gl.getProgramInfoLog(program);
        gl.deleteProgram(program);
        throw new Error(`[altitude-layer] program failed to link: ${log}`);
    }

    const handle = { program, attributes: {}, uniforms: {} };
    for (const name of attributes) handle.attributes[name] = gl.getAttribLocation(program, name);
    for (const name of uniforms) handle.uniforms[name] = gl.getUniformLocation(program, name);
    return handle;
}

// ---------------------------------------------------------------------------
// Geometry
// ---------------------------------------------------------------------------

// Corners of the two triangles that make one segment, as (endpoint, side) pairs.
const CORNERS = [[0, -1], [0, 1], [1, -1], [1, -1], [0, 1], [1, 1]];

const LINE_FLOATS_PER_VERTEX = 9;   // from(3) to(3) corner(2) alpha(1)
const FILL_FLOATS_PER_VERTEX = 3;   // pos(3)

// Two triangles per segment, spanning ground track to flight path.
function buildRibbon(segments) {
    const data = new Float32Array(segments.length * 6 * FILL_FLOATS_PER_VERTEX);
    let at = 0;
    const push = (x, y, z) => { data[at++] = x; data[at++] = y; data[at++] = z; };
    for (const segment of segments) {
        const [ax, ay, az] = segment.from;
        const [bx, by, bz] = segment.to;
        push(ax, ay, 0); push(ax, ay, az); push(bx, by, 0);
        push(bx, by, 0); push(ax, ay, az); push(bx, by, bz);
    }
    return data;
}

function buildLines(segments) {
    const data = new Float32Array(segments.length * CORNERS.length * LINE_FLOATS_PER_VERTEX);
    let at = 0;
    for (const segment of segments) {
        const [fx, fy, fz] = segment.from;
        const [tx, ty, tz] = segment.to;
        for (const [end, side] of CORNERS) {
            data[at++] = fx; data[at++] = fy; data[at++] = fz;
            data[at++] = tx; data[at++] = ty; data[at++] = tz;
            data[at++] = end; data[at++] = side;
            data[at++] = end < 0.5 ? segment.alphaFrom : segment.alphaTo;
        }
    }
    return data;
}

function setBatch(batch, data, floatsPerVertex) {
    batch.data = data;
    batch.count = data ? data.length / floatsPerVertex : 0;
    batch.dirty = true;
}

function upload(batch) {
    if (!batch.dirty) return;
    batch.dirty = false;
    if (!batch.count) return;
    if (!batch.buffer) batch.buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, batch.buffer);
    gl.bufferData(gl.ARRAY_BUFFER, batch.data, gl.DYNAMIC_DRAW);
}

// ---------------------------------------------------------------------------
// Public surface
// ---------------------------------------------------------------------------

// Aircraft to mark, as { lon, lat, lift, selected } with the lift already in meters
// and already exaggerated. Aircraft with no lift worth drawing are the caller's to
// leave out: a stalk of no length under an aircraft on the ground means nothing.
export function setMarks(marks) {
    const stalks = [];
    const selectedStalk = [];

    for (const mark of marks) {
        const x = mercatorXFromLongitude(mark.lon);
        const y = mercatorYFromLatitude(mark.lat);
        const z = mercatorZFromAltitude(mark.lift, mark.lat);
        const segment = {
            from: [x, y, 0],
            to: [x, y, z],
            alphaFrom: mark.selected ? SELECTED_ALPHA_GROUND : STALK_ALPHA_GROUND,
            alphaTo: mark.selected ? SELECTED_ALPHA_TOP : STALK_ALPHA_TOP
        };
        (mark.selected ? selectedStalk : stalks).push(segment);
    }

    setBatch(batches.stalks, buildLines(stalks), LINE_FLOATS_PER_VERTEX);
    setBatch(batches.selectedStalk, buildLines(selectedStalk), LINE_FLOATS_PER_VERTEX);
    if (map) map.triggerRepaint();
}

// The selected aircraft's path, as { lon, lat, lift } in order. A point without a
// height breaks the line rather than dropping it to sea level, so a gap in reporting
// reads as a gap.
export function setTrail(points) {
    const segments = [];
    for (let i = 1; i < points.length; i++) {
        const a = points[i - 1];
        const b = points[i];
        if (a.lift == null || b.lift == null) continue;
        segments.push({
            from: [mercatorXFromLongitude(a.lon), mercatorYFromLatitude(a.lat), mercatorZFromAltitude(a.lift, a.lat)],
            to: [mercatorXFromLongitude(b.lon), mercatorYFromLatitude(b.lat), mercatorZFromAltitude(b.lift, b.lat)],
            alphaFrom: 1,
            alphaTo: 1
        });
    }
    setBatch(batches.trail, buildLines(segments), LINE_FLOATS_PER_VERTEX);
    setBatch(batches.trailRibbon, buildRibbon(segments), FILL_FLOATS_PER_VERTEX);
    if (map) map.triggerRepaint();
}

// Whether the climbing trail has anything to draw. A trail of points without
// altitude produces no segments, and the flat trail has to stay up in that case.
export function hasTrail() {
    return batches.trail.count > 0;
}

export function setTrailColor(rgb) {
    batches.trail.color = rgb;
    batches.trailRibbon.color = rgb;
    if (map) map.triggerRepaint();
}

export function setVisible(next) {
    if (visible === next) return;
    visible = next;
    if (map) map.triggerRepaint();
}

// The matrix the aircraft on screen were placed with. See placementMatrix above.
export function setPlacementMatrix(matrix) {
    placementMatrix = matrix;
}

// The camera as the probe last saw it, for the placement arithmetic in MapManager.
// Null matrix until the first frame has been drawn.
export function cameraFrame() {
    return camera;
}

export function clear() {
    for (const key of Object.keys(batches)) setBatch(batches[key], null, LINE_FLOATS_PER_VERTEX);
    if (map) map.triggerRepaint();
}

// ---------------------------------------------------------------------------
// The layers
// ---------------------------------------------------------------------------

// Draws nothing, fails at nothing, and is the only way to see the projection matrix.
export const probeLayer = {
    id: PROBE_LAYER_ID,
    type: 'custom',
    renderingMode: '2d',
    render(_gl, args) {
        camera = { matrix: args.defaultProjectionData.mainMatrix, fov: args.fov };
    }
};

export const drawLayer = {
    id: DRAW_LAYER_ID,
    type: 'custom',
    renderingMode: '2d',

    onAdd(addedMap, context) {
        map = addedMap;
        gl = context;
        failed = false;
        try {
            lineProgram = link(LINE_VERTEX, ['a_from', 'a_to', 'a_corner', 'a_alpha'],
                ['u_matrix', 'u_viewport', 'u_width', 'u_color']);
            fillProgram = link(FILL_VERTEX, ['a_pos'], ['u_matrix', 'u_color']);
        } catch (error) {
            // The map is still correct without this: aircraft are placed from the
            // probe's matrix, which does not depend on any of it.
            console.error(error);
            failed = true;
        }
        for (const batch of Object.values(batches)) {
            batch.buffer = null;
            batch.dirty = true;
        }
    },

    onRemove(_removedMap, context) {
        for (const batch of Object.values(batches)) {
            if (batch.buffer) context.deleteBuffer(batch.buffer);
            batch.buffer = null;
        }
        if (lineProgram) context.deleteProgram(lineProgram.program);
        if (fillProgram) context.deleteProgram(fillProgram.program);
        lineProgram = null;
        fillProgram = null;
        map = null;
        gl = null;
    },

    render(context, args) {
        if (failed || !visible || !lineProgram || !fillProgram) return;

        gl = context;
        const matrix = placementMatrix || args.defaultProjectionData.mainMatrix;
        if (!matrix) return;
        matrix32.set(matrix);

        const canvas = map.getCanvas();
        const viewport = [gl.drawingBufferWidth, gl.drawingBufferHeight];
        // Line widths are given in CSS pixels; the drawing buffer is not.
        const pixelRatio = canvas.clientWidth ? gl.drawingBufferWidth / canvas.clientWidth : 1;

        gl.enable(gl.BLEND);
        gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);

        for (const batch of Object.values(batches)) {
            upload(batch);
            if (!batch.count) continue;

            const handle = batch.fill ? fillProgram : lineProgram;
            gl.useProgram(handle.program);
            gl.bindBuffer(gl.ARRAY_BUFFER, batch.buffer);

            if (batch.fill) {
                bind(handle.attributes.a_pos, 3, FILL_FLOATS_PER_VERTEX * 4, 0);
            } else {
                const stride = LINE_FLOATS_PER_VERTEX * 4;
                bind(handle.attributes.a_from, 3, stride, 0);
                bind(handle.attributes.a_to, 3, stride, 3 * 4);
                bind(handle.attributes.a_corner, 2, stride, 6 * 4);
                bind(handle.attributes.a_alpha, 1, stride, 8 * 4);
            }

            gl.uniformMatrix4fv(handle.uniforms.u_matrix, false, matrix32);
            if (!batch.fill) {
                gl.uniform2f(handle.uniforms.u_viewport, viewport[0], viewport[1]);
                gl.uniform1f(handle.uniforms.u_width, batch.width * pixelRatio);
            }
            gl.uniform4f(handle.uniforms.u_color,
                batch.color[0] / 255, batch.color[1] / 255, batch.color[2] / 255, batch.alpha);

            gl.drawArrays(gl.TRIANGLES, 0, batch.count);

            // Attribute arrays are global state, and MapLibre draws its own layers
            // either side of this one.
            for (const location of Object.values(handle.attributes)) {
                if (location >= 0) gl.disableVertexAttribArray(location);
            }
        }

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
    }
};

function bind(location, size, stride, offset) {
    if (location < 0) return;
    gl.enableVertexAttribArray(location);
    gl.vertexAttribPointer(location, size, gl.FLOAT, false, stride, offset);
}

// Adds both layers. The drawing layer goes below `beforeId` so stalks pass under
// traffic rather than over it; the probe can go anywhere, since it draws nothing.
export function addTo(targetMap, beforeId) {
    if (!targetMap.getLayer(PROBE_LAYER_ID)) targetMap.addLayer(probeLayer);
    if (!targetMap.getLayer(DRAW_LAYER_ID)) targetMap.addLayer(drawLayer, beforeId);
}
