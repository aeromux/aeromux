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

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    SERIES_COLORS, UNSLOTTED_COLOR, NO_TYPE_CODE_COLOR, HEALTH_COLORS,
    formatValue, formatShare, formatUptime, formatSamples, codeName,
    sourceList, nextSource, resolveSource, buildSources, buildDevice, buildMessageTypes, buildFooter, healthView,
} from '../Services/ReceiverStatistics.js';

// ─── Fixture: a two-device receiver with MLAT, from a real reading ───

const df = (entries) => entries.map(([DF, Total, PerSecond]) => ({ DF, Total, PerSecond }));
const tc = (entries) => entries.map(([TC, Total, PerSecond]) => ({ TC, Total, PerSecond }));
const bds = (entries) => entries.map(([BDS, Total, PerSecond]) => ({ BDS, Total, PerSecond }));

const SDR0 = {
    Name: 'sdr0', DeviceIndex: 0, SamplesPerSecond: 2400691.8,
    Extracted: 137264, ExtractedPerSecond: 973.6, Corrected: 10816, CorrectedPerSecond: 76.7,
    Duplicates: 23204, DuplicatesPerSecond: 164.6, Messages: 113588, MessagesPerSecond: 805.7,
    MessageTypes: {
        DownlinkFormats: df([[0, 7892, 53.3], [4, 9025, 64.9], [5, 8085, 57.5], [11, 22615, 162.2], [16, 1406, 9.3],
            [17, 24812, 175], [18, 1, 0], [19, 5, 0], [20, 23314, 167.1], [21, 16432, 116.4], [24, 1, 0]]),
        TypeCodes: tc([[0, 140, 1.2], [4, 941, 6.3], [11, 8666, 61], [18, 181, 1.4], [19, 8973, 63.2],
            [28, 594, 4.2], [29, 3603, 25.6], [31, 1714, 12.1]]),
        BdsCodes: bds([['1,0', 829, 5.7], ['1,7', 664, 5.1], ['2,0', 1324, 9.4], ['4,0', 14171, 101.7],
            ['5,0', 9002, 63.1], ['6,0', 12776, 91.2], ['Empty', 686, 5.2], ['Unknown', 294, 2.1]]),
    },
};

const SDR1 = {
    Name: 'sdr1', DeviceIndex: 1, SamplesPerSecond: 2400691.8,
    Extracted: 167247, ExtractedPerSecond: 1183.5, Corrected: 13281, CorrectedPerSecond: 94,
    Duplicates: 27090, DuplicatesPerSecond: 191.7, Messages: 139465, MessagesPerSecond: 986.9,
    MessageTypes: {
        DownlinkFormats: df([[0, 9984, 68.2], [4, 11523, 82], [5, 9921, 70.2], [11, 28901, 207.9], [16, 1858, 12.3],
            [17, 30007, 212.2], [18, 1, 0], [19, 3, 0], [20, 28021, 198], [21, 19245, 136.1], [24, 1, 0]]),
        TypeCodes: tc([[0, 162, 1.2], [4, 1108, 7.4], [11, 10519, 74.2], [18, 186, 1.4], [19, 10856, 76.7],
            [28, 779, 5.5], [29, 4303, 30.5], [31, 2094, 15.2]]),
        BdsCodes: bds([['1,0', 991, 6.8], ['1,7', 808, 6.1], ['2,0', 1548, 10.7], ['4,0', 16870, 118.7],
            ['5,0', 10742, 76.1], ['6,0', 15123, 107], ['Empty', 805, 5.9], ['Unknown', 379, 2.7]]),
    },
};

const MLAT = {
    Clients: 2, Messages: 24, MessagesPerSecond: 0.1,
    MessageTypes: { DownlinkFormats: df([[18, 24, 0.1]]), TypeCodes: tc([[18, 22, 0.1], [19, 2, 0]]), BdsCodes: [] },
};

const EMPTY_TYPES = { DownlinkFormats: [], TypeCodes: [], BdsCodes: [] };

