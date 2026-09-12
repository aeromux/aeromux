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

// Which slice of a long list to render for a given scroll position. Pure, so the
// arithmetic can be tested without a browser.
//
// The point is to make the rendered element count depend on the height of the
// panel rather than on how many aircraft are in range: a busy sky then costs no
// more to display than a quiet one.

// Rows rendered beyond each edge of the viewport, so a scroll does not expose
// blank space before the next render catches up. A fixed count rather than a
// proportion — twelve extra rows is negligible on any panel, and proportional
// overscan would be more moving parts for no visible gain.
export const OVERSCAN_ROWS = 6;

// Rendered before a row height is known. The height is measured from a rendered
// row, so returning an empty window here would deadlock: nothing rendered means
// nothing to measure, and the list would stay empty for good.
export const UNMEASURED_BATCH = 30;

export function visibleWindow({
    total,
    rowHeight,
    viewportHeight,
    scrollTop,
    overscan = OVERSCAN_ROWS
}) {
    if (!(total > 0)) {
        return { start: 0, end: 0, padTop: 0, padBottom: 0 };
    }

    if (!(rowHeight > 0)) {
        return { start: 0, end: Math.min(total, UNMEASURED_BATCH), padTop: 0, padBottom: 0 };
    }

    const first = Math.max(0, Math.floor(Math.max(0, scrollTop) / rowHeight) - overscan);
    const visible = Math.ceil(Math.max(0, viewportHeight) / rowHeight) + overscan * 2;
    const start = Math.min(first, Math.max(0, total - 1));
    const end = Math.min(total, start + Math.max(1, visible));

    return {
        start,
        end,
        // The spacers stand in for the rows that are not rendered, so the scrollbar
        // is the length it would be if they all were.
        padTop: start * rowHeight,
        padBottom: Math.max(0, (total - end) * rowHeight)
    };
}
