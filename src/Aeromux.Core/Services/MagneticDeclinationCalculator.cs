// Aeromux Multi-SDR Mode S and ADSB Demodulator and Decoder for .NET
// Copyright (C) 2025 Nandor Toth <dev@nandortoth.com>
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

// World Magnetic Model (WMM-2025) implementation
// Based on the World Magnetic Model developed by NOAA National Centers for Environmental Information (NCEI)
// and the British Geological Survey (BGS).
// WMM is in the public domain as a work of the U.S. government.
//
// Official Source: https://www.ncei.noaa.gov/products/world-magnetic-model
// Technical Report: https://www.ncei.noaa.gov/products/world-magnetic-model/technical-reports
// Coefficients Downloaded: November 13, 2024
// Valid Period: 2025.0 to 2030.0 (±5 years from epoch)

using Aeromux.Core.Tracking;

namespace Aeromux.Core.Services;

/// <summary>
/// World Magnetic Model (WMM-2025) calculator for computing magnetic declination.
/// Implements the WMM-2025 spherical harmonic model to calculate Earth's magnetic field components.
/// </summary>
/// <remarks>
/// <para>
/// The World Magnetic Model is the standard model of Earth's main magnetic field produced by
/// NOAA National Centers for Environmental Information (NCEI) and the British Geological Survey (BGS).
/// It is used by navigation systems, military applications, and scientific research worldwide.
/// </para>
/// <para>
/// <strong>Model Information:</strong>
/// <list type="bullet">
/// <item>Version: WMM-2025</item>
/// <item>Valid Period: 2025.0 to 2030.0 (5-year epoch)</item>
/// <item>Maximum Degree: 12 (spherical harmonic expansion)</item>
/// <item>Reference: https://www.ncei.noaa.gov/products/world-magnetic-model</item>
/// </list>
/// </para>
/// <para>
/// <strong>Implementation Notes:</strong>
/// <list type="bullet">
/// <item>Thread-safe: Uses lazy initialization for model coefficients</item>
/// <item>Coordinate System: WGS84 ellipsoid</item>
/// <item>Output: Declination in degrees (positive East, negative West)</item>
/// </list>
/// </para>
/// </remarks>
internal static class MagneticDeclinationCalculator
{
    private const int MaxDegree = 12;
    private const double Epoch = 2025.0;
    private const double A = 6378.137;      // WGS84 (World Geodetic System 1984) semi-major axis (km)
    private const double B = 6356.7523142;  // WGS84 semi-minor axis (km)
    private const double Re = 6371.2;       // Earth mean radius (km)
    private const double Pi = Math.PI;
    private const double DegToRad = Pi / 180.0;
    private const double CacheTtlSeconds = 5.0;

    /// <summary>
    /// WMM coefficient record structure.
    /// Represents one row of Gauss coefficients from the WMM model.
    /// </summary>
    /// <param name="N">Degree (n) of spherical harmonic.</param>
    /// <param name="M">Order (m) of spherical harmonic.</param>
    /// <param name="Gnm">Gauss coefficient g(n,m) in nT (nanotesla, 10⁻⁹ tesla).</param>
    /// <param name="Hnm">Gauss coefficient h(n,m) in nT (nanotesla).</param>
    /// <param name="Dgnm">Secular variation dg(n,m)/dt in nT/year (rate of change).</param>
    /// <param name="Dhnm">Secular variation dh(n,m)/dt in nT/year (rate of change).</param>
    private readonly record struct WmmCoefficient(int N, int M, double Gnm, double Hnm, double Dgnm, double Dhnm);