const beastSource = (overrides = {}) => ({
    Host: '192.168.1.20', Port: 30005, Connected: true, Since: '2026-10-26T03:00:00Z', Reconnects: 0,
    Messages: 0, MessagesPerSecond: 0, MessageTypes: EMPTY_TYPES, ...overrides,
});

const stats = (overrides = {}) => ({
    Version: '0.9.0', Timestamp: '2026-10-26T06:00:24Z', Uptime: 140, AircraftCount: 107,
    Messages: { Total: 253077, PerSecond: 1792.7, AveragePerSecond: 1796.3 },
    Sdr: {
        Extracted: 304511, ExtractedPerSecond: 2157.1, Corrected: 24097, CorrectedPerSecond: 170.7,
        Duplicates: 50294, DuplicatesPerSecond: 356.3, Messages: 253053, MessagesPerSecond: 1792.6,
        Devices: [SDR0, SDR1],
    },
    BeastSources: [],
    Mlat: MLAT,
    Outputs: { Beast: { Clients: 5 }, Json: null, Sbs: null },
    Database: { Version: '2026.4.w44_r1' },
    ...overrides,
});

// A formatted value back to a number in its shown unit: "1,792.7/s" → 1792.7,
// "113,588" → 113588, "70.6 M" → 70.6
const parse = (text) => Number(text.replace(/,/g, '').replace('/s', '').replace(' M', ''));

function assertRowsAddUp(list, scope) {
    const sum = list.rows.reduce((acc, row) => acc + parse(row.value), 0);
    const rounded = scope === 'now' || list.totalValue.endsWith(' M');
    const expected = rounded ? Math.round(sum * 10) / 10 : sum;
    assert.equal(parse(list.totalValue), expected, 'the total row is the sum of the rows as displayed');
}

// ─── Formatting ───

test('values are in US format: a rate with one decimal, a count with separators', () => {
    assert.equal(formatValue(1792.7, 'now'), '1,792.7/s');
    assert.equal(formatValue(0, 'now'), '0.0/s');
    assert.equal(formatValue(113588, 'total'), '113,588');
});

test('shares have one decimal, <0.1% for a sliver, and 0.0% of nothing', () => {
    assert.equal(formatShare(805.7, 1792.7), '44.9%');
    assert.equal(formatShare(0.1, 1792.7), '<0.1%');
    assert.equal(formatShare(0, 1792.7), '0.0%');
    assert.equal(formatShare(5, 0), '0.0%');
    assert.equal(formatShare(1792.7, 1792.7), '100.0%');
});

test('uptime uses its two largest units, leaving out a zero second unit', () => {
    assert.equal(formatUptime(0), '0 s');
    assert.equal(formatUptime(59), '59 s');
    assert.equal(formatUptime(60), '1 min');
    assert.equal(formatUptime(140), '2 min 20 s');
    assert.equal(formatUptime(3600), '1 h');
    assert.equal(formatUptime(3720), '1 h 2 min');
    assert.equal(formatUptime(86400), '1 d');
    assert.equal(formatUptime(2 * 86400 + 5 * 3600 + 59), '2 d 5 h');
});

test('samples in millions per second', () => {
    assert.equal(formatSamples(2400691.8), '2.40 M/s');
    assert.equal(formatSamples(0), '0.00 M/s');
});

// ─── Names ───

test('every downlink format has its short and full name', () => {
    const expected = {
        0: 'ACAS short', 4: 'Altitude reply', 5: 'Identity reply', 11: 'All-call reply', 16: 'ACAS long',
        17: 'ADS-B', 18: 'TIS-B, ADS-R', 19: 'Military', 20: 'Comm-B altitude', 21: 'Comm-B identity', 24: 'Comm-D',
    };
    for (const [code, short] of Object.entries(expected)) {
        assert.equal(codeName('DF', Number(code)).short, short);
    }
    assert.equal(codeName('DF', 17).full, 'Extended squitter (ADS-B)');
    assert.equal(codeName('DF', 7).short, 'Reserved');
});

