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
    mercatorZFromAltitude,
    TRANSFORM_TILE_SIZE
} from '../Services/AltitudeProjection.js';
import * as Atlas from './AircraftAtlas.js';

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

let map = null;
let gl = null;
let lineProgram = null;
let fillProgram = null;
let iconProgram = null;
let failed = false;
let visible = true;

// Aircraft icons. The GPU projects each from its true position every frame.
const icons = { data: null, buffer: null, count: 0 };
let iconMarks = [];
let iconTexture = null;
let iconTextureVersion = -1;
// The screen center in mercator, which the perspective compensation measures against.
let centerWorld = null;

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

// Icons lie in the ground plane, so the corner offset is applied in mercator and
// foreshortens with the tilt.
//
// `perspective` is MapLibre's own compensation for that foreshortening. For a quad
// pitched with the map, the ratio is the anchor's camera distance over the center's.
const ICON_VERTEX = `
attribute vec3 a_pos;
attribute vec2 a_offset;
attribute vec2 a_uv;
attribute float a_rotation;
attribute vec3 a_fill;

uniform mat4 u_matrix;
uniform float u_centerW;
uniform float u_worldSize;

varying vec2 v_uv;
varying vec3 v_fill;

void main() {
    vec4 anchor = u_matrix * vec4(a_pos, 1.0);
    if (anchor.w <= 0.0) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        return;
    }

    float perspective = clamp(0.5 + 0.5 * (anchor.w / u_centerW), 0.0, 4.0);

    float s = sin(a_rotation);
    float c = cos(a_rotation);
    vec2 rotated = vec2(a_offset.x * c - a_offset.y * s,
                        a_offset.x * s + a_offset.y * c);

    // CSS pixels to mercator. The map draws one mercator unit across u_worldSize
    // pixels, uniformly at every latitude.
    vec2 ground = rotated * (perspective / u_worldSize);

    vec4 clip = u_matrix * vec4(a_pos.x + ground.x, a_pos.y + ground.y, a_pos.z, 1.0);
    // Flat in depth, like the rest of this layer. Overlap order comes from the
    // back-to-front sort in writeIcons.
    clip.z = 0.0;
    gl_Position = clip;

    v_uv = a_uv;
    v_fill = a_fill;
}
`;

