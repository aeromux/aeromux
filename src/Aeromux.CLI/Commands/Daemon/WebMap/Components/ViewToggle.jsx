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

// Map / Sky switch. Reuses the unit-group segmented control so it matches the
// units buttons exactly and inherits the control panel's responsive cascade
// without any new positioning rules.
//
// The receiver location arrives asynchronously, so while it is still pending the
// Sky segment renders inert but not greyed out — flashing disabled and then
// enabling a moment later looks like a fault.
export function ViewToggle({ mode, hasReceiver, receiverPending, onChange }) {
    const unavailable = !receiverPending && !hasReceiver;

    return (
        <div class="unit-group view-toggle-row" role="group" aria-label="View mode">
            <button class={`unit-btn${mode === 'map' ? ' active' : ''}`}
                    onClick={() => onChange('map')}>Map</button>
            <button class={`unit-btn${mode === 'sky' ? ' active' : ''}`}
                    disabled={unavailable || receiverPending}
                    title={unavailable
                        ? 'Sky View needs a receiver location — set receiver.latitude and receiver.longitude in the configuration.'
                        : undefined}
                    onClick={() => onChange('sky')}>Sky</button>
        </div>
    );
}