    // WMM-2025 Gauss coefficients (Schmidt semi-normalized)
    // Downloaded from NOAA NCEI: November 13, 2024
    // Epoch: 2025.0, Valid: 2025.0 to 2030.0
    // Source: https://www.ncei.noaa.gov/products/world-magnetic-model
    private static readonly WmmCoefficient[] WmmCoefficients =
    [
        new( 1,  0, -29351.8,      0.0,    12.0,     0.0),
        new( 1,  1,  -1410.8,   4545.4,     9.7,   -21.5),
        new( 2,  0,  -2556.6,      0.0,   -11.6,     0.0),
        new( 2,  1,   2951.1,  -3133.6,    -5.2,   -27.7),
        new( 2,  2,   1649.3,   -815.1,    -8.0,   -12.1),
        new( 3,  0,   1361.0,      0.0,    -1.3,     0.0),
        new( 3,  1,  -2404.1,    -56.6,    -4.2,     4.0),
        new( 3,  2,   1243.8,    237.5,     0.4,    -0.3),
        new( 3,  3,    453.6,   -549.5,   -15.6,    -4.1),
        new( 4,  0,    895.0,      0.0,    -1.6,     0.0),
        new( 4,  1,    799.5,    278.6,    -2.4,    -1.1),
        new( 4,  2,     55.7,   -133.9,    -6.0,     4.1),
        new( 4,  3,   -281.1,    212.0,     5.6,     1.6),
        new( 4,  4,     12.1,   -375.6,    -7.0,    -4.4),
        new( 5,  0,   -233.2,      0.0,     0.6,     0.0),
        new( 5,  1,    368.9,     45.4,     1.4,    -0.5),
        new( 5,  2,    187.2,    220.2,     0.0,     2.2),
        new( 5,  3,   -138.7,   -122.9,     0.6,     0.4),
        new( 5,  4,   -142.0,     43.0,     2.2,     1.7),
        new( 5,  5,     20.9,    106.1,     0.9,     1.9),
        new( 6,  0,     64.4,      0.0,    -0.2,     0.0),
        new( 6,  1,     63.8,    -18.4,    -0.4,     0.3),
        new( 6,  2,     76.9,     16.8,     0.9,    -1.6),
        new( 6,  3,   -115.7,     48.8,     1.2,    -0.4),
        new( 6,  4,    -40.9,    -59.8,    -0.9,     0.9),
        new( 6,  5,     14.9,     10.9,     0.3,     0.7),
        new( 6,  6,    -60.7,     72.7,     0.9,     0.9),
        new( 7,  0,     79.5,      0.0,    -0.0,     0.0),
        new( 7,  1,    -77.0,    -48.9,    -0.1,     0.6),
        new( 7,  2,     -8.8,    -14.4,    -0.1,     0.5),
        new( 7,  3,     59.3,     -1.0,     0.5,    -0.8),
        new( 7,  4,     15.8,     23.4,    -0.1,     0.0),
        new( 7,  5,      2.5,     -7.4,    -0.8,    -1.0),
        new( 7,  6,    -11.1,    -25.1,    -0.8,     0.6),
        new( 7,  7,     14.2,     -2.3,     0.8,    -0.2),
        new( 8,  0,     23.2,      0.0,    -0.1,     0.0),
        new( 8,  1,     10.8,      7.1,     0.2,    -0.2),
        new( 8,  2,    -17.5,    -12.6,     0.0,     0.5),
        new( 8,  3,      2.0,     11.4,     0.5,    -0.4),
        new( 8,  4,    -21.7,     -9.7,    -0.1,     0.4),
        new( 8,  5,     16.9,     12.7,     0.3,    -0.5),
        new( 8,  6,     15.0,      0.7,     0.2,    -0.6),
        new( 8,  7,    -16.8,     -5.2,    -0.0,     0.3),
        new( 8,  8,      0.9,      3.9,     0.2,     0.2),
        new( 9,  0,      4.6,      0.0,    -0.0,     0.0),
        new( 9,  1,      7.8,    -24.8,    -0.1,    -0.3),
        new( 9,  2,      3.0,     12.2,     0.1,     0.3),
        new( 9,  3,     -0.2,      8.3,     0.3,    -0.3),
        new( 9,  4,     -2.5,     -3.3,    -0.3,     0.3),
        new( 9,  5,    -13.1,     -5.2,     0.0,     0.2),
        new( 9,  6,      2.4,      7.2,     0.3,    -0.1),
        new( 9,  7,      8.6,     -0.6,    -0.1,    -0.2),
        new( 9,  8,     -8.7,      0.8,     0.1,     0.4),
        new( 9,  9,    -12.9,     10.0,    -0.1,     0.1),
        new(10,  0,     -1.3,      0.0,     0.1,     0.0),
        new(10,  1,     -6.4,      3.3,     0.0,     0.0),
        new(10,  2,      0.2,      0.0,     0.1,    -0.0),
        new(10,  3,      2.0,      2.4,     0.1,    -0.2),
        new(10,  4,     -1.0,      5.3,    -0.0,     0.1),
        new(10,  5,     -0.6,     -9.1,    -0.3,    -0.1),
        new(10,  6,     -0.9,      0.4,     0.0,     0.1),
        new(10,  7,      1.5,     -4.2,    -0.1,     0.0),
        new(10,  8,      0.9,     -3.8,    -0.1,    -0.1),
        new(10,  9,     -2.7,      0.9,    -0.0,     0.2),
        new(10, 10,     -3.9,     -9.1,    -0.0,    -0.0),
        new(11,  0,      2.9,      0.0,     0.0,     0.0),
        new(11,  1,     -1.5,      0.0,    -0.0,    -0.0),
        new(11,  2,     -2.5,      2.9,     0.0,     0.1),
        new(11,  3,      2.4,     -0.6,     0.0,    -0.0),
        new(11,  4,     -0.6,      0.2,     0.0,     0.1),
        new(11,  5,     -0.1,      0.5,    -0.1,    -0.0),
        new(11,  6,     -0.6,     -0.3,     0.0,    -0.0),
        new(11,  7,     -0.1,     -1.2,    -0.0,     0.1),
        new(11,  8,      1.1,     -1.7,    -0.1,    -0.0),
        new(11,  9,     -1.0,     -2.9,    -0.1,     0.0),
        new(11, 10,     -0.2,     -1.8,    -0.1,     0.0),
        new(11, 11,      2.6,     -2.3,    -0.1,     0.0),
        new(12,  0,     -2.0,      0.0,     0.0,     0.0),
        new(12,  1,     -0.2,     -1.3,     0.0,    -0.0),
        new(12,  2,      0.3,      0.7,    -0.0,     0.0),
        new(12,  3,      1.2,      1.0,    -0.0,    -0.1),
        new(12,  4,     -1.3,     -1.4,    -0.0,     0.1),
        new(12,  5,      0.6,     -0.0,    -0.0,    -0.0),
        new(12,  6,      0.6,      0.6,     0.1,    -0.0),
        new(12,  7,      0.5,     -0.1,    -0.0,    -0.0),
        new(12,  8,     -0.1,      0.8,     0.0,     0.0),
        new(12,  9,     -0.4,      0.1,     0.0,    -0.0),
        new(12, 10,     -0.2,     -1.0,    -0.1,    -0.0),
        new(12, 11,     -1.3,      0.1,    -0.0,     0.0),
        new(12, 12,     -0.7,      0.2,    -0.1,    -0.1),
    ];

