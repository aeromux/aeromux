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
import { useMemo, useCallback } from 'preact/hooks';
import { formatAltitude, formatSpeed, haversineDistance, convertDistance } from '../Services/UnitConversion.js';

function getRawValue(item, column) {
    switch (column) {
        case 'callsign':
            return item.aircraft.Callsign || null;
        case 'altitude':
            return item.aircraft.BarometricAltitude ? item.aircraft.BarometricAltitude.Feet : null;
        case 'speed': {
            const vel = item.aircraft.Speed || item.aircraft.SpeedOnGround;
            return vel ? vel.Knots : null;
        }
        case 'distance':
            return item.distance;
        default:
            return null;
    }
}

export function AircraftList({ aircraftMap, receiverLocation, selectedIcao, units, sort, onSortChange, onSelect, viewCount, totalCount }) {
    const handleHeaderClick = useCallback((column) => {
        const next = sort.column === column
            ? { column, direction: sort.direction === 'asc' ? 'desc' : 'asc' }
            : { column, direction: 'asc' };
        onSortChange(next);
    }, [sort, onSortChange]);

    const sortedAircraft = useMemo(() => {
        const items = [];

        aircraftMap.forEach((aircraft, icao) => {
            if (!aircraft.Coordinate) return;

            let distance = null;
            if (receiverLocation) {
                distance = haversineDistance(
                    receiverLocation.lat, receiverLocation.lon,
                    aircraft.Coordinate.Latitude, aircraft.Coordinate.Longitude
                );
            }

            items.push({ icao, aircraft, distance });
        });

        const dir = sort.direction === 'asc' ? 1 : -1;

        items.sort((a, b) => {
            const valA = getRawValue(a, sort.column);
            const valB = getRawValue(b, sort.column);

            // Nulls always last regardless of direction
            if (valA == null && valB == null) return a.icao.localeCompare(b.icao);
            if (valA == null) return 1;
            if (valB == null) return -1;

            // Compare non-null values
            let cmp;
            if (typeof valA === 'string') {
                cmp = valA.localeCompare(valB);
            } else {
                cmp = valA - valB;
            }

            return cmp === 0 ? a.icao.localeCompare(b.icao) : cmp * dir;
        });

        return items;
    }, [aircraftMap, receiverLocation, sort]);

    const renderHeader = (column, label) => (
        <th
            class={column === 'callsign' ? 'aircraft-list-callsign' : 'aircraft-list-value'}
            onClick={() => handleHeaderClick(column)}
        >
            {label}
            {sort.column === column && (
                <span class="sort-indicator">{sort.direction === 'asc' ? '▲' : '▼'}</span>
            )}
        </th>
    );

    if (sortedAircraft.length === 0) {
        return <div class="aircraft-list-empty">No aircraft in view</div>;
    }

    return (
        <div class="aircraft-list">
            <div class="aircraft-list-stats">
                Aircraft: <span class="stats-count">{viewCount}</span> in view / <span class="stats-count">{totalCount}</span> total
            </div>
            <table class="aircraft-list-table">
                <thead>
                    <tr>
                        {renderHeader('callsign', 'Callsign')}
                        {renderHeader('altitude', 'Altitude')}
                        {renderHeader('speed', 'Speed')}
                        {receiverLocation && renderHeader('distance', 'Distance')}
                    </tr>
                </thead>
                <tbody>
                    {sortedAircraft.map(({ icao, aircraft, distance }) => {
                        const alt = formatAltitude(aircraft.BarometricAltitude, units.altitude);
                        const spd = formatSpeed(aircraft.Speed || aircraft.SpeedOnGround, units.speed);
                        const dist = distance != null
                            ? convertDistance(distance, units.distance)
                            : null;

                        return (
                            <tr
                                key={icao}
                                class={icao === selectedIcao ? 'selected' : ''}
                                onClick={() => onSelect(icao)}
                            >
                                <td class="aircraft-list-callsign">
                                    <div>{aircraft.Callsign || 'N/A'}</div>
                                    <div class="aircraft-list-icao">{icao}</div>
                                </td>
                                <td class="aircraft-list-value">{alt}</td>
                                <td class="aircraft-list-value">{spd}</td>
                                {receiverLocation && <td class="aircraft-list-value">{dist ? `${dist.value} ${dist.label}` : ''}</td>}
                            </tr>
                        );
                    })}
                </tbody>
            </table>
        </div>
    );
}
