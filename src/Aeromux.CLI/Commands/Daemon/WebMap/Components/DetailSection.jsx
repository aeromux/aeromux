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
import { useState } from 'preact/hooks';

export function DetailSection({ title, icon, defaultExpanded, children }) {
    const [expanded, setExpanded] = useState(defaultExpanded !== false);

    return (
        <div class="section">
            <div class="section-header" onClick={() => setExpanded(!expanded)}>
                <span class="section-header-title">
                    {icon && <span>{icon}</span>}
                    <span>{title}</span>
                </span>
                <span class={`section-chevron${expanded ? '' : ' collapsed'}`}>&#9660;</span>
            </div>
            <div class={`section-content${expanded ? '' : ' collapsed'}`}
                 style={expanded ? { maxHeight: '2000px' } : {}}>
                {children}
            </div>
        </div>
    );
}
