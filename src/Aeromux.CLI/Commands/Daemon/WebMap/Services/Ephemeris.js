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

// Apparent positions of the sun and the moon for an observer on the surface.
// Pure functions of the clock and the observer position — no DOM, no framework,
// so it is directly testable under `node --test`.
//
// "Apparent" is the contract: every position returned here has already had the
// corrections an observer actually sees applied — parallax for the moon, and
// refraction for both — so a caller never has to know which of them mattered.
// Getting that wrong is the easiest way to place a marker a degree from where the
// body plainly is, and a degree is two full moon diameters.

const DEG = Math.PI / 180;
const RAD = 180 / Math.PI;

const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);
const sin = (deg) => Math.sin(deg * DEG);
const cos = (deg) => Math.cos(deg * DEG);

// Mean Earth radius, matching SkyViewGeometry. Used as the observer's offset from
// the centre when converting the moon to a topocentric position.
const EARTH_RADIUS_KM = 6371;

// Physical diameters, for the angular size each body actually subtends.
const SUN_DIAMETER_KM = 1392000;
const MOON_DIAMETER_KM = 3474.8;

// Mean distances, used only where a caller wants a nominal size.
const SUN_DISTANCE_KM = 149597871;

// ---------- time ----------

// Julian day from a Date. The epoch offset is the Unix epoch expressed as a
// Julian day; Date already carries UTC, so no timezone handling belongs here.
export function julianDay(date) {
    return date.getTime() / 86400000 + 2440587.5;
}

// Days from the J2000.0 epoch, the argument every series below is written in.
export function daysSinceJ2000(date) {
    return julianDay(date) - 2451545.0;
}

// Greenwich mean sidereal time in degrees. Sidereal time is what turns a
// position on the celestial sphere into one relative to the observer's meridian:
// the sky runs about four minutes a day ahead of the clock, which is why the stars
// rise earlier each night and why the sun cannot simply be placed by local time.
export function gmstDeg(date) {
    const n = daysSinceJ2000(date);
    return wrap360(280.46061837 + 360.98564736629 * n);
}

// Obliquity of the ecliptic — the tilt that gives the seasons, and the only
// reason the sun's elevation changes across the year at all.
function obliquityDeg(n) {
    return 23.439291 - 3.563e-7 * n;
}

// ---------- angles ----------

export function wrap360(deg) {
    const d = deg % 360;
    return d < 0 ? d + 360 : d;
}

export function wrap180(deg) {
    const d = wrap360(deg);
    return d > 180 ? d - 360 : d;
}

// ---------- refraction ----------

// How much the atmosphere lifts a body above its true altitude. Bennett's
// formula: about 0.575 degrees right at the horizon, falling away quickly with
// height and negligible above roughly 30 degrees.
//
// This exists because the Sky View already refracts *aircraft*, through an
// effective Earth radius in SkyViewGeometry. Leaving the sun and moon unrefracted
// would draw two things a degree apart in the same frame under different physics.
export function refractionDeg(trueAltitudeDeg) {
    // Below the horizon the formula diverges, and refraction has no meaning for a
    // body the Earth is solidly in front of. Held at the horizon value so the
    // function stays monotonic through zero for callers testing visibility.
    const h = Math.max(trueAltitudeDeg, -1);
    const denom = Math.tan((h + 7.31 / (h + 4.4)) * DEG);
    if (denom === 0) return 0;
    // Floored at zero: the formula goes very slightly negative approaching the
    // zenith, and refraction that bends a body downward is not a thing.
    return Math.max(0, (1 / denom) / 60);
}

// ---------- coordinate conversion ----------

// Ecliptic longitude and latitude to the observer's horizon. Shared by both
// bodies: the series that produce the ecliptic position differ, everything after
// that point does not.
function eclipticToHorizon(lonDeg, latDeg, n, date, observerLatDeg, observerLonDeg) {
    const eps = obliquityDeg(n);

    // Ecliptic to equatorial.
    const sinLat = sin(latDeg);
    const cosLat = cos(latDeg);
    const raDeg = wrap360(
        Math.atan2(
            sin(lonDeg) * cos(eps) - (sinLat / cosLat) * sin(eps),
            cos(lonDeg)
        ) * RAD
    );
    const decDeg = Math.asin(
        clamp(sinLat * cos(eps) + cosLat * sin(eps) * sin(lonDeg), -1, 1)
    ) * RAD;

    // Equatorial to horizon, through the local hour angle.
    const haDeg = wrap180(gmstDeg(date) + observerLonDeg - raDeg);
    const sinAlt = clamp(
        sin(observerLatDeg) * sin(decDeg) + cos(observerLatDeg) * cos(decDeg) * cos(haDeg),
        -1, 1
    );
    const altitudeDeg = Math.asin(sinAlt) * RAD;
    // Measured from north through east, matching every other bearing in the view.
    const azimuthDeg = wrap360(
        Math.atan2(
            -cos(decDeg) * sin(haDeg),
            sin(decDeg) * cos(observerLatDeg) - cos(decDeg) * cos(haDeg) * sin(observerLatDeg)
        ) * RAD
    );

    return { azimuthDeg, altitudeDeg, raDeg, decDeg };
}