test('type codes are named by their range, at each edge', () => {
    const expected = [
        [0, 'No position'], [1, 'Identification'], [4, 'Identification'], [5, 'Surface position'],
        [8, 'Surface position'], [9, 'Position'], [18, 'Position'], [19, 'Velocity'], [20, 'Position, GNSS'],
        [22, 'Position, GNSS'], [23, 'Reserved'], [27, 'Reserved'], [28, 'Aircraft status'], [29, 'Target state'],
        [30, 'Reserved'], [31, 'Operational status'],
    ];
    for (const [code, short] of expected) {
        assert.equal(codeName('TC', code).short, short, `TC ${code}`);
    }
});

test('every Comm-B register has a name, and an unknown one falls back to its code', () => {
    const expected = {
        '1,0': 'Data link capability', '1,7': 'GICB capability', '2,0': 'Identification', '3,0': 'ACAS RA',
        '4,0': 'Vertical intention', '4,4': 'Meteo routine', '4,5': 'Meteo hazard', '5,0': 'Track and turn',
        '5,3': 'Air-referenced state', '6,0': 'Heading and speed', Empty: 'Empty reply', Unknown: 'Unknown register',
    };
    for (const [code, short] of Object.entries(expected)) {
        assert.equal(codeName('BDS', code).short, short);
    }
    assert.equal(codeName('BDS', '9,9').short, 'Register 9,9');
});

// ─── Sources ───

test('sources come in /stats order, each with its slot color', () => {
    const sources = sourceList(stats({ BeastSources: [beastSource()] }));

    assert.deepEqual(sources.map((s) => s.name), ['sdr0', 'sdr1', '192.168.1.20:30005', 'MLAT']);
    assert.deepEqual(sources.map((s) => s.color), SERIES_COLORS.slice(0, 4));
    assert.deepEqual(sources.map((s) => s.sub), [
        'SDR device, index 0', 'SDR device, index 1', 'Beast, connected 3 h', 'MLAT input, 2 clients',
    ]);
});

test('a Beast source reads connecting, disconnected or connected, short on the row', () => {
    const sources = sourceList(stats({
        Sdr: null,
        Mlat: null,
        BeastSources: [
            beastSource({ Since: null, Connected: false }),
            beastSource({ Port: 30006, Connected: false, Since: '2026-10-26T05:59:39Z', Reconnects: 3 }),
            beastSource({ Port: 30007, Since: '2026-10-26T05:59:24Z', Reconnects: 1 }),
        ],
    }));

    assert.deepEqual(sources.map((s) => s.sub), [
        'Beast, connecting',
        'Beast, disconnected 45 s',
        'Beast, connected 1 min',
    ]);
    assert.deepEqual(sources.map((s) => s.detail), [
        'Beast source, connecting',
        'Beast source, disconnected for 45 s, 3 reconnects',
        'Beast source, connected for 1 min, 1 reconnect',
    ]);
    // The row has room for about 22 characters: the state always fits, a long duration
    // may be cut, and the tooltip has it in full
    assert.ok(sources.every((s) => s.sub.match(/^Beast, \w+/)[0].length <= 22));
});

test('the same Beast source listed twice is told apart', () => {
    const sources = sourceList(stats({ Sdr: null, Mlat: null, BeastSources: [beastSource(), beastSource()] }));

    assert.deepEqual(sources.map((s) => s.name), ['192.168.1.20:30005', '192.168.1.20:30005 (2)']);
    assert.notEqual(sources[0].key, sources[1].key);
});

test('a source row shows that source, again or the total row shows all of them', () => {
    assert.equal(nextSource('all', 'sdr:sdr0'), 'sdr:sdr0', 'choose');
    assert.equal(nextSource('sdr:sdr0', 'mlat'), 'mlat', 'choose another');
    assert.equal(nextSource('mlat', 'mlat'), 'all', 'the same again');
    assert.equal(nextSource('sdr:sdr0', 'all'), 'all', 'the total row');
    assert.equal(nextSource('all', 'all'), 'all');
});

test('a chosen source that is gone falls back to all sources', () => {
    assert.equal(resolveSource(stats(), 'sdr:sdr1'), 'sdr:sdr1');
    assert.equal(resolveSource(stats(), 'beast:gone:0'), 'all');
    assert.equal(resolveSource(stats(), 'all'), 'all');
});

