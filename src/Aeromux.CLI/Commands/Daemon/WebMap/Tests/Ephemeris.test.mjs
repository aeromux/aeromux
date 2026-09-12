// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025-2026 Nandor Toth <dev@nandortoth.com>
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Tests for the solar and lunar ephemeris.
//
// Most of this subject checks without reference tables, and the invariants below
// do exactly that. They are necessary and they are NOT sufficient: transposing a
// digit in the solar equation of the centre (1.915 -> 1.519) leaves the solstice
// elevations at exactly the right values, because the obliquity sets those and not
// the series, so every invariant here still passes while the marker lands 0.396
// degrees out — three quarters of the sun's own diameter.
//
// The two "absolute" groups at the end are therefore the ones that matter. They
// are anchored to published values that do not come from this code at all.
//
// Run with `node --test`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    sunPosition,
    moonPosition,
    moonPhase,
    refractionDeg,
    isUp,
    lunarEcliptic,
    equationOfTimeMinutes,
    gmstDeg,
    julianDay,
    wrap180
} from '../Services/Ephemeris.js';

// Budapest, to match the figures quoted in the specification.
const LAT = 47.41;
const LON = 19.04;

const utc = (y, m, d, h = 0, min = 0) => new Date(Date.UTC(y, m - 1, d, h, min));

// Highest the sun reaches on a given UTC date, sampled finely enough to land on
// local noon whatever the longitude.
function peakSolarElevation(date, lat, lon) {
    let peak = -90;
    for (let h = 0; h < 24; h += 0.01) {
        const at = new Date(date.getTime() + h * 3600000);
        const el = sunPosition(at, lat, lon).elevationDeg;
        if (el > peak) peak = el;
    }
    return peak;
}

// ---------- solar geometry ----------

test('local noon elevation at the equinox is 90 degrees minus the latitude', () => {
    const peak = peakSolarElevation(utc(2026, 3, 20), LAT, LON);
    // Refraction lifts it a little, and the equinox instant is not exactly midday.
    assert.ok(Math.abs(peak - (90 - LAT)) < 0.5, `equinox peak ${peak}`);
});

test('the solstices sit one obliquity either side of that', () => {
    const summer = peakSolarElevation(utc(2026, 6, 21), LAT, LON);
    const winter = peakSolarElevation(utc(2026, 12, 21), LAT, LON);
    assert.ok(Math.abs(summer - (90 - LAT + 23.44)) < 0.5, `summer ${summer}`);
    assert.ok(Math.abs(winter - (90 - LAT - 23.44)) < 0.5, `winter ${winter}`);
});

test('the sun is due south at local noon from the northern hemisphere', () => {
    let peak = -90;
    let azimuth = 0;
    for (let h = 0; h < 24; h += 0.01) {
        const at = new Date(utc(2026, 6, 21).getTime() + h * 3600000);
        const p = sunPosition(at, LAT, LON);
        if (p.elevationDeg > peak) {
            peak = p.elevationDeg;
            azimuth = p.azimuthDeg;
        }
    }
    assert.ok(Math.abs(wrap180(azimuth - 180)) < 1, `noon azimuth ${azimuth}`);
});

test('the sun is up at midday and down at midnight', () => {
    // 11:00 UTC is near local noon at this longitude, 23:00 near local midnight.
    assert.ok(sunPosition(utc(2026, 6, 21, 11), LAT, LON).elevationDeg > 0);
    assert.ok(sunPosition(utc(2026, 6, 21, 23), LAT, LON).elevationDeg < 0);
});

test('solar elevation is periodic over a day', () => {
    const a = sunPosition(utc(2026, 4, 10, 9), LAT, LON).elevationDeg;
    const b = sunPosition(utc(2026, 4, 11, 9), LAT, LON).elevationDeg;
    // A day apart the sun is in nearly the same place, the seasonal drift aside.
    assert.ok(Math.abs(a - b) < 1, `${a} vs ${b}`);
});

