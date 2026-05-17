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

import { h, Fragment } from 'preact';
import { useState, useEffect } from 'preact/hooks';
import { fetchAircraftPhoto } from '../Services/ApiClient.js';

/**
 * Renders the photo content (image + attribution, or skeleton, or empty
 * placeholder). Always rendered inside a `<DetailSection>` — the parent owns
 * the section header, chevron, and expand/collapse mechanics.
 *
 * Visibility is gated at the parent (AircraftDetail.jsx): when the user
 * disables the "Aircraft photos" toggle in the settings panel, the parent
 * skips rendering the wrapping DetailSection entirely, so this component
 * doesn't even mount and the user cannot accidentally expand a hidden section.
 *
 * On `icao` change, cancels any in-flight request via AbortController and
 * starts a new fetch. Component-local state means switching aircraft resets
 * cleanly without app-wide store coupling.
 *
 * The `<img onError>` handler covers CDN failures that happen *after* the
 * metadata fetch succeeded — without it, a broken-image icon would render.
 */
export function AircraftPhoto({ icao }) {
    const [state, setState] = useState({ kind: 'loading', data: null });

    useEffect(() => {
        if (!icao) return;

        setState({ kind: 'loading', data: null });

        const controller = new AbortController();
        fetchAircraftPhoto(icao, controller.signal)
            .then(meta => {
                if (controller.signal.aborted) return;
                if (meta && meta.HasPhoto) {
                    setState({ kind: 'ok', data: meta });
                } else {
                    setState({ kind: 'none', data: null });
                }
            })
            .catch(err => {
                if (controller.signal.aborted) return;
                if (err && err.name === 'AbortError') return;
                setState({ kind: 'error', data: null });
            });

        return () => controller.abort();
    }, [icao]);

    if (state.kind === 'loading') {
        return <div class="aircraft-photo aircraft-photo-skeleton" />;
    }

    if (state.kind === 'ok') {
        const m = state.data;
        return (
            <Fragment>
                <img
                    class="aircraft-photo"
                    src={m.ThumbnailUrl}
                    alt={`Aircraft photo by ${m.Photographer}`}
                    onError={() => setState({ kind: 'error', data: null })}
                />
                <div class="aircraft-photo-attribution">
                    Photo: <a href={m.Link} target="_blank" rel="noopener">{m.Photographer}</a>
                    {' · Planespotters'}
                </div>
            </Fragment>
        );
    }

    // 'none' or 'error' — same low-contrast placeholder.
    return (
        <div class="aircraft-photo aircraft-photo-empty">
            No photo available
        </div>
    );
}
