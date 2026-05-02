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

const API = '/api/v1';

export const fetchStats = () => fetch(`${API}/stats`).then(r => r.json());

export const fetchAircraft = (bounds) => {
    const params = bounds
        ? `?bounds=${bounds.south},${bounds.west},${bounds.north},${bounds.east}`
        : '';
    return fetch(`${API}/aircraft${params}`).then(r => r.json());
};

export const fetchDetail = (icao) => fetch(`${API}/aircraft/${icao}`).then(r => r.json());

export const fetchHistory = (icao) =>
    fetch(`${API}/aircraft/${icao}/history?type=Position`).then(r => r.json());

// Fetches altitude/speed state history for the flight profile chart.
export const fetchStateHistory = (icao) =>
    fetch(`${API}/aircraft/${icao}/history?type=State`).then(r => r.json());

export const searchAircraft = (query) =>
    fetch(`${API}/aircraft?search=${encodeURIComponent(query)}`).then(r => r.json());

// Fetches photo metadata (Planespotters.net) for the given ICAO. Caller passes
// an AbortSignal so selecting a different aircraft cancels an in-flight request.
// On any non-OK response (400, 502), throws an Error so the component renders
// the "No photo available" placeholder.
export const fetchAircraftPhoto = (icao, signal) =>
    fetch(`${API}/aircraft/${icao}/photo`, { signal }).then(r => {
        if (!r.ok) throw new Error(`photo:${r.status}`);
        return r.json();
    });
