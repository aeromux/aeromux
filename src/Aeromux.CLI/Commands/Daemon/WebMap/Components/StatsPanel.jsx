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
import { useState, useRef, useEffect } from 'preact/hooks';
import { fetchStats } from '../Services/ApiClient.js';
import { loadStatsView, saveStatsView } from '../Services/UnitConversion.js';
import {
    sourceList, nextSource, resolveSource, buildSources, buildDevice, buildMessageTypes, buildFooter,
} from '../Services/ReceiverStatistics.js';

const KINDS = ['DF', 'TC', 'BDS'];

// Calls `callback` at once and then every `intervalMs`, but only while the page is
// visible: a tab left in the background makes no requests, and catches up with one
// call when it is shown again.
export function usePolling(callback, intervalMs) {
    const callbackRef = useRef(callback);
    callbackRef.current = callback;

    useEffect(() => {
        let timer = null;
        const run = () => callbackRef.current();
        const start = () => {
            if (timer === null) timer = setInterval(run, intervalMs);
        };
        const stop = () => {
            if (timer !== null) {
                clearInterval(timer);
                timer = null;
            }
        };
        const onVisibilityChange = () => {
            if (document.hidden) {
                stop();
            } else {
                run();
                start();
            }
        };

        if (!document.hidden) {
            run();
            start();
        }
        document.addEventListener('visibilitychange', onVisibilityChange);
        return () => {
            stop();
            document.removeEventListener('visibilitychange', onVisibilityChange);
        };
    }, [intervalMs]);
}

// Icon for the health status; the word beside it carries the meaning, the icon only
// helps the eye
function HealthIcon({ status }) {
    const common = { width: 14, height: 14, viewBox: '0 0 16 16', role: 'img', 'aria-label': status };
    if (status === 'OK') {
        return (
            <svg {...common}>
                <circle cx="8" cy="8" r="7" fill="#0ca30c" />
                <path d="M4.8 8.2l2.1 2.1 4.3-4.6" fill="none" stroke="white" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
        );
    }
    if (status === 'Degraded') {
        return (
            <svg {...common}>
                <path d="M8 1.2l7 12.6H1z" fill="#fab219" stroke="#fab219" stroke-width="1.2" stroke-linejoin="round" />
                <path d="M8 5.6v4" stroke="#1a1a1a" stroke-width="1.6" stroke-linecap="round" />
                <circle cx="8" cy="11.8" r="0.9" fill="#1a1a1a" />
            </svg>
        );
    }
    if (status === 'Unhealthy') {
        return (
            <svg {...common}>
                <circle cx="8" cy="8" r="7" fill="#d03b3b" />
                <path d="M5.5 5.5l5 5M10.5 5.5l-5 5" stroke="white" stroke-width="1.8" stroke-linecap="round" />
            </svg>
        );
    }
    return (
        <svg {...common}>
            <circle cx="8" cy="8" r="7" fill="#9ca3af" />
            <path d="M5 8h6" stroke="white" stroke-width="1.8" stroke-linecap="round" />
        </svg>
    );
}

// The state on one row, what it means below it, each line under the word rather than
// the icon. Nothing before the first answer.
function StatusSection({ health }) {
    if (!health.status) return null;
    return (
        <section class="stats-section">
            <div class="stats-section-head">
                <span class="stats-section-label">Status</span>
            </div>
            <div class="stats-health">
                <HealthIcon status={health.status} />
                <span class="stats-health-label">{health.label}</span>
            </div>
            {health.lines.map((line) => <div class="stats-health-line" key={line}>{line}</div>)}
        </section>
    );
}