test('the total row counts the sources it covers', () => {
    assert.equal(buildSources(stats(), 'now').totalSub, '3 sources together');
    assert.equal(buildSources(stats(), 'now').totalTitle, 'Show all sources');
    assert.equal(buildSources(stats({ Sdr: null, Mlat: null, BeastSources: [beastSource()] }), 'now').totalSub, '1 source');
});

test('a source row says what a click does; its bar segment says the figure', () => {
    const sources = buildSources(stats(), 'now');

    assert.equal(sources.rows[0].title, 'sdr0: SDR device, index 0. Click to show its message types.');
    assert.equal(sources.segments[0].title, 'sdr0: 805.7/s');
});

test('a ninth source is gray, as codes without a slot are', () => {
    const beast = Array.from({ length: 7 }, (_, i) => beastSource({ Port: 30000 + i }));
    const sources = sourceList(stats({ BeastSources: beast }));

    assert.equal(sources.length, 10);
    assert.equal(sources[7].color, SERIES_COLORS[7]);
    assert.equal(sources[8].color, UNSLOTTED_COLOR);
    assert.equal(sources[9].color, UNSLOTTED_COLOR);
});

test('sources add up to the total in both scopes', () => {
    for (const scope of ['now', 'total']) {
        assertRowsAddUp(buildSources(stats(), scope), scope);
    }
    const now = buildSources(stats(), 'now');
    assert.deepEqual(now.rows.map((r) => r.value), ['805.7/s', '986.9/s', '0.1/s']);
    assert.equal(now.totalValue, '1,792.7/s');
    assert.equal(buildSources(stats(), 'total').totalValue, '253,077');
});

test('missing inputs leave their rows out, and a silent source has a row but no segment', () => {
    const beastOnly = buildSources(stats({ Sdr: null, Mlat: null, BeastSources: [beastSource()] }), 'now');

    assert.deepEqual(beastOnly.rows.map((r) => r.label), ['192.168.1.20:30005']);
    assert.equal(beastOnly.segments.length, 0);
    assert.equal(buildSources(stats({ Sdr: null, Mlat: null }), 'total').rows.length, 0);
});

// ─── Large totals ───

// The fixture's devices after a day at the production rate
const dayLong = () => stats({
    Sdr: {
        ...stats().Sdr,
        Devices: [
            { ...SDR0, Messages: 70_612_345 },
            { ...SDR1, Messages: 84_887_655 },
        ],
    },
});

test('a list from 10,000,000 on is shown in millions, and still adds up', () => {
    const sources = buildSources(dayLong(), 'total');

    assert.deepEqual(sources.rows.map((r) => r.value), ['70.6 M', '84.9 M', '0.0 M']);
    assert.equal(sources.totalValue, '155.5 M');
    assertRowsAddUp(sources, 'total');
    assert.equal(sources.rows[2].share, '0.0%');
});

test('just below 10,000,000 the counts stay exact', () => {
    const below = stats({ Sdr: { ...stats().Sdr, Devices: [{ ...SDR0, Messages: 9_999_975 }] } });

    assert.equal(buildSources(below, 'total').totalValue, '9,999,999');
    const at = stats({ Sdr: { ...stats().Sdr, Devices: [{ ...SDR0, Messages: 9_999_976 }] } });
    assert.equal(buildSources(at, 'total').totalValue, '10.0 M');
});

test('in millions a row keeps its exact count in the tooltip', () => {
    const rows = buildSources(dayLong(), 'total').rows;

    assert.equal(rows[0].title, 'sdr0: SDR device, index 0. Click to show its message types. Exact count: 70,612,345.');
    assert.equal(rows[2].title, 'MLAT: MLAT input, 2 clients. Click to show its message types. Exact count: 24.');
});

test('rates are never shown in millions', () => {
    assert.equal(buildSources(dayLong(), 'now').totalValue, '1,792.7/s');
});

