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
import { useMemo, useCallback, useRef, useState, useLayoutEffect } from 'preact/hooks';
import { visibleWindow } from '../Services/ListWindow.js';
import { formatAltitude, formatSpeed, haversineDistance, convertDistance } from '../Services/UnitConversion.js';

function getCategoryClass(aircraft) {
    if (aircraft.Military) return 'dot-military';
    if (aircraft.Ladd || aircraft.Pia) return 'dot-privacy';
    return 'dot-normal';
}

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

export function AircraftList({ aircraftMap, receiverLocation, selectedIcao, units, sort, onSortChange, onSelect, onResetLayout, viewCount, totalCount }) {
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

    // Only the rows in view are rendered, so the element count follows the height of
    // the panel rather than how many aircraft are in range.
    const scrollRef = useRef(null);
    const rowRef = useRef(null);
    const [rowHeight, setRowHeight] = useState(0);
    const [scrollTop, setScrollTop] = useState(0);
    const [viewportHeight, setViewportHeight] = useState(0);

    // Measured from a rendered row rather than hardcoded: the height is uniform in
    // practice but is not declared in CSS, so a constant here would rot the moment
    // the padding changed. Re-measured whenever units change, since a different unit
    // can change a value's width but not its line count.
    useLayoutEffect(() => {
        const container = scrollRef.current;
        if (container && container.clientHeight !== viewportHeight) {
            setViewportHeight(container.clientHeight);
        }
        const measured = rowRef.current ? rowRef.current.offsetHeight : 0;
        if (measured > 0 && measured !== rowHeight) {
            setRowHeight(measured);
        }
    });

    useLayoutEffect(() => {
        const container = scrollRef.current;
        if (!container || typeof ResizeObserver === 'undefined') return undefined;
        // Follows the mobile bottom sheet, whose height the user drags.
        const observer = new ResizeObserver(() => setViewportHeight(container.clientHeight));
        observer.observe(container);
        return () => observer.disconnect();
    }, []);

    const handleScroll = useCallback((e) => {
        setScrollTop(e.currentTarget.scrollTop);
    }, []);

    // Fixed table layout takes its widths from the header cells, so each needs the
    // class its width is declared on.
    const columnClass = {
        callsign: 'aircraft-list-col-callsign',
        altitude: 'aircraft-list-col-altitude',
        speed: 'aircraft-list-col-speed',
        distance: 'aircraft-list-col-distance'
    };

    const renderHeader = (column, label) => (
        <th
            class={`${column === 'callsign' ? 'aircraft-list-callsign' : 'aircraft-list-value'} ${columnClass[column]}`}
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

    const window_ = visibleWindow({
        total: sortedAircraft.length,
        rowHeight,
        viewportHeight,
        scrollTop
    });
    const rows = sortedAircraft.slice(window_.start, window_.end);

    return (
        <div class="aircraft-list" ref={scrollRef} onScroll={handleScroll}>
            <div class="aircraft-list-stats">
                <span>Aircraft: <span class="stats-count">{viewCount}</span> in view / <span class="stats-count">{totalCount}</span> total</span>
                <button class="reset-layout" onClick={onResetLayout}>Reset layout</button>
            </div>
            <table class="aircraft-list-table">
                <thead>
                    <tr>
                        <th class="aircraft-list-dot-col"></th>
                        {renderHeader('callsign', 'Callsign')}
                        {renderHeader('altitude', 'Altitude')}
                        {renderHeader('speed', 'Speed')}
                        {receiverLocation && renderHeader('distance', 'Distance')}
                    </tr>
                </thead>
                <tbody>
                    {window_.padTop > 0 && (
                        <tr class="aircraft-list-spacer" style={{ height: `${window_.padTop}px` }} />
                    )}
                    {rows.map(({ icao, aircraft, distance }, index) => {
                        const alt = formatAltitude(aircraft.BarometricAltitude, units.altitude);
                        const spd = formatSpeed(aircraft.Speed || aircraft.SpeedOnGround, units.speed);
                        const dist = distance != null
                            ? convertDistance(distance, units.distance)
                            : null;

                        return (
                            <tr
                                key={icao}
                                // The first rendered row is the one measured for height.
                                ref={index === 0 ? rowRef : undefined}
                                class={icao === selectedIcao ? 'selected' : ''}
                                onClick={() => onSelect(icao)}
                            >
                                <td class="aircraft-list-dot-col">
                                    <span class={`aircraft-list-dot ${getCategoryClass(aircraft)}`} />
                                </td>
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
                    {window_.padBottom > 0 && (
                        <tr class="aircraft-list-spacer" style={{ height: `${window_.padBottom}px` }} />
                    )}
                </tbody>
            </table>
        </div>
    );
}