// One list drawn as a sum: a bar whose segments are the parts, the parts as table rows,
// and a total row. The bar is decorative; the table carries the same figures.
//
// With `selection`, a click on a row chooses it and a click on the total row chooses
// 'all'. The name is a button for the keyboard; it has no handler of its own, its click
// reaches the row's, so a click on the name and one beside it are the same single choice.
function StatsList({ list, section, caption, totalLabel = 'Total', withCode = false, hover, onHover, selection = null }) {
    const active = hover && hover.section === section ? hover : null;
    const rowClass = (row) => {
        const classes = [];
        if (active && (active.rowKey === row.key || (!active.rowKey && active.segmentKey === row.segmentKey))) {
            classes.push('hovered');
        }
        if (selection && selection.selectedKey === row.key) classes.push('selected');
        return classes.join(' ');
    };

    return (
        <>
            <div class="stats-bar" aria-hidden="true">
                {list.segments.map((segment) => (
                    <div
                        key={segment.key}
                        class={`stats-segment${active && active.segmentKey != null && active.segmentKey !== segment.key ? ' dimmed' : ''}`}
                        style={{ flexGrow: segment.weight, background: segment.color }}
                        title={segment.title}
                        onMouseEnter={() => onHover({ section, rowKey: null, segmentKey: segment.key })}
                    />
                ))}
            </div>
            <table class={`stats-table${selection ? ' stats-selectable' : ''}`}>
                <caption class="visually-hidden">{caption}</caption>
                <colgroup>
                    <col class="stats-col-swatch" />
                    {withCode && <col class="stats-col-code" />}
                    <col />
                    <col class="stats-col-value" />
                    <col class="stats-col-share" />
                </colgroup>
                <thead class="stats-table-head">
                    <tr>
                        <th scope="col"><span class="visually-hidden">Color</span></th>
                        {withCode && <th scope="col"><span class="visually-hidden">Code</span></th>}
                        <th scope="col"><span class="visually-hidden">Name</span></th>
                        <th scope="col"><span class="visually-hidden">Value</span></th>
                        <th scope="col"><span class="visually-hidden">Share</span></th>
                    </tr>
                </thead>
                <tbody>
                    {list.rows.map((row) => (
                        <tr
                            key={row.key}
                            class={rowClass(row)}
                            title={row.title}
                            onMouseEnter={() => onHover({ section, rowKey: row.key, segmentKey: row.segmentKey })}
                            onClick={selection ? () => selection.onSelect(row.key) : undefined}
                        >
                            <td><span class="stats-swatch" style={{ background: row.color }} /></td>
                            {withCode && <td class="stats-code">{row.code}</td>}
                            <td class="stats-name">
                                {selection
                                    ? (
                                        <button type="button" class="stats-row-button"
                                                aria-pressed={selection.selectedKey === row.key}
                                                aria-label={`${row.label}, show its message types`}>{row.label}</button>
                                    )
                                    : <span class="stats-name-text">{row.label}</span>}
                                {row.sub && <span class="stats-sub">{row.sub}</span>}
                            </td>
                            <td class="stats-value">{row.value}</td>
                            <td class="stats-share">{row.share}</td>
                        </tr>
                    ))}
                </tbody>
                <tfoot>
                    {/* As a choice, the total row is all sources: hovering it dims no segment */}
                    <tr
                        class={selection && active && active.rowKey === 'all' ? 'hovered' : ''}
                        title={selection ? list.totalTitle : undefined}
                        onMouseEnter={selection ? () => onHover({ section, rowKey: 'all', segmentKey: null }) : undefined}
                        onClick={selection ? () => selection.onSelect('all') : undefined}
                    >
                        <td />
                        {withCode && <td />}
                        <th scope="row" class="stats-name">
                            {selection
                                ? (
                                    <>
                                        <button type="button" class="stats-row-button"
                                                aria-pressed={selection.selectedKey === 'all'}
                                                aria-label={`${totalLabel}, show all sources`}>{totalLabel}</button>
                                        {list.totalSub && <span class="stats-sub">{list.totalSub}</span>}
                                    </>
                                )
                                : totalLabel}
                        </th>
                        <td class="stats-value">{list.totalValue}</td>
                        <td class="stats-share">{list.totalShare}</td>
                    </tr>
                </tfoot>
            </table>
        </>
    );
}