    // Normalized coefficients (computed once during initialization)
    private static readonly Lazy<WmmModel> Model = new(InitializeModel);

    private sealed class WmmModel
    {
        public double[,] C { get; } = new double[13, 13];
        public double[,] Cd { get; } = new double[13, 13];
        public double[,] K { get; } = new double[13, 13];
        public double[] Snorm { get; } = new double[169];
        public double[] Fn { get; } = new double[13];
        public double[] Fm { get; } = new double[13];
    }

    /// <summary>
    /// Get cached magnetic declination or calculate new one if expired.
    /// </summary>
    /// <param name="cached">Previously cached declination, or null if none.</param>
    /// <param name="latitude">Geodetic latitude in degrees (-90 to +90, positive North).</param>
    /// <param name="longitude">Geodetic longitude in degrees (-180 to +180, positive East).</param>
    /// <param name="altitudeKm">Altitude above WGS84 ellipsoid in kilometers.</param>
    /// <param name="timestamp">Current timestamp for cache expiry check and calculation.</param>
    /// <returns>
    /// Existing cached declination if still valid (within TTL),
    /// or newly calculated declination with updated timestamp.
    /// </returns>
    public static MagneticDeclination GetOrCalculate(
        MagneticDeclination? cached,
        double latitude,
        double longitude,
        double altitudeKm,
        DateTime timestamp)
    {
        // Check if cache is still valid (5-second TTL)
        if (cached != null && (timestamp - cached.CalculatedAt).TotalSeconds <= CacheTtlSeconds)
        {
            return cached; // Return existing - still valid
        }

        // Calculate fresh declination
        double declination = CalculateDeclination(latitude, longitude, altitudeKm, timestamp);
        return new MagneticDeclination(declination, timestamp);
    }

    /// <summary>
    /// Calculate magnetic declination for a given position and time.
    /// </summary>
    /// <param name="latitude">Geodetic latitude in degrees (-90 to +90, positive North).</param>
    /// <param name="longitude">Geodetic longitude in degrees (-180 to +180, positive East).</param>
    /// <param name="altitudeKm">Altitude above WGS84 ellipsoid in kilometers.</param>
    /// <param name="date">Date for calculation.</param>
    /// <returns>Magnetic declination in degrees (positive East, negative West).</returns>
    private static double CalculateDeclination(double latitude, double longitude, double altitudeKm, DateTime date)
    {
        // Convert date to decimal year
        int daysInYear = DateTime.IsLeapYear(date.Year) ? 366 : 365;
        double decimalYear = date.Year + ((date.DayOfYear - 1.0) / daysInYear);

        // Call internal calculation
        Calculate(altitudeKm, latitude, longitude, decimalYear, out double declination, out _, out _, out _);

        return declination;
    }

