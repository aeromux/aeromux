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
import { useState, useEffect, useRef, useCallback, useMemo } from 'preact/hooks';
import { fetchStats, fetchAircraft, fetchDetail, fetchHistory, fetchStateHistory } from '../Services/ApiClient.js';
import * as MapManager from '../Map/MapManager.js';
import * as SkyViewManager from '../SkyView/SkyViewManager.js';
import * as SignalR from '../Services/SignalRClient.js';
import { loadUnits, saveUnits, loadSettings, saveSettings, loadSort, saveSort, resetAllSettings, loadSheetHeight, saveSheetHeight, clearSheetHeight, nmToKm, resolveDeviceDefaults } from '../Services/UnitConversion.js';
import { receiverBox, aircraftAltitudeM } from '../Services/SkyViewGeometry.js';
import { computeInsets } from '../Services/SafeInsets.js';
import { clampSheetPx, pxToFraction } from '../Services/SheetHeight.js';
import { HoverTooltip } from './HoverTooltip.jsx';
import { AircraftList } from './AircraftList.jsx';
import { AircraftDetail } from './AircraftDetail.jsx';
import { ControlPanel } from './ControlPanel.jsx';

// Fallback when the server does not report how many state snapshots it keeps.
const DEFAULT_STATE_HISTORY_CAPACITY = 1000;