test('a device in millions shows its Corrected note in millions, its share from the counts', () => {
    const device = buildDevice({
        ...SDR0, Extracted: 120_000_000, Corrected: 9_480_000, Duplicates: 20_000_000, Messages: 99_500_000,
    }, 'total');

    assert.deepEqual(device.rows.map((r) => r.value), ['99.5 M', '20.0 M', '0.5 M']);
    assert.equal(device.totalValue, '120.0 M');
    assert.equal(device.corrected, '9.5 M');
    assert.equal(device.correctedShare, '7.9%');
    assertRowsAddUp(device, 'total');
});

// ─── A device ───

test('a device extracted Messages + Duplicates + Unconfirmed', () => {
    const device = buildDevice(SDR0, 'total');

    assert.deepEqual(device.rows.map((r) => [r.label, r.value]), [
        ['Messages', '113,588'], ['Duplicates', '23,204'], ['Unconfirmed', '472'],
    ]);
    assert.equal(device.totalValue, '137,264', 'the device row shows its total row');
    assertRowsAddUp(device, 'total');
    assertRowsAddUp(buildDevice(SDR0, 'now'), 'now');
});

test('rounded rates never make Unconfirmed negative', () => {
    const device = buildDevice({
        ...SDR0, ExtractedPerSecond: 10, MessagesPerSecond: 8, DuplicatesPerSecond: 2.1,
    }, 'now');

    assert.equal(device.rows[2].value, '0.0/s');
});

test('Corrected is a share of the extracted frames, not a part', () => {
    const device = buildDevice(SDR0, 'total');

    assert.ok(!device.rows.some((r) => r.label === 'Corrected'));
    assert.equal(device.corrected, '10,816');
    assert.equal(device.correctedShare, '7.9%');
    assert.equal(device.samples, '2.40 M/s');
});

// ─── Message types ───

test('downlink formats: colored codes in their order, then the others as gray rows', () => {
    const types = buildMessageTypes(stats(), 'sdr:sdr0', 'DF', 'total');

    assert.deepEqual(types.rows.map((r) => r.code), [
        'DF 17', 'DF 20', 'DF 11', 'DF 21', 'DF 4', 'DF 5', 'DF 0', 'DF 16', 'DF 18', 'DF 19', 'DF 24',
    ]);
    assert.deepEqual(types.rows.slice(8).map((r) => r.color), [UNSLOTTED_COLOR, UNSLOTTED_COLOR, UNSLOTTED_COLOR]);
    assert.equal(types.segments.filter((s) => s.color === UNSLOTTED_COLOR).length, 1, 'the gray codes share one segment');
    assert.equal(types.heading, 'All messages');
    assert.equal(types.totalValue, '113,588');
});

test('MLAT alone is one named gray row at 100%', () => {
    const types = buildMessageTypes(stats(), 'mlat', 'DF', 'total');

    assert.deepEqual(types.rows.map((r) => [r.code, r.label, r.share]), [['DF 18', 'TIS-B, ADS-R', '100.0%']]);
});

test('every message type list adds up, for every source, kind and scope', () => {
    for (const source of ['all', 'sdr:sdr0', 'sdr:sdr1', 'mlat']) {
        for (const kind of ['DF', 'TC', 'BDS']) {
            for (const scope of ['now', 'total']) {
                assertRowsAddUp(buildMessageTypes(stats(), source, kind, scope), scope);
            }
        }
    }
});

test('type codes add up to DF 17 + 18, with the frames that have none in their own row', () => {
    const types = buildMessageTypes(stats(), 'all', 'TC', 'total');
    const none = types.rows.find((r) => r.label === 'No type code');

    assert.equal(types.heading, 'Squitters, DF 17 + DF 18');
    assert.equal(none.value, '2', 'two DF 18 frames from the devices carry no type code');
    assert.equal(none.color, NO_TYPE_CODE_COLOR);
    assert.equal(types.totalValue, '54,845');
});

test('without DF 18 frames lacking a type code there is no such row', () => {
    const noDf18 = {
        ...SDR0,
        MessageTypes: {
            ...SDR0.MessageTypes,
            DownlinkFormats: SDR0.MessageTypes.DownlinkFormats.filter((e) => e.DF !== 18),
        },
    };
    const types = buildMessageTypes(stats({ Sdr: { ...stats().Sdr, Devices: [noDf18] } }), 'sdr:sdr0', 'TC', 'total');

    assert.ok(!types.rows.some((r) => r.label === 'No type code'));
});