    private static WmmModel InitializeModel()
    {
        var model = new WmmModel();

        // Load WMM coefficients into model arrays
        foreach (WmmCoefficient coef in WmmCoefficients)
        {
            model.C[coef.M, coef.N] = coef.Gnm;
            model.Cd[coef.M, coef.N] = coef.Dgnm;
            if (coef.M != 0)
            {
                model.C[coef.N, coef.M - 1] = coef.Hnm;
                model.Cd[coef.N, coef.M - 1] = coef.Dhnm;
            }
        }

        // Convert Schmidt normalized Gauss coefficients to unnormalized
        model.Snorm[0] = 1.0;
        model.Fm[0] = 0.0;

        for (int n = 1; n <= MaxDegree; n++)
        {
            model.Snorm[n] = model.Snorm[n - 1] * ((2 * n) - 1) / n;
            int j = 2;

            for (int m = 0; m <= n; m++)
            {
                model.K[m, n] = (((n - 1) * (n - 1)) - (m * m)) / (double)(((2 * n) - 1) * ((2 * n) - 3));
                if (m > 0)
                {
                    double flnmj = ((n - m + 1) * j) / (double)(n + m);
                    model.Snorm[n + (m * 13)] = model.Snorm[n + ((m - 1) * 13)] * Math.Sqrt(flnmj);
                    j = 1;
                    model.C[n, m - 1] = model.Snorm[n + (m * 13)] * model.C[n, m - 1];
                    model.Cd[n, m - 1] = model.Snorm[n + (m * 13)] * model.Cd[n, m - 1];
                }
                model.C[m, n] = model.Snorm[n + (m * 13)] * model.C[m, n];
                model.Cd[m, n] = model.Snorm[n + (m * 13)] * model.Cd[m, n];
            }
            model.Fn[n] = n + 1;
            model.Fm[n] = n;
        }
        model.K[1, 1] = 0.0;

        return model;
    }

