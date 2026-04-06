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
import { useState, useRef, useEffect, useCallback } from 'preact/hooks';
import { searchAircraft } from '../Services/ApiClient.js';

// Splits text around a case-insensitive query match and wraps the matched portion in a highlight span
function highlightMatch(text, query) {
    if (!text || !query) return text;
    const idx = text.toLowerCase().indexOf(query.toLowerCase());
    if (idx === -1) return text;
    return (
        <>
            {text.substring(0, idx)}
            <span class="search-highlight">{text.substring(idx, idx + query.length)}</span>
            {text.substring(idx + query.length)}
        </>
    );
}

export function ControlPanel({ units, onUnitsChange, settings, onSettingsChange, onSelect, onReset }) {
    const [query, setQuery] = useState('');
    const [results, setResults] = useState(null);
    const [loading, setLoading] = useState(false);
    const [searchOpen, setSearchOpen] = useState(false);
    const [settingsOpen, setSettingsOpen] = useState(false);
    const [confirmingReset, setConfirmingReset] = useState(false);

    // Reset confirmation state when settings dropdown closes
    useEffect(() => {
        if (!settingsOpen) setConfirmingReset(false);
    }, [settingsOpen]);
    const debounceRef = useRef(null);
    const wrapperRef = useRef(null);

    // Close dropdowns on outside click or Escape
    useEffect(() => {
        const handleClick = (e) => {
            if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
                setSearchOpen(false);
                setSettingsOpen(false);
            }
        };
        const handleKey = (e) => {
            if (e.key === 'Escape') {
                setSearchOpen(false);
                setSettingsOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClick);
        document.addEventListener('keydown', handleKey);
        return () => {
            document.removeEventListener('mousedown', handleClick);
            document.removeEventListener('keydown', handleKey);
        };
    }, []);

    const handleInput = useCallback((e) => {
        const val = e.target.value;
        setQuery(val);
        setSettingsOpen(false);

        if (debounceRef.current) clearTimeout(debounceRef.current);

        if (!val.trim()) {
            setResults(null);
            setSearchOpen(false);
            return;
        }

        setLoading(true);
        debounceRef.current = setTimeout(async () => {
            try {
                const data = await searchAircraft(val.trim());
                setResults(data.Aircraft || []);
                setSearchOpen(true);
            } catch (e) {
                setResults([]);
                setSearchOpen(true);
            }
            setLoading(false);
        }, 300);
    }, []);

    const handleSelectResult = useCallback((icao, coordinate) => {
        setQuery('');
        setResults(null);
        setSearchOpen(false);
        onSelect(icao, coordinate);
    }, [onSelect]);

    const toggleSettings = useCallback(() => {
        setSettingsOpen(prev => !prev);
        setSearchOpen(false);
    }, []);

    const setUnit = useCallback((key, value) => {
        onUnitsChange({ ...units, [key]: value });
    }, [units, onUnitsChange]);

    const toggleSetting = useCallback((key) => {
        onSettingsChange({ ...settings, [key]: !settings[key] });
    }, [settings, onSettingsChange]);

    return (
        <div class="control-panel panel" ref={wrapperRef}>
            <div class="control-panel-row">
                <div class="search-input-wrapper">
                    <span class="search-icon">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                            <path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001q.044.06.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1 1 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0"/>
                        </svg>
                    </span>
                    <input
                        type="text"
                        class="search-input"
                        placeholder="Search..."
                        value={query}
                        onInput={handleInput}
                    />
                    {searchOpen && results && (
                        <div class="search-dropdown">
                            {loading && <div class="search-no-results">Searching...</div>}
                            {!loading && results.length === 0 && (
                                <div class="search-no-results">No aircraft found</div>
                            )}
                            {!loading && results.map((a) => (
                                <div
                                    key={a.ICAO}
                                    class="search-result"
                                    onClick={() => handleSelectResult(a.ICAO, a.Coordinate)}
                                >
                                    <div class="search-result-callsign">{highlightMatch(a.Callsign || a.ICAO, query)}</div>
                                    <div class="search-result-meta">
                                        {highlightMatch(a.ICAO, query)}
                                        {a.Registration && <>{' \u00B7 '}{highlightMatch(a.Registration, query)}</>}
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
                <button class={`settings-btn${settingsOpen ? ' active' : ''}`} onClick={toggleSettings} title="Settings">
                    <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                        <path d="M8 4.754a3.246 3.246 0 1 0 0 6.492 3.246 3.246 0 0 0 0-6.492M5.754 8a2.246 2.246 0 1 1 4.492 0 2.246 2.246 0 0 1-4.492 0"/>
                        <path d="M9.796 1.343c-.527-1.79-3.065-1.79-3.592 0l-.094.319a.873.873 0 0 1-1.255.52l-.292-.16c-1.64-.892-3.433.902-2.54 2.541l.159.292a.873.873 0 0 1-.52 1.255l-.319.094c-1.79.527-1.79 3.065 0 3.592l.319.094a.873.873 0 0 1 .52 1.255l-.16.292c-.892 1.64.901 3.434 2.541 2.54l.292-.159a.873.873 0 0 1 1.255.52l.094.319c.527 1.79 3.065 1.79 3.592 0l.094-.319a.873.873 0 0 1 1.255-.52l.292.16c1.64.893 3.434-.902 2.54-2.541l-.159-.292a.873.873 0 0 1 .52-1.255l.319-.094c1.79-.527 1.79-3.065 0-3.592l-.319-.094a.873.873 0 0 1-.52-1.255l.16-.292c.893-1.64-.902-3.433-2.541-2.54l-.292.159a.873.873 0 0 1-1.255-.52zm-2.633.283c.246-.835 1.428-.835 1.674 0l.094.319a1.873 1.873 0 0 0 2.693 1.115l.291-.16c.764-.415 1.6.42 1.184 1.185l-.159.292a1.873 1.873 0 0 0 1.116 2.692l.318.094c.835.246.835 1.428 0 1.674l-.319.094a1.873 1.873 0 0 0-1.115 2.693l.16.291c.415.764-.42 1.6-1.185 1.184l-.291-.159a1.873 1.873 0 0 0-2.693 1.116l-.094.318c-.246.835-1.428.835-1.674 0l-.094-.319a1.873 1.873 0 0 0-2.692-1.115l-.292.16c-.764.415-1.6-.42-1.184-1.185l.159-.291A1.873 1.873 0 0 0 1.945 8.93l-.319-.094c-.835-.246-.835-1.428 0-1.674l.319-.094A1.873 1.873 0 0 0 3.06 4.377l-.16-.292c-.415-.764.42-1.6 1.185-1.184l.292.159a1.873 1.873 0 0 0 2.692-1.115z"/>
                    </svg>
                </button>
            </div>
            {settingsOpen && (
                <div class="settings-dropdown">
                    <div class="settings-category">Units</div>
                    <div class="unit-group">
                        <button class={`unit-btn${units.speed === 'kts' ? ' active' : ''}`} onClick={() => setUnit('speed', 'kts')}>kts</button>
                        <button class={`unit-btn${units.speed === 'kmh' ? ' active' : ''}`} onClick={() => setUnit('speed', 'kmh')}>km/h</button>
                        <button class={`unit-btn${units.speed === 'mph' ? ' active' : ''}`} onClick={() => setUnit('speed', 'mph')}>mph</button>
                    </div>
                    <div class="unit-group">
                        <button class={`unit-btn${units.altitude === 'ft' ? ' active' : ''}`} onClick={() => setUnit('altitude', 'ft')}>ft</button>
                        <button class={`unit-btn${units.altitude === 'm' ? ' active' : ''}`} onClick={() => setUnit('altitude', 'm')}>m</button>
                    </div>
                    <div class="unit-group">
                        <button class={`unit-btn${units.distance === 'nm' ? ' active' : ''}`} onClick={() => setUnit('distance', 'nm')}>nm</button>
                        <button class={`unit-btn${units.distance === 'km' ? ' active' : ''}`} onClick={() => setUnit('distance', 'km')}>km</button>
                        <button class={`unit-btn${units.distance === 'mi' ? ' active' : ''}`} onClick={() => setUnit('distance', 'mi')}>mi</button>
                    </div>
                    <div class="settings-category">Interface</div>
                    <div class="settings-toggle" onClick={() => toggleSetting('rangeRings')}>
                        <div class={`toggle-track${settings.rangeRings ? ' active' : ''}`}>
                            <div class="toggle-knob" />
                        </div>
                        Range rings
                    </div>
                    <div class="settings-separator" />
                    {confirmingReset ? (
                        <div class="reset-confirm">
                            <span class="reset-confirm-text">Are you sure?</span>
                            <div class="reset-confirm-buttons">
                                <button class="reset-yes" onClick={() => { onReset(); setConfirmingReset(false); }}>Yes</button>
                                <button class="reset-no" onClick={() => setConfirmingReset(false)}>No</button>
                            </div>
                        </div>
                    ) : (
                        <button class="reset-btn" onClick={() => setConfirmingReset(true)}>Reset to defaults</button>
                    )}
                </div>
            )}
        </div>
    );
}