// The atlas holds a mask per shape: white body, black stroke, transparent outside.
// Multiplying by the fill colors the body and leaves the stroke black.
const ICON_FRAGMENT = `
precision mediump float;
uniform sampler2D u_atlas;
varying vec2 v_uv;
varying vec3 v_fill;
void main() {
    vec4 mask = texture2D(u_atlas, v_uv);
    float alpha = mask.a;
    vec3 color = v_fill * mask.r;
    gl_FragColor = vec4(color * alpha, alpha);
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

function link(vertexSource, attributes, uniforms, fragmentSource = FRAGMENT) {
    const program = gl.createProgram();
    const vertex = compile(gl.VERTEX_SHADER, vertexSource);
    const fragment = compile(gl.FRAGMENT_SHADER, fragmentSource);
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
const ICON_FLOATS_PER_VERTEX = 11;  // pos(3) offset(2) uv(2) rotation(1) fill(3)

// Two triangles. Corners in [-1, 1], y negative up-screen, matching the mercator axes.
const ICON_CORNERS = [[-1, -1], [1, -1], [-1, 1], [-1, 1], [1, -1], [1, 1]];

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

// The aircraft to draw, as { lon, lat, lift, shapeName, sizeW, sizeH, rotationDeg, fill }.
// Held as marks; writeIcons turns them into vertices each frame.
export function setIcons(marks) {
    iconMarks = [];
    for (const mark of marks) {
        const rect = Atlas.rectFor(mark.shapeName) || Atlas.rectFor('unknown');
        if (!rect) continue;
        iconMarks.push({
            x: mercatorXFromLongitude(mark.lon),
            y: mercatorYFromLatitude(mark.lat),
            z: mark.lift > 0 ? mercatorZFromAltitude(mark.lift, mark.lat) : 0,
            halfW: mark.sizeW / 2,
            halfH: mark.sizeH / 2,
            rotation: mark.rotationDeg * Math.PI / 180,
            r: mark.fill[0] / 255,
            g: mark.fill[1] / 255,
            b: mark.fill[2] / 255,
            rect,
            depth: 0
        });
    }
    // Sized here, filled per frame by writeIcons.
    icons.data = new Float32Array(iconMarks.length * 6 * ICON_FLOATS_PER_VERTEX);
    icons.count = iconMarks.length * 6;
    if (map) map.triggerRepaint();
}

// Writes the marks into the vertex buffer, farthest first, so a nearer aircraft draws
// over a farther one. Runs every frame, because the order depends on the camera: one
// distance per mark, a sort, and about 26 KB written.
function writeIcons(matrix) {
    for (const mark of iconMarks) {
        mark.depth = matrix[3] * mark.x + matrix[7] * mark.y + matrix[11] * mark.z + matrix[15];
    }
    iconMarks.sort((a, b) => b.depth - a.depth);

    const data = icons.data;
    let at = 0;
    for (const mark of iconMarks) {
        const { rect } = mark;
        for (const [cx, cy] of ICON_CORNERS) {
            data[at++] = mark.x; data[at++] = mark.y; data[at++] = mark.z;
            data[at++] = cx * mark.halfW; data[at++] = cy * mark.halfH;
            // v runs down the atlas the way y runs down the screen, so the corner that
            // is up on screen takes the top of the bitmap.
            data[at++] = cx < 0 ? rect.u0 : rect.u1;
            data[at++] = cy < 0 ? rect.v0 : rect.v1;
            data[at++] = mark.rotation;
            data[at++] = mark.r; data[at++] = mark.g; data[at++] = mark.b;
        }
    }
}

// The screen center in mercator, which the perspective compensation measures against.
export function setCenterWorld(world) {
    centerWorld = world;
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

// The camera as the probe last saw it, for the placement arithmetic in MapManager.
// Null matrix until the first frame has been drawn.
export function cameraFrame() {
    return camera;
}

export function clear() {
    for (const key of Object.keys(batches)) setBatch(batches[key], null, LINE_FLOATS_PER_VERTEX);
    icons.data = null;
    icons.count = 0;
    iconMarks = [];
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
            iconProgram = link(ICON_VERTEX,
                ['a_pos', 'a_offset', 'a_uv', 'a_rotation', 'a_fill'],
                ['u_matrix', 'u_centerW', 'u_worldSize', 'u_atlas'],
                ICON_FRAGMENT);
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
        icons.buffer = null;
        iconTexture = null;
        iconTextureVersion = -1;

        // Decoding 82 silhouettes is asynchronous; the repaint draws them once ready.
        Atlas.build().then(() => {
            if (map) map.triggerRepaint();
        });
    },

    onRemove(_removedMap, context) {
        for (const batch of Object.values(batches)) {
            if (batch.buffer) context.deleteBuffer(batch.buffer);
            batch.buffer = null;
        }
        if (icons.buffer) context.deleteBuffer(icons.buffer);
        icons.buffer = null;
        if (iconTexture) context.deleteTexture(iconTexture);
        iconTexture = null;
        iconTextureVersion = -1;
        if (lineProgram) context.deleteProgram(lineProgram.program);
        if (fillProgram) context.deleteProgram(fillProgram.program);
        if (iconProgram) context.deleteProgram(iconProgram.program);
        lineProgram = null;
        fillProgram = null;
        iconProgram = null;
        map = null;
        gl = null;
    },

    render(context, args) {
        if (failed || !lineProgram || !fillProgram) return;

        gl = context;
        const matrix = args.defaultProjectionData.mainMatrix;
        if (!matrix) return;

        const canvas = map.getCanvas();
        const viewport = [gl.drawingBufferWidth, gl.drawingBufferHeight];
        // Line widths are given in CSS pixels; the drawing buffer is not.
        const pixelRatio = canvas.clientWidth ? gl.drawingBufferWidth / canvas.clientWidth : 1;

        gl.enable(gl.BLEND);
        gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);

        // `visible` gates the stalks and the trail, which apply only to a tilted map.
        // Icons draw at every pitch.
        if (visible) {
            matrix32.set(matrix);
            drawMarks(viewport, pixelRatio);
        }

        // Last, so an aircraft sits on top of its own stalk.
        drawIcons(args);

        gl.bindBuffer(gl.ARRAY_BUFFER, null);
    }
};

function drawMarks(viewport, pixelRatio) {
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
}

// Uploads the atlas when its version changes. 2048 square RGBA, 16 MB on the GPU.
function syncTexture() {
    const source = Atlas.atlasCanvas();
    if (!source) return false;
    if (iconTexture && iconTextureVersion === Atlas.atlasVersion()) return true;

    if (!iconTexture) iconTexture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, iconTexture);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
    // Linear and no mipmaps: icons draw at close to their stored size.
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    iconTextureVersion = Atlas.atlasVersion();
    return true;
}

// Draws the icon batch with the live matrix.
function drawIcons(args) {
    if (!iconProgram || !icons.count || !centerWorld) return;
    if (!syncTexture()) return;

    const liveMatrix = args.defaultProjectionData.mainMatrix;
    if (!liveMatrix) return;
    matrix32.set(liveMatrix);

    // The screen center's camera distance, from the same matrix the icons use.
    const centerW = liveMatrix[3] * centerWorld[0]
                  + liveMatrix[7] * centerWorld[1]
                  + liveMatrix[11] * 0
                  + liveMatrix[15];
    if (!(centerW > 0)) return;

    writeIcons(liveMatrix);
    if (!icons.buffer) icons.buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, icons.buffer);
    gl.bufferData(gl.ARRAY_BUFFER, icons.data, gl.DYNAMIC_DRAW);

    gl.useProgram(iconProgram.program);
    gl.bindBuffer(gl.ARRAY_BUFFER, icons.buffer);

    const stride = ICON_FLOATS_PER_VERTEX * 4;
    bind(iconProgram.attributes.a_pos, 3, stride, 0);
    bind(iconProgram.attributes.a_offset, 2, stride, 3 * 4);
    bind(iconProgram.attributes.a_uv, 2, stride, 5 * 4);
    bind(iconProgram.attributes.a_rotation, 1, stride, 7 * 4);
    bind(iconProgram.attributes.a_fill, 3, stride, 8 * 4);

    gl.uniformMatrix4fv(iconProgram.uniforms.u_matrix, false, matrix32);
    gl.uniform1f(iconProgram.uniforms.u_centerW, centerW);
    // Pixels the map draws one mercator unit across. MapLibre's transform holds 512
    // whatever tile size a source declares, so the constant is shared with the altitude
    // arithmetic.
    gl.uniform1f(iconProgram.uniforms.u_worldSize,
        TRANSFORM_TILE_SIZE * Math.pow(2, map.getZoom()));

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, iconTexture);
    gl.uniform1i(iconProgram.uniforms.u_atlas, 0);

    gl.drawArrays(gl.TRIANGLES, 0, icons.count);

    for (const location of Object.values(iconProgram.attributes)) {
        if (location >= 0) gl.disableVertexAttribArray(location);
    }
}

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