    private static void Calculate(
        double alt,
        double glat,
        double glon,
        double time,
        out double dec,
        out double dip,
        out double ti,
        out double gv)
    {
        WmmModel model = Model.Value;

        double dt = time - Epoch;
        double rlon = glon * DegToRad;
        double rlat = glat * DegToRad;
        double srlon = Math.Sin(rlon);
        double srlat = Math.Sin(rlat);
        double crlon = Math.Cos(rlon);
        double crlat = Math.Cos(rlat);
        double srlat2 = srlat * srlat;
        double crlat2 = crlat * crlat;

        double[] sp = new double[13];
        double[] cp = new double[13];
        sp[0] = 0.0;
        cp[0] = 1.0;
        sp[1] = srlon;
        cp[1] = crlon;

        // Convert from geodetic coords to spherical coords
        double a2 = A * A;
        double b2 = B * B;
        double c2 = a2 - b2;
        double a4 = a2 * a2;
        double b4 = b2 * b2;
        double c4 = a4 - b4;

        double q = Math.Sqrt(a2 - (c2 * srlat2));
        double q1 = alt * q;
        double q2 = ((q1 + a2) / (q1 + b2)) * ((q1 + a2) / (q1 + b2));
        double ct = srlat / Math.Sqrt((q2 * crlat2) + srlat2);
        double st = Math.Sqrt(1.0 - (ct * ct));
        double r2 = (alt * alt) + (2.0 * q1) + ((a4 - (c4 * srlat2)) / (q * q));
        double r = Math.Sqrt(r2);
        double d = Math.Sqrt((a2 * crlat2) + (b2 * srlat2));
        double ca = (alt + d) / r;
        double sa = c2 * crlat * srlat / (r * d);

        for (int m = 2; m <= MaxDegree; m++)
        {
            sp[m] = (sp[1] * cp[m - 1]) + (cp[1] * sp[m - 1]);
            cp[m] = (cp[1] * cp[m - 1]) - (sp[1] * sp[m - 1]);
        }

        double aor = Re / r;
        double ar = aor * aor;
        double br = 0.0, bt = 0.0, bp = 0.0, bpp = 0.0;

        double[] p = new double[169];
        double[,] dp = new double[13, 13];
        double[,] tc = new double[13, 13];
        double[] pp = new double[13];

        p[0] = 1.0;
        pp[0] = 1.0;
        dp[0, 0] = 0.0;

        for (int n = 1; n <= MaxDegree; n++)
        {
            ar = ar * aor;
            for (int m = 0; m <= n; m++)
            {
                // Compute unnormalized associated Legendre polynomials and derivatives
                if (n == m)
                {
                    p[n + (m * 13)] = st * p[n - 1 + ((m - 1) * 13)];
                    dp[m, n] = (st * dp[m - 1, n - 1]) + (ct * p[n - 1 + ((m - 1) * 13)]);
                }
                else if (n == 1 && m == 0)
                {
                    p[n + (m * 13)] = ct * p[n - 1 + (m * 13)];
                    dp[m, n] = (ct * dp[m, n - 1]) - (st * p[n - 1 + (m * 13)]);
                }
                else if (n > 1 && n != m)
                {
                    if (m > n - 2)
                    {
                        p[n - 2 + (m * 13)] = 0.0;
                    }

                    if (m > n - 2)
                    {
                        dp[m, n - 2] = 0.0;
                    }

                    p[n + (m * 13)] = (ct * p[n - 1 + (m * 13)]) - (model.K[m, n] * p[n - 2 + (m * 13)]);
                    dp[m, n] = (ct * dp[m, n - 1]) - (st * p[n - 1 + (m * 13)]) - (model.K[m, n] * dp[m, n - 2]);
                }

                // Time adjust the Gauss coefficients
                tc[m, n] = model.C[m, n] + (dt * model.Cd[m, n]);
                if (m != 0)
                {
                    tc[n, m - 1] = model.C[n, m - 1] + (dt * model.Cd[n, m - 1]);
                }

                // Accumulate terms of the spherical harmonic expansions
                double par = ar * p[n + (m * 13)];
                double temp1, temp2;
                if (m == 0)
                {
                    temp1 = tc[m, n] * cp[m];
                    temp2 = tc[m, n] * sp[m];
                }
                else
                {
                    temp1 = (tc[m, n] * cp[m]) + (tc[n, m - 1] * sp[m]);
                    temp2 = (tc[m, n] * sp[m]) - (tc[n, m - 1] * cp[m]);
                }

                bt = bt - (ar * temp1 * dp[m, n]);
                bp += (model.Fm[m] * temp2 * par);
                br += (model.Fn[n] * temp1 * par);

                // Special case: North/South geographic poles
                if (st == 0.0 && m == 1)
                {
                    if (n == 1)
                    {
                        pp[n] = pp[n - 1];
                    }
                    else
                    {
                        pp[n] = (ct * pp[n - 1]) - (model.K[m, n] * pp[n - 2]);
                    }

                    double parp = ar * pp[n];
                    bpp += (model.Fm[m] * temp2 * parp);
                }
            }
        }

        if (st == 0.0)
        {
            bp = bpp;
        }
        else
        {
            bp /= st;
        }

        // Rotate magnetic vector components from spherical to geodetic coordinates
        double bx = (-bt * ca) - (br * sa);
        double by = bp;
        double bz = (bt * sa) - (br * ca);

        // Compute declination (DEC), inclination (DIP) and total intensity (TI)
        double bh = Math.Sqrt((bx * bx) + (by * by));
        ti = Math.Sqrt((bh * bh) + (bz * bz));
        dec = Math.Atan2(by, bx) / DegToRad;
        dip = Math.Atan2(bz, bh) / DegToRad;

        // Compute magnetic grid variation for polar regions (|lat| >= 55°)
        gv = -999.0;
        if (Math.Abs(glat) >= 55.0)
        {
            if (glat > 0.0 && glon >= 0.0)
            {
                gv = dec - glon;
            }

            if (glat > 0.0 && glon < 0.0)
            {
                gv = dec + Math.Abs(glon);
            }

            if (glat < 0.0 && glon >= 0.0)
            {
                gv = dec + glon;
            }

            if (glat < 0.0 && glon < 0.0)
            {
                gv = dec - Math.Abs(glon);
            }

            if (gv > +180.0)
            {
                gv -= 360.0;
            }

            if (gv < -180.0)
            {
                gv += 360.0;
            }
        }
    }
}
