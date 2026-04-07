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
import { useState, useEffect, useRef, useCallback } from 'preact/hooks';
import { fetchStats, fetchAircraft, fetchDetail, fetchHistory, fetchStateHistory } from '../Services/ApiClient.js';
import * as MapManager from '../Map/MapManager.js';
import * as SignalR from '../Services/SignalRClient.js';
import { loadUnits, saveUnits, loadSettings, saveSettings, loadSort, saveSort, resetAllSettings } from '../Services/UnitConversion.js';
import { HoverTooltip } from './HoverTooltip.jsx';
import { AircraftList } from './AircraftList.jsx';
import { AircraftDetail } from './AircraftDetail.jsx';
import { ControlPanel } from './ControlPanel.jsx';

export function App() {
    const [aircraftMap, setAircraftMap] = useState(new Map());
    const [selectedIcao, setSelectedIcao] = useState(null);
    const [detail, setDetail] = useState(null);
    const [expired, setExpired] = useState(false);
    const [totalCount, setTotalCount] = useState(0);
    const [receiverLocation, setReceiverLocation] = useState(null);
    const [hover, setHover] = useState(null);
    const [units, setUnits] = useState(loadUnits());
    const [trail, setTrail] = useState([]);
    const [version, setVersion] = useState(null);
    const [settings, setSettings] = useState(loadSettings);
    const [sort, setSort] = useState(loadSort);
    const aircraftMapRef = useRef(new Map());
    const selectedRef = useRef(null);
    const trailRef = useRef([]);
    const [stateHistory, setStateHistory] = useState(null);
    const stateHistoryRef = useRef(null);
    const updateBuffer = useRef([]);
    const bufferTimer = useRef(null);

    // Flush buffered aircraft updates to state and map.
    // SignalR pushes individual aircraft updates rapidly — batching them into 50ms
    // windows avoids triggering a React re-render for every single update.
    const flushUpdates = useCallback(() => {
        if (updateBuffer.current.length === 0) return;
        const updates = updateBuffer.current.splice(0);
        const mapCopy = new Map(aircraftMapRef.current);

        for (const update of updates) {
            if (update.type === 'update') {
                mapCopy.set(update.icao, update.data);
            } else if (update.type === 'remove') {
                mapCopy.delete(update.icao);
            }
        }

        aircraftMapRef.current = mapCopy;
        setAircraftMap(mapCopy);
        MapManager.updateMarkers(mapCopy);
    }, []);

    const bufferUpdate = useCallback((type, icao, data) => {
        updateBuffer.current.push({ type, icao, data });
        if (!bufferTimer.current) {
            bufferTimer.current = setTimeout(() => {
                bufferTimer.current = null;
                flushUpdates();
            }, 50);
        }
    }, [flushUpdates]);

    // Select aircraft handler
    const handleSelect = useCallback(async (icao, { panTo: shouldPan = false, coordinate } = {}) => {
        selectedRef.current = icao;
        setSelectedIcao(icao);
        setExpired(false);
        setDetail(null);
        setTrail([]);
        trailRef.current = [];
        setStateHistory(null);
        stateHistoryRef.current = null;

        MapManager.highlightSelected(icao);
        MapManager.updateMarkers(aircraftMapRef.current);

        if (shouldPan) {
            const coord = coordinate || aircraftMapRef.current.get(icao)?.Coordinate;
            if (coord) {
                MapManager.panTo(coord.Latitude, coord.Longitude, true);
            }
        }

        // Fetch detail, position history, and state history in parallel
        const [detailData, historyData, stateData] = await Promise.all([
            fetchDetail(icao).catch(e => { console.error('Failed to fetch detail:', e); return null; }),
            fetchHistory(icao).catch(e => { console.error('Failed to fetch history:', e); return null; }),
            fetchStateHistory(icao).catch(e => { console.error('Failed to fetch state history:', e); return null; }),
        ]);

        if (selectedRef.current === icao) {
            if (detailData) setDetail(detailData);
            if (historyData?.Position?.Entries) {
                const positions = historyData.Position.Entries.map(e => e.Position);
                trailRef.current = positions;
                setTrail(positions);
                MapManager.updateTrail(positions);
            }
            if (stateData?.State) {
                const sh = {
                    enabled: stateData.State.Enabled,
                    entries: (stateData.State.Entries || []).map(e => ({
                        timestamp: new Date(e.Timestamp).getTime(),
                        altitudeFeet: e.Altitude?.Feet ?? null,
                        altitudeMeters: e.Altitude?.Meters ?? null,
                        speedKnots: e.Speed?.Knots ?? null,
                        speedKmh: e.Speed?.KilometersPerHour ?? null,
                        speedMph: e.Speed?.MilesPerHour ?? null,
                    })),
                    lastSequenceId: stateData.State.Entries?.length
                        ? stateData.State.Entries[stateData.State.Entries.length - 1].SequenceId
                        : 0,
                };
                stateHistoryRef.current = sh;
                setStateHistory(sh);
            }
        }

        // Tell SignalR we want detail pushes
        SignalR.selectAircraft(icao);
    }, []);

    // Deselect handler
    const handleBack = useCallback(() => {
        selectedRef.current = null;
        setSelectedIcao(null);
        setDetail(null);
        setExpired(false);
        setTrail([]);
        trailRef.current = [];
        setStateHistory(null);
        stateHistoryRef.current = null;

        MapManager.clearSelection();
        MapManager.clearTrail();
        MapManager.updateMarkers(aircraftMapRef.current);

        SignalR.deselectAircraft();
    }, []);

    // Unit change handler
    const handleUnitsChange = useCallback((newUnits) => {
        setUnits(newUnits);
        saveUnits(newUnits);
    }, []);

    // Settings change handler
    const handleSettingsChange = useCallback((newSettings) => {
        setSettings(newSettings);
        saveSettings(newSettings);
    }, []);

    // Sort change handler
    const handleSortChange = useCallback((newSort) => {
        setSort(newSort);
        saveSort(newSort);
    }, []);

    // Reset all settings to defaults
    const handleReset = useCallback(() => {
        resetAllSettings();
        setUnits(loadUnits());
        setSettings(loadSettings());
        setSort(loadSort());
    }, []);

    // Initialize on mount
    useEffect(() => {
        const mapInstance = MapManager.init('map-container');

        // Set up map event handlers
        MapManager.onMarkerClick((icao) => handleSelect(icao));
        MapManager.onMapClick(() => handleBack());
        MapManager.onMarkerHover(
            (data) => setHover(data),
            () => setHover(null)
        );

        // Viewport changes → send to SignalR
        MapManager.onViewportChange((bounds) => {
            SignalR.updateViewport(bounds.south, bounds.west, bounds.north, bounds.east);
        });

        // Fetch stats for receiver location, then initial aircraft
        (async () => {
            try {
                const stats = await fetchStats();
                if (stats.Version) setVersion(stats.Version);
                if (stats.Receiver && stats.Receiver.Latitude != null && stats.Receiver.Longitude != null) {
                    const loc = { lat: stats.Receiver.Latitude, lon: stats.Receiver.Longitude };
                    setReceiverLocation(loc);
                    MapManager.setCenter(loc.lat, loc.lon, 8);
                    MapManager.updateRangeRings(loc.lat, loc.lon, loadSettings().rangeRings, loadUnits().distance);
                }

                // Wait for map to settle, then fetch initial aircraft
                setTimeout(async () => {
                    const bounds = MapManager.getViewportBounds();
                    if (bounds) {
                        try {
                            const data = await fetchAircraft(bounds);
                            const newMap = new Map();
                            data.Aircraft.forEach(a => newMap.set(a.ICAO, a));
                            aircraftMapRef.current = newMap;
                            setAircraftMap(newMap);
                            setTotalCount(data.Count);
                            MapManager.updateMarkers(newMap);

                            // If no receiver location, fit to aircraft
                            if (!stats.Receiver || stats.Receiver.Latitude == null) {
                                const positions = data.Aircraft
                                    .filter(a => a.Coordinate)
                                    .map(a => ({ lat: a.Coordinate.Latitude, lon: a.Coordinate.Longitude }));
                                if (positions.length > 0) {
                                    MapManager.fitToAircraft(positions);
                                }
                            }
                        } catch (e) {
                            // Ignore
                        }
                    }

                    // Connect SignalR
                    connectSignalR(bounds);
                }, 500);
            } catch (e) {
                // Stats fetch failed — try aircraft without center
                setTimeout(() => {
                    const bounds = MapManager.getViewportBounds();
                    connectSignalR(bounds);
                }, 500);
            }
        })();

        function connectSignalR(initialBounds) {
            SignalR.connect({
                handlers: {
                    onAircraftUpdated: (data) => {
                        bufferUpdate('update', data.ICAO, data);

                        // Append trail if this is the selected aircraft with a new position
                        if (selectedRef.current === data.ICAO && data.Coordinate) {
                            const lastTrail = trailRef.current[trailRef.current.length - 1];
                            if (!lastTrail ||
                                lastTrail.Latitude !== data.Coordinate.Latitude ||
                                lastTrail.Longitude !== data.Coordinate.Longitude) {
                                trailRef.current = [...trailRef.current, data.Coordinate];
                                setTrail(trailRef.current);
                                MapManager.updateTrail(trailRef.current);
                            }
                        }
                    },
                    onAircraftRemoved: (icao) => {
                        bufferUpdate('remove', icao, null);

                        // If selected aircraft expired
                        if (selectedRef.current === icao) {
                            setExpired(true);
                        }
                    },
                    onDetailUpdated: (data) => {
                        if (selectedRef.current) {
                            setDetail(data);

                            // Append real-time data point to flight profile chart
                            const prev = stateHistoryRef.current;
                            if (prev && prev.enabled !== false) {
                                const ts = data.Timestamp ? new Date(data.Timestamp).getTime() : Date.now();
                                const altFeet = data.Position?.BarometricAltitude?.Feet ?? null;
                                const altMeters = data.Position?.BarometricAltitude?.Meters ?? null;
                                const spdKnots = data.VelocityAndDynamics?.Speed?.Knots ?? null;
                                const spdKmh = data.VelocityAndDynamics?.Speed?.KilometersPerHour ?? null;
                                const spdMph = data.VelocityAndDynamics?.Speed?.MilesPerHour ?? null;

                                if (altFeet != null || spdKnots != null) {
                                    const lastEntry = prev.entries[prev.entries.length - 1];
                                    if (!lastEntry || ts > lastEntry.timestamp) {
                                        let entries = [...prev.entries, {
                                            timestamp: ts,
                                            altitudeFeet: altFeet,
                                            altitudeMeters: altMeters,
                                            speedKnots: spdKnots,
                                            speedKmh: spdKmh,
                                            speedMph: spdMph,
                                        }];
                                        // Hysteresis: trim to 500 when buffer exceeds 600 to avoid slicing every update
                                        if (entries.length > 600) entries = entries.slice(-500);
                                        const updated = { ...prev, entries };
                                        stateHistoryRef.current = updated;
                                        setStateHistory(updated);
                                    }
                                }
                            }
                        }
                    },
                    onMetadata: (meta) => {
                        setTotalCount(meta.TotalAircraftCount);
                    },
                    onReconnected: async () => {
                        // Re-fetch aircraft to reconcile stale state
                        const bounds = MapManager.getViewportBounds();
                        if (bounds) {
                            try {
                                const data = await fetchAircraft(bounds);
                                const newMap = new Map();
                                data.Aircraft.forEach(a => newMap.set(a.ICAO, a));
                                aircraftMapRef.current = newMap;
                                setAircraftMap(newMap);
                                MapManager.updateMarkers(newMap);
                                SignalR.updateViewport(bounds.south, bounds.west, bounds.north, bounds.east);
                            } catch (e) {
                                // Ignore
                            }
                        }
                    }
                }
            }).then(() => {
                if (initialBounds) {
                    SignalR.updateViewport(
                        initialBounds.south, initialBounds.west,
                        initialBounds.north, initialBounds.east
                    );
                }
            });
        }
    }, []);

    // Update range rings when settings or distance unit changes
    useEffect(() => {
        if (receiverLocation) {
            MapManager.updateRangeRings(receiverLocation.lat, receiverLocation.lon, settings.rangeRings, units.distance);
        }
    }, [settings.rangeRings, units.distance, receiverLocation]);

    const viewCount = aircraftMap.size;

    return (
        <div>
            <div id="map-container" class="map-container"></div>

            <HoverTooltip hover={hover} units={units} />

            <div class="left-panel panel">
                <div class="logo-header">
                    <img src="img/logo.svg" alt="Aeromux" class="logo-img" />
                    <div class="logo-text">
                        <div class="logo-title">AEROMUX</div>
                        <div class="logo-subtitle">Web Map{version ? ` (v${version})` : ''}</div>
                    </div>
                </div>
                {selectedIcao ? (
                    <AircraftDetail
                        detail={detail}
                        expired={expired}
                        units={units}
                        receiverLocation={receiverLocation}
                        stateHistory={stateHistory}
                        onBack={handleBack}
                    />
                ) : (
                    <AircraftList
                        aircraftMap={aircraftMap}
                        receiverLocation={receiverLocation}
                        selectedIcao={selectedIcao}
                        units={units}
                        sort={sort}
                        onSortChange={handleSortChange}
                        onSelect={(icao) => handleSelect(icao, { panTo: true })}
                        viewCount={viewCount}
                        totalCount={totalCount}
                    />
                )}
            </div>

            <ControlPanel
                units={units}
                onUnitsChange={handleUnitsChange}
                settings={settings}
                onSettingsChange={handleSettingsChange}
                onSelect={(icao, coordinate) => handleSelect(icao, { panTo: true, coordinate })}
                onReset={handleReset}
            />
        </div>
    );
}
