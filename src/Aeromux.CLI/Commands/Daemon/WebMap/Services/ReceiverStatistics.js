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

// Pure helpers that turn GET /api/v1/stats and /api/v1/health into the rows of the
// statistics dropdown, isolated from Preact so they can be unit-tested with
// `node --test`. Reads the PascalCase API payloads.
//
// Every list is drawn as a sum: a bar, its parts as rows, and a total row that is the
// sum of the rows as displayed. Rates are rounded to 0.1/s before they are summed, so
// what the user reads as A + B + C = D is true on screen.

// Categorical colors, validated for color vision deficiency as adjacent pairs on the
// panel's surface. A source or a code keeps its slot whatever the numbers, so nothing
// repaints or jumps when the scope or the source changes.
export const SERIES_COLORS = [
    '#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948',
];

// Codes and sources without a slot of their own
export const UNSLOTTED_COLOR = '#b4b9c0';

// DF 18 frames without an ICAO address, which carry no type code
export const NO_TYPE_CODE_COLOR = '#d6d9de';

// What a device extracted but did not pass on, in gray beside the messages it kept
export const DROPPED_COLORS = { duplicates: '#9ca3af', unconfirmed: '#d1d5db' };

// Status colors, distinct from the series, and always shown with an icon and a word
export const HEALTH_COLORS = {
    OK: '#0ca30c', Degraded: '#fab219', Unhealthy: '#d03b3b', Unreachable: '#9ca3af',
};

// The codes that get a color, in the order of typical traffic, so the colored rows
// usually come out largest first. Any other code gets a gray row after them.
export const CODE_ORDER = {
    DF: [17, 20, 11, 21, 4, 5, 0, 16],
    TC: [19, 11, 29, 31, 4, 28, 12, 0],
    BDS: ['4,0', '6,0', '5,0', '2,0', '1,0', '1,7', 'Empty', 'Unknown'],
};