export function App() {
    const [aircraftMap, setAircraftMap] = useState(new Map());
    const [selectedIcao, setSelectedIcao] = useState(null);
    const [detail, setDetail] = useState(null);
    const [expired, setExpired] = useState(false);
    const [totalCount, setTotalCount] = useState(0);
    const [receiverLocation, setReceiverLocation] = useState(null);
    const [hover, setHover] = useState(null);
    const [selectedTooltip, setSelectedTooltip] = useState(null);
    const [units, setUnits] = useState(loadUnits());
    const [trail, setTrail] = useState([]);
    const [version, setVersion] = useState(null);
    const [databaseVersion, setDatabaseVersion] = useState(null);
    const [settings, setSettings] = useState(loadSettings);
    const [sort, setSort] = useState(loadSort);
    const aircraftMapRef = useRef(new Map());
    const panelRef = useRef(null);
    const controlPanelRef = useRef(null);
    const sheetDrag = useRef(null);
    const selectedRef = useRef(null);
    const trailRef = useRef([]);
    const [stateHistory, setStateHistory] = useState(null);
    const stateHistoryRef = useRef(null);
    const [rangeOutline, setRangeOutline] = useState([]);
    const rangeOutlineRef = useRef([]);
    const [heatmapCollectionEnabled, setHeatmapCollectionEnabled] = useState(false);
    const [heatmapScale, setHeatmapScale] = useState(null);
    const [heatmapHover, setHeatmapHover] = useState(null);
    // Mirror the toggle into a ref so the mount-registered push handler sees the current
    // state and drops heatmap frames arriving right after the overlay is toggled off.
    const heatmapEnabledRef = useRef(settings.heatmap);
    heatmapEnabledRef.current = settings.heatmap;
    // Mirror the view mode and receiver location into refs so the
    // mount-registered SignalR handlers — onReconnected above all — read current
    // values rather than the ones captured when they were registered.
    const viewModeRef = useRef('map');
    const receiverRef = useRef(null);
    // The renderer for the active mode. Held in a ref as well as in state because
    // the mount-registered handlers and the buffered-update flush both run outside
    // the render cycle.
    const viewRef = useRef(MapManager);
    const [viewMode, setViewMode] = useState('map');
    const [receiverPending, setReceiverPending] = useState(true);
    const updateBuffer = useRef([]);
    const bufferTimer = useRef(null);
    const defaultSections = {
        identification: true, photo: true, database: true, profile: true, status: true,
        position: true, velocity: true, autopilot: false, meteorology: false,
        acas: false, capabilities: false, dataQuality: false,
    };
    const [sections, setSections] = useState({ ...defaultSections });
    const [showMore, setShowMore] = useState({});

    // Single source of truth for which region we are subscribed to. The server
    // keeps one viewport per client, so both views share it: Map mode follows the
    // MapLibre camera, Sky mode follows the receiver and its max-range setting.
    // Every caller goes through here, otherwise a reconnect would silently revert
    // the subscription to whatever the hidden map last showed.
    const activeBounds = useCallback(() => {
        if (viewModeRef.current === 'sky' && receiverRef.current) {
            const { lat, lon } = receiverRef.current;
            // Clamped because nothing validates persisted settings; 300 nm is the
            // range-outline tracker's own cap.
            const rangeNm = Math.min(300, Math.max(10, loadSettings().skyMaxRangeNm));
            return receiverBox(lat, lon, nmToKm(rangeNm));
        }
        return MapManager.getViewportBounds();
    }, []);

    const applyBounds = useCallback(() => {
        const bounds = activeBounds();
        if (bounds) {
            SignalR.updateViewport(bounds.south, bounds.west, bounds.north, bounds.east);
        }
        return bounds;
    }, [activeBounds]);

    // Some defaults depend on the device rather than being fixed. Resolved in one
    // place so both first load and Reset-to-defaults go through it: leaving the
    // sentinel in place means no option in the group matches, and the control
    // renders with nothing selected.
    const withDeviceDefaults = useCallback((stored) => resolveDeviceDefaults(
        stored,
        window.matchMedia('(max-width: 768px)').matches
    ), []);

    // Measured from the live layout rather than hard-coded, so a future panel
    // resize cannot silently desynchronise the scene from what covers it. The control
    // panel is measured too: the readouts sit between the two, and what is left
    // between them is what decides how much of a readout there is room for.
    const currentInsets = useCallback(() => computeInsets({
        mobile: window.matchMedia('(max-width: 768px)').matches,
        panelRect: panelRef.current ? panelRef.current.getBoundingClientRect() : null,
        controlRect: controlPanelRef.current ? controlPanelRef.current.getBoundingClientRect() : null,
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight
    }), []);

    // Both views carry a readout positioned clear of the floating panels, so the
    // insets go to whichever renderer is on screen rather than to a fixed one.
    const pushInsets = useCallback(() => {
        const view = viewModeRef.current === 'sky' ? SkyViewManager : MapManager;
        view.setSafeInsets(currentInsets());
    }, [currentInsets]);

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
        viewRef.current.updateMarkers(mapCopy);
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

        viewRef.current.highlightSelected(icao);
        viewRef.current.updateMarkers(aircraftMapRef.current);

        const selectedAircraft = aircraftMapRef.current.get(icao);
        const category = selectedAircraft?.Military ? 'military'
            : (selectedAircraft?.Ladd || selectedAircraft?.Pia) ? 'privacy'
            : 'normal';
        viewRef.current.setTrailColor(category);

        if (shouldPan) {
            const coord = coordinate || aircraftMapRef.current.get(icao)?.Coordinate;
            if (coord) {
                // Altitude too: the sky view has to look up as well as round, or an
                // aircraft passing overhead ends up above the frame.
                const altitude = aircraftAltitudeM(selectedAircraft || {});
                viewRef.current.focusOn(
                    coord.Latitude, coord.Longitude, altitude ? altitude.metres : 0
                );
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
                    // How many snapshots the server itself keeps. The live buffer is
                    // trimmed to the same figure, so the client never discards a point
                    // the server would still have given us.
                    capacity: stateData.State.Capacity ?? DEFAULT_STATE_HISTORY_CAPACITY,
                    entries: (stateData.State.Entries || []).map(e => ({
                        timestamp: new Date(e.Timestamp).getTime(),
                        // Kept for the sky view's path through space. The state
                        // history carries position and altitude in one record, so
                        // this needs no extra request. The flight-profile chart
                        // reads by field name and ignores it.
                        position: e.Position
                            ? { Latitude: e.Position.Latitude, Longitude: e.Position.Longitude }
                            : null,
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

    // Selecting from the list always pans, and the identity has to be stable or the
    // memoised list below would rebuild on every render anyway.
    const handleSelectFromList = useCallback(
        (icao) => handleSelect(icao, { panTo: true }),
        [handleSelect]
    );

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

        viewRef.current.clearSelection();
        viewRef.current.clearTrail();
        viewRef.current.updateMarkers(aircraftMapRef.current);

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

    // Layout state callbacks for detail panel sections
    const toggleSection = useCallback((key) => {
        setSections(prev => ({ ...prev, [key]: !prev[key] }));
    }, []);
    const toggleMore = useCallback((key) => {
        setShowMore(prev => ({ ...prev, [key]: !prev[key] }));
    }, []);
    // Mobile bottom-sheet height — applied imperatively as a CSS custom property
    // rather than via a React `style` prop. The panel re-renders on every
    // buffered aircraft update; managing the property outside React's render
    // cycle keeps live drag values from being clobbered mid-gesture. The desktop
    // layout never reads --sheet-height, so it has no visual effect above the breakpoint.
    const applySheetHeight = useCallback((cssValue) => {
        const panel = panelRef.current;
        if (!panel) return;
        if (cssValue) {
            panel.style.setProperty('--sheet-height', cssValue);
        } else {
            panel.style.removeProperty('--sheet-height');
        }
    }, []);

    const handleGrabberPointerDown = useCallback((e) => {
        // Resizing only exists in the mobile bottom-sheet layout; on wider
        // screens the header is a plain (non-draggable) element.
        if (!window.matchMedia('(max-width: 768px)').matches) return;
        const panel = panelRef.current;
        if (!panel) return;
        sheetDrag.current = {
            startY: e.clientY,
            startHeight: panel.getBoundingClientRect().height,
            lastPx: panel.getBoundingClientRect().height,
        };
        e.currentTarget.setPointerCapture(e.pointerId);
        e.preventDefault();
    }, []);

    const handleGrabberPointerMove = useCallback((e) => {
        const drag = sheetDrag.current;
        if (!drag) return;
        // Drag up (smaller clientY) grows the sheet.
        const desired = drag.startHeight + (drag.startY - e.clientY);
        const px = clampSheetPx(desired, window.innerHeight);
        drag.lastPx = px;
        applySheetHeight(`${px}px`);
    }, [applySheetHeight]);

    const handleGrabberPointerUp = useCallback((e) => {
        const drag = sheetDrag.current;
        if (!drag) return;
        sheetDrag.current = null;
        e.currentTarget.releasePointerCapture(e.pointerId);
        const fraction = pxToFraction(drag.lastPx, window.innerHeight);
        // Persist as a fraction and re-apply as dvh so rotation is handled by CSS.
        applySheetHeight(`${(fraction * 100).toFixed(2)}dvh`);
        saveSheetHeight(fraction);
        pushInsets();
    }, [applySheetHeight, pushInsets]);

    const resetLayout = useCallback(() => {
        setSections({ ...defaultSections });
        setShowMore({});
        applySheetHeight(null);
        clearSheetHeight();
    }, [applySheetHeight]);

    // Reset all settings to defaults
    const handleReset = useCallback(() => {
        resetAllSettings();
        setUnits(loadUnits());
        const defaults = withDeviceDefaults(loadSettings());
        saveSettings(defaults);
        setSettings(defaults);
        setSort(loadSort());
        applySheetHeight(null);
        // Defaults put the view back to the map, so the refs and the renderer have
        // to follow or the toggle and what is on screen would disagree.
        if (viewModeRef.current !== 'map') {
            viewModeRef.current = 'map';
            viewRef.current = MapManager;
            setViewMode('map');
            MapManager.resize();
            MapManager.updateMarkers(aircraftMapRef.current);
            applyBounds();
        }
    }, [applySheetHeight, applyBounds, withDeviceDefaults]);

    // Only the synchronous part of a switch lives here. Everything that depends on
    // the container actually being visible has to wait for the DOM, so it runs in
    // the effect below instead.
    const handleViewModeChange = useCallback((mode) => {
        if (mode === viewModeRef.current) return;
        if (mode === 'sky' && !receiverRef.current) return;

        viewModeRef.current = mode;
        viewRef.current = mode === 'sky' ? SkyViewManager : MapManager;
        setViewMode(mode);
        saveSettings({ ...loadSettings(), viewMode: mode });
        setSettings(s => ({ ...s, viewMode: mode }));

        // Swap the server-side subscription to whatever the new view needs.
        applyBounds();
    }, [applyBounds]);

    // Adopting a view has to happen after the DOM has been committed: until then
    // the incoming container is still display:none, so it measures zero and the
    // canvas would be sized 0x0 and draw nothing at all. Both renderers need this —
    // MapLibre also caches the zero size it measured while hidden.
    useEffect(() => {
        SkyViewManager.setActive(viewMode === 'sky');
        MapManager.setActive(viewMode === 'map');

        const view = viewMode === 'sky' ? SkyViewManager : MapManager;
        const icao = selectedRef.current;

        view.setSafeInsets(currentInsets());

        if (viewMode === 'sky') {
            const r = receiverRef.current;
            if (!r) return;
            SkyViewManager.setReceiver(r.lat, r.lon, r.altM);
            SkyViewManager.setRangeOutline(rangeOutlineRef.current);
        }
        // Before updateMarkers: the safe area is derived from the canvas size, and a
        // container that was display:none measures zero until it is resized.
        view.resize();
        view.updateMarkers(aircraftMapRef.current);

        // Selection and trail are application state, but each renderer keeps its own
        // copy and only the visible one is kept current. The incoming view is
        // therefore synchronised in full — including the cleared case, which is what
        // deselecting in one view and returning to the other depends on.
        if (icao) {
            const aircraft = aircraftMapRef.current.get(icao);
            const category = aircraft?.Military ? 'military'
                : (aircraft?.Ladd || aircraft?.Pia) ? 'privacy'
                : 'normal';
            view.setTrailColor(category);
            view.highlightSelected(icao);
            // Both views bring the selection into view on a switch. The sky view has
            // to turn and look up to find it; the map centres on it, so that arriving
            // from the sky with an aircraft selected does not drop the user wherever
            // the map camera happened to be left.
            if (aircraft?.Coordinate) {
                const altitude = aircraftAltitudeM(aircraft);
                view.focusOn(
                    aircraft.Coordinate.Latitude,
                    aircraft.Coordinate.Longitude,
                    altitude ? altitude.metres : 0
                );
            }
        } else {
            view.clearSelection();
        }

        // The two views take different trail data: the map draws the flat position
        // history, the sky needs altitude with each point and so reads the state
        // history instead.
        const trailPoints = viewMode === 'sky'
            ? (stateHistoryRef.current ? stateHistoryRef.current.entries : null)
            : (trailRef.current.length ? trailRef.current : null);
        if (icao && trailPoints && trailPoints.length) {
            view.updateTrail(trailPoints);
        } else {
            view.clearTrail();
        }
    }, [viewMode, currentInsets]);

    // Initialize on mount
    useEffect(() => {
        const mapInstance = MapManager.init('map-container');
        SkyViewManager.init('sky-container');
        // Settings must reach the renderer before it can draw anything; without
        // them its first frame returns early and the canvas stays blank.
        SkyViewManager.setSettings(loadSettings());

        // Registered on both renderers once, rather than re-registered on every
        // switch, so the switch itself stays stateless. Each callback is gated on
        // its own view being the active one: a hidden renderer can still emit — the
        // sky view republishes the pinned tooltip on every frame it draws — and two
        // renderers writing the same tooltip state makes it flicker between their
        // two positions.
        for (const [mode, view] of [['map', MapManager], ['sky', SkyViewManager]]) {
            const whenActive = (fn) => (...args) => {
                if (viewModeRef.current === mode) fn(...args);
            };
            view.onMarkerClick(whenActive((icao) => handleSelect(icao)));
            view.onMapClick(whenActive(() => handleBack()));
            view.onMarkerHover(
                whenActive((data) => setHover(data)),
                whenActive(() => setHover(null))
            );
            view.onSelectedTooltip(whenActive((data) => setSelectedTooltip(data)));
        }
        MapManager.onHeatmapHover((data) => setHeatmapHover(data));

        // Viewport changes → send to SignalR. Guarded by mode: map.resize() on the
        // way back from Sky mode fires a move event, which would otherwise overwrite
        // the sky subscription.
        MapManager.onViewportChange(() => {
            if (viewModeRef.current === 'map') {
                applyBounds();
            }
        });

        // Fetch stats for receiver location, then initial aircraft
        (async () => {
            try {
                const stats = await fetchStats();
                if (stats.Version) setVersion(stats.Version);
                setHeatmapCollectionEnabled(stats.HeatmapCollectionEnabled === true);
                if (stats.Receiver && stats.Receiver.Latitude != null && stats.Receiver.Longitude != null) {
                    // altM feeds the Sky View horizon; the config key is optional, so
                    // an unset altitude means a horizon exactly at eye level.
                    const loc = {
                        lat: stats.Receiver.Latitude,
                        lon: stats.Receiver.Longitude,
                        altM: stats.Receiver.AltitudeMeters ?? 0
                    };
                    receiverRef.current = loc;
                    setReceiverLocation(loc);
                    SkyViewManager.setReceiver(loc.lat, loc.lon, loc.altM);
                    MapManager.setReceiver(loc.lat, loc.lon);
                    MapManager.setCenter(loc.lat, loc.lon, 8);
                    MapManager.updateRangeRings(loc.lat, loc.lon, loadSettings().rangeRings, loadUnits().distance);
                }
                setReceiverPending(false);

                // The persisted mode is applied only now: a stored 'sky' must not
                // strand the user on an empty view, nor be discarded from a receiver
                // that simply had not loaded yet.
                const stored = loadSettings();
                const resolved = withDeviceDefaults(stored);
                if (resolved !== stored) {
                    saveSettings(resolved);
                    setSettings(resolved);
                }
                if (stored.viewMode === 'sky') {
                    if (receiverRef.current) {
                        handleViewModeChange('sky');
                    } else {
                        saveSettings({ ...loadSettings(), viewMode: 'map' });
                        setSettings(s => ({ ...s, viewMode: 'map' }));
                    }
                }

                // Wait for map to settle, then fetch initial aircraft
                setTimeout(async () => {
                    const bounds = activeBounds();
                    if (bounds) {
                        try {
                            const data = await fetchAircraft(bounds);
                            const newMap = new Map();
                            data.Aircraft.forEach(a => newMap.set(a.ICAO, a));
                            aircraftMapRef.current = newMap;
                            setAircraftMap(newMap);
                            setTotalCount(data.Count);
                            viewRef.current.updateMarkers(newMap);

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
                    connectSignalR();
                }, 500);
            } catch (e) {
                // Stats fetch failed — try aircraft without center
                setReceiverPending(false);
                setTimeout(() => {
                    connectSignalR();
                }, 500);
            }
        })();

        function connectSignalR() {
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
                                // Barometric first, geometric as the fallback — the same
                                // order the state history itself records, so live points
                                // extend that series on one datum. Preferring geometric
                                // here instead puts a step of several hundred feet at the
                                // join, which shows up as a kink in the sky trail. The
                                // fallback still covers aircraft that report only GNSS
                                // height, which would otherwise contribute no point at all.
                                const altSource = data.Position?.BarometricAltitude
                                    ?? data.Position?.GeometricAltitude;
                                const altFeet = altSource?.Feet ?? null;
                                const altMeters = altSource?.Meters ?? null;
                                const spdKnots = data.VelocityAndDynamics?.Speed?.Knots ?? null;
                                const spdKmh = data.VelocityAndDynamics?.Speed?.KilometersPerHour ?? null;
                                const spdMph = data.VelocityAndDynamics?.Speed?.MilesPerHour ?? null;

                                if (altFeet != null || spdKnots != null) {
                                    const lastEntry = prev.entries[prev.entries.length - 1];
                                    if (!lastEntry || ts > lastEntry.timestamp) {
                                        let entries = [...prev.entries, {
                                            timestamp: ts,
                                            // Without this the sky trail freezes at
                                            // whatever the initial fetch returned and
                                            // then breaks on every later point.
                                            position: data.Position?.Coordinate
                                                ? {
                                                    Latitude: data.Position.Coordinate.Latitude,
                                                    Longitude: data.Position.Coordinate.Longitude
                                                }
                                                : null,
                                            altitudeFeet: altFeet,
                                            altitudeMeters: altMeters,
                                            speedKnots: spdKnots,
                                            speedKmh: spdKmh,
                                            speedMph: spdMph,
                                        }];
                                        // Trimmed to exactly what the server retains, one
                                        // point per append, so the oldest end rolls off
                                        // imperceptibly. The previous rule kept a hundred
                                        // points of slack and then dropped them in one go,
                                        // which visibly lopped the start off the sky view's
                                        // trail every hundred seconds. Copying a
                                        // thousand-element array once a second costs
                                        // nothing next to that.
                                        const cap = prev.capacity || DEFAULT_STATE_HISTORY_CAPACITY;
                                        if (entries.length > cap) {
                                            entries = entries.slice(-cap);
                                        }
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
                        setDatabaseVersion(meta.DatabaseEnabled ? (meta.DatabaseVersion ?? null) : null);
                    },
                    onRangeOutlineUpdated: (data) => {
                        rangeOutlineRef.current = data;
                        setRangeOutline(data);
                    },
                    onHeatmapUpdated: (data) => {
                        // Drop frames that arrive after toggle-off — an in-flight push would
                        // otherwise repopulate the cleared overlay.
                        // Sky mode renders no heatmap, and a frame in flight across
                        // the switch would otherwise repopulate a cleared overlay.
                        if (!heatmapEnabledRef.current || viewModeRef.current !== 'map') return;
                        MapManager.setHeatmap(data);
                        setHeatmapScale({ scaleMax: data.ScaleMax, maxCount: data.MaxCount });
                    },
                    onReconnected: async () => {
                        // Re-fetch aircraft to reconcile stale state. Bounds come from
                        // activeBounds() so a reconnect while in Sky mode re-asserts the
                        // receiver box rather than the hidden map's stale viewport.
                        const bounds = activeBounds();
                        if (bounds) {
                            try {
                                const data = await fetchAircraft(bounds);
                                const newMap = new Map();
                                data.Aircraft.forEach(a => newMap.set(a.ICAO, a));
                                aircraftMapRef.current = newMap;
                                setAircraftMap(newMap);
                                viewRef.current.updateMarkers(newMap);
                                applyBounds();
                            } catch (e) {
                                // Ignore
                            }
                        }
                        // Re-assert heatmap params so server state matches the UI.
                        const s = loadSettings();
                        if (s.heatmap) {
                            SignalR.updateHeatmap(true, s.heatmapCellNm, s.heatmapWindowHours * 60);
                        }
                    }
                }
            }).then(() => {
                // Re-derived here rather than captured at call time, so the bounds sent
                // are whatever the active view wants once the connection is actually up.
                applyBounds();
                // Assert heatmap params once the connection is up. The settings-sync
                // effect runs at mount before the connection is Connected, so its
                // enable call is lost; re-send it here (and on reconnect).
                const s = loadSettings();
                if (s.heatmap) {
                    SignalR.updateHeatmap(true, s.heatmapCellNm, s.heatmapWindowHours * 60);
                }
            });
        }
    }, []);

    // Apply the persisted bottom-sheet height on mount.
    useEffect(() => {
        const fraction = loadSheetHeight();
        if (fraction) applySheetHeight(`${(fraction * 100).toFixed(2)}dvh`);
    }, [applySheetHeight]);

    // Update range rings when settings or distance unit changes. Map-only: the sky
    // scene is gridded by elevation angle, since at realistic antenna heights
    // almost no ground is visible to draw rings on.
    useEffect(() => {
        if (receiverLocation) {
            MapManager.updateRangeRings(receiverLocation.lat, receiverLocation.lon, settings.rangeRings, units.distance);
        }
    }, [settings.rangeRings, units.distance, receiverLocation]);

    // The coverage ribbon carries the only distance scale in the sky view, and its
    // scale steps in the selected unit, so the unit has to reach that renderer just
    // as it reaches the map's range rings. The map readout measures in the same unit.
    useEffect(() => {
        SkyViewManager.setDistanceUnit(units.distance);
        MapManager.setDistanceUnit(units.distance);
    }, [units.distance]);

    // What the map readout counts is what the aircraft list footer counts: aircraft
    // on screen over aircraft tracked.
    useEffect(() => {
        MapManager.setCounts(aircraftMap.size, totalCount);
    }, [aircraftMap, totalCount]);

    // The range outline feeds both views: the map overlay and the sky view's
    // coverage ribbon read the same pushed array.
    useEffect(() => {
        MapManager.updateRangeOutline(rangeOutline, settings.rangeOutline);
        SkyViewManager.setRangeOutline(rangeOutline);
    }, [settings.rangeOutline, rangeOutline]);

    // Settings reach the renderer here. Without this every sky setting — field of
    // view, max range, flatten, ribbon, trail, labels — would appear to do nothing.
    // Runs in both modes so the canvas is already correct the instant sky mode is
    // entered rather than one render later.
    useEffect(() => {
        SkyViewManager.setSettings(settings);
    }, [settings]);

    // Maximum range changes the server-side subscription as well as the drawing.
    useEffect(() => {
        if (viewMode === 'sky') applyBounds();
    }, [settings.skyMaxRangeNm, viewMode, applyBounds]);

    // The sky trail needs position and altitude together, which only the state
    // history carries; the flat map trail has no altitude and would draw nothing.
    useEffect(() => {
        if (stateHistory?.entries) {
            SkyViewManager.updateTrail(stateHistory.entries);
        } else {
            SkyViewManager.clearTrail();
        }
    }, [stateHistory]);

    // Recomputed on selection change too, because selecting is itself what grows
    // the mobile sheet from list to detail, moving the sky view's horizon and the
    // corner both readouts sit in.
    useEffect(() => {
        pushInsets();
    }, [viewMode, selectedIcao, detail, pushInsets]);

    useEffect(() => {
        const onViewportResize = () => {
            pushInsets();
            SkyViewManager.resize();
        };
        window.addEventListener('resize', onViewportResize);
        window.addEventListener('orientationchange', onViewportResize);
        return () => {
            window.removeEventListener('resize', onViewportResize);
            window.removeEventListener('orientationchange', onViewportResize);
        };
    }, [pushInsets]);

    // Sky mode renders no heatmap, and because both views share one server-side
    // viewport slot, leaving it subscribed would make the server re-project the
    // whole grid for the sky box and again on the way back.
    useEffect(() => {
        if (viewMode === 'sky') {
            SignalR.updateHeatmap(false, settings.heatmapCellNm, settings.heatmapWindowHours * 60);
            MapManager.clearHeatmap();
            setHeatmapScale(null);
            setHeatmapHover(null);
        } else if (settings.heatmap) {
            SignalR.updateHeatmap(true, settings.heatmapCellNm, settings.heatmapWindowHours * 60);
        }
    }, [viewMode]);

    // Sync heatmap overlay with settings (drives the toggle AND Reset-to-defaults).
    useEffect(() => {
        if (viewModeRef.current !== 'map') return;
        if (settings.heatmap) {
            SignalR.updateHeatmap(true, settings.heatmapCellNm, settings.heatmapWindowHours * 60);
        } else {
            SignalR.updateHeatmap(false, settings.heatmapCellNm, settings.heatmapWindowHours * 60);
            MapManager.clearHeatmap();
            setHeatmapScale(null);
            setHeatmapHover(null);
        }
    }, [settings.heatmap, settings.heatmapCellNm, settings.heatmapWindowHours]);

    const viewCount = aircraftMap.size;

    // Held apart from the rest of the render so that state the list does not consume
    // cannot rebuild it. Hover is the one that matters: it changes as fast as the
    // pointer moves, and the list has hundreds of rows.
    const aircraftListElement = useMemo(() => (
        <AircraftList
            aircraftMap={aircraftMap}
            receiverLocation={receiverLocation}
            selectedIcao={selectedIcao}
            units={units}
            sort={sort}
            onSortChange={handleSortChange}
            onSelect={handleSelectFromList}
            onResetLayout={resetLayout}
            viewCount={viewCount}
            totalCount={totalCount}
        />
    ), [
        aircraftMap, receiverLocation, selectedIcao, units, sort,
        handleSortChange, handleSelectFromList, resetLayout, viewCount, totalCount
    ]);

    return (
        <div>
            <div id="map-container" class={`map-container${viewMode === 'sky' ? ' hidden-view' : ''}`}></div>
            {/* Both canvases stay mounted; only visibility changes, so neither
                renderer is ever torn down and rebuilt on a switch. */}
            <div id="sky-container" class={`sky-container${viewMode === 'sky' ? '' : ' hidden-view'}`}></div>

            {/* Pinned tooltip for the selected aircraft (always shown while
                selected); rendered first so a transient hover paints on top. */}
            <HoverTooltip hover={selectedTooltip} units={units} pinned />
            {/* Hover tooltip for any aircraft except the selected one, whose
                tooltip is already pinned above — avoids two identical
                overlapping tooltips. */}
            <HoverTooltip hover={hover && hover.icao !== selectedIcao ? hover : null} units={units} />

            {settings.heatmap && heatmapCollectionEnabled && heatmapHover && (
                <div class="heatmap-tooltip" style={{ left: heatmapHover.x + 'px', top: (heatmapHover.y - 12) + 'px' }}>
                    {heatmapHover.count} aircraft · last {settings.heatmapWindowHours} h
                </div>
            )}

            <div class="left-panel panel" ref={panelRef}>
                {/* The whole grabber + header strip is the drag target for
                    resizing the bottom sheet on mobile; the pill is just the
                    visual affordance. Pointer capture routes move/up here, and
                    the handler is a no-op above the mobile breakpoint. */}
                <div
                    class="sheet-drag-region"
                    onPointerDown={handleGrabberPointerDown}
                    onPointerMove={handleGrabberPointerMove}
                    onPointerUp={handleGrabberPointerUp}
                >
                    <div class="sheet-grabber">
                        <div class="sheet-grabber-pill"></div>
                    </div>
                    <div class="logo-header">
                        <img src="img/logo.svg" alt="Aeromux" class="logo-img" />
                        <div class="logo-text">
                            <div class="logo-title">AEROMUX</div>
                            <div class="logo-subtitle">Web Map{version ? ` (${version})` : ''}{databaseVersion ? ` with Database (${databaseVersion})` : ''}</div>
                        </div>
                    </div>
                </div>
                {selectedIcao ? (
                    <AircraftDetail
                        detail={detail}
                        expired={expired}
                        units={units}
                        receiverLocation={receiverLocation}
                        stateHistory={stateHistory}
                        sections={sections}
                        showMore={showMore}
                        settings={settings}
                        onToggleSection={toggleSection}
                        onToggleMore={toggleMore}
                        onResetLayout={resetLayout}
                        onBack={handleBack}
                    />
                ) : aircraftListElement}
            </div>

            <ControlPanel
                rootRef={controlPanelRef}
                units={units}
                onUnitsChange={handleUnitsChange}
                settings={settings}
                onSettingsChange={handleSettingsChange}
                onSelect={(icao, coordinate) => handleSelect(icao, { panTo: true, coordinate })}
                onReset={handleReset}
                receiverLocation={receiverLocation}
                heatmapCollectionEnabled={heatmapCollectionEnabled}
                heatmapScale={heatmapScale}
                viewMode={viewMode}
                receiverPending={receiverPending}
                onViewModeChange={handleViewModeChange}
            />
        </div>
    );
}