// Geocentric to topocentric: the correction for standing on the surface rather
// than at the centre of the Earth.
//
// This is the single most important correction in this module and it is not a
// refinement. The moon is close enough that an observer is displaced from the
// geocentre by up to 1.03 degrees of its apparent position — larger than the whole
// error budget of every other term here combined, and largest at the horizon,
// exactly where a low moon is worth pointing out. Azimuth is unaffected; the body
// is pulled down, away from the zenith.
function topocentricAltitudeDeg(altitudeDeg, distanceKm, observerAltM = 0) {
    const rho = (EARTH_RADIUS_KM + observerAltM / 1000) / distanceKm;
    const parallax = Math.asin(clamp(rho * cos(altitudeDeg), -1, 1)) * RAD;
    return altitudeDeg - parallax;
}

// Angular diameter of a sphere of known size at a known distance.
function angularDiameterDeg(diameterKm, distanceKm) {
    return 2 * Math.atan(diameterKm / 2 / distanceKm) * RAD;
}

// ---------- the sun ----------

// Geocentric ecliptic longitude of the sun, and its distance.
//
// The USNO low-precision formulae from The Astronomical Almanac: mean longitude,
// mean anomaly, then the equation of the centre for the eccentricity of the
// Earth's orbit. Good to about 0.01 degrees in longitude across 1950-2050, which
// is two orders of magnitude finer than this view can show.
//
// The coefficients are the substance of this function. A transposed digit leaves
// every geometric invariant intact — the solstice elevations are set by the
// obliquity above, not by these — while moving the sun most of its own diameter,
// which is why the tests pin the equation of time against its published extremes
// rather than only checking that the seasons come out right.
function solarEcliptic(n) {
    const meanLonDeg = 280.460 + 0.9856474 * n;
    const meanAnomDeg = 357.528 + 0.9856003 * n;
    const lonDeg = wrap360(
        meanLonDeg + 1.915 * sin(meanAnomDeg) + 0.020 * sin(2 * meanAnomDeg)
    );
    // Astronomical units, converted for the angular size.
    const distanceAu = 1.00014 - 0.01671 * cos(meanAnomDeg) - 0.00014 * cos(2 * meanAnomDeg);

    return { lonDeg, latDeg: 0, meanLonDeg: wrap360(meanLonDeg), distanceKm: distanceAu * SUN_DISTANCE_KM };
}

// Apparent position of the sun: refracted, and with its angular diameter. The
// sun's parallax is 0.0024 degrees at the horizon, a hundredth of a pixel, so the
// topocentric correction the moon needs is deliberately not applied here.
export function sunPosition(date, latDeg, lonDeg, observerAltM = 0) {
    const n = daysSinceJ2000(date);
    const ecl = solarEcliptic(n);
    const h = eclipticToHorizon(ecl.lonDeg, 0, n, date, latDeg, lonDeg);

    return {
        azimuthDeg: h.azimuthDeg,
        elevationDeg: h.altitudeDeg + refractionDeg(h.altitudeDeg),
        trueElevationDeg: h.altitudeDeg,
        distanceKm: ecl.distanceKm,
        diameterDeg: angularDiameterDeg(SUN_DIAMETER_KM, ecl.distanceKm)
    };
}

// Equation of time in minutes: how far true solar time runs ahead of mean solar
// time. Exported because it is the one solar quantity with well known published
// extremes, which makes it the external anchor for the series above.
export function equationOfTimeMinutes(date) {
    const n = daysSinceJ2000(date);
    const ecl = solarEcliptic(n);
    const h = eclipticToHorizon(ecl.lonDeg, 0, n, date, 0, 0);
    return wrap180(ecl.meanLonDeg - h.raDeg) * 4;
}

// ---------- the moon ----------