// Always US format, whatever the browser's language, so every receiver's figures read
// the same.
const WHOLE = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const ONE_DECIMAL = new Intl.NumberFormat('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const TWO_DECIMALS = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const round1 = (value) => Math.round(value * 10) / 10;

// ─── Formatting ───

// 'now' is a rate over the last 60 seconds, 'total' a count since the daemon started.
export function formatValue(value, scope) {
    return scope === 'now' ? `${ONE_DECIMAL.format(value)}/s` : WHOLE.format(value);
}

export function formatShare(value, total) {
    if (!(total > 0)) return `${ONE_DECIMAL.format(0)}%`;
    const percent = (value * 100) / total;
    if (value > 0 && percent < 0.05) return '<0.1%';
    return `${ONE_DECIMAL.format(percent)}%`;
}

// "45 s", "2 min 20 s", "3 h 2 min", "2 d 5 h": the two largest units, the second
// left out when it is zero.
export function formatUptime(seconds) {
    const s = Math.max(0, Math.floor(seconds));
    const units = [[86400, 'd'], [3600, 'h'], [60, 'min'], [1, 's']];
    for (let i = 0; i < units.length; i++) {
        const [size, name] = units[i];
        if (s < size && size > 1) continue;
        const major = Math.floor(s / size);
        if (i === units.length - 1) return `${major} ${name}`;
        const [nextSize, nextName] = units[i + 1];
        const minor = Math.floor((s % size) / nextSize);
        return minor > 0 ? `${major} ${name} ${minor} ${nextName}` : `${major} ${name}`;
    }
    return '0 s';
}

export function formatSamples(perSecond) {
    return `${TWO_DECIMALS.format(perSecond / 1e6)} M/s`;
}

const plural = (count, one, many) => `${WHOLE.format(count)} ${count === 1 ? one : many}`;

// ─── Code names ───

const DF_NAMES = {
    0: ['ACAS short', 'Short air-air surveillance (ACAS)'],
    4: ['Altitude reply', 'Surveillance altitude reply'],
    5: ['Identity reply', 'Surveillance identity reply'],
    11: ['All-call reply', 'All-call reply'],
    16: ['ACAS long', 'Long air-air surveillance (ACAS)'],
    17: ['ADS-B', 'Extended squitter (ADS-B)'],
    18: ['TIS-B, ADS-R', 'Extended squitter, non-transponder'],
    19: ['Military', 'Military extended squitter'],
    20: ['Comm-B altitude', 'Comm-B altitude reply'],
    21: ['Comm-B identity', 'Comm-B identity reply'],
    24: ['Comm-D', 'Comm-D extended length message'],
};

const BDS_NAMES = {
    '1,0': ['Data link capability', 'Data link capability report'],
    '1,7': ['GICB capability', 'Common usage GICB capability report'],
    '2,0': ['Identification', 'Aircraft identification'],
    '3,0': ['ACAS RA', 'ACAS active resolution advisory'],
    '4,0': ['Vertical intention', 'Selected vertical intention'],
    '4,4': ['Meteo routine', 'Meteorological routine air report'],
    '4,5': ['Meteo hazard', 'Meteorological hazard report'],
    '5,0': ['Track and turn', 'Track and turn report'],
    '5,3': ['Air-referenced state', 'Air-referenced state vector'],
    '6,0': ['Heading and speed', 'Heading and speed report'],
    Empty: ['Empty reply', 'All-zero Comm-B reply'],
    Unknown: ['Unknown register', 'No register matched the content'],
};

function typeCodeName(code) {
    const tc = Number(code);
    if (tc === 0) return ['No position', 'No position information'];
    if (tc >= 1 && tc <= 4) return ['Identification', 'Aircraft identification and category'];
    if (tc >= 5 && tc <= 8) return ['Surface position', 'Surface position'];
    if (tc >= 9 && tc <= 18) return ['Position', 'Airborne position, barometric altitude'];
    if (tc === 19) return ['Velocity', 'Airborne velocity'];
    if (tc >= 20 && tc <= 22) return ['Position, GNSS', 'Airborne position, GNSS altitude'];
    if (tc === 28) return ['Aircraft status', 'Aircraft status (emergency, ACAS RA)'];
    if (tc === 29) return ['Target state', 'Target state and status'];
    if (tc === 31) return ['Operational status', 'Operational status'];
    return ['Reserved', 'Reserved type code'];
}

// A short name for the row and a full one for its tooltip
export function codeName(kind, code) {
    let names;
    if (kind === 'DF') names = DF_NAMES[code] ?? ['Reserved', 'Reserved downlink format'];
    else if (kind === 'TC') names = typeCodeName(code);
    else names = BDS_NAMES[code] ?? [`Register ${code}`, `BDS ${code}`];
    return { short: names[0], full: names[1] };
}

// The code column: "DF 17", "TC 19", "4,0", and nothing for Empty and Unknown, whose
// names say what they are.
function codeLabel(kind, code) {
    if (kind === 'BDS') return code === 'Empty' || code === 'Unknown' ? '' : String(code);
    return `${kind} ${code}`;
}

// ─── Lists ───

// From this total on, a Since start list is shown in millions: "100,000,000" is wider
// than the value column, "9,999.9 M" is not. A receiver at 1,800 messages per second
// gets there in about an hour and a half.
const MILLIONS_FROM = 10_000_000;

// Items → the bar's segments, the rows and their total. Values are rounded to what is
// shown first (0.1/s for rates, 0.1 M for a list in millions) and the total is the sum of
// the rounded values, so the rows add up to it on screen. One unit per list. Gray items
// keep a row each but share one segment.
function buildList(items, scope) {
    const rawSum = items.reduce((acc, item) => acc + item.value, 0);
    const millions = scope === 'total' && rawSum >= MILLIONS_FROM;
    const toShown = (value) => {
        if (scope === 'now') return round1(value);
        return millions ? round1(value / 1e6) : value;
    };
    const format = (shownValue) => (millions ? `${ONE_DECIMAL.format(shownValue)} M` : formatValue(shownValue, scope));

    const shown = items.map((item) => ({ ...item, raw: item.value, value: toShown(item.value) }));
    const sum = shown.reduce((acc, item) => acc + item.value, 0);
    const total = scope === 'now' || millions ? round1(sum) : sum;
    const grayTotal = shown.filter((item) => item.gray).reduce((acc, item) => acc + item.value, 0);

    const segments = [];
    let grayAdded = false;
    for (const item of shown) {
        if (item.gray) {
            if (grayAdded || !(grayTotal > 0)) continue;
            grayAdded = true;
            segments.push({
                key: 'gray',
                color: UNSLOTTED_COLOR,
                weight: grayTotal,
                title: `Without a color of their own: ${format(scope === 'total' && !millions ? grayTotal : round1(grayTotal))}`,
            });
        } else if (item.value > 0) {
            segments.push({
                key: item.key,
                color: item.color,
                weight: item.value,
                title: `${item.title}: ${format(item.value)}`,
            });
        }
    }

    // In millions the rows lose their exact count, so their tooltip keeps it
    const rowTitle = (item) => {
        const base = item.rowTitle ?? item.title;
        return millions ? `${base.replace(/\.$/, '')}. Exact count: ${WHOLE.format(item.raw)}.` : base;
    };

    const rows = shown.map((item) => ({
        key: item.key,
        segmentKey: item.gray ? 'gray' : item.key,
        code: item.code ?? '',
        label: item.label,
        sub: item.sub ?? '',
        title: rowTitle(item),
        color: item.color,
        value: format(item.value),
        share: formatShare(item.value, total),
    }));

    return {
        segments,
        rows,
        total,
        totalValue: format(total),
        totalShare: formatShare(total, total),
        // A count of this list's kind in its unit, for figures shown beside the list
        formatCount: (value) => format(toShown(value)),
    };
}

const pick = (scope, total, rate) => (scope === 'now' ? rate ?? 0 : total ?? 0);

function durationSince(since, now) {
    const seconds = (Date.parse(now) - Date.parse(since)) / 1000;
    return formatUptime(Number.isFinite(seconds) ? seconds : 0);
}

// A Beast source's state twice: short for the row, which has room for about 22
// characters, and in full, with the reconnects, for its tooltip
function beastStatus(source, timestamp) {
    if (source.Since == null) {
        return { sub: 'Beast, connecting', detail: 'Beast source, connecting' };
    }
    const state = source.Connected ? 'connected' : 'disconnected';
    const duration = durationSince(source.Since, timestamp);
    const reconnects = source.Reconnects > 0 ? `, ${plural(source.Reconnects, 'reconnect', 'reconnects')}` : '';
    return {
        sub: `Beast, ${state} ${duration}`,
        detail: `Beast source, ${state} for ${duration}${reconnects}`,
    };
}

// Every input in /stats order: SDR devices, Beast sources, MLAT. A source keeps its
// slot (and color) by that order; past the eighth it is gray.
export function sourceList(stats) {
    const sources = [];
    for (const device of stats?.Sdr?.Devices ?? []) {
        sources.push({
            key: `sdr:${device.Name}`,
            name: device.Name,
            sub: `SDR device, index ${device.DeviceIndex}`,
            detail: `SDR device, index ${device.DeviceIndex}`,
            messages: { total: device.Messages, rate: device.MessagesPerSecond },
            types: device.MessageTypes,
        });
    }

    // The same host and port may be listed twice; the second gets "(2)" so the source
    // selector can tell them apart
    const seen = new Map();
    (stats?.BeastSources ?? []).forEach((source, index) => {
        const address = `${source.Host}:${source.Port}`;
        const occurrence = (seen.get(address) ?? 0) + 1;
        seen.set(address, occurrence);
        sources.push({
            key: `beast:${address}:${index}`,
            name: occurrence > 1 ? `${address} (${occurrence})` : address,
            ...beastStatus(source, stats.Timestamp),
            messages: { total: source.Messages, rate: source.MessagesPerSecond },
            types: source.MessageTypes,
        });
    });

    if (stats?.Mlat) {
        sources.push({
            key: 'mlat',
            name: 'MLAT',
            sub: `MLAT input, ${plural(stats.Mlat.Clients, 'client', 'clients')}`,
            detail: `MLAT input, ${plural(stats.Mlat.Clients, 'client', 'clients')}`,
            messages: { total: stats.Mlat.Messages, rate: stats.Mlat.MessagesPerSecond },
            types: stats.Mlat.MessageTypes,
        });
    }

    return sources.map((source, index) => ({
        ...source,
        color: index < SERIES_COLORS.length ? SERIES_COLORS[index] : UNSLOTTED_COLOR,
        gray: index >= SERIES_COLORS.length,
    }));
}

// The source rows choose what Message types shows: a click on a source shows it, a click
// on the total row or on the source already shown goes back to all sources.
export function nextSource(current, clicked) {
    return clicked === 'all' || clicked === current ? 'all' : clicked;
}

// The source to show: the chosen one while it exists, otherwise all of them
export function resolveSource(stats, key) {
    return sourceList(stats).some((source) => source.key === key) ? key : 'all';
}

// The total row is a choice too (all sources), so it says what it covers and what a
// click does
export function buildSources(stats, scope) {
    const sources = sourceList(stats);
    const list = buildList(sources.map((source) => ({
        key: source.key,
        label: source.name,
        sub: source.sub,
        title: source.name,
        rowTitle: `${source.name}: ${source.detail}. Click to show its message types.`,
        color: source.color,
        gray: source.gray,
        value: pick(scope, source.messages.total, source.messages.rate),
    })), scope);
    return {
        ...list,
        totalSub: sources.length === 1 ? '1 source' : `${plural(sources.length, 'source', 'sources')} together`,
        totalTitle: 'Show all sources',
    };
}

// A device's extracted frames as Messages + Duplicates + Unconfirmed. Unconfirmed is
// not counted by the daemon: it is the remainder, the frames held back because their
// address belongs to no aircraft confirmed yet. The daemon reads the later stages
// first, so it is never negative in a reading; rounded rates can make it -0.1, which
// shows as 0.
export function buildDevice(device, scope) {
    const shownValue = (total, rate) => {
        const value = pick(scope, total, rate);
        return scope === 'now' ? round1(value) : value;
    };
    const extracted = shownValue(device.Extracted, device.ExtractedPerSecond);
    const messages = shownValue(device.Messages, device.MessagesPerSecond);
    const duplicates = shownValue(device.Duplicates, device.DuplicatesPerSecond);
    const remainder = extracted - messages - duplicates;
    const unconfirmed = Math.max(0, scope === 'now' ? round1(remainder) : remainder);
    const corrected = shownValue(device.Corrected, device.CorrectedPerSecond);

    const list = buildList([
        { key: 'messages', label: 'Messages', title: 'Messages: confident, unique frames, decoded', color: SERIES_COLORS[0], value: messages },
        { key: 'duplicates', label: 'Duplicates', title: 'Duplicates: the same frame again within the deduplication window', color: DROPPED_COLORS.duplicates, value: duplicates },
        { key: 'unconfirmed', label: 'Unconfirmed', title: 'Unconfirmed: frames from addresses not yet confirmed', color: DROPPED_COLORS.unconfirmed, value: unconfirmed },
    ], scope);

    return {
        key: `sdr:${device.Name}`,
        name: device.Name,
        ...list,
        // In the list's unit, so it reads like the rows; the share from the counts
        corrected: list.formatCount(corrected),
        correctedShare: formatShare(corrected, extracted),
        samples: formatSamples(device.SamplesPerSecond ?? 0),
    };
}

// A source's codes of one kind as { code: { total, rate } }, merged over sources
function mergeCodes(sources, kind) {
    const field = { DF: 'DownlinkFormats', TC: 'TypeCodes', BDS: 'BdsCodes' }[kind];
    const merged = new Map();
    for (const source of sources) {
        for (const entry of source.types?.[field] ?? []) {
            const code = String(entry[kind]);
            const current = merged.get(code) ?? { total: 0, rate: 0 };
            merged.set(code, { total: current.total + entry.Total, rate: current.rate + entry.PerSecond });
        }
    }
    return merged;
}

const HEADINGS = {
    DF: 'All messages',
    TC: 'Squitters, DF 17 + DF 18',
    BDS: 'Comm-B replies, DF 20 + DF 21',
};

// The messages of the chosen source ('all' or a source key) by kind: DF adds up to the
// messages, TC to DF 17 + 18 (with a "No type code" row for the DF 18 frames that have
// none), BDS to DF 20 + 21.
export function buildMessageTypes(stats, sourceKey, kind, scope) {
    const sources = sourceList(stats);
    const chosen = sourceKey === 'all' ? sources : sources.filter((source) => source.key === sourceKey);
    const from = chosen.length > 0 ? chosen : sources;
    const codes = mergeCodes(from, kind);

    const order = CODE_ORDER[kind].map(String);
    const present = [...codes.keys()].filter((code) => codes.get(code).total > 0);
    const slotted = order.filter((code) => present.includes(code));
    const unslotted = present
        .filter((code) => !order.includes(code))
        .sort((a, b) => a.localeCompare(b, 'en', { numeric: true }));

    const item = (code, color, gray) => {
        const names = codeName(kind, kind === 'BDS' ? code : Number(code));
        const label = codeLabel(kind, code);
        const { total, rate } = codes.get(code);
        return {
            key: `${kind}:${code}`,
            code: label,
            label: names.short,
            title: label ? `${label}: ${names.full}` : names.full,
            color,
            gray,
            value: pick(scope, total, rate),
        };
    };

    const items = [
        ...slotted.map((code) => item(code, SERIES_COLORS[order.indexOf(code)], false)),
        ...unslotted.map((code) => item(code, UNSLOTTED_COLOR, true)),
    ];

    if (kind === 'TC') {
        // The base is DF 17 + 18; what the type codes do not cover is the DF 18 frames
        // without an ICAO address. Shown when there are any since start, so the row does
        // not come and go with the scope.
        const df = mergeCodes(from, 'DF');
        const base = (code) => df.get(code) ?? { total: 0, rate: 0 };
        const baseTotal = base('17').total + base('18').total;
        const typeTotal = present.reduce((acc, code) => acc + codes.get(code).total, 0);
        if (baseTotal - typeTotal > 0) {
            const baseRate = round1(base('17').rate + base('18').rate);
            const typeRate = present.reduce((acc, code) => acc + round1(codes.get(code).rate), 0);
            items.push({
                key: 'TC:none',
                code: '',
                label: 'No type code',
                title: 'DF 18 frames without an ICAO address, which carry no type code',
                color: NO_TYPE_CODE_COLOR,
                gray: false,
                value: scope === 'now' ? Math.max(0, round1(baseRate - typeRate)) : baseTotal - typeTotal,
            });
        }
    }

    return { heading: HEADINGS[kind], ...buildList(items, scope) };
}

// ─── Footer and health ───

export function buildFooter(stats) {
    const rows = [];
    const output = (label, value) => {
        if (value) rows.push({ label, value: plural(value.Clients, 'client', 'clients') });
    };
    output('Beast output', stats?.Outputs?.Beast);
    output('JSON output', stats?.Outputs?.Json);
    output('SBS output', stats?.Outputs?.Sbs);
    rows.push({ label: 'Aircraft tracked', value: WHOLE.format(stats?.AircraftCount ?? 0) });
    rows.push({ label: 'Aeromux version', value: stats?.Version ?? '' });
    if (stats?.Database) {
        rows.push({ label: 'Database version', value: stats.Database.Version ?? 'Not loaded yet' });
    }
    rows.push({ label: 'Uptime', value: formatUptime(stats?.Uptime ?? 0) });
    return rows;
}

// The Status section and the button's dot. `failed` is true when the last /health
// request got no answer. `lines` elaborate on the state below it: what it means, or the
// problems themselves, which say more than their count. Every state has a dot, green for
// OK too: a missing dot would say nothing. Only before the first answer is there none,
// so a page load does not flash.
export function healthView(health, failed) {
    if (failed) {
        return {
            status: 'Unreachable',
            label: 'Unreachable',
            lines: ['No answer from the receiver'],
            showDot: true,
            dotColor: HEALTH_COLORS.Unreachable,
            ariaLabel: 'Statistics, no answer from the receiver',
        };
    }
    if (!health) {
        return { status: null, label: '', lines: [], showDot: false, dotColor: null, ariaLabel: 'Statistics' };
    }

    const problems = health.Problems ?? [];
    const status = health.Status;
    const fallback = {
        OK: 'Every source delivering',
        Degraded: 'Some sources are failing',
        Unhealthy: 'No source is delivering',
    }[status];
    return {
        status,
        label: status,
        lines: status !== 'OK' && problems.length > 0 ? problems : [fallback ?? status],
        showDot: true,
        dotColor: HEALTH_COLORS[status] ?? HEALTH_COLORS.Unreachable,
        ariaLabel: `Statistics, ${status}`,
    };
}