// The statistics dropdown. Mounted only while open: it fetches /stats at once and
// every 5 seconds (the rates already cover 60 seconds), and costs nothing when closed.
export function StatsPanel({ health }) {
    const [stats, setStats] = useState(null);
    const [failed, setFailed] = useState(false);
    const [view, setView] = useState(loadStatsView);
    const [source, setSource] = useState('all');
    // Every device starts collapsed: one of two open looked arbitrary, both open took
    // much of the dropdown
    const [expanded, setExpanded] = useState(() => new Set());
    const [hover, setHover] = useState(null);
    const mounted = useRef(true);
    const typesRef = useRef(null);

    useEffect(() => () => { mounted.current = false; }, []);

    usePolling(() => {
        fetchStats()
            .then((data) => {
                if (!mounted.current) return;
                setStats(data);
                setFailed(false);
            })
            .catch(() => {
                if (mounted.current) setFailed(true);
            });
    }, 5000);

    const changeView = (change) => {
        const next = { ...view, ...change };
        setView(next);
        saveStatsView(next);
    };

    const toggleDevice = (key) => {
        const next = new Set(expanded);
        if (next.has(key)) next.delete(key);
        else next.add(key);
        setExpanded(next);
    };

    if (!stats) {
        return (
            <div class="stats-panel">
                <StatusSection health={health} />
                <div class="stats-loading">{failed ? 'No answer from the receiver' : 'Loading…'}</div>
            </div>
        );
    }

    const scope = view.scope;
    const selected = resolveSource(stats, source);
    const chosen = sourceList(stats).find((item) => item.key === selected);

    // A source row chooses what Message types shows. The section may be below the fold,
    // so it is scrolled into view, without moving when it is already visible.
    const chooseSource = (key) => {
        setSource(nextSource(selected, key));
        setHover(null);
        const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
        typesRef.current?.scrollIntoView({ block: 'nearest', behavior: reduceMotion ? 'auto' : 'smooth' });
    };
    const sources = buildSources(stats, scope);
    const devices = (stats.Sdr?.Devices ?? []).map((device) => buildDevice(device, scope));
    const types = buildMessageTypes(stats, selected, view.kind, scope);
    const footer = buildFooter(stats);
    const listProps = { hover, onHover: setHover };

    return (
        <div class="stats-panel" onMouseLeave={() => setHover(null)}>
            <StatusSection health={health} />

            {/* The period decides what every number below means */}
            <section class="stats-section">
                <div class="stats-section-head">
                    <span class="stats-section-label">Period</span>
                </div>
                <div class="unit-group stats-scope" role="group" aria-label="Period">
                    <button type="button" class={`unit-btn${scope === 'now' ? ' active' : ''}`} aria-pressed={scope === 'now'}
                            onClick={() => changeView({ scope: 'now' })}>Now</button>
                    <button type="button" class={`unit-btn${scope === 'total' ? ' active' : ''}`} aria-pressed={scope === 'total'}
                            onClick={() => changeView({ scope: 'total' })}>Since start</button>
                </div>
                <div class="stats-caption">
                    {scope === 'now' ? 'Per second, over the last 60 seconds' : 'Totals since the daemon started'}
                </div>
                {failed && <div class="stats-stale-note">No answer from the receiver; these are the last figures.</div>}
            </section>

            <div class={failed ? 'stats-stale' : ''}>
                <section class="stats-section">
                    <div class="stats-section-head">
                        <span class="stats-section-label">Messages by source</span>
                        <span class="stats-section-value">{sources.totalValue}</span>
                    </div>
                    <StatsList list={sources} section="sources" caption="Messages by source" {...listProps}
                               selection={{ selectedKey: selected, onSelect: chooseSource }} />
                </section>

                {devices.length > 0 && (
                    <section class="stats-section">
                        <div class="stats-section-head">
                            <span class="stats-section-label">SDR devices</span>
                        </div>
                        {devices.map((device) => {
                            const open = expanded.has(device.key);
                            return (
                                <div class="stats-device" key={device.key}>
                                    <button type="button" class="stats-device-toggle" aria-expanded={open}
                                            onClick={() => toggleDevice(device.key)}>
                                        <svg class={`stats-chevron${open ? ' open' : ''}`} width="10" height="10" viewBox="0 0 16 16" aria-hidden="true">
                                            <path d="M6 3l5 5-5 5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
                                        </svg>
                                        <span class="stats-device-name">{device.name}</span>
                                        <span class="stats-device-label">Extracted</span>
                                        <span class="stats-device-value">{device.totalValue}</span>
                                    </button>
                                    {open && (
                                        <div class="stats-device-body">
                                            <StatsList list={device} section={device.key} caption={`${device.name}: extracted frames`}
                                                       totalLabel="Extracted" {...listProps} />
                                            <div class="stats-note">
                                                <span>Corrected</span>
                                                <span><span class="stats-note-value">{device.corrected}</span> · {device.correctedShare} of extracted</span>
                                            </div>
                                            <div class="stats-note">
                                                <span>Samples</span>
                                                <span class="stats-note-value">{device.samples}</span>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            );
                        })}
                    </section>
                )}

                <section class="stats-section">
                    <div ref={typesRef}>
                        {/* Two rows, as Status has: the title with the way back, then the
                            source on a row of its own, so a long name has the full width */}
                        <div class="stats-section-head">
                            <span class="stats-section-label">Message types</span>
                            {chosen && (
                                <button type="button" class="stats-show-all" onClick={() => chooseSource('all')}>Show all</button>
                            )}
                        </div>
                        <div class="stats-types-source">
                            <span class="stats-types-from">From:</span>
                            {chosen && <span class="stats-swatch" style={{ background: chosen.color }} aria-hidden="true" />}
                            <span class="stats-types-source-name">{chosen ? chosen.name : 'All sources'}</span>
                        </div>
                        <div class="unit-group stats-kinds" role="group" aria-label="Message kind">
                            {KINDS.map((kind) => (
                                <button key={kind} type="button" class={`unit-btn${view.kind === kind ? ' active' : ''}`}
                                        aria-pressed={view.kind === kind}
                                        onClick={() => { changeView({ kind }); setHover(null); }}>{kind}</button>
                            ))}
                        </div>
                    </div>
                    <div class="stats-types-head">
                        <span>{types.heading}</span>
                        <span class="stats-section-value">{types.totalValue}</span>
                    </div>
                    {types.rows.length === 0
                        ? <div class="stats-empty">None since start</div>
                        : <StatsList list={types} section="types" caption={`Message types: ${types.heading}`} withCode {...listProps} />}
                </section>

                <section class="stats-section stats-footer">
                    {footer.map((row) => (
                        <div class="stats-footer-row" key={row.label}>
                            <span>{row.label}</span>
                            <span class="stats-footer-value">{row.value}</span>
                        </div>
                    ))}
                </section>
            </div>
        </div>
    );
}
