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

// Which edges of the full-viewport canvas are covered by floating panels.
//
// The canvas fills the viewport and every panel sits above it, so the visible
// scene is smaller than the canvas and the renderer has to be told by how much.
// Kept pure and separate from the component so the arithmetic — especially the
// mobile bottom-sheet case, which is awkward to exercise by hand — is testable.

// Height of the control panel strip at the top of the mobile layout.
export const MOBILE_TOP_BAND_PX = 56;

// Margin the readout keeps from each panel it sits between, matching the inset every
// panel keeps from the edge of the screen.
export const READOUT_MARGIN_PX = 16;

// In the desktop layout the list/detail panel floats at the left, so the scene is
// inset by its right edge. In the mobile layout the same panel becomes a bottom
// sheet, so the scene is inset from below by however much of the viewport it
// covers — which grows when a selection swaps the sheet from list to detail.
//
// `readoutMaxWidth` is how wide the readout row may be. It is reported separately
// from the insets rather than as `right`, because the scene's safe area consumes a
// right inset and would shrink the whole sky view by the width of the control panel.
export function computeInsets({
    mobile,
    panelRect,
    controlRect,
    viewportWidth,
    viewportHeight,
    topBandPx = MOBILE_TOP_BAND_PX
}) {
    if (!panelRect) {
        return {};
    }

    if (mobile) {
        // The panels stack above and below rather than flanking, so the row has the
        // full width between the screen edges.
        return {
            top: topBandPx,
            bottom: Math.max(0, viewportHeight - panelRect.top),
            readoutMaxWidth: readoutWidth(viewportWidth, 0)
        };
    }

    const left = Math.max(0, panelRect.right);
    // Without a measured control panel the row is bounded by the screen edge, which
    // is the pre-existing behavior and better than reporting a width of zero.
    const rightEdge = controlRect ? controlRect.left : viewportWidth;

    return { left, readoutMaxWidth: readoutWidth(rightEdge, left) };
}

function readoutWidth(rightEdge, left) {
    if (!Number.isFinite(rightEdge) || !Number.isFinite(left)) return 0;
    return Math.max(0, rightEdge - left - 2 * READOUT_MARGIN_PX);
}