test('sidereal time advances about four minutes a day on the clock', () => {
    const a = gmstDeg(utc(2026, 4, 10, 0));
    const b = gmstDeg(utc(2026, 4, 11, 0));
    // 360.9856 degrees per day, so roughly one degree of drift.
    assert.ok(Math.abs(wrap180(b - a) - 0.9856) < 0.01, `${a} -> ${b}`);
});

test('julian day matches the J2000.0 epoch', () => {
    assert.ok(Math.abs(julianDay(utc(2000, 1, 1, 12)) - 2451545.0) < 1e-6);
});

// ---------- lunar bounds ----------

test('lunar ecliptic latitude stays inside the orbit inclination', () => {
    // The orbit is inclined about 5.14 degrees to the ecliptic, and the truncated
    // series must not manufacture more than the perturbations really allow.
    // Sampled off any whole number of days so the series is exercised broadly.
    let worst = 0;
    for (let d = 0; d < 7300; d += 0.37) {
        worst = Math.max(worst, Math.abs(lunarEcliptic(d).latDeg));
    }
    assert.ok(worst < 5.4, `latitude exceeded the inclination: ${worst}`);
    // And it does reach the bound — a series that never leaves the ecliptic would
    // pass the line above while being badly wrong.
    assert.ok(worst > 5.0, `latitude never approached the inclination: ${worst}`);
});

test('lunar distance stays within the real perigee and apogee', () => {
    let lo = Infinity;
    let hi = 0;
    for (let d = 0; d < 7300; d += 0.37) {
        const at = new Date(utc(2020, 1, 1).getTime() + d * 86400000);
        const { distanceKm } = moonPosition(at, LAT, LON);
        lo = Math.min(lo, distanceKm);
        hi = Math.max(hi, distanceKm);
    }
    assert.ok(lo > 356000 && lo < 358000, `perigee ${lo}`);
    assert.ok(hi > 405000 && hi < 407500, `apogee ${hi}`);
});

test('successive new moons are a synodic month apart', () => {
    const newMoons = [];
    let previous = moonPhase(utc(2026, 1, 1)).fraction;
    let falling = false;
    for (let h = 0; h < 24 * 200; h += 0.5) {
        const at = new Date(utc(2026, 1, 1).getTime() + h * 3600000);
        const f = moonPhase(at).fraction;
        if (f < previous) falling = true;
        else if (falling) {
            newMoons.push(at.getTime());
            falling = false;
        }
        previous = f;
    }
    assert.ok(newMoons.length >= 5, `only found ${newMoons.length} new moons`);
    for (let i = 1; i < newMoons.length; i++) {
        const days = (newMoons[i] - newMoons[i - 1]) / 86400000;
        assert.ok(Math.abs(days - 29.53) < 0.6, `interval ${days} days`);
    }
});

// ---------- phase ----------

test('illuminated fraction runs new to full to new across a lunation', () => {
    const samples = [];
    for (let d = 0; d <= 30; d += 0.5) {
        const at = new Date(utc(2026, 1, 6).getTime() + d * 86400000);
        samples.push(moonPhase(at).fraction);
    }
    for (const f of samples) {
        assert.ok(f >= 0 && f <= 1, `fraction out of range ${f}`);
    }
    assert.ok(Math.max(...samples) > 0.97, 'never reached full');
    assert.ok(Math.min(...samples) < 0.03, 'never reached new');
});

test('waxing is reported while the illuminated fraction is growing', () => {
    const start = utc(2026, 1, 20);
    for (let d = 0; d < 25; d += 1) {
        const at = new Date(start.getTime() + d * 86400000);
        const next = new Date(at.getTime() + 6 * 3600000);
        const here = moonPhase(at);
        const later = moonPhase(next);
        // Skip the two turning points, where a six-hour step cannot resolve it.
        if (Math.abs(later.fraction - here.fraction) < 0.005) continue;
        assert.equal(
            here.waxing, later.fraction > here.fraction,
            `waxing ${here.waxing} but fraction ${here.fraction} -> ${later.fraction}`
        );
    }
});

