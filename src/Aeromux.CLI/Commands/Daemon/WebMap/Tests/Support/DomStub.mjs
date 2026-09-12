// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Minimal DOM and canvas stand-ins so the Sky View renderer can be exercised
// under `node --test`. The 2D context records every call instead of painting, so
// tests can assert on draw order and on what was drawn, not merely that drawing
// happened. Rasterized output is deliberately out of scope.
//
// Lives under Tests/Support/ rather than Tests/ so the `Tests/*.mjs` glob does
// not load it as a test file with no tests in it.

export const calls = [];

// Virtual clock, shared with the performance.now() installed below.
let virtualNow = 0;

export function advanceClock(ms) {
    virtualNow += ms;
}

function recordingContext() {
    const record = (name) => (...args) => { calls.push({ name, args }); };

    return {
        globalAlpha: 1,
        fillStyle: '',
        strokeStyle: '',
        lineWidth: 1,
        font: '',
        setTransform: record('setTransform'),
        clearRect: record('clearRect'),
        beginPath: record('beginPath'),
        moveTo: record('moveTo'),
        lineTo: record('lineTo'),
        arc: record('arc'),
        // The moon's terminator is an ellipse drawn in a rotated, translated frame.
        ellipse: record('ellipse'),
        save: record('save'),
        restore: record('restore'),
        translate: record('translate'),
        rotate: record('rotate'),
        stroke: record('stroke'),
        fill: record('fill'),
        rect: record('rect'),
        fillRect: record('fillRect'),
        strokeRect: record('strokeRect'),
        fillText: record('fillText'),
        closePath: record('closePath'),
        createLinearGradient: () => ({ addColorStop() {} }),
        // Proportional enough for collision tests; the real metric depends on the
        // font, which is not available here.
        measureText: (text) => ({ width: String(text).length * 6 })
    };
}

// A canvas whose listeners can be fired directly, so pointer and wheel handling
// is testable without a real event loop.
export function makeCanvas(width, height) {
    const listeners = {};

    return {
        width,
        height,
        style: {},
        getContext: () => recordingContext(),
        getBoundingClientRect: () => ({
            left: 0, top: 0, width, height, right: width, bottom: height
        }),
        addEventListener: (type, fn) => { (listeners[type] ||= []).push(fn); },
        removeEventListener: () => {},
        setPointerCapture: () => {},
        releasePointerCapture: () => {},
        dispatch: (type, event) => {
            for (const fn of listeners[type] || []) {
                fn({ preventDefault() {}, ...event });
            }
        }
    };
}

// A minimal element, enough for the renderer to build its readout.
function makeElement(tag) {
    const el = {
        tagName: tag,
        className: '',
        style: {},
        textContent: '',
        children: [],
        appendChild(child) { el.children.push(child); return child; }
    };
    return el;
}

export function installGlobals(canvas, container) {
    globalThis.window = {
        devicePixelRatio: 2,
        innerWidth: 1400,
        innerHeight: 900,
        matchMedia: () => ({ matches: false }),
        addEventListener: () => {},
        removeEventListener: () => {}
    };

    globalThis.document = {
        createElement: (tag) => (tag === 'canvas' ? canvas : makeElement(tag)),
        getElementById: () => container
    };

    // Synchronous frames on a virtual 16 ms clock. Without advancing the clock an
    // animation that polls performance.now() would never finish and would recurse
    // until the stack blew. Also advanceable by hand, because gesture timing reads
    // performance.now() and a clock that only moves per frame cannot express
    // "too slow to be a double-tap".
    virtualNow = 0;
    globalThis.performance = { now: () => virtualNow };
    globalThis.requestAnimationFrame = (fn) => { virtualNow += 16; fn(virtualNow); return 1; };
}

export function resetCalls() {
    calls.length = 0;
}