// Geocentric ecliptic position and distance of the moon.
//
// Meeus, Astronomical Algorithms, chapter 47, truncated to the largest periodic
// terms: evection, variation, the annual equation and the principal latitude
// terms. Accurate to roughly 0.02 degrees in longitude, which is well inside the
// parallax correction that follows and far inside what a 10-pixel disc can show.
//
// Exported for the tests: the ecliptic latitude has a hard physical bound that is
// worth asserting directly, and it is not recoverable from a horizon position.
// Takes days from J2000 rather than a Date, matching the solar series.
export function lunarEcliptic(n) {
    // Mean elements.
    const L = 218.316 + 13.176396 * n;      // mean longitude
    const M = 134.963 + 13.064993 * n;      // mean anomaly
    const F = 93.272 + 13.229350 * n;       // argument of latitude
    const D = 297.850 + 12.190749 * n;      // mean elongation from the sun
    const Msun = 357.528 + 0.9856003 * n;   // the sun's mean anomaly

    const lonDeg = wrap360(
        L
        + 6.289 * sin(M)                    // equation of the centre
        + 1.274 * sin(2 * D - M)            // evection
        + 0.658 * sin(2 * D)                // variation
        - 0.186 * sin(Msun)                 // annual equation
        - 0.059 * sin(2 * M - 2 * D)
        - 0.057 * sin(M - 2 * D + Msun)
        + 0.053 * sin(M + 2 * D)
        + 0.046 * sin(2 * D - Msun)
        + 0.041 * sin(M - Msun)
        - 0.035 * sin(D)
        - 0.031 * sin(M + Msun)
    );

    const latDeg =
        5.128 * sin(F)
        + 0.281 * sin(M + F)
        - 0.278 * sin(F - M)
        - 0.173 * sin(F - 2 * D)
        + 0.055 * sin(2 * D + F - M)
        - 0.046 * sin(2 * D + M - F)
        + 0.033 * sin(2 * D + F);

    const distanceKm =
        385000.56
        - 20905.355 * cos(M)
        - 3699.111 * cos(2 * D - M)
        - 2955.968 * cos(2 * D)
        - 569.925 * cos(2 * M)
        + 246.158 * cos(2 * M - 2 * D)
        - 204.586 * cos(Msun - 2 * D)
        - 170.733 * cos(M + 2 * D);

    return { lonDeg, latDeg, distanceKm };
}

// Apparent position of the moon: topocentric, then refracted, with the angular
// diameter for its current distance. The diameter varies by about 12 per cent
// across the month, which is enough to be worth carrying rather than fixing.
export function moonPosition(date, latDeg, lonDeg, observerAltM = 0) {
    const n = daysSinceJ2000(date);
    const ecl = lunarEcliptic(n);
    const h = eclipticToHorizon(ecl.lonDeg, ecl.latDeg, n, date, latDeg, lonDeg);
    const topocentricDeg = topocentricAltitudeDeg(h.altitudeDeg, ecl.distanceKm, observerAltM);

    return {
        azimuthDeg: h.azimuthDeg,
        elevationDeg: topocentricDeg + refractionDeg(topocentricDeg),
        trueElevationDeg: topocentricDeg,
        geocentricElevationDeg: h.altitudeDeg,
        distanceKm: ecl.distanceKm,
        diameterDeg: angularDiameterDeg(MOON_DIAMETER_KM, ecl.distanceKm)
    };
}

// Illuminated fraction of the moon's disc, and whether it is waxing.
//
// Deliberately returns no orientation. The lit side faces the sun by definition,
// so the renderer takes the direction in screen space from the moon to the sun,
// which is exactly right and avoids converting an astronomical position angle
// through the parallactic angle.
export function moonPhase(date) {
    const n = daysSinceJ2000(date);
    const moon = lunarEcliptic(n);
    const sun = solarEcliptic(n);

    // Elongation east of the sun: 0 at new, 180 at full.
    const elongationDeg = wrap360(moon.lonDeg - sun.lonDeg);
    // The phase angle is the supplement, and the illuminated fraction follows from
    // the terminator being a half ellipse of the disc.
    const fraction = (1 - cos(elongationDeg)) / 2;

    return { fraction, waxing: elongationDeg < 180, elongationDeg };
}

// ---------- visibility ----------

// Whether a body is up, judged on its apparent upper limb rather than the
// geometric centre.
//
// The distinction is the whole of sunset. Refraction lifts the sun 0.575 degrees
// at the horizon and its own radius adds another 0.27, so the disc is still in
// the sky until its centre has reached about -0.83 degrees. Testing the centre
// against zero would hide the sun through the minutes most worth looking at.
export function isUp(position, horizonDepressionDeg = 0) {
    return position.elevationDeg + position.diameterDeg / 2 > horizonDepressionDeg;
}