// ---------- angular size ----------

test('both bodies subtend about half a degree', () => {
    const sun = sunPosition(utc(2026, 6, 1, 12), LAT, LON);
    const moon = moonPosition(utc(2026, 6, 1, 12), LAT, LON);
    assert.ok(sun.diameterDeg > 0.52 && sun.diameterDeg < 0.55, `sun ${sun.diameterDeg}`);
    assert.ok(moon.diameterDeg > 0.48 && moon.diameterDeg < 0.57, `moon ${moon.diameterDeg}`);
});

test('the moon is visibly larger at perigee than at apogee', () => {
    let small = Infinity;
    let large = 0;
    for (let d = 0; d < 400; d += 0.37) {
        const at = new Date(utc(2026, 1, 1).getTime() + d * 86400000);
        const { diameterDeg } = moonPosition(at, LAT, LON);
        small = Math.min(small, diameterDeg);
        large = Math.max(large, diameterDeg);
    }
    // About 12 per cent across the month — enough to be worth carrying per body
    // rather than fixing at a mean.
    assert.ok(large / small > 1.10, `ratio ${large / small}`);
});

// ---------- parallax ----------

test('lunar parallax is nil at the zenith and about a degree at the horizon', () => {
    // The assertion is the magnitude, not its absence: this correction is larger
    // than the entire error budget of everything else in the module.
    let nearZenith = null;
    let nearHorizon = null;
    for (let h = 0; h < 24 * 40; h += 0.25) {
        const at = new Date(utc(2026, 1, 1).getTime() + h * 3600000);
        // A tropical latitude, so the moon genuinely passes close to overhead.
        const m = moonPosition(at, 20, 0);
        const shift = m.geocentricElevationDeg - m.trueElevationDeg;
        if (m.geocentricElevationDeg > 88 && nearZenith === null) nearZenith = shift;
        if (Math.abs(m.geocentricElevationDeg) < 0.2) nearHorizon = shift;
    }
    assert.ok(nearZenith !== null, 'never found the moon near the zenith');
    assert.ok(nearHorizon !== null, 'never found the moon near the horizon');
    assert.ok(Math.abs(nearZenith) < 0.05, `zenith parallax ${nearZenith}`);
    assert.ok(nearHorizon > 0.88 && nearHorizon < 1.05, `horizon parallax ${nearHorizon}`);
});

test('parallax always lowers the moon, never raises it', () => {
    for (let h = 0; h < 24 * 30; h += 1) {
        const at = new Date(utc(2026, 1, 1).getTime() + h * 3600000);
        const m = moonPosition(at, LAT, LON);
        assert.ok(
            m.trueElevationDeg <= m.geocentricElevationDeg + 1e-9,
            `parallax raised the moon at ${at.toISOString()}`
        );
    }
});

test('the sun needs no topocentric correction', () => {
    // Same geometry at 150 million km is 0.0024 degrees, a hundredth of a pixel,
    // which is why sunPosition deliberately does not apply it.
    const parallaxDeg = Math.asin(6371 / 149597871) * 180 / Math.PI;
    assert.ok(parallaxDeg < 0.005, `solar parallax ${parallaxDeg}`);
});

// ---------- refraction ----------

test('refraction matches the documented curve', () => {
    assert.ok(Math.abs(refractionDeg(0) - 0.575) < 0.005, refractionDeg(0));
    assert.ok(Math.abs(refractionDeg(5) - 0.165) < 0.005, refractionDeg(5));
    assert.ok(Math.abs(refractionDeg(30) - 0.029) < 0.005, refractionDeg(30));
});