test('Comm-B registers add up to DF 20 + 21; Empty and Unknown have no code', () => {
    const types = buildMessageTypes(stats(), 'sdr:sdr0', 'BDS', 'total');

    assert.equal(types.heading, 'Comm-B replies, DF 20 + DF 21');
    assert.equal(types.totalValue, '39,746');
    assert.deepEqual(types.rows.slice(-2).map((r) => [r.code, r.label]), [['', 'Empty reply'], ['', 'Unknown register']]);
});

test('a code keeps its color whatever the scope and the source', () => {
    const colorOf = (source, scope) =>
        buildMessageTypes(stats(), source, 'DF', scope).rows.find((r) => r.code === 'DF 11').color;

    const colors = new Set([colorOf('all', 'now'), colorOf('all', 'total'), colorOf('sdr:sdr1', 'now')]);
    assert.deepEqual([...colors], [SERIES_COLORS[2]]);
});

test('All sources merges every source', () => {
    const types = buildMessageTypes(stats(), 'all', 'DF', 'total');

    assert.equal(types.rows.find((r) => r.code === 'DF 18').value, '26');
    assert.equal(types.totalValue, '253,077');
});

test('an unknown source key falls back to all sources', () => {
    const types = buildMessageTypes(stats(), 'beast:gone:0', 'DF', 'total');

    assert.equal(types.totalValue, '253,077');
});

// ─── Footer and health ───

test('the footer lists enabled outputs, the aircraft, the versions and the uptime', () => {
    assert.deepEqual(buildFooter(stats()), [
        { label: 'Beast output', value: '5 clients' },
        { label: 'Aircraft tracked', value: '107' },
        { label: 'Aeromux version', value: '0.9.0' },
        { label: 'Database version', value: '2026.4.w44_r1' },
        { label: 'Uptime', value: '2 min 20 s' },
    ]);
});

test('the footer leaves out a disabled database and shows one not loaded yet', () => {
    const labels = buildFooter(stats({ Database: null, Outputs: { Beast: null, Json: { Clients: 1 }, Sbs: null } }));

    assert.deepEqual(labels.map((r) => r.label), ['JSON output', 'Aircraft tracked', 'Aeromux version', 'Uptime']);
    assert.equal(labels[0].value, '1 client');
    assert.equal(buildFooter(stats({ Database: { Version: null } })).at(-2).value, 'Not loaded yet');
});

test('a healthy receiver has a green dot', () => {
    const view = healthView({ Status: 'OK', Problems: [] }, false);

    assert.equal(view.showDot, true);
    assert.equal(view.dotColor, HEALTH_COLORS.OK);
    assert.deepEqual(view.lines, ['Every source delivering']);
    assert.equal(view.ariaLabel, 'Statistics, OK');
});

test('a degraded or unhealthy receiver shows its dot and its problems', () => {
    const degraded = healthView({ Status: 'Degraded', Problems: ['Beast source has been disconnected for 45 s'] }, false);
    const unhealthy = healthView({ Status: 'Unhealthy', Problems: ['a', 'b'] }, false);

    assert.equal(degraded.dotColor, HEALTH_COLORS.Degraded);
    assert.deepEqual(degraded.lines, ['Beast source has been disconnected for 45 s'], 'the problems, not their count');
    assert.equal(degraded.ariaLabel, 'Statistics, Degraded');
    assert.equal(unhealthy.dotColor, HEALTH_COLORS.Unhealthy);
    assert.deepEqual(unhealthy.lines, ['a', 'b']);
    assert.deepEqual(healthView({ Status: 'Degraded', Problems: [] }, false).lines, ['Some sources are failing']);
});

test('no answer turns the dot gray; before the first answer there is nothing', () => {
    const failed = healthView({ Status: 'OK', Problems: [] }, true);

    assert.equal(failed.dotColor, HEALTH_COLORS.Unreachable);
    assert.equal(failed.label, 'Unreachable');
    assert.deepEqual(failed.lines, ['No answer from the receiver']);
    assert.equal(healthView(null, false).showDot, false);
});
