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

// signalR is available as a global from the script tag in index.html
const { HubConnectionBuilder, HttpTransportType } = window.signalR;

let connection = null;

export function connect({ handlers }) {
    connection = new HubConnectionBuilder()
        .withUrl('/maphub')
        .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
        .build();

    connection.on('AircraftUpdated', (data) => handlers.onAircraftUpdated?.(data));
    connection.on('AircraftRemoved', (icao) => handlers.onAircraftRemoved?.(icao));
    connection.on('AircraftDetailUpdated', (data) => handlers.onDetailUpdated?.(data));
    connection.on('Metadata', (meta) => handlers.onMetadata?.(meta));
    connection.on('RangeOutlineUpdated', (data) => handlers.onRangeOutlineUpdated?.(data));

    connection.onreconnected(() => handlers.onReconnected?.());

    return connection.start();
}

export function updateViewport(south, west, north, east) {
    return connection?.invoke('UpdateViewport', south, west, north, east);
}

export function selectAircraft(icao) {
    return connection?.invoke('SelectAircraft', icao);
}

export function deselectAircraft() {
    return connection?.invoke('DeselectAircraft');
}