test('refraction tends to zero at the zenith and is never negative', () => {
    assert.ok(refractionDeg(90) < 0.001);
    for (let h = -1; h <= 90; h += 0.5) {
        assert.ok(refractionDeg(h) >= 0, `negative refraction at ${h}`);
    }
});

test('refraction decreases with altitude', () => {
    let previous = Infinity;
    for (let h = 0; h <= 90; h += 1) {
        const r = refractionDeg(h);
        assert.ok(r <= previous + 1e-9, `refraction rose at ${h}`);
        previous = r;
    }
});

// ---------- the horizon test ----------

test('the sun is still up when its geometric centre is below the horizon', () => {
    // Sunset is the case this protects. Refraction lifts the disc 0.575 degrees and
    // its own radius adds 0.27, so the sun remains visible until its true centre
    // has reached about -0.83. Testing the centre against zero would delete the
    // last several minutes of daylight — the moment the whole feature is for.
    const apparent = (geometricDeg) => ({
        elevationDeg: geometricDeg + refractionDeg(geometricDeg),
        diameterDeg: 0.533
    });
    assert.equal(isUp(apparent(-0.5)), true, 'sun hidden before it has set');
    assert.equal(isUp(apparent(-1.5)), false, 'sun still drawn well after setting');
});

test('a body well above the horizon is up and one well below is not', () => {
    assert.equal(isUp({ elevationDeg: 30, diameterDeg: 0.53 }), true);
    assert.equal(isUp({ elevationDeg: -10, diameterDeg: 0.53 }), false);
});

test('the horizon test respects the receiver depression', () => {
    // An elevated receiver sees past the geometric horizon, so a body slightly
    // below level is still up for it.
    const low = { elevationDeg: -0.4, diameterDeg: 0.53 };
    assert.equal(isUp(low, 0), false);
    assert.equal(isUp(low, -1), true);
});

// ---------- absolute: the external anchors ----------

test('the equation of time reaches its published extremes', () => {
    // Nothing here comes from our own code: the equation of time is tabulated, and
    // it swings to about -14.2 minutes in mid-February and +16.4 in early November.
    // This is the assertion a mistyped solar coefficient fails and every invariant
    // above passes.
    let lo = Infinity;
    let hi = -Infinity;
    let loAt = null;
    let hiAt = null;
    for (let d = 0; d < 365; d += 0.25) {
        const at = new Date(utc(2026, 1, 1).getTime() + d * 86400000);
        const v = equationOfTimeMinutes(at);
        if (v < lo) { lo = v; loAt = at; }
        if (v > hi) { hi = v; hiAt = at; }
    }
    assert.ok(Math.abs(lo + 14.2) < 0.5, `minimum ${lo} (expected -14.2)`);
    assert.ok(Math.abs(hi - 16.4) < 0.5, `maximum ${hi} (expected +16.4)`);
    assert.equal(loAt.getUTCMonth() + 1, 2, 'minimum not in February');
    assert.equal(hiAt.getUTCMonth() + 1, 11, 'maximum not in November');
});

test('the reference new moon comes out as new', () => {
    // The new moon of 2000-01-06 18:14 UTC is a published instant. The moon
    // separates from the sun at about 0.51 degrees an hour, so landing within two
    // hours bounds the sun-moon longitude error at roughly one degree. An
    // independent anchor, and a wrong lunar coefficient fails it outright.
    const reference = Date.UTC(2000, 0, 6, 18, 14);
    let best = Infinity;
    let bestAt = null;
    for (let h = -48; h <= 48; h += 0.05) {
        const at = new Date(reference + h * 3600000);
        const { fraction } = moonPhase(at);
        if (fraction < best) { best = fraction; bestAt = at; }
    }
    const offsetHours = (bestAt.getTime() - reference) / 3600000;
    assert.ok(Math.abs(offsetHours) < 2, `new moon off by ${offsetHours} hours`);
    assert.ok(best < 0.001, `minimum illumination ${best}`);
});
